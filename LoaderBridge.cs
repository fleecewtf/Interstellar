using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

#if !BRIDGE_FIXTURE
using BepInEx;
using Interstellar.LoaderRuntime;
using Interstellar.Menu;
using UnityEngine;

namespace Interstellar
{


    internal sealed class LoaderBridge : MonoBehaviour
    {
        private BridgeServer server;
        private float loadRequestedAt;

        internal static bool DeferNormalStartup()
        {
            return BridgeStartupOptions.Read(Path.Combine(Paths.ConfigPath, "Interstellar.Loader.cfg")).Defer;
        }

        private void Awake()
        {
            try { server = new BridgeServer(PluginInfo.Version); }
            catch (Exception ex) { Debug.LogWarning("Interstellar loader connection is unavailable: " + ex.Message); }
        }

        private void Update()
        {
            if (server == null) return;
            if (Main.HasLoaded) server.SetLoaded();
            else if (server.IsLoaded) server.SetError("Interstellar was unloaded. Restart Gorilla Tag to load it again.");
            if (server.TryTakeLoadRequest())
            {
                loadRequestedAt = Time.realtimeSinceStartup;
                try { Bootstrapper.Initialize(); }
                catch (Exception) { server.SetError("Interstellar could not start. Restart Gorilla Tag and retry."); }
            }
            if (server.IsLoading && Time.realtimeSinceStartup - loadRequestedAt > 180f)
                server.SetError("Interstellar did not finish starting. Restart Gorilla Tag and retry.");
        }

        private void OnDestroy()
        {
            server?.Dispose();
            server = null;
        }
    }
}
#endif

namespace Interstellar.LoaderRuntime
{
    internal sealed class BridgeStartupOptions
    {
        internal bool Managed;
        internal bool LoadOnStartup;
        internal bool Defer => Managed && !LoadOnStartup;

        internal static BridgeStartupOptions Read(string path)
        {
            var defaults = new BridgeStartupOptions();
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > 4096) return defaults;
                bool section = false, haveManaged = false, haveStartup = false;
                var parsed = new BridgeStartupOptions();
                foreach (string source in File.ReadAllLines(path))
                {
                    string line = source.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal)) continue;
                    if (line.StartsWith("[", StringComparison.Ordinal))
                    {
                        section = line.Equals("[Loader]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!section) continue;
                    int equals = line.IndexOf('=');
                    if (equals < 1) return defaults;
                    string key = line.Substring(0, equals).Trim();
                    if (!bool.TryParse(line.Substring(equals + 1).Trim(), out bool value)) return defaults;
                    if (key.Equals("managed", StringComparison.OrdinalIgnoreCase))
                    {
                        if (haveManaged) return defaults;
                        haveManaged = true; parsed.Managed = value;
                    }
                    else if (key.Equals("loadOnStartup", StringComparison.OrdinalIgnoreCase))
                    {
                        if (haveStartup) return defaults;
                        haveStartup = true; parsed.LoadOnStartup = value;
                    }
                    else return defaults;
                }
                return haveManaged && haveStartup ? parsed : defaults;
            }
            catch (IOException) { return defaults; }
            catch (UnauthorizedAccessException) { return defaults; }
        }
    }

    internal sealed class BridgeServer : IDisposable
    {
        private const int RequestBytesLimit = 16;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly string version;
        private readonly string ownerSid;
        private readonly string pipeName;
        private string state = "READY";
        private int pendingLoad;

        internal BridgeServer(string menuVersion)
        {
            if (!Version.TryParse(menuVersion, out _)) throw new ArgumentException("Invalid menu version.", nameof(menuVersion));
            version = menuVersion;
            ownerSid = BridgeNative.CurrentUserSid();
            pipeName = "Interstellar.Loader." + BridgeNative.GetCurrentProcessId();
            NamedPipeServerStream first = BridgeNative.CreateLocalPipe(pipeName, ownerSid);
            _ = Task.Run(() => ListenAsync(first));
        }

        internal bool IsLoading => Volatile.Read(ref state) == "LOADING";
        internal bool IsLoaded => Volatile.Read(ref state) == "LOADED";
        internal bool TryTakeLoadRequest() => Interlocked.Exchange(ref pendingLoad, 0) == 1;
        internal void SetLoaded() => Volatile.Write(ref state, "LOADED");
        internal void SetError(string error) => Volatile.Write(ref state, "ERROR|" + Sanitize(error));

        private string Handle(string command)
        {
            if (command != "STATUS" && command != "LOAD") return "ERROR|Unknown request.";
            string current = Volatile.Read(ref state);
            if (command == "LOAD" && current == "READY")
            {
                if (Interlocked.CompareExchange(ref state, "LOADING", "READY") == "READY")
                    Interlocked.Exchange(ref pendingLoad, 1);
                current = Volatile.Read(ref state);
            }
            return current.StartsWith("ERROR|", StringComparison.Ordinal) ? current : current + "|" + version;
        }

        private async Task ListenAsync(NamedPipeServerStream first)
        {
            NamedPipeServerStream next = first;
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    using (var pipe = next)
                    using (lifetime.Token.Register(() => pipe.Dispose()))
                    {
                        await pipe.WaitForConnectionAsync(lifetime.Token).ConfigureAwait(false);
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                        {
                            timeout.CancelAfter(1000);
                            using (timeout.Token.Register(() => pipe.Dispose()))
                            {
                                try
                                {
                                    string request = await ReadRequestAsync(pipe, timeout.Token).ConfigureAwait(false);
                                    string response = Handle(request);
                                    byte[] bytes = Encoding.ASCII.GetBytes(response + "\n");
                                    await pipe.WriteAsync(bytes, 0, bytes.Length, timeout.Token).ConfigureAwait(false);
                                    await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);
                                }
                                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException ||
                                                           ex is OperationCanceledException || ex is Win32Exception)
                                {

                                }
                            }
                        }
                    }
                    if (!lifetime.IsCancellationRequested) next = BridgeNative.CreateLocalPipe(pipeName, ownerSid);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is OperationCanceledException || ex is Win32Exception)
            {
                if (!lifetime.IsCancellationRequested) SetError("The Interstellar connection stopped. Restart Gorilla Tag and retry.");
            }
        }

        private static async Task<string> ReadRequestAsync(Stream pipe, CancellationToken token)
        {
            var bytes = new byte[RequestBytesLimit];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (await pipe.ReadAsync(bytes, i, 1, token).ConfigureAwait(false) == 0) return "";
                if (bytes[i] == (byte)'\n') return Encoding.ASCII.GetString(bytes, 0, i);
                if (bytes[i] < 32 || bytes[i] > 126) return "";
            }
            return "";
        }

        private static string Sanitize(string value)
        {
            var clean = new StringBuilder();
            foreach (char character in value ?? "The menu could not start.")
            {
                if (clean.Length >= 220) break;
                clean.Append(character >= ' ' && character <= '~' && character != '|' ? character : ' ');
            }
            return clean.ToString();
        }

        public void Dispose()
        {

            lifetime.Cancel();
        }
    }

    internal static class BridgeNative
    {
        private const uint TokenQuery = 0x0008;
        private const int TokenUser = 1;
        private const uint PipeAccessDuplex = 0x00000003;
        private const uint FileFlagOverlapped = 0x40000000;
        private const uint FileFlagFirstPipeInstance = 0x00080000;
        private const uint PipeRejectRemoteClients = 0x00000008;

        internal static NamedPipeServerStream CreateLocalPipe(string name, string ownerSid)
        {

            if (!ConvertStringSecurityDescriptorToSecurityDescriptor("D:P(A;;GA;;;" + ownerSid + ")", 1, out IntPtr descriptor, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var attributes = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), SecurityDescriptor = descriptor };
                SafePipeHandle handle = CreateNamedPipe("\\\\.\\pipe\\" + name,
                    PipeAccessDuplex | FileFlagOverlapped | FileFlagFirstPipeInstance,
                    PipeRejectRemoteClients, 1, 256, 32, 0, ref attributes);
                if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
                try { return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle); }
                catch { handle.Dispose(); throw; }
            }
            finally { LocalFree(descriptor); }
        }

        internal static string CurrentUserSid()
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out IntPtr token)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try { return UserSid(token); }
            finally { CloseHandle(token); }
        }

        private static string UserSid(IntPtr token)
        {
            GetTokenInformation(token, TokenUser, IntPtr.Zero, 0, out int size);
            if (size <= 0 || size > 65536) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr data = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(token, TokenUser, data, size, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!ConvertSidToStringSid(Marshal.ReadIntPtr(data), out IntPtr sid)) throw new Win32Exception(Marshal.GetLastWin32Error());
                try { return Marshal.PtrToStringUni(sid); }
                finally { LocalFree(sid); }
            }
            finally { Marshal.FreeHGlobal(data); }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            internal int Length;
            internal IntPtr SecurityDescriptor;
            [MarshalAs(UnmanagedType.Bool)] internal bool InheritHandle;
        }

        [DllImport("kernel32.dll")] internal static extern uint GetCurrentProcessId();
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafePipeHandle CreateNamedPipe(string name, uint access, uint mode, uint maximumInstances,
            uint outputBuffer, uint inputBuffer, uint timeout, ref SecurityAttributes attributes);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int informationClass, IntPtr information, int size, out int needed);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool ConvertSidToStringSid(IntPtr sid, out IntPtr text);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string descriptor, uint revision, out IntPtr result, out uint size);
    }
}

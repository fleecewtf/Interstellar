using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interstellar.Utilities
{
    internal static class DesktopMouseLook
    {
        internal const float DegreesPerPixel = 0.06f;
        internal static bool IsCaptured { get; private set; }

        private static bool nativeInput;
        private static CursorLockMode previousLock;
        private static bool previousVisible;
        private static IntPtr window;
        private static NativePoint anchor;
        private static NativePoint originalPosition;
        private static NativePoint clientOrigin;
        private static NativeRect clientArea;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr handle, out NativeRect rectangle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr handle, ref NativePoint point);

        internal static void Capture()
        {
            if (IsCaptured || !Application.isFocused)
                return;

            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            IsCaptured = true;
            nativeInput = false;
            window = IntPtr.Zero;
            Cursor.visible = false;

            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                try
                {
                    IntPtr foreground = GetForegroundWindow();
                    if (!OwnsWindow(foreground))
                    {
                        Release();
                        return;
                    }
                    if (GetCursorPos(out originalPosition) &&
                        TryGetClientArea(foreground, out NativeRect area, out NativePoint origin))
                    {
                        if (!Contains(area, origin, originalPosition))
                        {
                            Release();
                            return;
                        }
                        window = foreground;
                        Cursor.lockState = CursorLockMode.Confined;
                        if (Recenter(area, origin))
                        {
                            clientArea = area;
                            clientOrigin = origin;
                            nativeInput = true;
                            return;
                        }
                    }
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            }

            if (!HasGameForeground())
            {
                Release();
                return;
            }
            Cursor.lockState = CursorLockMode.Locked;
        }

        internal static Vector2 ReadDelta(Mouse mouse)
        {
            if (!IsCaptured || mouse == null || !Application.isFocused || !HasGameForeground())
            {
                Release();
                return Vector2.zero;
            }

            if (nativeInput)
            {
                try
                {
                    if (GetForegroundWindow() != window || !OwnsWindow(window))
                    {
                        Release();
                        return Vector2.zero;
                    }

                    if (Cursor.lockState == CursorLockMode.Confined &&
                        TryGetClientArea(window, out NativeRect area, out NativePoint origin) &&
                        GetCursorPos(out NativePoint position))
                    {
                        Vector2 delta = new Vector2(position.X - anchor.X, anchor.Y - position.Y);
                        bool changed = area.Left != clientArea.Left || area.Top != clientArea.Top ||
                            area.Right != clientArea.Right || area.Bottom != clientArea.Bottom ||
                            origin.X != clientOrigin.X || origin.Y != clientOrigin.Y;
                        if (Recenter(area, origin))
                        {
                            clientArea = area;
                            clientOrigin = origin;
                            return changed ? Vector2.zero : delta;
                        }
                    }
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }

                nativeInput = false;
                if (!HasGameForeground())
                {
                    Release();
                    return Vector2.zero;
                }
                Cursor.lockState = CursorLockMode.Locked;
                return Vector2.zero;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Vector2 raw = mouse.delta.ReadValue();
            return float.IsNaN(raw.x) || float.IsInfinity(raw.x) || float.IsNaN(raw.y) || float.IsInfinity(raw.y)
                ? Vector2.zero : raw;
        }

        internal static void Release()
        {
            if (!IsCaptured)
                return;

            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
            IsCaptured = false;
            nativeInput = false;

            if (window != IntPtr.Zero && previousLock != CursorLockMode.Locked && Application.isFocused)
            {
                try
                {
                    if (GetForegroundWindow() == window && OwnsWindow(window) &&
                        TryGetClientArea(window, out NativeRect area, out NativePoint origin) &&
                        Contains(area, origin, originalPosition))
                        SetCursorPos(originalPosition.X, originalPosition.Y);
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            }
            window = IntPtr.Zero;
        }

        private static bool OwnsWindow(IntPtr handle)
        {
            return handle != IntPtr.Zero && GetWindowThreadProcessId(handle, out uint processId) != 0 &&
                processId == GetCurrentProcessId();
        }

        private static bool HasGameForeground()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
                return true;
            try
            {
                IntPtr foreground = GetForegroundWindow();
                return OwnsWindow(foreground) && (window == IntPtr.Zero || foreground == window);
            }
            catch (DllNotFoundException) { return true; }
            catch (EntryPointNotFoundException) { return true; }
        }

        private static bool Contains(NativeRect area, NativePoint origin, NativePoint point)
        {
            return point.X >= origin.X + area.Left && point.X < origin.X + area.Right &&
                point.Y >= origin.Y + area.Top && point.Y < origin.Y + area.Bottom;
        }

        private static bool TryGetClientArea(IntPtr handle, out NativeRect area, out NativePoint origin)
        {
            origin = new NativePoint();
            return GetClientRect(handle, out area) && area.Right - area.Left > 1 && area.Bottom - area.Top > 1 &&
                ClientToScreen(handle, ref origin);
        }

        private static bool Recenter(NativeRect area, NativePoint origin)
        {
            if (!Application.isFocused || !HasGameForeground())
                return false;
            int x = origin.X + area.Left + (area.Right - area.Left) / 2;
            int y = origin.Y + area.Top + (area.Bottom - area.Top) / 2;
            return SetCursorPos(x, y) && GetCursorPos(out anchor);
        }
    }
}

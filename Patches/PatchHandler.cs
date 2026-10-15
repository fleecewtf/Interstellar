using HarmonyLib;
using Interstellar.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Interstellar.Patches
{
    public class PatchHandler
    {
        public static bool IsPatched { get; internal set; }
        public static int PatchErrors { get; internal set; }

        public static bool CriticalPatchFailed { get; internal set; }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class SecurityPatch : Attribute { }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class PatchOnAwake : Attribute { }

        private static readonly HashSet<MethodBase> ProtectedMethods = new HashSet<MethodBase>();
        private static readonly Dictionary<Type, HashSet<MethodBase>> PatchClassOriginals = new Dictionary<Type, HashSet<MethodBase>>();
        private static readonly object protectedLock = new object();

        public static void PatchAll(bool awake = false)
        {
            if (IsPatched && !awake) return;

            instance ??= new HarmonyLib.Harmony(PluginInfo.GUID);

            Type[] types;
            try
            {
                types = Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }

            foreach (var type in types
                         .Where(t => t != null && t.IsClass && t.GetCustomAttribute<HarmonyPatch>() != null && (t.GetCustomAttribute<PatchOnAwake>() != null) == awake))
            {
                try
                {
                    var originals = instance.CreateClassProcessor(type).Patch();

                    if (type.GetCustomAttribute<SecurityPatch>() != null && originals != null)
                    {
                        lock (protectedLock)
                        {
                            if (!PatchClassOriginals.TryGetValue(type, out var set))
                            {
                                set = new HashSet<MethodBase>();
                                PatchClassOriginals[type] = set;
                            }

                            foreach (var m in originals)
                            {
                                if (m == null) continue;
                                ProtectedMethods.Add(m);
                                set.Add(m);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PatchErrors++;

                    if (type.GetCustomAttribute<SecurityPatch>() != null)
                        CriticalPatchFailed = true;

                    LogManager.LogError($"Failed to patch {type.FullName}: {ex}");
                }
            }

            LogManager.Log($"Patched with {PatchErrors} errors");

            if (!awake)
                IsPatched = true;
        }

        public static void UnpatchAll()
        {
            if (instance == null || !IsPatched) return;

            var securityTypes = GetTypes()
                .Where(t => t?.GetCustomAttribute<SecurityPatch>() != null)
                .ToArray();

            instance.UnpatchSelf();
            IsPatched = false;

            if (securityTypes.Length > 0)
            {
                var reinit = new HarmonyLib.Harmony(PluginInfo.GUID);
                foreach (var type in securityTypes)
                {
                    try
                    {
                        reinit.CreateClassProcessor(type).Patch();
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogError($"Failed to restore security patch {type.FullName} after UnpatchAll: {ex}");
                        CriticalPatchFailed = true;
                    }
                }
                instance = reinit;
                IsPatched = true;
            }
            else
            {
                instance = null;
            }
        }

        private static Type[] GetTypes()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null).ToArray();
            }
        }

        public static void PatchIntegrityCheck()
        {
            if (instance == null) return;

            List<MethodBase> protectedSnapshot;
            lock (protectedLock)
            {
                protectedSnapshot = ProtectedMethods.ToList();
            }

            var infoCache = new Dictionary<MethodBase, HarmonyLib.Patches>(protectedSnapshot.Count);
            var missingByType = new HashSet<Type>();

            foreach (var method in protectedSnapshot)
            {
                var info = HarmonyLib.Harmony.GetPatchInfo(method);
                infoCache[method] = info;

                if (info == null)
                {
                    CriticalPatchFailed = true;
                    continue;
                }

                bool owned = false;
                if (!ContainsOwner(info.Prefixes, instance.Id) &&
                    !ContainsOwner(info.Postfixes, instance.Id) &&
                    !ContainsOwner(info.Transpilers, instance.Id) &&
                    !ContainsOwner(info.Finalizers, instance.Id))
                {
                    RemoveForeignOwners(method, info);
                    owned = false;
                }
                else
                {
                    owned = true;
                }

                if (!owned)
                    CriticalPatchFailed = true;
            }

            var securityTypes = GetTypes().Where(t => t?.GetCustomAttribute<SecurityPatch>() != null);
            foreach (var type in securityTypes)
            {
                bool stillMissing = protectedSnapshot
                    .Where(m => ProtectedMethodBelongsToPatchClass(type, m))
                    .Any(m => !infoCache.TryGetValue(m, out var info) || info == null ||
                              !ContainsOwner(info.Prefixes, instance.Id) &&
                              !ContainsOwner(info.Postfixes, instance.Id) &&
                              !ContainsOwner(info.Transpilers, instance.Id) &&
                              !ContainsOwner(info.Finalizers, instance.Id));

                if (!stillMissing) continue;
                ReapplySecurityPatch(type);
            }
        }

        private static void ReapplySecurityPatch(Type type)
        {
            try
            {
                var reapplied = instance.CreateClassProcessor(type).Patch();
                if (reapplied != null)
                {
                    lock (protectedLock)
                    {
                        if (!PatchClassOriginals.TryGetValue(type, out var set))
                        {
                            set = new HashSet<MethodBase>();
                            PatchClassOriginals[type] = set;
                        }

                        foreach (var m in reapplied)
                        {
                            if (m == null) continue;
                            ProtectedMethods.Add(m);
                            set.Add(m);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to reapply security patch on {type.FullName}: {ex}");
            }
        }

        private static bool ContainsOwner(IEnumerable<Patch> patches, string ownerId)
        {
            foreach (var p in patches)
                if (p.owner == ownerId) return true;
            return false;
        }

        private static void RemoveForeignOwners(MethodBase method, HarmonyLib.Patches info)
        {
            var foreignOwners = new HashSet<string>();
            CollectOwners(info.Prefixes, instance.Id, foreignOwners);
            CollectOwners(info.Postfixes, instance.Id, foreignOwners);
            CollectOwners(info.Transpilers, instance.Id, foreignOwners);
            CollectOwners(info.Finalizers, instance.Id, foreignOwners);

            foreach (var owner in foreignOwners)
            {
                try { instance.Unpatch(method, HarmonyPatchType.All, owner); }
                catch (Exception ex) { LogManager.LogError($"Failed to remove patch from {method.Name}: {ex}"); }
            }
        }

        private static void CollectOwners(IEnumerable<Patch> patches, string excludeId, HashSet<string> into)
        {
            foreach (var p in patches)
                if (!string.IsNullOrEmpty(p.owner) && p.owner != excludeId)
                    into.Add(p.owner);
        }

        private static bool ProtectedMethodBelongsToPatchClass(Type patchClass, MethodBase original)
        {
            lock (protectedLock)
            {
                return PatchClassOriginals.TryGetValue(patchClass, out var known) && known.Contains(original);
            }
        }

        internal static HarmonyLib.Harmony instance;
    }
}

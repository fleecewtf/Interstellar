using Interstellar.Managers;
using Interstellar.Menu;
using Interstellar.Patches;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Interstellar
{
    internal static class Bootstrapper
    {
        private static bool initialized;
        public static bool FirstLaunch;
        public static GameObject Loader;

        private const float IntegrityCheckIntervalSeconds = 120f;

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            FirstLaunch = !Directory.Exists(PluginInfo.BaseDirectory);
            Directory.CreateDirectory(PluginInfo.BaseDirectory);
            PatchHandler.PatchAll(true);
            GorillaTagger.OnPlayerSpawned(LoadMenu);
        }
        private static void LoadMenu()
        {
            PatchHandler.PatchAll();
            Loader = new GameObject("Interstellar_Loader");
            CoroutineManager coroutineManager = Loader.AddComponent<CoroutineManager>();
            Loader.AddComponent<NotificationManager>();
            Loader.AddComponent<CustomBoardManager>();
            Loader.AddComponent<UI>();
            Loader.AddComponent<Interstellar.Utilities.GunVisualUpdater>();
            Loader.AddComponent<Interstellar.Mods.World>();
            Loader.AddComponent<Interstellar.Mods.Room>();
            Loader.AddComponent<Interstellar.Mods.GameModes.Infection>();
            Loader.AddComponent<Interstellar.Mods.GameModes.Guardian>();
            Loader.AddComponent<Interstellar.Utilities.FeatureRuntime>();
            UnityEngine.Object.DontDestroyOnLoad(Loader);
            coroutineManager.StartCoroutine(PatchIntegrityLoop());
        }

        private static IEnumerator PatchIntegrityLoop()
        {
            var wait = new WaitForSeconds(IntegrityCheckIntervalSeconds);

            while (true)
            {
                if (PatchHandler.instance != null)
                {
                    try
                    {
                        PatchHandler.PatchIntegrityCheck();
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogError($"PatchIntegrityCheck threw: {ex}");
                    }
                }

                yield return wait;
            }
        }
    }
}

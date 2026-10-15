using System;
using System.Collections.Generic;
using Interstellar.Classes.Menu;
using Interstellar.Managers;
using Interstellar.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Interstellar.Mods
{
    public sealed class World : MonoBehaviour
    {
        private const string NetworkTriggerPath = "TriggerZones_Prefab/JoinRoomTriggers_Prefab";
        private const string BoundaryVolumeName = "LevelBoundaryForceVolume";
        private static readonly string[] TimeLabels = { "Night", "Morning", "Day" };
        private static readonly int[] TimeIndices = { 0, 1, 3 };
        private static int selectedTimeIndex = -1;
        private static bool noRain;
        private static bool presentationRefreshPending;
        private static GameObject triggerOwner;
        private static bool originalTriggerActive;
        private static bool networkTriggersDisabled;
        private static bool windBarriersDisabled;
        private static float nextWindScan;
        private static readonly Dictionary<Collider, bool> windColliders = new Dictionary<Collider, bool>();
        private static readonly List<Collider> removedWindColliders = new List<Collider>();

        private void Awake()
        {
            RoomSystem.JoinedRoomEvent += OnRoomJoined;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void LateUpdate()
        {
            if (presentationRefreshPending) RefreshPresentation();
            UpdateNetworkTriggers();
            UpdateWindBarriers();
        }

        private void OnDestroy()
        {
            RoomSystem.JoinedRoomEvent -= OnRoomJoined;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreAll();
        }

        private void OnRoomJoined()
        {


            presentationRefreshPending = selectedTimeIndex >= 0 || noRain;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            presentationRefreshPending = selectedTimeIndex >= 0 || noRain;
            nextWindScan = 0f;
        }

        public static void EnableMorning() => EnableTime(1);
        public static void EnableDay() => EnableTime(3);
        public static void EnableNight() => EnableTime(0);
        public static void DisableMorning() => DisableTime(1);
        public static void DisableDay() => DisableTime(3);
        public static void DisableNight() => DisableTime(0);

        private static void EnableTime(int timeIndex)
        {
            for (int i = 0; i < TimeLabels.Length; i++)
            {
                if (TimeIndices[i] == timeIndex) continue;
                ButtonInfo button = Buttons.GetIndex(TimeLabels[i]);
                if (button == null) continue;
                button.enabled = false;
                button.firstFrame = true;
            }
            selectedTimeIndex = timeIndex;
            RefreshPresentation();
        }

        private static void DisableTime(int timeIndex)
        {
            if (selectedTimeIndex != timeIndex) return;
            selectedTimeIndex = -1;
            RefreshPresentation();
        }

        internal static bool TryGetVisualTime(BetterDayNightManager manager, out int index)
        {
            index = selectedTimeIndex;
            return manager != null && manager == BetterDayNightManager.instance &&
                index >= 0 && manager.timeOfDayRange != null && index < manager.timeOfDayRange.Length;
        }

        internal static bool SuppressRain(BetterDayNightManager manager) =>
            noRain && manager != null && manager == BetterDayNightManager.instance;

        public static void EnableNoRain()
        {
            noRain = true;
            RefreshPresentation();
        }

        public static void DisableNoRain()
        {
            noRain = false;
            RefreshPresentation();
        }

        private static void RefreshPresentation()
        {
            BetterDayNightManager manager = BetterDayNightManager.instance;
            if (!ReadyForPresentation(manager))
            {
                presentationRefreshPending = true;
                return;
            }

            presentationRefreshPending = false;



            manager.ChangeMaps(manager.currentTimeIndex, (manager.currentTimeIndex + 1) % manager.timeOfDayRange.Length);
            manager.ChangeLerps(manager.currentLerp);
            manager.RequestRepopulateLightmaps();
            RefreshRainControllers(manager);
        }

        private static bool ReadyForPresentation(BetterDayNightManager manager) =>
            manager != null && manager.timeOfDayRange != null && manager.timeOfDayRange.Length > 0 &&
            manager.currentTimeIndex >= 0 && manager.currentTimeIndex < manager.timeOfDayRange.Length &&
            manager.weatherCycle != null && manager.weatherCycle.Length > 0 &&
            manager.currentWeatherIndex >= 0 && manager.currentWeatherIndex < manager.weatherCycle.Length;

        private static void RefreshRainControllers(BetterDayNightManager manager)
        {
            foreach (TimeOfDayDependentAudio rain in UnityEngine.Object.FindObjectsByType<TimeOfDayDependentAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rain == null || !rain.gameObject.scene.IsValid() || !rain.gameObject.scene.isLoaded ||
                    rain.myWeather != BetterDayNightManager.WeatherType.Raining || rain.dependentStuff == null ||
                    rain.volumes == null || rain.volumes.Length < manager.timeOfDayRange.Length)
                    continue;
                if (rain.includesAudio && (rain.audioSources == null || Array.Exists(rain.audioSources, source => source == null)))
                    continue;

                try
                {



                    rain.SliceUpdate();
                }
                catch (Exception error)
                {
                    LogManager.LogError("Could not refresh rain controller " + rain.name + ": " + error);
                }
            }
        }

        public static void DisableNetworkTriggers()
        {
            networkTriggersDisabled = true;
            UpdateNetworkTriggers();
        }

        public static void RestoreNetworkTriggers()
        {
            networkTriggersDisabled = false;
            GameObject previousOwner = triggerOwner;
            triggerOwner = null;
            if (previousOwner != null) previousOwner.SetActive(originalTriggerActive);
        }

        private static void UpdateNetworkTriggers()
        {
            if (!networkTriggersDisabled) return;
            if (triggerOwner == null)
            {
                GameObject environment = GameObject.Find("Environment Objects");
                if (environment == null) return;
                Transform triggers = environment.transform.Find(NetworkTriggerPath);
                if (triggers == null) return;
                triggerOwner = triggers.gameObject;
                originalTriggerActive = triggerOwner.activeSelf;
            }
            if (triggerOwner.activeSelf) triggerOwner.SetActive(false);
        }

        public static void DisableWindBarriers()
        {
            windBarriersDisabled = true;
            nextWindScan = 0f;
            UpdateWindBarriers();
        }

        public static void RestoreWindBarriers()
        {
            windBarriersDisabled = false;
            foreach (KeyValuePair<Collider, bool> saved in windColliders)
                if (saved.Key != null) saved.Key.enabled = saved.Value;
            windColliders.Clear();
            removedWindColliders.Clear();
        }

        private static void UpdateWindBarriers()
        {
            if (!windBarriersDisabled) return;


            removedWindColliders.Clear();
            foreach (KeyValuePair<Collider, bool> saved in windColliders)
            {
                if (saved.Key == null) removedWindColliders.Add(saved.Key);
                else if (saved.Key.enabled) saved.Key.enabled = false;
            }
            foreach (Collider removed in removedWindColliders) windColliders.Remove(removed);

            if (Time.unscaledTime < nextWindScan) return;
            nextWindScan = Time.unscaledTime + 1f;
            foreach (ForceVolume volume in UnityEngine.Object.FindObjectsByType<ForceVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume == null || !volume.gameObject.scene.IsValid() || !volume.gameObject.scene.isLoaded || !IsWindBarrier(volume.transform))
                    continue;


                foreach (Collider collider in volume.GetComponents<Collider>())
                {
                    if (collider == null || !collider.isTrigger) continue;
                    if (!windColliders.ContainsKey(collider)) windColliders.Add(collider, collider.enabled);
                    collider.enabled = false;
                }
            }
        }

        internal static bool IsWindBarrier(Transform transform)
        {


            for (Transform ancestor = transform; ancestor != null; ancestor = ancestor.parent)
            {
                if (IsNumberedBoundaryName(ancestor.name)) return true;
                if (ancestor.name == "ReturnToCity" && HasAncestor(ancestor.parent, "ForceVolumesMetro_Rivers")) return true;
                if ((ancestor.name == "CliffForces" || ancestor.name == "ReturnToBeach") && HasAncestor(ancestor.parent, "ForceVolumesOcean_Combo_V2")) return true;
                if (ancestor.name == "Border" && HasAncestor(ancestor.parent, "ForceVols_Thermals_Border_MidMap")) return true;
                if (ancestor.name == "ForceVols" && ancestor.parent != null && ancestor.parent.name == "Border" && HasAncestor(ancestor.parent.parent, "Bayou_Layout")) return true;
            }
            return false;
        }

        private static bool HasAncestor(Transform transform, string name)
        {
            for (Transform ancestor = transform; ancestor != null; ancestor = ancestor.parent)
                if (ancestor.name == name) return true;
            return false;
        }

        private static bool IsNumberedBoundaryName(string name)
        {
            if (name == BoundaryVolumeName) return true;
            string prefix = BoundaryVolumeName + " (";
            if (name == null || !name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(")", StringComparison.Ordinal)) return false;
            int digitEnd = name.Length - 1;
            if (digitEnd == prefix.Length) return false;
            for (int i = prefix.Length; i < digitEnd; i++)
                if (name[i] < '0' || name[i] > '9') return false;
            return true;
        }

        public static void RestoreAll()
        {
            selectedTimeIndex = -1;
            noRain = false;
            RestoreNetworkTriggers();
            RestoreWindBarriers();
            RefreshPresentation();
            presentationRefreshPending = false;
        }
    }
}

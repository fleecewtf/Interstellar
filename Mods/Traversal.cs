using GorillaLocomotion;
using Interstellar.Managers;
using Interstellar.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Interstellar.Menu.Main;

namespace Interstellar.Mods
{
    public static class Traversal
    {
        public sealed class MapDestination
        {
            public string Name { get; }
            internal GTZone Zone { get; }
            internal string SpawnName { get; }
            internal MapDestination(string name, GTZone zone, string spawn) { Name = name; Zone = zone; SpawnName = spawn; }
        }

        public static IReadOnlyList<MapDestination> MapDestinations { get; } = Array.AsReadOnly(new[] {
            new MapDestination("Forest", GTZone.forest, "JoinPublicRoom - Forest, Tree Exit"),
            new MapDestination("City", GTZone.city, "JoinPublicRoom - City Front"),
            new MapDestination("Canyons", GTZone.canyon, "JoinPublicRoom - Canyon"),
            new MapDestination("Clouds", GTZone.skyJungle, "JoinPublicRoom - Clouds From Computer"),
            new MapDestination("Caves", GTZone.cave, "JoinPublicRoom - Cave"),
            new MapDestination("Beach", GTZone.beach, "JoinPublicRoom - Beach for Computer"),
            new MapDestination("Mountains", GTZone.mountain, "JoinPublicRoom - Mountain"),
            new MapDestination("Basement", GTZone.basement, "JoinPublicRoom - Basement For Computer"),
            new MapDestination("Metropolis", GTZone.Metropolis, "JoinPublicRoom - Metropolis from Computer"),
            new MapDestination("Arcade", GTZone.arcade, "JoinPublicRoom - City frm Arcade"),
            new MapDestination("Critters", GTZone.critters, "JoinPublicRoom - City from Critters"),
            new MapDestination("Rotating", GTZone.rotating, "JoinPublicRoom - Rotating Map"),
            new MapDestination("Bayou", GTZone.bayou, "JoinPublicRoom - BayouComputer2")
        });

        private static GTPlayer owner;
        private static int sceneHandle = int.MinValue;
        private static string roomName;
        private static bool wasInRoom;
        private static bool previousFire;
        private static bool fireAwaitRelease;
        private static GTPlayer slideOwner;
        private static float previousSlide;
        private static bool slideEnabled;
        private static bool noSlipperySurfaces;
        private static Coroutine mapCoroutine;
        private static CoroutineManager mapScheduler;
        private static GTPlayer mapPlayer;
        private static Rigidbody mapBody;
        private static bool mapWasKinematic, mapWasMovementDisabled;
        private static GTZone[] previousZones;
        private static GorillaNetworking.GorillaNetworkJoinTrigger mapJoinGuard;
        private static Collider[] mapGuardColliders;
        private static float mapGuardUntil;

        public static void ObserveContext()
        {
            GTPlayer player = GTPlayer.Instance;
            NetworkSystem network = NetworkSystem.Instance;
            bool inRoom = network != null && network.InRoom;
            string currentRoom = inRoom ? network.RoomName : null;
            int scene = SceneManager.GetActiveScene().handle;
            if (owner != player || scene != sceneHandle || inRoom != wasInRoom || !string.Equals(currentRoom, roomName, StringComparison.Ordinal))
            {
                if (owner != player || inRoom != wasInRoom || !string.Equals(currentRoom, roomName, StringComparison.Ordinal))
                {
                    CancelMap(false);
                    mapJoinGuard = null; mapGuardColliders = null;
                }
                ResetTransient();
                RestoreSlide();
                owner = player;
                sceneHandle = scene;
                wasInRoom = inRoom;
                roomName = currentRoom;
                if (slideEnabled) CaptureSlide();
            }

            if (mapJoinGuard != null) BlockMapJoin(mapJoinGuard);
        }

        private static bool Ready()
        {
            ObserveContext();
            return owner != null && GorillaTagger.Instance != null && GorillaTagger.Instance.rigidbody != null &&
                GorillaTagger.Instance.offlineVRRig != null && !owner.inOverlay && FeatureInput.CanUseGameInput;
        }

        public static void TeleportGun()
        {
            bool fire = GunUtilities.GetGunInput(true);
            if (!Ready()) { previousFire = fire; fireAwaitRelease = true; return; }
            if (fireAwaitRelease) { if (fire) return; fireAwaitRelease = false; }
            if (!GunUtilities.GetGunInput(false)) { previousFire = false; return; }
            RaycastHit hit = GunUtilities.RenderGun();
            if (fire && !previousFire && hit.collider != null && hit.collider.GetComponentInParent<VRRig>() == null)
            {
                float scale = Mathf.Max(0.1f, owner.scale);
                TeleportBody(hit.point + hit.normal * (0.3f * scale) + Vector3.up * (0.6f * scale));
            }
            previousFire = fire;
        }

        public static void DisableTeleportGun() { previousFire = false; fireAwaitRelease = true; GunUtilities.ReleaseLock(); }

        public static void TeleportToMap(string name)
        {
            ObserveContext();

            if (owner == null || GorillaTagger.Instance == null || GorillaTagger.Instance.offlineVRRig == null ||
                !(UnityEngine.XR.XRSettings.isDeviceActive || Application.isFocused) || inTextInput || CurrentPrompt != null || Interstellar.Menu.UI.HasKeyboardFocus) return;
            MapDestination selected = null;
            foreach (MapDestination destination in MapDestinations)
                if (string.Equals(name, destination.Name, StringComparison.OrdinalIgnoreCase)) { selected = destination; break; }
            if (selected == null) return;
            Transform spawn = null;
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.isLoaded &&
                    candidate.name == selected.SpawnName && candidate.GetComponent<GorillaNetworking.GorillaNetworkJoinTrigger>() != null)
                { spawn = candidate; break; }
            }
            if (spawn == null || ZoneManagement.instance == null)
            {
                NotificationManager.SendNotification(selected.Name + " is not available in the current scene.");
                return;
            }
            if (CoroutineManager.instance == null || GorillaTagger.Instance.rigidbody == null) return;
            CancelMap(false);
            ResetTransient();
            var active = new List<GTZone>();
            foreach (GTZone zone in Enum.GetValues(typeof(GTZone)))
                if (ZoneManagement.instance.IsZoneActive(zone)) active.Add(zone);
            previousZones = active.ToArray();
            mapPlayer = owner; mapBody = GorillaTagger.Instance.rigidbody;
            mapWasMovementDisabled = owner.disableMovement; mapWasKinematic = mapBody.isKinematic;
            owner.disableMovement = true; mapBody.isKinematic = true;
            mapJoinGuard = spawn.GetComponent<GorillaNetworking.GorillaNetworkJoinTrigger>();
            mapGuardColliders = spawn.GetComponentsInChildren<Collider>(true);
            mapGuardUntil = Time.unscaledTime + 1f;
            mapScheduler = CoroutineManager.instance;
            mapCoroutine = mapScheduler.StartCoroutine(TeleportAfterMapLoads(selected, spawn));
        }

        private static IEnumerator TeleportAfterMapLoads(MapDestination destination, Transform spawn)
        {
            bool arrived = false;
            try
            {
                ZoneManagement.SetActiveZones(new[] { destination.Zone });
                float deadline = Time.unscaledTime + 10f;
                yield return null;
                while (mapPlayer == owner && owner != null && spawn != null && ZoneManagement.instance != null &&
                    !ZoneManagement.instance.IsSceneLoaded(destination.Zone) && Time.unscaledTime < deadline)
                    yield return null;
                if (mapPlayer == owner && owner != null && spawn != null && ZoneManagement.instance != null &&
                    ZoneManagement.instance.IsSceneLoaded(destination.Zone))
                {

                    TeleportBody(spawn.position + Vector3.up * Mathf.Max(0.5f, owner.scale));
                    mapGuardUntil = Time.unscaledTime + 1f;
                    arrived = true;
                }
                else
                {
                    if (previousZones != null && previousZones.Length > 0 && ZoneManagement.instance != null)
                    {
                        ZoneManagement.SetActiveZones(previousZones);
                        float restoreDeadline = Time.unscaledTime + 5f;
                        while (!PreviousZonesLoaded() && Time.unscaledTime < restoreDeadline) yield return null;
                    }
                    NotificationManager.SendNotification(destination.Name + " could not finish loading. Try again after the map loads.");
                }
            }
            finally
            {
                RestoreMapMovement();
                previousZones = null; mapCoroutine = null; mapScheduler = null;
                if (!arrived) { mapJoinGuard = null; mapGuardColliders = null; }
            }
        }

        private static bool PreviousZonesLoaded()
        {
            if (ZoneManagement.instance == null || previousZones == null) return true;
            foreach (GTZone zone in previousZones)
                if (!ZoneManagement.instance.IsSceneLoaded(zone)) return false;
            return true;
        }

        private static void RestoreMapMovement()
        {
            if (mapPlayer != null) mapPlayer.disableMovement = mapWasMovementDisabled;
            if (mapBody != null) mapBody.isKinematic = mapWasKinematic;
            mapPlayer = null; mapBody = null;
        }

        private static void CancelMap(bool restoreZone)
        {
            GTZone[] zones = previousZones;
            if (mapScheduler != null && mapCoroutine != null) mapScheduler.StopCoroutine(mapCoroutine);
            RestoreMapMovement();
            mapCoroutine = null; mapScheduler = null; previousZones = null;
            if (restoreZone && zones != null && zones.Length > 0 && ZoneManagement.instance != null)
                ZoneManagement.SetActiveZones(zones);
        }

        public static void PrepareExplicitRoomJoin()
        {

            CancelMap(true);
            mapJoinGuard = null;
            mapGuardColliders = null;
            mapGuardUntil = 0f;
        }

        internal static bool BlockMapJoin(GorillaNetworking.GorillaNetworkJoinTrigger trigger)
        {
            if (mapPlayer != null) return true;
            if (mapJoinGuard == null || trigger != mapJoinGuard) return false;
            if (Time.unscaledTime < mapGuardUntil) return true;
            Transform head = GorillaTagger.Instance != null ? GorillaTagger.Instance.headCollider?.transform : null;
            if (head != null && mapGuardColliders != null)
                foreach (Collider collider in mapGuardColliders)
                {
                    if (collider == null) continue;
                    if (collider is BoxCollider box)
                    {
                        Bounds localBounds = new Bounds(box.center, box.size);
                        localBounds.Expand(0.25f);
                        if (localBounds.Contains(box.transform.InverseTransformPoint(head.position))) return true;
                    }
                    else if (collider.bounds.Contains(head.position)) return true;
                }
            mapJoinGuard = null; mapGuardColliders = null;
            return false;
        }

        private static void TeleportBody(Vector3 bodyPosition)
        {
            if (owner == null || owner.bodyCollider == null || !Finite(bodyPosition)) return;
            Vector3 position = owner.transform.position + bodyPosition - owner.bodyCollider.transform.position;
            owner.TeleportTo(position, owner.transform.rotation, false);
            Movement.lastPosition = owner.transform.position;
        }

        public static void EnableLowGravity() => LocalGravity.EnableLowGravity();
        public static void DisableLowGravity() => LocalGravity.DisableLowGravity();

        public static void EnableNoSlipperySurfaces() => noSlipperySurfaces = true;
        public static void DisableNoSlipperySurfaces() => noSlipperySurfaces = false;
        internal static bool RemoveSlip(GTPlayer player) => noSlipperySurfaces && player == GTPlayer.Instance && !player.inOverlay && FeatureInput.CanUseGameInput;

        public static void EnableSlideControl() { slideEnabled = true; ObserveContext(); CaptureSlide(); }
        private static void CaptureSlide()
        {
            if (!slideEnabled || owner == null || slideOwner == owner) return;
            slideOwner = owner; previousSlide = owner.slideControl;
        }
        public static void SlideControl()
        {
            if (!Ready()) { RestoreSlide(false); return; }
            CaptureSlide();
            if (slideOwner != null) slideOwner.slideControl = 1f;
        }
        private static void RestoreSlide(bool releaseOwner = true)
        {
            if (slideOwner != null) slideOwner.slideControl = previousSlide;
            if (releaseOwner) slideOwner = null;
        }
        public static void DisableSlideControl() { slideEnabled = false; RestoreSlide(); }

        public static void ResetTransient()
        {
            DisableTeleportGun();
            LocalGravity.ClearWallContact();
        }
        public static void Cleanup()
        {
            CancelMap(true); mapJoinGuard = null; mapGuardColliders = null;
            ResetTransient(); DisableSlideControl(); DisableNoSlipperySurfaces(); DisableLowGravity();
            owner = null; sceneHandle = int.MinValue; roomName = null; wasInRoom = false;
        }
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}

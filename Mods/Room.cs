using GorillaNetworking;
using Interstellar.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Interstellar.Mods
{
    public sealed class Room : MonoBehaviour
    {
        private static NetworkSystem trackedNetwork;
        private static string lastRoomName;
        private static Coroutine joinCoroutine;
        private static CoroutineManager coroutineOwner;
        private static int joinGeneration;
        private static PhotonNetworkController pendingController;
        private static GorillaNetworkJoinTrigger pendingTrigger;
        private static string pendingRoomName;

        private void Awake() => ObserveRoom();
        private void Update() => ObserveRoom();
        private void OnDestroy() => Cleanup();

        private static void ObserveRoom()
        {
            NetworkSystem network = NetworkSystem.Instance;
            if (trackedNetwork != network)
            {
                CancelJoin();
                trackedNetwork = network;
            }
            if (IsConnected(network))
                lastRoomName = network.RoomName;
        }

        public static bool IsConnected(NetworkSystem network) =>
            network != null && network.InRoom && network.netState == NetSystemState.InGame &&
            !string.IsNullOrWhiteSpace(network.RoomName) &&
            (GorillaComputer.instance == null || !GorillaComputer.instance.roomNotAllowed);

        public static void Reconnect()
        {
            ObserveRoom();
            if (string.IsNullOrWhiteSpace(lastRoomName))
            {
                NotificationManager.SendNotification("There is no previous room to reconnect to.");
                return;
            }
            BeginJoin(lastRoomName, null);
        }

        public static void Disconnect()
        {
            CancelJoin();
            PhotonNetworkController.Instance?.ClearDeferredJoin();
            NetworkSystem network = NetworkSystem.Instance;
            if (network != null)
                _ = network.ReturnToSinglePlayer();
        }

        public static void JoinRoom(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return;
            roomName = roomName.Trim().ToUpperInvariant();
            if (roomName.Length > 10) return;
            foreach (char character in roomName)
                if (!((character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9'))) return;
            ObserveRoom();
            NetworkSystem network = NetworkSystem.Instance;
            if (network != null && network.InRoom && string.Equals(network.RoomName, roomName, StringComparison.OrdinalIgnoreCase)) return;
            BeginJoin(roomName, null);
        }

        public static void JoinRandom()
        {
            ObserveRoom();
            GorillaNetworkJoinTrigger trigger = FindPublicJoinTrigger();
            if (trigger == null)
            {
                NotificationManager.SendNotification("Move into a public map before joining a random room.");
                return;
            }
            BeginJoin(null, trigger);
        }

        private static GorillaNetworkJoinTrigger FindPublicJoinTrigger()
        {
            PhotonNetworkController controller = PhotonNetworkController.Instance;
            GorillaComputer computer = GorillaComputer.instance;
            ZoneEntityBSP zone = VRRig.LocalRig == null ? null : VRRig.LocalRig.zoneEntity;
            if (controller == null || computer == null || zone == null || zone.currentNode == null) return null;

            GorillaNetworkJoinTrigger fallback = null;
            foreach (GorillaNetworkJoinTrigger candidate in UnityEngine.Object.FindObjectsByType<GorillaNetworkJoinTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {


                GorillaNetworkJoinTrigger trigger = string.IsNullOrEmpty(candidate.networkZone)
                    ? null : computer.GetJoinTriggerForZone(candidate.networkZone);
                if (trigger == null || trigger == controller.privateTrigger || trigger.isSubsOnly ||
                    string.Equals(trigger.networkZone, "private", StringComparison.OrdinalIgnoreCase) ||
                    trigger.myCollider == null || trigger.myCollider.myAllowedMapsToJoin == null) continue;

                bool allowsMap = false;
                foreach (string map in trigger.myCollider.myAllowedMapsToJoin)
                    if (!string.IsNullOrEmpty(map) && trigger.networkZone.StartsWith(map, StringComparison.Ordinal))
                    {
                        allowsMap = true;
                        break;
                    }
                if (!allowsMap) continue;

                GroupJoinZoneAB requiredZones = trigger.groupJoinRequiredZonesAB;
                if (requiredZones != default && !requiredZones.HasAnyFlag(zone.GroupZone)) continue;
                if (trigger.zone == zone.currentZone) return trigger;
                if (requiredZones == default) continue;
                if (trigger == controller.currentJoinTrigger) fallback = trigger;
                else if (fallback == null) fallback = trigger;
            }
            return fallback;
        }

        private static void BeginJoin(string roomName, GorillaNetworkJoinTrigger randomTrigger)
        {
            NetworkSystem network = NetworkSystem.Instance;
            CoroutineManager scheduler = CoroutineManager.instance;
            PhotonNetworkController controller = PhotonNetworkController.Instance;
            if (network == null || scheduler == null || controller == null) return;
            if (joinCoroutine != null || (network.netState != NetSystemState.Idle && network.netState != NetSystemState.InGame))
            {
                NotificationManager.SendNotification("A room connection is already in progress.");
                return;
            }
            Traversal.PrepareExplicitRoomJoin();
            CancelJoin();
            controller.ClearDeferredJoin();
            coroutineOwner = scheduler;
            int generation = joinGeneration;
            joinCoroutine = scheduler.StartCoroutine(JoinAfterLeaving(network, controller, roomName, randomTrigger, generation));
        }

        private static IEnumerator JoinAfterLeaving(NetworkSystem network, PhotonNetworkController controller,
            string roomName, GorillaNetworkJoinTrigger randomTrigger, int generation)
        {
            try
            {
                yield return null;
                if (!IsCurrent(network, controller, generation)) yield break;
                if (network.InRoom)
                {
                    Task leaving = network.ReturnToSinglePlayer();
                    float deadline = Time.realtimeSinceStartup + 10f;
                    while (IsCurrent(network, controller, generation) &&
                        (network.InRoom || network.netState != NetSystemState.Idle || (leaving != null && !leaving.IsCompleted)) &&
                        Time.realtimeSinceStartup < deadline)
                        yield return null;
                    if (!IsCurrent(network, controller, generation)) yield break;
                    if (network.InRoom || network.netState != NetSystemState.Idle || (leaving != null && (!leaving.IsCompleted || leaving.IsFaulted || leaving.IsCanceled)))
                    {
                        CompleteJoin(generation);
                        NotificationManager.SendNotification("Could not leave the current room. Try again.");
                        yield break;
                    }
                }
                if (!IsCurrent(network, controller, generation) || network.InRoom) yield break;
                if (network.netState != NetSystemState.Idle)
                {
                    float deadline = Time.realtimeSinceStartup + 10f;
                    while (IsCurrent(network, controller, generation) && !network.InRoom && network.netState != NetSystemState.Idle && Time.realtimeSinceStartup < deadline)
                        yield return null;
                }
                if (!IsCurrent(network, controller, generation)) yield break;
                if (network.InRoom || network.netState != NetSystemState.Idle)
                {
                    CompleteJoin(generation);
                    if (!network.InRoom) NotificationManager.SendNotification("Room connection is not ready. Try again.");
                    yield break;
                }
                if (roomName == null)
                {


                    randomTrigger = FindPublicJoinTrigger();
                    if (randomTrigger == null)
                    {
                        NotificationManager.SendNotification("Move into a public map before joining a random room.");
                        yield break;
                    }
                    GorillaComputer.instance.allowedMapsToJoin = randomTrigger.myCollider.myAllowedMapsToJoin;
                    var properties = new List<(string, string)>();
                    if (randomTrigger.additionalJoinCustomProperties != null)
                        foreach (AdditionalCustomProperty property in randomTrigger.additionalJoinCustomProperties)
                            properties.Add((property.key, property.value));
                    controller.ClearDeferredJoin();
                    pendingController = controller;
                    pendingTrigger = randomTrigger;
                    controller.AttemptToJoinPublicRoom(randomTrigger, JoinType.Solo, properties, false);
                }
                else
                {
                    pendingController = controller;
                    pendingTrigger = controller.privateTrigger;
                    pendingRoomName = roomName;
                    controller.AttemptToJoinSpecificRoom(roomName, JoinType.Solo);
                }


                yield return null;
                while (IsCurrent(network, controller, generation) &&
                    (controller.deferredJoin || (network.netState != NetSystemState.Idle && network.netState != NetSystemState.InGame)))
                    yield return null;
            }
            finally
            {
                CompleteJoin(generation);
            }
        }

        private static bool IsCurrent(NetworkSystem network, PhotonNetworkController controller, int generation) =>
            generation == joinGeneration && network != null && network == NetworkSystem.Instance &&
            controller != null && controller == PhotonNetworkController.Instance;

        private static void CompleteJoin(int generation)
        {
            if (generation != joinGeneration) return;
            joinCoroutine = null;
            coroutineOwner = null;
            pendingController = null;
            pendingTrigger = null;
            pendingRoomName = null;
        }

        public static void CancelJoin()
        {
            if (pendingController != null && pendingController.deferredJoin &&
                pendingController.currentJoinTrigger == pendingTrigger &&
                (pendingRoomName == null || string.Equals(pendingController.customRoomID, pendingRoomName, StringComparison.Ordinal)))
                pendingController.ClearDeferredJoin();
            joinGeneration++;
            if (joinCoroutine != null && coroutineOwner != null) coroutineOwner.StopCoroutine(joinCoroutine);
            joinCoroutine = null;
            coroutineOwner = null;
            pendingController = null;
            pendingTrigger = null;
            pendingRoomName = null;
        }

        public static void Cleanup()
        {
            CancelJoin();
            trackedNetwork = null;
        }
    }
}

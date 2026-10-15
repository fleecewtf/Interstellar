using System;
using System.Linq;
using GorillaGameModes;
using GorillaNetworking;
using HarmonyLib;
using Interstellar.Managers;
using Interstellar.Menu;
using Photon.Pun;

namespace Interstellar.Mods
{
    public static class JoinSettings
    {
        private static readonly string[] Queues = { "DEFAULT", "MINIGAMES", "COMPETITIVE" };
        private static string preferredRegion;
        private static GorillaComputer computerOwner;
        private static string originalQueue, originalMode, lastQueue, lastMode;
        private static bool originalTroopQueue;
        private static int originalTroopPopulation;
        private static string originalFixedRegion, lastFixedRegion;
        private static bool regionCaptured;

        public static string Queue => GorillaComputer.instance?.currentQueue ?? "DEFAULT";
        public static string Mode => GorillaComputer.instance?.currentGameMode?.Value ?? "INFECTION";
        public static string Region => preferredRegion ?? "";

        private static void Capture(GorillaComputer computer)
        {
            if (computerOwner == computer) return;
            RestoreComputer();
            computerOwner = computer;
            originalQueue = computer.currentQueue;
            originalMode = computer.currentGameMode?.Value;
            originalTroopQueue = computer.troopQueueActive;
            originalTroopPopulation = computer.currentTroopPopulation;
        }

        public static void CycleQueue()
        {
            int next = (Array.IndexOf(Queues, Queue) + 1) % Queues.Length;
            SetQueue(Queues[next]);
            NotificationManager.SendNotification("Queue set for future joins: " + Friendly(Queue) + ".");
        }

        public static void SetQueue(string queue)
        {
            GorillaComputer computer = GorillaComputer.instance;
            if (computer == null || !Queues.Contains(queue)) return;
            Capture(computer);
            computer.currentQueue = lastQueue = queue;
            computer.troopQueueActive = false;
            computer.currentTroopPopulation = -1;
            RefreshLabels();
        }

        public static void CycleMode()
        {
            string[] modes = GameMode.gameModeKeyByName.OrderBy(entry => entry.Value).Select(entry => entry.Key)
                .Where(name => name != "NONE" && name != "CUSTOM").ToArray();
            if (modes.Length == 0) { NotificationManager.SendNotification("Gamemodes are not available yet."); return; }
            SetMode(modes[(Array.IndexOf(modes, Mode) + 1) % modes.Length]);
            NotificationManager.SendNotification("Gamemode set for future joins: " + Friendly(Mode) + ".");
        }

        public static void SetMode(string mode)
        {
            GorillaComputer computer = GorillaComputer.instance;
            if (computer == null || string.IsNullOrEmpty(mode) || !GameMode.gameModeKeyByName.ContainsKey(mode)) return;
            Capture(computer);
            computer.SetGameModeWithoutButton(mode);
            lastMode = mode;
            RefreshLabels();
        }

        public static void CycleRegion()
        {
            string[] regions = NetworkSystem.Instance?.regionNames;
            if (regions == null || regions.Length == 0) return;
            int next = preferredRegion == null ? 0 : Array.IndexOf(regions, preferredRegion) + 1;
            SetRegion(next >= regions.Length ? "" : regions[next]);
            NotificationManager.SendNotification("Region set for future public joins: " + RegionLabel + ".");
        }

        public static void SetRegion(string region)
        {
            string[] regions = NetworkSystem.Instance?.regionNames;
            preferredRegion = regions != null && Array.IndexOf(regions, region) >= 0 ? region : null;
            RefreshLabels();
        }

        private static string RegionLabel => preferredRegion == null ? "Auto" : preferredRegion.ToUpperInvariant();
        private static string Friendly(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Default";
            switch (value.ToUpperInvariant())
            {
                case "BATTLE": return "Paintbrawl";
                case "SUPERINFECT": return "Guardian";
                case "FREEZETAG": return "Freeze Tag";
                case "GHOST": return "Ghost Tag";
                default: return char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();
            }
        }

        public static void RefreshLabels()
        {
            SetLabel("Join Queue", "Queue · " + Friendly(Queue));
            SetLabel("Join Gamemode", "Gamemode · " + Friendly(Mode));
            SetLabel("Join Region", "Region · " + RegionLabel);
        }

        private static void SetLabel(string name, string label)
        {
            var button = Buttons.GetIndex(name);
            if (button != null) button.overlapText = label;
        }

        internal static void ApplyRegionForPublicJoin()
        {
            NetworkSystem network = NetworkSystem.Instance;
            PhotonNetworkController controller = PhotonNetworkController.Instance;
            if (preferredRegion == null || !(network is NetworkSystemPUN) || network.netState != NetSystemState.Connecting ||
                network.groupJoinInProgress || controller == null ||
                controller.currentJoinTrigger == null || controller.currentJoinTrigger == controller.privateTrigger) return;
            int index = Array.IndexOf(network.regionNames, preferredRegion);
            var settings = PhotonNetwork.PhotonServerSettings?.AppSettings;
            if (index < 0 || settings == null) return;
            if (!regionCaptured) { originalFixedRegion = settings.FixedRegion; regionCaptured = true; }


            settings.FixedRegion = lastFixedRegion = preferredRegion;
            network.currentRegionIndex = index;
        }

        private static void RestoreComputer()
        {
            if (computerOwner != null)
            {
                if (computerOwner.currentQueue == lastQueue)
                {
                    computerOwner.currentQueue = originalQueue;
                    computerOwner.troopQueueActive = originalTroopQueue;
                    computerOwner.currentTroopPopulation = originalTroopPopulation;
                }
                if (computerOwner.currentGameMode?.Value == lastMode && originalMode != null)
                    computerOwner.SetGameModeWithoutButton(originalMode);
            }
            computerOwner = null;
            lastQueue = lastMode = null;
        }

        public static void Shutdown()
        {
            RestoreComputer();
            var settings = PhotonNetwork.PhotonServerSettings?.AppSettings;
            if (regionCaptured && settings != null && settings.FixedRegion == lastFixedRegion)
                settings.FixedRegion = originalFixedRegion;
            preferredRegion = null;
            regionCaptured = false;
        }
    }

    [HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.ConnectUsingSettings), new Type[0])]
    internal static class PreferredPublicRegionPatch
    {
        private static void Prefix() => JoinSettings.ApplyRegionForPublicJoin();
    }
}

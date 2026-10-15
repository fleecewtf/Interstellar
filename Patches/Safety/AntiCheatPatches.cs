using HarmonyLib;
using UnityEngine;
using static Interstellar.Patches.PatchHandler;

namespace Interstellar.Patches.Safety
{
    public class AntiCheatPatches
    {
        [SecurityPatch]
        [HarmonyPatch(typeof(MonkeAgent), nameof(MonkeAgent.SendReport))]
        public class SendReportPatch
        {
            private static bool Prefix() =>
                false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(MonkeAgent), nameof(MonkeAgent.CloseInvalidRoom))]
        public class NoCloseInvalidRoom
        {
            private static bool Prefix() =>
                false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(MonkeAgent), nameof(MonkeAgent.CheckReports))]
        public class NoCheckReports
        {
            private static bool Prefix() =>
                false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(MonkeAgent), nameof(MonkeAgent.DispatchReport))]
        public class NoDispatchReport
        {
            private static bool Prefix() =>
                false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(MonkeAgent), nameof(MonkeAgent.GetRPCCallTracker))]
        internal class NoGetRPCCallTracker
        {
            private static bool Prefix() =>
                false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(MonkeAgent), nameof(MonkeAgent.LogErrorCount))]
        public class NoLogErrorCount
        {
            private static bool Prefix(string logString, string stackTrace, LogType type) =>
                false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(MonkeAgent), nameof(MonkeAgent.QuitDelay), MethodType.Enumerator)]
        public class NoQuitDelay
        {
            private static bool Prefix() =>
                false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(GorillaGameManager), nameof(GorillaGameManager.ForceStopGame_DisconnectAndDestroy))]
        public class NoQuitOnBan
        {
            private static bool Prefix() =>
                false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(MonkeAgent), nameof(MonkeAgent.ShouldDisconnectFromRoom))]
        public class NoShouldDisconnectFromRoom
        {
            private static bool Prefix() =>
                false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(GorillaNetworkPublicTestsJoin), nameof(GorillaNetworkPublicTestsJoin.GracePeriod))]
        public class GracePeriodPatch1
        {
            private static bool Prefix() =>
                false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(GorillaNetworkPublicTestJoin2), nameof(GorillaNetworkPublicTestJoin2.GracePeriod))]
        public class GracePeriodPatch2
        {
            private static bool Prefix() =>
                false;
        }
    }
}

using GorillaNetworking;
using HarmonyLib;
using Interstellar.Mods;

namespace Interstellar.Patches.Menu
{
    [HarmonyPatch(typeof(GorillaNetworkJoinTrigger), nameof(GorillaNetworkJoinTrigger.OnBoxTriggered))]
    internal static class TraversalMapJoinPatch
    {
        private static bool Prefix(GorillaNetworkJoinTrigger __instance) => !Traversal.BlockMapJoin(__instance);
    }
}

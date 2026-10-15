using GorillaLocomotion;
using HarmonyLib;
using Interstellar.Mods;

namespace Interstellar.Patches.Menu
{
    [HarmonyPatch(typeof(GTPlayer), nameof(GTPlayer.GetSlidePercentage))]
    internal static class TraversalSlipPatch
    {
        private static void Postfix(GTPlayer __instance, ref float __result)
        {
            if (Traversal.RemoveSlip(__instance)) __result = 0f;
        }
    }
}

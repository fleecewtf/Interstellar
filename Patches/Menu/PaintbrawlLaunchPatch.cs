using HarmonyLib;
using Interstellar.Mods.GameModes;
using UnityEngine;

namespace Interstellar.Patches.Menu
{
    [HarmonyPatch(typeof(Slingshot), "GetLaunchVelocity")]
    internal static class PaintbrawlLaunchPatch
    {
        internal static void Postfix(Slingshot __instance, ref Vector3 __result) =>
            Paintbrawl.AssistLaunch(__instance, ref __result);
    }
}

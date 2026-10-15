using GorillaTag.Gravity;
using HarmonyLib;
using Interstellar.Utilities;
using UnityEngine;

namespace Interstellar.Patches.Menu
{
    [HarmonyPatch(typeof(MonkeGravityController), nameof(MonkeGravityController.ApplyGravityForce))]
    internal static class LocalGravityPatch
    {
        private static void Postfix(MonkeGravityController __instance, Vector3 force, ForceMode forceType,
            bool ___m_overrideForceMode, ForceMode ___m_forceModeOverride)
        {
            LocalGravity.AdjustGravity(__instance, force,
                ___m_overrideForceMode ? ___m_forceModeOverride : forceType);
        }
    }
}

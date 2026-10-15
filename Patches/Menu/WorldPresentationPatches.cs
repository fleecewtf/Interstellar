using HarmonyLib;
using Interstellar.Mods;

namespace Interstellar.Patches.Menu
{
    [HarmonyPatch(typeof(BetterDayNightManager), "ChangeMaps", new[] { typeof(int), typeof(int) })]
    internal static class WorldSkyTimePatch
    {
        internal static void Prefix(BetterDayNightManager __instance, ref int fromIndex, ref int toIndex)
        {
            if (!World.TryGetVisualTime(__instance, out int index)) return;
            fromIndex = index;
            toIndex = (index + 1) % __instance.timeOfDayRange.Length;
        }
    }

    [HarmonyPatch(typeof(BetterDayNightManager), nameof(BetterDayNightManager.PopulateAllLightmaps), new[] { typeof(int), typeof(int) })]
    internal static class WorldLightmapTimePatch
    {
        internal static void Prefix(BetterDayNightManager __instance, ref int fromIndex, ref int toIndex) =>
            WorldSkyTimePatch.Prefix(__instance, ref fromIndex, ref toIndex);
    }

    [HarmonyPatch(typeof(BetterDayNightManager), "ChangeLerps", new[] { typeof(float) })]
    internal static class WorldTimeBlendPatch
    {
        internal static void Prefix(BetterDayNightManager __instance, ref float newLerp)
        {
            if (World.TryGetVisualTime(__instance, out _)) newLerp = 0f;
        }
    }

    [HarmonyPatch(typeof(BetterDayNightManager), nameof(BetterDayNightManager.CurrentWeather))]
    internal static class WorldCurrentRainPatch
    {
        internal static void Postfix(BetterDayNightManager __instance, ref BetterDayNightManager.WeatherType __result)
        {
            if (World.SuppressRain(__instance)) __result = BetterDayNightManager.WeatherType.None;
        }
    }

    [HarmonyPatch(typeof(BetterDayNightManager), nameof(BetterDayNightManager.NextWeather))]
    internal static class WorldNextRainPatch
    {
        internal static void Postfix(BetterDayNightManager __instance, ref BetterDayNightManager.WeatherType __result) =>
            WorldCurrentRainPatch.Postfix(__instance, ref __result);
    }

    [HarmonyPatch(typeof(BetterDayNightManager), nameof(BetterDayNightManager.LastWeather))]
    internal static class WorldPreviousRainPatch
    {
        internal static void Postfix(BetterDayNightManager __instance, ref BetterDayNightManager.WeatherType __result) =>
            WorldCurrentRainPatch.Postfix(__instance, ref __result);
    }
}

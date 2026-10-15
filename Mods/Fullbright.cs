using HarmonyLib;

namespace Interstellar.Mods
{
    public static class Fullbright
    {
        private static bool enabled;
        private static GameLightingManager owner;
        private static bool originalLighting;
        private static bool internalWrite;

        public static void Enable() { enabled = true; Tick(); }
        public static void Disable()
        {
            enabled = false;
            Restore();
        }

        private static void Restore()
        {
            if (owner != null) Write(owner, originalLighting);
            owner = null;
        }

        private static void Write(GameLightingManager manager, bool value)
        {
            internalWrite = true;
            try { manager.SetCustomDynamicLightingEnabled(value); }
            finally { internalWrite = false; }
        }

        internal static void FilterLighting(GameLightingManager manager, ref bool value)
        {
            if (!enabled || internalWrite) return;
            if (owner != manager) { Restore(); owner = manager; }

            originalLighting = value;
            value = false;
        }

        internal static void Tick()
        {
            if (!enabled) return;
            GameLightingManager current = GameLightingManager.instance;
            if (current != owner)
            {
                Restore();
                owner = current;
                if (owner != null) originalLighting = owner.customVertexLightingEnabled;
            }
            if (owner != null && owner.customVertexLightingEnabled)
                Write(owner, false);
        }
    }

    [HarmonyPatch(typeof(GameLightingManager), nameof(GameLightingManager.SetCustomDynamicLightingEnabled))]
    internal static class FullbrightLightingPatch
    {
        private static void Prefix(GameLightingManager __instance, ref bool enable) => Fullbright.FilterLighting(__instance, ref enable);
    }
}

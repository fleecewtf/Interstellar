namespace Interstellar
{
    public class PluginInfo
    {
        public const string GUID = "org.Interstellar.gorillatag.Interstellarmenu";
        public const string Name = "Interstellar Menu";
        public const string Description = "Public mod menu for Gorilla Tag.";
        public const string BuildTimestamp = "2026-10-14";
        public const string Version = "1.0.0";

        public const string BaseDirectory = "InterstellarMenu";
        public const string ClientResourcePath = "InterstellarMenu.Resources.Client";

#if DEBUG
        public static bool BetaBuild = true;
#else
        public static bool BetaBuild = false;
#endif
    }
}

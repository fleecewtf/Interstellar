using Interstellar;

#pragma warning disable IDE0130
namespace Loading
#pragma warning restore IDE0130
{
    public static class Loader
    {
        public static void Load() =>
            Plugin.InjectDontDestroy();
    }
}

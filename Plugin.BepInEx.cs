using BepInEx;
using Interstellar.Managers;
using Interstellar.Menu;
using System.ComponentModel;

namespace Interstellar
{
    [Description(PluginInfo.Description)]
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class PluginBepInEx : BaseUnityPlugin
    {
        private void Awake()
        {
            LogManager.SetLogger((level, msg) =>
            {
                switch (level)
                {
                    case Level.Error:
                        Logger.LogError(msg);
                        break;
                    case Level.Warning:
                        Logger.LogWarning(msg);
                        break;
                    case Level.Debug:
                        Logger.LogDebug(msg);
                        break;
                    default:
                        Logger.LogInfo(msg);
                        break;
                }
            });
            gameObject.AddComponent<LoaderBridge>();
            if (!LoaderBridge.DeferNormalStartup()) Bootstrapper.Initialize();
        }

        private void OnDestroy() =>
            Main.UnloadMenu();
    }
}

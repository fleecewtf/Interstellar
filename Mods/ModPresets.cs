using System;
using System.IO;
using System.Linq;
using Interstellar.Classes.Menu;
using Interstellar.Managers;
using Interstellar.Menu;
using UnityEngine;

namespace Interstellar.Mods
{
    public static class ModPresets
    {
        [Serializable]
        public sealed class Preset
        {
            public int schema = 1;
            public string[] enabledMods;
            public int cameraView;
            public int cameraFov;
            public string queue;
            public string gamemode;
            public string region;
        }

        private static int slot = 1;
        private static string FilePath => Path.Combine(PluginInfo.BaseDirectory, "Presets", slot + ".json");
        private static ButtonInfo[] ModButtons() => Buttons.buttons.SelectMany(category => category)
            .Where(button => button.isTogglable && !button.label && !button.excludeFromSearch && !button.isSetting).ToArray();

        private static Preset Capture() => new Preset
        {
            enabledMods = ModButtons().Where(button => button.enabled).Select(button => button.buttonText).ToArray(),
            cameraView = CameraTools.ViewIndex,
            cameraFov = CameraTools.FovIndex,
            queue = JoinSettings.Queue,
            gamemode = JoinSettings.Mode,
            region = JoinSettings.Region
        };

        public static void CycleSlot()
        {
            slot = slot % 3 + 1;
            RefreshLabel();
        }

        public static void RefreshLabel()
        {
            ButtonInfo button = Buttons.GetIndex("Preset Slot");
            if (button != null) button.overlapText = "Preset · " + slot;
        }

        public static void Save()
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(Capture(), true));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                NotificationManager.SendNotification("Preset " + slot + " saved.");
            }
            catch (Exception exception)
            {
                LogManager.LogError("Unable to save the mod preset: " + exception.Message);
                NotificationManager.SendNotification("The preset could not be saved.");
            }
        }

        public static bool IsValid(Preset preset) => preset != null && preset.schema == 1 &&
            preset.enabledMods != null && preset.enabledMods.Length <= 256 &&
            preset.enabledMods.All(name => !string.IsNullOrWhiteSpace(name) && name.Length <= 128) &&
            (preset.queue == "DEFAULT" || preset.queue == "MINIGAMES" || preset.queue == "COMPETITIVE") &&
            !string.IsNullOrWhiteSpace(preset.gamemode) && preset.gamemode.Length <= 64 &&
            preset.region != null && preset.region.Length <= 24 &&
            preset.cameraView >= 0 && preset.cameraView <= 2 && preset.cameraFov >= 0 && preset.cameraFov <= 5;

        public static void Load()
        {
            if (!File.Exists(FilePath)) { NotificationManager.SendNotification("Preset " + slot + " has not been saved yet."); return; }
            Preset previous = null;
            try
            {
                if (new FileInfo(FilePath).Length > 65536) throw new InvalidDataException("The preset is too large.");
                Preset preset = JsonUtility.FromJson<Preset>(File.ReadAllText(FilePath));
                if (!IsValid(preset)) throw new InvalidDataException("The preset format is not supported.");
                previous = Capture();
                Apply(preset);
                NotificationManager.SendNotification("Preset " + slot + " loaded.");
            }
            catch (Exception exception)
            {
                LogManager.LogError("Unable to load the mod preset: " + exception.Message);
                if (previous != null)
                {
                    try { Apply(previous); }
                    catch (Exception restoreError) { LogManager.LogError("Preset rollback failed: " + restoreError.Message); }
                }
                NotificationManager.SendNotification("The preset could not be loaded.");
            }
            Main.ReloadMenu();
        }

        private static void Apply(Preset preset)
        {
            ButtonInfo[] available = ModButtons();
            foreach (ButtonInfo button in available)
            {
                if (!button.enabled) continue;
                button.enabled = false;
                button.firstFrame = true;
                button.disableMethod?.Invoke();
            }
            CameraTools.SetView(preset.cameraView);
            CameraTools.SetFovIndex(preset.cameraFov);
            JoinSettings.SetQueue(preset.queue);
            JoinSettings.SetMode(preset.gamemode);
            JoinSettings.SetRegion(preset.region);
            foreach (string name in preset.enabledMods.Distinct())
            {
                ButtonInfo button = available.FirstOrDefault(candidate => candidate.buttonText == name);
                if (button == null) continue;
                button.enabled = true;
                button.firstFrame = true;
                button.enableMethod?.Invoke();
            }
        }
    }
}

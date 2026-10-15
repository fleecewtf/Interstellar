using GorillaLocomotion;
using Interstellar.Classes.Menu;
using Interstellar.Extensions;
using Interstellar.Managers;
using Interstellar.Mods;
using Interstellar.Mods.GameModes;
using Interstellar.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using static Interstellar.Menu.Main;

namespace Interstellar.Menu
{
    public static class Buttons
    {
        public static ButtonInfo[][] buttons =
        {
            new[] {
                Category("Basic Mods"),
                Category("Movement"),
                Category("Rig"),
                Category("World"),
                Category("Room"),
                Category("Gamemode"),
                Category("Visuals"),
                Category("Camera"),
                Category("Settings")
            },

            new[] {
                new ButtonInfo { buttonText = "Exit Basic Mods", excludeFromSearch = true, method =() => CurrentCategoryName = "Main", isTogglable = false, toolTip = "Returns you back to the main page." },

                new ButtonInfo { buttonText = "WASD Fly", aliases = new[] { "PC Fly" }, enableMethod = Movement.EnableWASDFly, method = Movement.WASDFly, disableMethod = Movement.DisableWASDFly, toolTip = "Moves your rig with <color=green>WASD</color>."},
                new ButtonInfo { buttonText = "PC Button Click", aliases = new[] { "PC Click" }, method = Important.PCButtonClick, disableMethod = Important.DisablePCButtonClick, toolTip = "Lets you click in-game buttons with your mouse."},

                new ButtonInfo { buttonText = "Platforms", inputHint = "G", method = Movement.Platforms, disableMethod = Movement.DisablePlatforms, toolTip = "Spawns a platform under your hand while you hold grip."},
                new ButtonInfo { buttonText = "Fly", inputHint = "A", method = Movement.Fly, toolTip = "Hold <color=green>A</color> to fly in the direction you are looking."},
                new ButtonInfo { buttonText = "Joystick Fly", method = Movement.JoystickFly, toolTip = "Flies you around with the left joystick."},
                new ButtonInfo { buttonText = "Mute Gun", inputHint = "T", enableMethod = LocalTools.EnableMuteGun, method = LocalTools.MuteGun, disableMethod = LocalTools.DisableMuteGun, toolTip = "Click a player once to mute or unmute them for yourself." },
                new ButtonInfo { buttonText = "Focus Voice", inputHint = "T", enableMethod = LocalTools.EnableFocusVoice, method = LocalTools.FocusVoice, disableMethod = LocalTools.DisableFocusVoice, toolTip = "Select a player to hear normally while other voices become quieter. Disable to restore their volumes." },
                new ButtonInfo { buttonText = "Restart Microphone", method = LocalTools.RestartMicrophone, isTogglable = false, toolTip = "Restarts your microphone recorder if your voice stops transmitting." },
                new ButtonInfo { buttonText = "Anti AFK", enableMethod = LocalTools.EnableAntiAfk, disableMethod = LocalTools.DisableAntiAfk, toolTip = "Prevents the local AFK kick while enabled." }
            },

            new[] {
                new ButtonInfo { buttonText = "Exit Favorite Mods", excludeFromSearch = true, method =() => CurrentCategoryName = "Main", isTogglable = false, toolTip = "Returns you back to the main page." }
            },

            new[] {
                new ButtonInfo { buttonText = "Exit Enabled Mods", excludeFromSearch = true, method =() => CurrentCategoryName = "Main", isTogglable = false, toolTip = "Returns you back to the main page." }
            },

            new[] {
                new ButtonInfo { buttonText = "Search", excludeFromSearch = true, method = Settings.Search, isTogglable = false, toolTip = "Lets you search for specific mods." },
                new ButtonInfo { buttonText = "Global Return", excludeFromSearch = true, method = Settings.GlobalReturn, isTogglable = false, toolTip = "Returns you to the main page." },
                new ButtonInfo { buttonText = "Disconnect", excludeFromSearch = true, method = Room.Disconnect, isTogglable = false, toolTip = "Disconnects you from the room." },
                new ButtonInfo { buttonText = "Menu Home", excludeFromSearch = true, method = Settings.GlobalReturn, isTogglable = false, toolTip = "Returns you to the main page." },
                new ButtonInfo { buttonText = "Info Screen", excludeFromSearch = true, method = Settings.Debug, enableMethod = Settings.ShowDebug, disableMethod = Settings.HideDebug, toolTip = "Shows game and modding related information." },

                new ButtonInfo { buttonText = "Accept Prompt", excludeFromSearch = true, method =() => ResolvePrompt(true), isTogglable = false },
                new ButtonInfo { buttonText = "Decline Prompt", excludeFromSearch = true, method =() => ResolvePrompt(false), isTogglable = false }
            },

            new ButtonInfo[] { },

            new[] {
                Back("Movement"),
                new ButtonInfo { buttonText = "Speed Boost", method = Movement.SpeedBoost, disableMethod = Movement.DisableSpeedBoost, toolTip = "Increases movement speed while enabled." },
                new ButtonInfo { buttonText = "Noclip", inputHint = "T", method = Movement.Noclip, disableMethod = Movement.DisableNoclip, toolTip = "Hold the right trigger to pass through surfaces." },
                new ButtonInfo { buttonText = "Uncap Max Velocity", method = Movement.UncapMaxVelocity, disableMethod = Movement.DisableUncapMaxVelocity, toolTip = "Removes the movement velocity cap while enabled." },
                new ButtonInfo { buttonText = "Wall Walk", inputHint = "G", enableMethod = Movement.EnableWallWalk, disableMethod = Movement.DisableWallWalk, toolTip = "Hold grip to walk along the surface your hand touches." },
                new ButtonInfo { buttonText = "Pull Mod", inputHint = "G", method = Movement.PullMod, disableMethod = Movement.DisablePullMod, toolTip = "Hold grip to pull yourself along the direction of your movement." },
                new ButtonInfo { buttonText = "Teleport Gun", inputHint = "T", method = Traversal.TeleportGun, disableMethod = Traversal.DisableTeleportGun, toolTip = "Click a surface to teleport above it. One teleport per press." },
                new ButtonInfo { buttonText = "Teleport to Map", method = () => CurrentCategoryName = "Teleport to Map", isTogglable = false, toolTip = "Choose a map to load and teleport to." },
                new ButtonInfo { buttonText = "Low Gravity", enableMethod = Traversal.EnableLowGravity, disableMethod = Traversal.DisableLowGravity, toolTip = "Reduces gravity while enabled." },
                new ButtonInfo { buttonText = "No Slippery Surfaces", enableMethod = Traversal.EnableNoSlipperySurfaces, disableMethod = Traversal.DisableNoSlipperySurfaces, toolTip = "Makes slippery surfaces grippy for your hands." },
                new ButtonInfo { buttonText = "Slide Control", enableMethod = Traversal.EnableSlideControl, method = Traversal.SlideControl, disableMethod = Traversal.DisableSlideControl, toolTip = "Improves steering while sliding. Restores your original control when disabled." }
            },

            new[] {
                Back("Rig"),
                new ButtonInfo { buttonText = "Ghost", inputHint = "B", method = Rig.Ghost, disableMethod = Rig.DisableGhost, toolTip = "Press B to toggle a stationary rig." },
                new ButtonInfo { buttonText = "Invis", inputHint = "A", method = Rig.Invis, disableMethod = Rig.DisableInvis, toolTip = "Press A to toggle hiding your rig." },
                new ButtonInfo { buttonText = "Jumpscare Gun", inputHint = "T", method = Rig.JumpscareGun, disableMethod = Rig.DisableJumpscareGun, toolTip = "Use the gun to position your rig in front of a player." },
                new ButtonInfo { buttonText = "Orbit Player Gun", inputHint = "T", method = Rig.OrbitPlayerGun, disableMethod = Rig.DisableOrbitPlayerGun, toolTip = "Use the gun to orbit your rig around a player." },
                new ButtonInfo { buttonText = "Chase Player Gun", inputHint = "T", method = Rig.ChasePlayerGun, disableMethod = Rig.DisableChasePlayerGun, toolTip = "Use the gun to move your rig toward a player." },
                new ButtonInfo { buttonText = "Spin Rig", method = Rig.SpinRig, disableMethod = Rig.DisableSpinRig, toolTip = "Spins your rig while enabled." },
                new ButtonInfo { buttonText = "Upside Down Rig", method = Rig.UpsideDownRig, disableMethod = Rig.DisableUpsideDownRig, toolTip = "Turns your rig upside down while enabled." },
                new ButtonInfo { buttonText = "Stare At Closest", method = Rig.StareAtClosest, disableMethod = Rig.DisableStareAtClosest, toolTip = "Turns your rig toward the closest player." },
                new ButtonInfo { buttonText = "Spin Head", method = Rig.SpinHead, disableMethod = Rig.DisableSpinHead, toolTip = "Spins your rig's head while enabled." }
            },

            new[] {
                Back("World"),
                new ButtonInfo { buttonText = "Morning", enableMethod = World.EnableMorning, disableMethod = World.DisableMorning, toolTip = "Locks the world to morning until disabled." },
                new ButtonInfo { buttonText = "Day", enableMethod = World.EnableDay, disableMethod = World.DisableDay, toolTip = "Locks the world to day until disabled." },
                new ButtonInfo { buttonText = "Night", enableMethod = World.EnableNight, disableMethod = World.DisableNight, toolTip = "Locks the world to night until disabled." },
                new ButtonInfo { buttonText = "No Rain", enableMethod = World.EnableNoRain, disableMethod = World.DisableNoRain, toolTip = "Removes rain particles and rain audio until disabled." },
                new ButtonInfo { buttonText = "Disable Network Triggers", enableMethod = World.DisableNetworkTriggers, disableMethod = World.RestoreNetworkTriggers, toolTip = "Disables automatic network triggers until disabled." },
                new ButtonInfo { buttonText = "Disable Wind Barriers", enableMethod = World.DisableWindBarriers, disableMethod = World.RestoreWindBarriers, toolTip = "Disables wind barriers until this toggle is turned off." },
                new ButtonInfo { buttonText = "Fullbright", enableMethod = Fullbright.Enable, disableMethod = Fullbright.Disable, toolTip = "Removes dynamic map darkness without changing the time of day. Restores the previous lighting when disabled." }
            },

            new[] {
                Back("Room"),
                new ButtonInfo { buttonText = "Reconnect", method = Room.Reconnect, isTogglable = false, toolTip = "Reconnects to the last room you joined." },
                new ButtonInfo { buttonText = "Join Random", method = Room.JoinRandom, isTogglable = false, toolTip = "Joins a random room using the current queue and gamemode." },
                new ButtonInfo { buttonText = "Join DAISY09", method = () => Room.JoinRoom("DAISY09"), isTogglable = false, toolTip = "Joins room DAISY09." },
                new ButtonInfo { buttonText = "Join PBBV", method = () => Room.JoinRoom("PBBV"), isTogglable = false, toolTip = "Joins room PBBV." },
                new ButtonInfo { buttonText = "Join MODS", method = () => Room.JoinRoom("MODS"), isTogglable = false, toolTip = "Joins room MODS." },
                new ButtonInfo { buttonText = "Join Queue", method = JoinSettings.CycleQueue, isTogglable = false, isSetting = true, toolTip = "Cycles the queue used for future room joins." },
                new ButtonInfo { buttonText = "Join Gamemode", method = JoinSettings.CycleMode, isTogglable = false, isSetting = true, toolTip = "Cycles the gamemode used for future room joins." },
                new ButtonInfo { buttonText = "Join Region", method = JoinSettings.CycleRegion, isTogglable = false, isSetting = true, toolTip = "Cycles Auto and the game's available regions for future public joins." }
            },

            new[] {
                Back("Gamemode"),
                Category("Infection"),
                Category("Guardian"),
                Category("Paintbrawl")
            },

            new[] {
                Back("Infection", "Gamemode"),
                new ButtonInfo { buttonText = "Tag All", method = Infection.TagAll, disableMethod = Infection.DisableTagAll, toolTip = "Tags other players in Infection while enabled." },
                new ButtonInfo { buttonText = "Tag Gun", inputHint = "T", method = Infection.TagGun, disableMethod = Infection.DisableTagGun, toolTip = "Uses the gun to tag a player in Infection." },
                new ButtonInfo { buttonText = "No Tag On Join", enableMethod = Infection.EnableNoTagOnJoin, method = Infection.NoTagOnJoin, disableMethod = Infection.DisableNoTagOnJoin, toolTip = "Disables tag on join until this toggle is disabled." },
                new ButtonInfo { buttonText = "No Tag Freeze", method = Infection.NoTagFreeze, disableMethod = Infection.DisableNoTagFreeze, toolTip = "Prevents the movement freeze caused by being tagged." }
            },

            new[] {
                Back("Guardian", "Gamemode"),
                new ButtonInfo { buttonText = "Always Guardian", method = Guardian.AlwaysGuardian, disableMethod = Guardian.DisableAlwaysGuardian, toolTip = "Keeps acquiring Guardian while enabled." },
                new ButtonInfo { buttonText = "Guardian Protector", method = Guardian.Protector, toolTip = "Pushes players away from Guardian idols while you are Guardian." },
                new ButtonInfo { buttonText = "Guardian Fling All", method = Guardian.FlingAll, toolTip = "Flings other players while you are Guardian." },
                new ButtonInfo { buttonText = "Guardian Fling Gun", inputHint = "T", method = Guardian.FlingGun, disableMethod = GunUtilities.ReleaseLock, toolTip = "Uses the gun to fling a player while you are Guardian." },
                new ButtonInfo { buttonText = "Guardian Orbit All", method = Guardian.OrbitAll, toolTip = "Moves other players around you while you are Guardian." },
                new ButtonInfo { buttonText = "Guardian Orbit Gun", inputHint = "T", method = Guardian.OrbitGun, disableMethod = GunUtilities.ReleaseLock, toolTip = "Uses the gun to orbit a player while you are Guardian." },
                new ButtonInfo { buttonText = "Guardian Freeze Gun", inputHint = "T", method = Guardian.FreezeGun, disableMethod = GunUtilities.ReleaseLock, toolTip = "Uses the gun to hold a player down while you are Guardian." }
            },

            new[] {
                Back("Visuals"),
                new ButtonInfo { buttonText = "Name Tags", enableMethod = PlayerVisuals.EnableNameTags, disableMethod = PlayerVisuals.DisableNameTags, toolTip = "Shows clean Interstellar name plates above other players." },
                new ButtonInfo { buttonText = "Voice Indicators", enableMethod = PlayerVisuals.EnableVoiceIndicators, disableMethod = PlayerVisuals.DisableVoiceIndicators, toolTip = "Shows a small speaking indicator above players while they talk." },
                new ButtonInfo { buttonText = "Automatic Tracers", enableMethod = PlayerVisuals.EnableAutomaticTracers, disableMethod = PlayerVisuals.DisableAutomaticTracers, toolTip = "Draws subtle lines to relevant players for the current gamemode." }
            },

            new[] {
                Back("Camera"),
                new ButtonInfo { buttonText = "Camera View", method = SelectCameraView, isTogglable = false, isSetting = true, toolTip = "Cycles Standard, First Person and Third Person for the desktop camera." },
                new ButtonInfo { buttonText = "Camera FOV", method = CameraTools.CycleFov, isTogglable = false, isSetting = true, toolTip = "Cycles the desktop camera field of view. Default restores the original lens." },
                new ButtonInfo { buttonText = "Free Camera", enableMethod = EnableFreeCamera, method = CameraTools.FreeCamera, disableMethod = CameraTools.DisableFreeCamera, toolTip = "Moves the desktop camera independently. Use joysticks in VR, or WASD and right mouse on PC." },
                new ButtonInfo { buttonText = "Spectate Gun", inputHint = "T", enableMethod = EnableSpectateGun, method = CameraTools.SpectateGun, disableMethod = CameraTools.DisableSpectateGun, toolTip = "Hold the gun trigger on a player to follow a view near their head on desktop." }
            },

            new[] {
                Back("Paintbrawl", "Gamemode"),
                new ButtonInfo { buttonText = "Slingshot Aimbot", enableMethod = Paintbrawl.EnableAimbot, disableMethod = Paintbrawl.DisableAimbot, toolTip = "Aims local slingshot shots toward a nearby living opponent in Paintbrawl." },
                new ButtonInfo { buttonText = "Projectile Trajectories", enableMethod = Paintbrawl.EnableProjectileTrajectories, disableMethod = Paintbrawl.DisableProjectileTrajectories, toolTip = "Shows estimated slingshot and projectile paths using gravity and collision checks." },
                new ButtonInfo { buttonText = "Restart Round", method = Paintbrawl.RestartRound, isTogglable = false, toolTip = "Restarts the Paintbrawl round when you are master client." }
            },

            new[] {
                Back("Settings"),
                new ButtonInfo { buttonText = "Menu Size", overlapText = "Size · 100%", incremental = true, isSetting = true, isTogglable = false, method = () => Settings.ChangeMenuSize(true), cycleValue = Settings.ChangeMenuSize, toolTip = "Use - and + to set the menu size from 75% to 125%. Your choice is saved." },
                new ButtonInfo { buttonText = "Reset Menu Size", method = Settings.ResetMenuSize, isTogglable = false, isSetting = true, toolTip = "Restores the original menu size." },
                new ButtonInfo { buttonText = "Preset Slot", overlapText = "Preset · 1", method = ModPresets.CycleSlot, isTogglable = false, isSetting = true, toolTip = "Selects one of three saved mod presets." },
                new ButtonInfo { buttonText = "Save Preset", method = ModPresets.Save, isTogglable = false, toolTip = "Saves enabled mods, camera settings and room join settings to the selected slot." },
                new ButtonInfo { buttonText = "Load Preset", method = ModPresets.Load, isTogglable = false, toolTip = "Loads the selected preset and restores replaced mods before applying it." },
                Category("Favorite Mods"),
                Category("Enabled Mods")
            },

            CreateMapButtons()
        };

        public static string[] categoryNames = {
            "Main",
            "Basic Mods",
            "Favorite Mods",
            "Enabled Mods",
            "Internal Mods",
            "Temporary Category",
            "Movement",
            "Rig",
            "World",
            "Room",
            "Gamemode",
            "Infection",
            "Guardian",
            "Visuals",
            "Camera",
            "Paintbrawl",
            "Settings",
            "Teleport to Map"
        };

        private static ButtonInfo[] CreateMapButtons()
        {
            var result = new List<ButtonInfo> { Back("Teleport to Map", "Movement") };
            foreach (Traversal.MapDestination destination in Traversal.MapDestinations)
            {
                string name = destination.Name;
                result.Add(new ButtonInfo { buttonText = "Teleport to " + name, excludeFromSearch = true,
                    method = () => Traversal.TeleportToMap(name), isTogglable = false,
                    toolTip = "Loads " + name + " and teleports you to its entrance." });
            }
            return result.ToArray();
        }

        private static void DisableCameraButton(string name)
        {
            ButtonInfo button = GetIndex(name);
            if (button == null || !button.enabled) return;
            button.enabled = false;
            button.disableMethod?.Invoke();
        }

        private static void SelectCameraView()
        {
            DisableCameraButton("Free Camera");
            DisableCameraButton("Spectate Gun");
            CameraTools.CycleView();
        }

        private static void EnableFreeCamera() { DisableCameraButton("Spectate Gun"); CameraTools.EnableFreeCamera(); }
        private static void EnableSpectateGun() { DisableCameraButton("Free Camera"); CameraTools.EnableSpectateGun(); }

        private static ButtonInfo Category(string name) => new ButtonInfo
        {
            buttonText = name,
            excludeFromSearch = true,
            isTogglable = false,
            method = () => CurrentCategoryName = name,
            toolTip = "Opens the " + name.ToLowerInvariant() + " category."
        };

        private static ButtonInfo Back(string name, string parent = "Main") => new ButtonInfo
        {
            buttonText = "Exit " + name,
            excludeFromSearch = true,
            isTogglable = false,
            method = () => CurrentCategoryName = parent,
            toolTip = "Returns you back to the " + parent.ToLowerInvariant() + " page."
        };

        public static int _currentCategoryIndex;
        public static event Action OnCategoryChanged;

        public static int CurrentCategoryIndex
        {
            get => _currentCategoryIndex;
            set
            {
                _currentCategoryIndex = value;
                pageNumber = 0;
                pageOffset = 0;

                OnCategoryChanged?.Invoke();
            }
        }

        public static string CurrentCategoryName
        {
            get => Buttons.categoryNames[CurrentCategoryIndex];
            set =>
                CurrentCategoryIndex = Buttons.GetCategory(value);
        }

        private static readonly Dictionary<string, (int Category, int Index)> cacheGetIndex = new Dictionary<string, (int Category, int Index)>();

        public static ButtonInfo GetIndex(string buttonText)
        {
            if (buttons == null)
                return null;
            if (buttonText == null)
                return null;

            if (cacheGetIndex.TryGetValue(buttonText, out var cacheData))
            {
                try
                {
                    if (buttons[cacheData.Category][cacheData.Index].buttonText == buttonText)
                        return buttons[cacheData.Category][cacheData.Index];
                }
                catch { cacheGetIndex.Remove(buttonText); }
            }

            int categoryIndex = 0;
            foreach (ButtonInfo[] categoryButtons in buttons)
            {
                int buttonIndex = 0;
                foreach (ButtonInfo button in categoryButtons)
                {
                    if (button.buttonText == buttonText)
                    {
                        try
                        {
                            cacheGetIndex.Add(buttonText, (categoryIndex, buttonIndex));
                        }
                        catch
                        {
                            cacheGetIndex.Remove(buttonText);
                        }

                        return button;
                    }
                    buttonIndex++;
                }
                categoryIndex++;
            }

            return null;
        }

        public static int GetCategory(string categoryName) =>
            Array.IndexOf(categoryNames, categoryName);

        public static void AddButton(int category, ButtonInfo button, int index = -1)
        {
            List<ButtonInfo> buttonInfoList = buttons[category].ToList();
            if (index > 0)
                buttonInfoList.Insert(index, button);
            else
                buttonInfoList.Add(button);

            buttons[category] = buttonInfoList.ToArray();
        }

        public static void RemoveButton(int category, string name, int index = -1)
        {
            List<ButtonInfo> buttonInfoList = buttons[category].ToList();
            if (index > 0)
                buttonInfoList.RemoveAt(index);
            else
            {
                foreach (var button in buttonInfoList.Where(button => button.buttonText == name))
                {
                    buttonInfoList.Remove(button);
                    break;
                }
            }

            buttons[category] = buttonInfoList.ToArray();
        }
    }
}

public class UpdateButtonText : MonoBehaviour
{
    public ButtonInfo button;
    public int buttonIndex;
    public float offset;

    private TextMeshPro tmp;
    private UIColorChanger colorChanger;
    private TextMeshPro hintText;
    private bool initialized;
    private string lastRendered;

    private bool ModelRow => ModelButtonLayout.IsRow(buttonIndex) && !checkMode;

    public void Init(ButtonInfo b, int idx, float off)
    {
        button = b;
        buttonIndex = idx;
        offset = off;

        EnsureReferences();
        ApplyLayout();
        initialized = true;
    }

    private void EnsureReferences()
    {
        if (tmp == null) tmp = GetComponent<TextMeshPro>();
        if (!ModelRow && colorChanger == null)
            colorChanger = GetComponent<UIColorChanger>() ?? gameObject.AddComponent<UIColorChanger>();
    }

    private void ApplyLayout()
    {
        if (ModelRow)
        {
            ApplyModelLayout();
            return;
        }
        bool wideText = !checkMode && buttonIndex >= 0 && !(button != null && button.incremental && incrementalButtons);

        RectTransform textTransform = tmp.rectTransform;
        textTransform.localPosition = Vector3.zero;
        textTransform.sizeDelta = new Vector2(
            button != null && button.incremental && incrementalButtons ? .18f : wideText ? .3f : .2f,
            .03f * (ButtonDistance / 0.1f));
        if (NoAutoSizeText) textTransform.sizeDelta = new Vector2(9f, 0.015f);
        if (hideTextOnCamera) textTransform.gameObject.layer = 19;
        textTransform.localPosition = new Vector3(.064f, 0, .111f - offset / 2.6f);
        textTransform.localRotation = Quaternion.Euler(180f, 90f, 90f);

        tmp.font = activeFont;
        tmp.spriteAsset = ButtonSpriteSheet;
        tmp.richText = true;
        tmp.fontSize = 1;
        tmp.alignment = checkMode || wideText ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
        tmp.margin = wideText ? new Vector4(0.022f, 0f, 0.022f, 0f) : Vector4.zero;
        tmp.fontStyle = activeFontStyle;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0;
    }

    public void UpdateText()
    {
        if (!initialized || button == null) return;
        EnsureReferences();
        ApplyLayout();

        if (ModelRow)
        {
            UpdateModelText();
            return;
        }

        string targetButtonText = ButtonText();
        lastRendered = targetButtonText;
        tmp.SafeSetText(targetButtonText);
        FollowMenuSettings(tmp);

        if (joystickMenu && buttonIndex == joystickButtonSelected && themeType == 30)
            tmp.color = Color.red;
        else
            colorChanger.colors = textColors[button.enabled ? 2 : 1];
    }

    private void LateUpdate()
    {
        if (!initialized || button == null || tmp == null) return;
        EnsureReferences();

        if (ModelRow)
        {
            UpdateModelText();
            return;
        }

        string targetButtonText = ButtonText();

        if (lastRendered != targetButtonText || string.IsNullOrEmpty(tmp.text))
        {
            lastRendered = targetButtonText;
            tmp.SafeSetText(targetButtonText);
            FollowMenuSettings(tmp);
        }

        if (joystickMenu && buttonIndex == joystickButtonSelected && themeType == 30)
            tmp.color = Color.red;
        else
            colorChanger.colors = textColors[button.enabled ? 2 : 1];
    }

    private void ApplyModelLayout()
    {
        bool back = buttonIndex == -2;
        float width = ModelButtonLayout.RowWidth;
        RectTransform rect = tmp.rectTransform;
        rect.localPosition = ModelButtonLayout.TextPosition(buttonIndex, back);
        rect.localRotation = Quaternion.Euler(180f, 90f, 90f);
        rect.sizeDelta = new Vector2(width, ModelButtonLayout.RowHeight(back));
        if (button.incremental && incrementalButtons)
            rect.sizeDelta = new Vector2(width * 0.64f, ModelButtonLayout.RowHeight(back));
        tmp.margin = back ? Vector4.zero : new Vector4(width * ModelButtonLayout.LabelInsetRatio, 0f,
            width * ModelButtonLayout.LabelInsetRatio, 0f);
        tmp.spriteAsset = ButtonSpriteSheet;
        tmp.richText = true;
        tmp.alignment = back ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
        if (button.incremental && incrementalButtons)
            tmp.alignment = TextAlignmentOptions.Midline;
        ModelButtonLayout.StyleText(tmp, false, back);

        if (hintText == null && !back)
        {
            hintText = new GameObject("Input Hint").AddComponent<TextMeshPro>();
            hintText.transform.SetParent(transform, false);
            hintText.richText = false;
            hintText.alignment = TextAlignmentOptions.MidlineRight;
            hintText.rectTransform.localRotation = Quaternion.identity;
            hintText.rectTransform.localPosition = new Vector3(width * (0.5f - ModelButtonLayout.LabelInsetRatio
                - ModelButtonLayout.HintWidthRatio * 0.5f), 0f, 0f);
            hintText.rectTransform.sizeDelta = new Vector2(width * ModelButtonLayout.HintWidthRatio,
                ModelButtonLayout.RowHeight(false));
            ModelButtonLayout.StyleText(hintText, true, false);
        }

        if (hideTextOnCamera)
        {
            gameObject.layer = 19;
            if (hintText != null) hintText.gameObject.layer = 19;
        }
    }

    private void UpdateModelText()
    {
        bool back = buttonIndex == -2;
        string label = back ? FollowMenuSettings("BACK") : ButtonText(false);
        string hint = back ? "" : button.inputHint ?? "";



        const string bindingMarker = "<color=grey>[</color>";
        int bindingStart = label.LastIndexOf(bindingMarker, StringComparison.Ordinal);
        if (bindingStart >= 0)
        {
            string suffix = NoRichtextTags(label.Substring(bindingStart)).Trim();
            if (suffix.StartsWith("[") && suffix.EndsWith("]"))
            {
                string candidate = suffix.Substring(1, suffix.Length - 2).Trim();
                if (Regex.IsMatch(candidate, @"^(?:[ABXYGT]|[LR][AB]|[LR]TP|[LR][TGJ]|LS|RS|LMB|RMB|MMB)(?:\s*(?:\+|/)\s*(?:[ABXYGT]|[LR][AB]|[LR]TP|[LR][TGJ]|LS|RS|LMB|RMB|MMB))*$"))
                {
                    label = label.Substring(0, bindingStart).TrimEnd();
                    hint = candidate;
                }
            }
        }

        if (button.rebindKey != null) hint = button.rebindKey;
        if (button.customBind != null && !hint.Contains(button.customBind))
            hint = string.IsNullOrEmpty(hint) ? button.customBind : hint + " / " + button.customBind;

        if (adaptiveButtons && hint == "A" && ControllerUtilities.GetRightControllerType() == ControllerUtilities.ControllerType.ValveIndex)
            hint = "RA";
        if (adaptiveButtons && hint == "A" && ControllerUtilities.GetRightControllerType() == ControllerUtilities.ControllerType.VIVE)
            hint = "RTP";

        hint = FollowMenuSettings(hint.Replace("+", " + ").Replace("  ", " ").Trim());
        label = FollowMenuSettings(FixTMProTags(label));
        if (!back && favorites.Contains(button.buttonText))
            label += "  <sprite name=\"Favorite\">";

        float width = ModelButtonLayout.RowWidth;
        tmp.margin = back ? Vector4.zero : new Vector4(width * ModelButtonLayout.LabelInsetRatio, 0f,
            width * (ModelButtonLayout.LabelInsetRatio + (string.IsNullOrEmpty(hint) ? 0f : ModelButtonLayout.HintWidthRatio + 0.03f)), 0f);
        if (button.incremental && incrementalButtons) tmp.margin = Vector4.zero;
        tmp.SafeSetText(label);
        Color background = buttonColors[button.enabled ? 1 : 0].GetCurrentColor();
        MenuVisuals.SetTextBackground(tmp, background);
        if (hintText != null) MenuVisuals.SetTextBackground(hintText, background);
        if (hintText != null)
        {
            hintText.SafeSetText(hint);
            hintText.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            hintText.color = ModelPalette.Hint(button.enabled);
        }
        tmp.color = joystickMenu && buttonIndex == joystickButtonSelected && themeType == 30
            ? Color.red : textColors[button.enabled ? 2 : 1].GetCurrentColor();
    }

    private string ButtonText(bool decorate = true)
    {
        string targetButtonText = button.overlapText ?? button.buttonText;

        if (adaptiveButtons)
        {
            switch (ControllerUtilities.GetLeftControllerType())
            {
                case ControllerUtilities.ControllerType.ValveIndex:
                    targetButtonText = targetButtonText.Replace("<color=green>X</color>", "<color=green>LA</color>");
                    targetButtonText = targetButtonText.Replace("<color=green>Y</color>", "<color=green>LB</color>");
                    break;
                case ControllerUtilities.ControllerType.VIVE:
                    targetButtonText = targetButtonText.Replace("<color=green>X</color>", "<color=green>LTP</color>");
                    break;
            }
            switch (ControllerUtilities.GetRightControllerType())
            {
                case ControllerUtilities.ControllerType.ValveIndex:
                    targetButtonText = targetButtonText.Replace("<color=green>A</color>", "<color=green>RA</color>");
                    targetButtonText = targetButtonText.Replace("<color=green>B</color>", "<color=green>RB</color>");
                    break;
                case ControllerUtilities.ControllerType.VIVE:
                    targetButtonText = targetButtonText.Replace("<color=green>A</color>", "<color=green>RTP</color>");
                    break;
            }
        }

        if (!decorate) return targetButtonText;

        if (button.rebindKey != null && targetButtonText.Contains("</color><color=grey>]</color>"))
        {
            targetButtonText = targetButtonText.Split("<color=grey>[</color><color=green>")[0]
                + "<color=grey>[</color><color=green>"
                + button.rebindKey
                + "</color><color=grey>]</color>";
        }

        if (button.customBind != null)
        {
            if (targetButtonText.Contains("</color><color=grey>]</color>"))
                targetButtonText = targetButtonText.Replace("</color><color=grey>]</color>", $"/{button.customBind}</color><color=grey>]</color>");
            else
                targetButtonText += $" <color=grey>[</color><color=green>{button.customBind}</color><color=grey>]</color>";
        }

        if (inputTextColor != "green")
            targetButtonText = targetButtonText.Replace(" <color=grey>[</color><color=green>", $" <color=grey>[</color><color={inputTextColor}>");

        targetButtonText = FixTMProTags(targetButtonText);
        targetButtonText = FollowMenuSettings(targetButtonText);

        if (favorites.Contains(button.buttonText))
            targetButtonText = $"    {targetButtonText}    <sprite name=\"Favorite\">";

        return targetButtonText;
    }
}

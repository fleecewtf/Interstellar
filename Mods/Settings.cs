using GorillaLocomotion;
using Photon.Pun;
using Interstellar.Classes.Menu;
using Interstellar.Extensions;
using Interstellar.Managers;
using Interstellar.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using static Interstellar.Menu.Main;
using static Interstellar.Utilities.AssetUtilities;
using Object = UnityEngine.Object;

namespace Interstellar.Mods
{
    public static class Settings
    {
        public static void InitializeMenuSize()
        {
            MenuSizing.Load();
            RefreshMenuSizeLabel();
        }

        public static void ChangeMenuSize(bool increase)
        {
            MenuSizing.Change(increase);
            RefreshMenuSizeLabel();
        }

        public static void ResetMenuSize()
        {
            MenuSizing.Reset();
            RefreshMenuSizeLabel();
        }

        private static void RefreshMenuSizeLabel()
        {
            ButtonInfo button = Buttons.GetIndex("Menu Size");
            if (button != null) button.overlapText = "Size · " + MenuSizing.Percent + "%";
        }

        public static void Search()
        {
            isSearching = !isSearching;

            pageNumber = 0;
            keyboardInput = "";

            if (isSearching)
                SpawnKeyboard();
            else
                DestroyKeyboard();
        }

        public static void SpawnKeyboard()
        {
            isKeyboardPc = isOnPC || toggleButtonActive && keyboardWithToggleButton;
            inTextInput = true;
            keyboardInput = "";

            shift = false;
            lockShift = false;

            if (isKeyboardPc)
                lastPressedKeys.Add(Key.Q);

            if (!isKeyboardPc)
            {
                if (VRKeyboard == null)
                {
                    VRKeyboard = LoadObject<GameObject>("VRKeyboard");
                    VRKeyboard.transform.position = GorillaTagger.Instance.bodyCollider.transform.position;
                    VRKeyboard.transform.rotation = GorillaTagger.Instance.bodyCollider.transform.rotation;

                    menuSpawnPosition = VRKeyboard.transform.Find("MenuSpawnPosition").gameObject;
                    VRKeyboard.transform.Find("Canvas").AddComponent<ColorChanger>().colors = textColors[1];

                    VRKeyboard.transform.localScale *= scaleWithPlayer ? GTPlayer.Instance.scale * menuScale : menuScale;
                    menuSpawnPosition.transform.localScale *= scaleWithPlayer ? GTPlayer.Instance.scale * menuScale : menuScale;

                    ColorChanger backgroundColorChanger = VRKeyboard.transform.Find("Background").gameObject.AddComponent<ColorChanger>();
                    backgroundColorChanger.colors = menuBackgroundColor;

                    foreach (GameObject key in VRKeyboard.transform.Find("Seperate").Children()
                        .Select(t => t.gameObject)
                        .Concat(new[] { VRKeyboard.transform.Find("Keys/default").gameObject }))
                    {
                        ColorChanger keyColorChanger = key.AddComponent<ColorChanger>();
                        keyColorChanger.colors = buttonColors[0];
                    }

                    if (shouldOutline)
                        OutlineObject(VRKeyboard.transform.Find("Background").gameObject, true);

                    var keys = new[] { "Numbers", "Letters", "Special", "Seperate" }
                        .Select(name => VRKeyboard.transform.Find(name))
                        .Where(t => t != null)
                        .SelectMany(t => t.Children())
                        .Select(t => t.gameObject);

                    foreach (GameObject v in keys)
                    {
                        v.AddComponent<KeyboardKey>().key = v.name;
                        v.layer = 2;

                        if (shouldOutline)
                            OutlineObject(v, true);
                    }
                }
            }

            if (lKeyReference == null)
            {
                lKeyReference = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Sphere);
                lKeyReference.transform.parent = GorillaTagger.Instance.leftHandTransform;
                lKeyReference.GetComponent<Renderer>().material.color = backgroundColor.GetColor(0);
                lKeyReference.transform.localPosition = pointerOffset;
                lKeyReference.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                lKeyCollider = lKeyReference.GetComponent<SphereCollider>();

                ColorChanger colorChanger = lKeyReference.AddComponent<ColorChanger>();
                colorChanger.colors = backgroundColor;
            }

            if (rKeyReference == null)
            {
                rKeyReference = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Sphere);
                rKeyReference.transform.parent = GorillaTagger.Instance.rightHandTransform;
                rKeyReference.GetComponent<Renderer>().material.color = backgroundColor.GetColor(0);
                rKeyReference.transform.localPosition = pointerOffset;
                rKeyReference.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                rKeyCollider = rKeyReference.GetComponent<SphereCollider>();

                ColorChanger colorChanger = rKeyReference.AddComponent<ColorChanger>();
                colorChanger.colors = backgroundColor;
            }
        }

        public static void DestroyKeyboard()
        {
            inTextInput = false;
            isKeyboardPc = false;

            if (lKeyReference != null)
            {
                Object.Destroy(lKeyReference);
                lKeyReference = null;
            }

            if (rKeyReference != null)
            {
                Object.Destroy(rKeyReference);
                rKeyReference = null;
            }

            if (VRKeyboard != null)
            {
                Object.Destroy(VRKeyboard);
                VRKeyboard = null;
            }

            if (TPC != null && TPC.transform.parent != null && TPC.transform.parent.gameObject.name.Contains("CameraTablet") && isOnPC)
            {
                isOnPC = false;
                TPC.transform.position = TPC.transform.parent.position;
                TPC.transform.rotation = TPC.transform.parent.rotation;
            }
        }

        public static void GlobalReturn()
        {
            DestroyKeyboard();
            isSearching = false;
            keyboardInput = "";
            Buttons.CurrentCategoryIndex = Buttons.GetCategory("Main");
            pageNumber = 0;
            prompts.Clear();
            NotificationManager.ClearAllNotifications();
            ReloadMenu();
            SoundManager.Play("Return");
        }

        public static void ShowDebug()
        {
            int category = Buttons.GetCategory("Temporary Category");

            string version = PluginInfo.Version;
            if (PluginInfo.BetaBuild) version = "<color=blue>Beta</color> " + version;
            Buttons.AddButton(category, new ButtonInfo { buttonText = "Exit Info Screen", excludeFromSearch = true, method = () => Toggle("Info Screen"), isTogglable = false, toolTip = "Returns you back to the main page." });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugMenuName", overlapText = "<color=grey><b>Interstellar </b></color>" + version, label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugColor", overlapText = "Loading...", label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugName", overlapText = "Loading...", label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugFps", overlapText = "Loading...", label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugRoomA", overlapText = "Loading...", label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugRoomB", overlapText = "Loading...", label = true });

            Debug();
            Buttons.CurrentCategoryName = "Temporary Category";
        }

        public static void Debug()
        {
            string red = "<color=red>" + MathF.Floor(PlayerPrefs.GetFloat("redValue") * 255f) + "</color>";
            string green = ", <color=green>" + MathF.Floor(PlayerPrefs.GetFloat("greenValue") * 255f) + "</color>";
            string blue = ", <color=blue>" + MathF.Floor(PlayerPrefs.GetFloat("blueValue") * 255f) + "</color>";
            Buttons.GetIndex("DebugColor").overlapText = "Color: " + red + green + blue;

            string master = NetworkSystem.Instance.InRoom && PhotonNetwork.IsMasterClient ? "<color=red> [Master]</color>" : "";
            Buttons.GetIndex("DebugName").overlapText = CleanPlayerName(PhotonNetwork.LocalPlayer.NickName) + master;

            Buttons.GetIndex("DebugFps").overlapText = "<b>" + lastDeltaTime + "</b> FPS <b>" + PhotonNetwork.GetPing() + "</b> Ping";
            Buttons.GetIndex("DebugRoomA").overlapText = "<color=blue>" + NetworkSystem.Instance.regionNames[NetworkSystem.Instance.currentRegionIndex].ToUpper() + "</color> " + PhotonNetwork.PlayerList.Length + " Players";

            string priv = NetworkSystem.Instance.InRoom ? NetworkSystem.Instance.SessionIsPrivate ? "Private" : "Public" : "";
            Buttons.GetIndex("DebugRoomB").overlapText = "<color=blue>" + priv + "</color> " + (NetworkSystem.Instance.InRoom ? PhotonNetwork.CurrentRoom.Name : "Not in room");
        }
        public static void HideDebug()
        {
            int category = Buttons.GetCategory("Temporary Category");

            Buttons.RemoveButton(category, "DebugMenuName");
            Buttons.RemoveButton(category, "DebugColor");
            Buttons.RemoveButton(category, "DebugName");
            Buttons.RemoveButton(category, "DebugFps");
            Buttons.RemoveButton(category, "DebugRoomA");
            Buttons.RemoveButton(category, "DebugRoomB");
            Buttons.CurrentCategoryName = "Main";
        }

        public enum ControllerBinding
        {
            None,
            LeftTrigger,
            RightTrigger,
            LeftGrip,
            RightGrip,
            LeftPrimaryButton,
            RightPrimaryButton,
            LeftSecondaryButton,
            RightSecondaryButton,
            JoystickClick,
            LeftOverride
        }

        public static readonly Dictionary<ControllerBinding, Key> pcBindings = new Dictionary<ControllerBinding, Key>
        {
            { ControllerBinding.RightPrimaryButton, Key.E },
            { ControllerBinding.RightSecondaryButton, Key.R },
            { ControllerBinding.LeftPrimaryButton, Key.F },
            { ControllerBinding.LeftSecondaryButton, Key.G },
            { ControllerBinding.LeftGrip, Key.LeftBracket },
            { ControllerBinding.RightGrip, Key.RightBracket },
            { ControllerBinding.LeftTrigger, Key.Minus },
            { ControllerBinding.RightTrigger, Key.Equals },
            { ControllerBinding.JoystickClick, Key.Enter },
            { ControllerBinding.LeftOverride, Key.LeftAlt }
        };

    }
}

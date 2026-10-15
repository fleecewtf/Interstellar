using GorillaExtensions;
using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Interstellar.Classes.Menu;
using Interstellar.Classes.Mods;
using Interstellar.Extensions;
using Interstellar.Managers;
using Interstellar.Mods;
using Interstellar.Patches;
using Interstellar.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TextCore;
using UnityEngine.UI;
using UnityEngine.XR;
using Valve.VR;
using static Interstellar.Utilities.AssetUtilities;
using static Interstellar.Utilities.RandomUtilities;
using ButtonCollider = Interstellar.Classes.Menu.ButtonCollider;
using CommonUsages = UnityEngine.XR.CommonUsages;
using Random = UnityEngine.Random;

namespace Interstellar.Menu
{
    [HarmonyPatch(typeof(GTPlayer), nameof(GTPlayer.LateUpdate))]
    public class Main : MonoBehaviour
    {
        public static void OnLaunch()
        {
            if (CoroutineManager.instance == null)
                LogManager.LogError("CoroutineManager instance is null on menu launch. Features may not function properly.");

            if (NotificationManager.Instance == null)
                LogManager.LogError("NotificationManager instance is null on menu launch. Features may not function properly.");

            timeMenuStarted = Time.time;
            IsSteam = PlayFabAuthenticator.instance.platform;


            activeFont = MenuTypography.Font;
            activeFontStyle = MenuTypography.Style;
            JoinSettings.RefreshLabels();
            CameraTools.RefreshLabels();
            ModPresets.RefreshLabel();
            Settings.InitializeMenuSize();

            NetworkSystem.Instance.OnMultiplayerStarted += OnJoinRoom;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += OnLeaveRoom;
            NetworkSystem.Instance.OnMasterClientSwitchedEvent += OnMasterClientSwitch;

            NetworkSystem.Instance.OnPlayerJoined += OnPlayerJoin;
            NetworkSystem.Instance.OnPlayerLeft += OnPlayerLeave;

            if (Interstellar.Mods.Room.IsConnected(NetworkSystem.Instance))
                OnJoinRoom();

            for (int i = 0; i < 2; i++)
                Visuals.InstantiateWatch();
            CrystalMaterial = GetObject("Environment Objects/LocalObjects_Prefab/ForestToCave/C_Crystal_Chunk")?.GetComponent<Renderer>()?.material;
            MenuModel.Load();

            fullModAmount ??= Buttons.buttons.SelectMany(list => list).ToArray().Length;

            try
            {
                string allButtonsPath = $"{PluginInfo.BaseDirectory}/AllButtons.txt";

                string[] newButtonNames = Buttons.buttons
                        .SelectMany(list => list)
                        .Select(button => button.buttonText)
                        .ToArray();

                if (File.Exists(allButtonsPath))
                {
                    string[] oldButtonNames = File.ReadAllText(allButtonsPath).Split("\n");

                    foreach (string name in newButtonNames)
                    {
                        if (oldButtonNames.Contains(name)) continue;
                        ButtonInfo button = Buttons.GetIndex(name);
                        string buttonText = button.overlapText ?? button.buttonText;
                        button.overlapText ??= buttonText + " <color=grey>[</color><color=green>New</color><color=grey>]</color>";
                    }
                }

                File.WriteAllText(allButtonsPath, string.Join("\n", newButtonNames));
            }
            catch { }

            if (PatchHandler.PatchErrors > 0)
            {
                if (PatchHandler.CriticalPatchFailed)
                {
                    string message = "A critical patch has failed, and you have been blocked from joining rooms for safety reasons. Please report this as an issue to the GitHub repository.";
                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> {message}", 10000);
                    GorillaComputer.instance.GeneralFailureMessage(message);
                    if (NetworkSystem.Instance.InRoom)
                        NetworkSystem.Instance.ReturnToSinglePlayer();
                }
                else
                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> {PatchHandler.PatchErrors} patch{(PatchHandler.PatchErrors > 1 ? "es" : "")} failed to initialize. Please report this as an issue to the GitHub repository.", 10000);
            }
        }

        public static void Prefix()
        {
            shouldBePC = !XRSettings.isDeviceActive;

            try
            {
                rightPrimary = ControllerInputPoller.instance.rightControllerPrimaryButton || UnityInput.GetKey(Settings.pcBindings[Settings.ControllerBinding.RightPrimaryButton]);
                rightSecondary = ControllerInputPoller.instance.rightControllerSecondaryButton || UnityInput.GetKey(Settings.pcBindings[Settings.ControllerBinding.RightSecondaryButton]);
                leftPrimary = ControllerInputPoller.instance.leftControllerPrimaryButton || UnityInput.GetKey(Settings.pcBindings[Settings.ControllerBinding.LeftPrimaryButton]);
                leftSecondary = ControllerInputPoller.instance.leftControllerSecondaryButton || UnityInput.GetKey(Settings.pcBindings[Settings.ControllerBinding.LeftSecondaryButton]);
                leftGrab = ControllerInputPoller.instance.leftGrab || UnityInput.GetKey(Settings.pcBindings[Settings.ControllerBinding.LeftGrip]);
                rightGrab = ControllerInputPoller.instance.rightGrab || UnityInput.GetKey(Settings.pcBindings[Settings.ControllerBinding.RightGrip]);
                leftTrigger = ControllerInputPoller.TriggerFloat(XRNode.LeftHand);
                rightTrigger = ControllerInputPoller.TriggerFloat(XRNode.RightHand);

                if (UnityInput.GetKey(Settings.pcBindings[Settings.ControllerBinding.LeftTrigger]))
                    leftTrigger = 1f;

                if (UnityInput.GetKey(Settings.pcBindings[Settings.ControllerBinding.RightTrigger]))
                    rightTrigger = 1f;

                if (IsSteam)
                {
                    leftJoystick = SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.GetAxis(SteamVR_Input_Sources.LeftHand);
                    rightJoystick = SteamVR_Actions.gorillaTag_RightJoystick2DAxis.GetAxis(SteamVR_Input_Sources.RightHand);

                    leftJoystickClick = SteamVR_Actions.gorillaTag_LeftJoystickClick.GetState(SteamVR_Input_Sources.LeftHand);
                    rightJoystickClick = SteamVR_Actions.gorillaTag_RightJoystickClick.GetState(SteamVR_Input_Sources.RightHand);
                }
                else
                {
                    ControllerInputPoller.instance.leftControllerDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftJoystick);
                    ControllerInputPoller.instance.rightControllerDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightJoystick);

                    ControllerInputPoller.instance.leftControllerDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out leftJoystickClick);
                    ControllerInputPoller.instance.rightControllerDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out rightJoystickClick);
                }

                bool arrowKeysPressed = UnityInput.GetKey(Key.UpArrow) || UnityInput.GetKey(Key.DownArrow) || UnityInput.GetKey(Key.LeftArrow) || UnityInput.GetKey(Key.RightArrow);
                bool leftOverride = UnityInput.GetKey(Settings.pcBindings[Settings.ControllerBinding.LeftOverride]);

                if (arrowKeysPressed)
                {
                    Vector2 direction = new Vector2((UnityInput.GetKey(Key.RightArrow) ? 1f : 0f) + (UnityInput.GetKey(Key.LeftArrow) ? -1f : 0f), (UnityInput.GetKey(Key.UpArrow) ? 1f : 0f) + (UnityInput.GetKey(Key.DownArrow) ? -1f : 0f));
                    if (leftOverride)
                        rightJoystick = direction;
                    else
                        leftJoystick = direction;
                }

                if (UnityInput.GetKey(Settings.pcBindings[Settings.ControllerBinding.JoystickClick]))
                {
                    if (leftOverride)
                        rightJoystickClick = true;
                    else
                        leftJoystickClick = true;
                }

                if (adaptiveButtons)
                {
                    switch (ControllerUtilities.GetLeftControllerType())
                    {
                        case ControllerUtilities.ControllerType.ValveIndex:
                            leftGrab = ControllerInputPoller.instance.leftControllerGripFloat > 0.75f;
                            break;
                        case ControllerUtilities.ControllerType.VIVE:
                            leftPrimary = leftJoystickClick;
                            break;
                    }

                    switch (ControllerUtilities.GetRightControllerType())
                    {
                        case ControllerUtilities.ControllerType.ValveIndex:
                            rightGrab = ControllerInputPoller.instance.rightControllerGripFloat > 0.75f;
                            break;
                        case ControllerUtilities.ControllerType.VIVE:
                            rightPrimary = rightJoystickClick;
                            break;
                    }
                }

            }
            catch { }


            try
            {

                if (!HasLoaded)
                {
                    HasLoaded = true;
                    OnLaunch();
                }



                bool GetLeftInput(int i) => i switch
                {
                    0 => leftPrimary,
                    1 => leftSecondary,
                    2 => leftGrab,
                    3 => leftTrigger > 0.5f,
                    4 => leftJoystickClick,
                    _ => false
                };

                bool GetRightInput(int i) => i switch
                {
                    0 => rightPrimary,
                    1 => rightSecondary,
                    2 => rightGrab,
                    3 => rightTrigger > 0.5f,
                    4 => rightJoystickClick,
                    _ => false
                };

                bool isKeyboardCondition = (UnityInput.GetKey(Key.Q) && !UI.HasKeyboardFocus) || (inTextInput && isKeyboardPc);
                bool buttonCondition = rightHand ? GetRightInput(menuButtonIndex) : GetLeftInput(menuButtonIndex);

                if (oneHand)
                    buttonCondition = rightHand ? GetLeftInput(menuButtonIndex) : GetRightInput(menuButtonIndex);

                if (bothHands)
                    buttonCondition = GetRightInput(menuButtonIndex) || GetLeftInput(menuButtonIndex);

                if (!XRSettings.isDeviceActive)
                    buttonCondition = false;

                if (wristMenu)
                {
                    bool shouldOpen = Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position + GorillaTagger.Instance.leftHandTransform.forward * 0.1f, ControllerUtilities.GetTrueRightHand().position) < 0.1f;
                    if (rightHand)
                        shouldOpen = Vector3.Distance(ControllerUtilities.GetTrueLeftHand().position, GorillaTagger.Instance.rightHandTransform.position + GorillaTagger.Instance.rightHandTransform.forward * 0.1f) < 0.1f;

                    if (shouldOpen && !lastChecker)
                        wristOpen = !wristOpen;

                    lastChecker = shouldOpen;

                    buttonCondition = wristOpen;
                }

                if (joystickMenu)
                {
                    bool shouldOpen = rightJoystickClick;

                    if (shouldOpen && !lastChecker)
                    {
                        joystickOpen = !joystickOpen;
                        joystickDelay = Time.time + 0.2f;
                    }
                    lastChecker = shouldOpen;

                    buttonCondition = joystickOpen;
                }
                else
                    joystickButtonSelected = 0;

                if (physicalMenu)
                {
                    if (buttonCondition)
                        physicalOpenPosition = Vector3.zero;

                    buttonCondition = true;
                }

                if (toggleButton)
                {
                    if ((buttonCondition || isKeyboardCondition) && !toggleButtonHeld)
                    {
                        toggleButtonActive = !toggleButtonActive;
                        keyboardWithToggleButton = isKeyboardCondition;
                    }

                    toggleButtonHeld = buttonCondition || isKeyboardCondition;
                    buttonCondition = toggleButtonActive;
                    isKeyboardCondition = toggleButtonActive && keyboardWithToggleButton;
                }

                buttonCondition |= isKeyboardCondition;
                buttonCondition |= inTextInput;
                buttonCondition &= !Lockdown;

                if (watchMenu)
                    buttonCondition = isKeyboardCondition;

                if (chestMenu)
                    buttonCondition = isKeyboardCondition || chestMenuOpen;
                switch (buttonCondition)
                {
                    case true when menu == null:
                        OpenMenu();
                        break;
                    case false when menu != null:
                        CloseMenu();
                        break;
                }

                if (buttonCondition && menu != null)
                    RecenterMenu();



                try
                {
                    if (TPC == null)
                    {
                        try
                        {
                            TPC = GetObject("Player Objects/Third Person Camera/Shoulder Camera").GetComponent<Camera>();
                        }
                        catch
                        {
                            TPC = GetObject("Shoulder Camera").GetComponent<Camera>();
                        }
                    }
                }
                catch { }



                if (fpsCountAverage)
                {
                    fpsAverageNumber *= 99f;
                    fpsAverageNumber += 1f / Time.unscaledDeltaTime;
                    fpsAverageNumber /= 100f;
                }
                else
                    fpsAverageNumber = 1f / Time.unscaledDeltaTime;

                if (Time.time > fpsAvgTime || !fpsCountTimed)
                {
                    lastDeltaTime = Mathf.Ceil(fpsAverageNumber);
                    fpsAvgTime = Time.time + 1f;
                }

                if (fpsCount != null)
                {
                    string textToSet = ftCount ? $"FT: {Mathf.Floor(1f / lastDeltaTime * 10000f) / 10f} ms" : $"FPS: {lastDeltaTime}";
                    if (hidetitle && !noPageNumber) textToSet += "      ";
                    if (disableFpsCounter) textToSet = "";
                    if (hidetitle && !noPageNumber) textToSet += "Page " + (pageNumber + 1);

                    fpsCount.text = FollowMenuSettings(textToSet);
                }

                if (watermarkImage != null)
                    watermarkImage.GetComponent<RectTransform>().localRotation = Quaternion.Euler(new Vector3(0f, 90f, 90f));

                if (menu != null)
                {
                    float press = Mathf.Clamp01((panelPressTime + PanelPressTime - Time.time) / PanelPressTime) * PanelPressDepth;

                    if (!Mathf.Approximately(press, lastPanelPress))
                    {
                        Vector3 delta = new Vector3(press - lastPanelPress, 0f, 0f);
                        foreach (Transform child in menu.transform)
                            child.localPosition -= delta;

                        lastPanelPress = press;
                    }
                }

                if (animatedTitle && title != null)
                {
                    string targetString = doCustomName ? NoRichtextTags(customMenuName) : "Interstellar";
                    int length = (int)Mathf.PingPong(Time.time / 0.25f, targetString.Length + 1);
                    title.text = length > 0 ? targetString[..length] : "";
                }

                if (gradientTitle && title != null)
                    title.text = RichtextGradient(NoRichtextTags(title.text),
                        new[]
                        {
                            new GradientColorKey(BrightenColor(buttonColors[0].GetColor(0)), 0f),
                            new GradientColorKey(BrightenColor(buttonColors[0].GetColor(0), 0.95f), 0.5f),
                            new GradientColorKey(BrightenColor(buttonColors[0].GetColor(0)), 1f)
                        });

                if (keyboardInputObject != null)
                    keyboardInputObject.text = FollowMenuSettings(keyboardInput) + (Time.frameCount / 45 % 2 == 0 ? "|" : " ");



                Watches.ForEach(watch => watch.Update());

                if (longmenu && pageNumber != 0)
                {
                    pageNumber = 0;
                    ReloadMenu();
                }

                if (physicalMenu && menu != null)
                {
                    try
                    {
                        if (Vector3.Distance(GorillaTagger.Instance.bodyCollider.transform.position, menu.transform.position) < 1.5f)
                        {
                            if (reference == null)
                                CreateReference();
                        }
                        else
                        {
                            if (reference != null)
                            {
                                Destroy(reference);
                                reference = null;
                            }
                        }
                    }
                    catch { }
                }

                UpdateKeyboard();

                if (annoyingMode)
                {
                    CustomBoardManager.BoardMaterial.color = new Color32(226, 74, 44, 255);
                    int randomChance = Random.Range(1, 400);
                    if (randomChance == 21)
                    {
                        VRRig.LocalRig.PlayHandTapLocal(84, true, 0.4f);
                        VRRig.LocalRig.PlayHandTapLocal(84, false, 0.4f);
                    }
                }

                if (CurrentPrompt != null && CurrentPrompt.IsText && !inTextInput)
                    Settings.SpawnKeyboard();



                if (menu != null)
                {
                    if (pageButtonType == 3 || pageButtonType == 4)
                    {
                        bool previousButton = pageButtonType == 3 ? leftGrab : leftTrigger > 0.5f;
                        bool nextButton = pageButtonType == 3 ? rightGrab : rightTrigger > 0.5f;

                        if (Time.time > pageButtonChangeDelay)
                        {
                            if (previousButton)
                            {
                                pageButtonChangeDelay = Time.time + 0.2f;
                                if (exclusivePageSounds || (!string.IsNullOrEmpty(SoundManager.DefaultSoundpack) && SoundManager.DefaultSoundpack != "None"))
                                    SoundManager.Play("Previous");
                                else
                                    SoundManager.Play("Button");
                                Toggle("PreviousPage");
                            }

                            if (nextButton)
                            {
                                pageButtonChangeDelay = Time.time + 0.2f;
                                if (exclusivePageSounds || (!string.IsNullOrEmpty(SoundManager.DefaultSoundpack) && SoundManager.DefaultSoundpack != "None"))
                                    SoundManager.Play("Next");
                                else
                                    SoundManager.Play("Button");
                                Toggle("NextPage");
                            }
                        }

                        if (!previousButton && !nextButton)
                            pageButtonChangeDelay = -1f;
                    }
                }

                if (joystickMenu && joystickOpen)
                {
                    Vector2 js = leftJoystick;
                    if (Time.time > joystickDelay)
                    {
                        int lastButton = PageSize;

                        if (joystickMenuSearching)
                            lastButton++;

                        if (js.x > 0.5f)
                        {
                            if (dynamicSounds)
                                SoundManager.Play("Next");

                            Toggle("NextPage");
                            joystickDelay = Time.time + 0.2f;
                        }
                        if (js.x < -0.5f)
                        {
                            if (dynamicSounds)
                                SoundManager.Play("Previous");

                            Toggle("PreviousPage");
                            joystickDelay = Time.time + 0.2f;
                        }

                        if (js.y > 0.5f)
                        {
                            if (dynamicSounds)
                                SoundManager.Play("Up");

                            MoveJoystickSelection(-1, lastButton);

                            ReloadMenu();
                            joystickDelay = Time.time + 0.2f;
                        }
                        if (js.y < -0.5f)
                        {
                            if (dynamicSounds)
                                SoundManager.Play("Down");

                            MoveJoystickSelection(1, lastButton);

                            ReloadMenu();
                            joystickDelay = Time.time + 0.2f;
                        }

                        if (leftJoystickClick)
                        {
                            if (dynamicSounds)
                                SoundManager.Play("Select");

                            ButtonInfo button = string.IsNullOrEmpty(joystickSelectedButton) ? null : Buttons.GetIndex(joystickSelectedButton);
                            if (button != null && button.incremental)
                                ToggleIncremental(joystickSelectedButton, leftTrigger < 0.5f);
                            else if (button != null)
                                Toggle(joystickSelectedButton, true);
                            ReloadMenu();
                            joystickDelay = Time.time + 0.2f;
                        }
                    }
                }

                if (chestMenu)
                {
                    if (chestMenuOpen)
                    {
                        if (chestMenuGrabbed == null)
                        {
                            if (menuBackground != null)
                            {
                                bool leftGrabbing = Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position, menuBackground.transform.position) < 0.4f && leftGrab;
                                bool rightGrabbing = Vector3.Distance(GorillaTagger.Instance.rightHandTransform.position, menuBackground.transform.position) < 0.4f && rightGrab;

                                if (leftGrabbing || rightGrabbing)
                                {
                                    chestMenuGrabbed = leftGrabbing;
                                    rightHand = rightGrabbing;

                                    if (reference != null)
                                    {
                                        Destroy(reference);
                                        reference = null;

                                        CreateReference();
                                    }
                                }
                            }
                        }
                        else
                        {
                            bool gripping = chestMenuGrabbed.Value ? leftGrab : rightGrab;

                            if (!gripping)
                            {
                                chestMenuGrabbed = null;
                                chestMenuOpen = false;
                            }
                        }
                    }
                    else
                    {
                        static bool IsBangingPosition(Vector3 position)
                        {
                            Vector3 closestPoint = GorillaTagger.Instance.bodyCollider.ClosestPoint(position);
                            return closestPoint.Distance(position) <= 0.1f;
                        }

                        bool bangingChest = IsBangingPosition(ControllerUtilities.GetTrueLeftHand().position) || IsBangingPosition(ControllerUtilities.GetTrueRightHand().position);

                        if (bangingChest && !previousChestTapState)
                        {
                            if (Time.time > chestTapDelay)
                                chestTapCount = 0;

                            chestTapDelay = Time.time + 0.5f;
                            chestTapCount++;

                            if (chestTapCount >= 3)
                                chestMenuOpen = true;
                        }

                        previousChestTapState = bangingChest;
                    }
                }

                if (pageScrolling)
                {
                    bool shouldReload = false;
                    if (pageNumber != 0)
                    {
                        pageNumber = 0;
                        shouldReload = true;
                    }

                    Vector2 js = rightHand ? leftJoystick : rightJoystick;
                    if (Time.time > scrollDelay)
                    {
                        if (js.y > 0.5f)
                        {
                            pageOffset = Mathf.Clamp(pageOffset - 1, 0, DisplayedItemCount - PageSize);

                            shouldReload = true;
                            scrollDelay = Time.time + 0.1f;
                        }

                        if (js.y < -0.5f)
                        {
                            pageOffset = Mathf.Clamp(pageOffset + 1, 0, DisplayedItemCount - PageSize);

                            shouldReload = true;
                            scrollDelay = Time.time + 0.1f;
                        }
                    }

                    if (shouldReload)
                        ReloadMenu();
                }

                try
                {
                    if (watchMenu)
                    {
                        ButtonInfo[] toSortOf = Buttons.buttons[Buttons.CurrentCategoryIndex];

                        if (Buttons.CurrentCategoryName == "Favorite Mods")
                            toSortOf = StringsToInfos(favorites.ToArray());

                        if (Buttons.CurrentCategoryName == "Enabled Mods")
                        {
                            List<ButtonInfo> enabledMods = new List<ButtonInfo>();
                            int categoryIndex = 0;
                            foreach (ButtonInfo[] buttonList in Buttons.buttons)
                            {
                                enabledMods.AddRange(buttonList.Where(v => v.enabled && (!hideSettings || !Buttons.categoryNames[categoryIndex].Contains("Settings"))));
                                categoryIndex++;
                            }
                            enabledMods = enabledMods.OrderBy(v => v.overlapText ?? v.buttonText).ToList();
                            enabledMods.Insert(0, Buttons.GetIndex("Exit Enabled Mods"));
                            toSortOf = enabledMods.ToArray();
                        }

                        TextMeshProUGUI watchText = Watches[0].text;

                        string text = toSortOf[watchMenuIndex].buttonText;

                        if (toSortOf[watchMenuIndex].overlapText != null)
                            text = toSortOf[watchMenuIndex].overlapText;

                        text += $"\n<color=grey>[{watchMenuIndex + 1}/{toSortOf.Length}]\n{DateTime.Now:hh:mm tt}</color>";

                        text = FollowMenuSettings(text);

                        watchText.SafeSetText(text);

                        if (watchIndicatorMat == null)
                            watchIndicatorMat = new Material(Shader.Find("GorillaTag/UberShader"));

                        watchIndicatorMat.color = toSortOf[watchMenuIndex].enabled ? buttonColors[1].GetCurrentColor() : buttonColors[0].GetCurrentColor();
                        Watches[0].indicator.material = watchIndicatorMat;

                        Vector2 js = rightHand ? rightJoystick : leftJoystick;
                        if (Time.time > wristMenuDelay)
                        {
                            if (js.x > 0.5f || (rightHand ? js.y < -0.5f : js.y > 0.5f))
                            {
                                watchMenuIndex++;
                                if (watchMenuIndex > toSortOf.Length - 1)
                                    watchMenuIndex = 0;

                                wristMenuDelay = Time.time + 0.2f;
                            }
                            if (js.x < -0.5f || (rightHand ? js.y > 0.5f : js.y < -0.5f))
                            {
                                watchMenuIndex--;
                                if (watchMenuIndex < 0)
                                    watchMenuIndex = toSortOf.Length - 1;

                                wristMenuDelay = Time.time + 0.2f;
                            }
                            if (rightHand ? rightJoystickClick : leftJoystickClick)
                            {
                                int archive = Buttons.CurrentCategoryIndex;
                                Toggle(toSortOf[watchMenuIndex].buttonText, true);
                                if (Buttons.CurrentCategoryIndex != archive)
                                    watchMenuIndex = 0;

                                wristMenuDelay = Time.time + 0.2f;
                            }
                        }
                    }
                }
                catch { }



                try
                {
                    if (!legacyGhostview && GhostRig == null)
                    {
                        GameObject ghostRigHolder = new GameObject("ghostRigHolder");
                        ghostRigHolder.SetActive(false);

                        GhostRig = Instantiate(VRRig.LocalRig, GTPlayer.Instance.transform.position, GTPlayer.Instance.transform.rotation, ghostRigHolder.transform);
                        GhostRig.headBodyOffset = Vector3.zero;

                        GhostRig.gameObject.SetActive(false);
                        GhostRig.transform.SetParent(VRRig.LocalRig.transform.parent);

                        Destroy(ghostRigHolder);

                        GhostRig.transform.Find("VR Constraints/LeftArm/Left Arm IK/SlideAudio")?.gameObject.SetActive(false);
                        GhostRig.transform.Find("VR Constraints/RightArm/Right Arm IK/SlideAudio")?.gameObject.SetActive(false);
                        Transform bodySlideAudio = GhostRig.transform.Find("bodySlideAudio") ?? GhostRig.transform.Find("rig/body_pivot/SlideAudio");
                        if (bodySlideAudio != null) bodySlideAudio.gameObject.SetActive(false);
                        OwnershipGaurd ownershipGuard = GhostRig.GetComponent<OwnershipGaurd>();
                        if (ownershipGuard != null) ownershipGuard.enabled = false;

                        Visuals.FixRigMaterialESPColors(GhostRig);

                        GhostRig.transform.position = Vector3.one * float.MaxValue;
                    }

                    if (GhostMaterial == null)
                        GhostMaterial = new Material(Shader.Find("GUI/Text Shader"));

                    if (legacyGhostViewLeft == null)
                    {
                        legacyGhostViewLeft = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Sphere);
                        Destroy(legacyGhostViewLeft.GetComponent<SphereCollider>());

                        legacyGhostViewLeft.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                    }

                    if (legacyGhostViewRight == null)
                    {
                        legacyGhostViewRight = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Sphere);
                        Destroy(legacyGhostViewRight.GetComponent<SphereCollider>());

                        legacyGhostViewRight.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                    }

                    if ((!VRRig.LocalRig.enabled || ghostException) && !disableGhostview)
                    {
                        Color color = backgroundColor.GetCurrentColor();

                        if (legacyGhostview)
                        {
                            if (GhostRig != null && GhostRig.gameObject.activeSelf)
                            {
                                GhostRig.gameObject.SetActive(false);
                                GhostRig.transform.position = Vector3.one * float.MaxValue;
                            }

                            legacyGhostViewLeft.SetActive(true);
                            legacyGhostViewLeft.transform.position = ControllerUtilities.GetTrueLeftHand().position;
                            legacyGhostViewLeft.GetComponent<Renderer>().material.color = color;

                            legacyGhostViewRight.SetActive(true);
                            legacyGhostViewRight.transform.position = ControllerUtilities.GetTrueRightHand().position;
                            legacyGhostViewRight.GetComponent<Renderer>().material.color = color;
                        }
                        else
                        {
                            if (legacyGhostViewLeft != null && legacyGhostViewLeft.activeSelf)
                                legacyGhostViewLeft.SetActive(false);
                            if (legacyGhostViewRight != null && legacyGhostViewRight.activeSelf)
                                legacyGhostViewRight.SetActive(false);

                            GhostRig.gameObject.SetActive(true);

                            Color ghm = color;
                            ghm.a = 0.5f;

                            GhostMaterial.color = ghm;
                            GhostRig.mainSkin.material = GhostMaterial;
                        }
                    }
                    else
                    {
                        if (GhostRig != null)
                        {
                            GhostRig.gameObject.SetActive(false);
                            GhostRig.transform.position = Vector3.one * float.MaxValue;
                        }

                        legacyGhostViewLeft.SetActive(false);
                        legacyGhostViewRight.SetActive(false);
                    }
                }
                catch { }





                try
                {
                    Inputs["A"] = rightPrimary;
                    Inputs["B"] = rightSecondary;
                    Inputs["X"] = leftPrimary;
                    Inputs["Y"] = leftSecondary;
                    Inputs["LG"] = leftGrab;
                    Inputs["RG"] = rightGrab;
                    Inputs["LT"] = leftTrigger > 0.5f;
                    Inputs["RT"] = rightTrigger > 0.5f;
                    Inputs["LJ"] = leftJoystickClick;
                    Inputs["RJ"] = rightJoystickClick;

                    foreach (KeyValuePair<string, List<string>> binding in ModBindings)
                    {
                        string bindInput = binding.Key;
                        List<string> boundMods = binding.Value;

                        if (boundMods.Count <= 0) continue;
                        bool bindValue = Inputs[bindInput];
                        foreach (string modName in boundMods)
                        {
                            ButtonInfo buttonInfo = Buttons.GetIndex(modName);
                            if (buttonInfo == null) continue;
                            buttonInfo.customBind = bindInput;

                            if (ToggleBindings || !buttonInfo.isTogglable)
                            {
                                if (bindValue && !BindStates[bindInput])
                                    Toggle(modName, true, true);
                            }

                            if (ToggleBindings) continue;
                            if ((bindValue && !buttonInfo.enabled) || (!bindValue && buttonInfo.enabled))
                                Toggle(modName, true, true);
                        }

                        BindStates[bindInput] = bindValue;
                    }
                }
                catch { }


                GunUtilities.CleanUp();


                foreach (ButtonInfo[] buttonList in Buttons.buttons)
                {
                    foreach (ButtonInfo button in buttonList)
                    {
                        if (!(button.enabled || button.label)) continue;
                        if (button.method == null && button.postMethod == null) continue;
                        if (button.postMethod != null) postActions.Add(button.buttonText);
                        if (button.firstFrame) button.firstFrame = false;
                        else if (button.method != null)
                            InvokeButton(button, button.method);
                    }
                }

            }
            catch (Exception exc)
            {
                LogManager.LogError($"Error with prefix at {exc.StackTrace}: {exc.Message}");
            }
        }
        private static readonly List<string> postActions = new List<string>();
        public static void Postfix()
        {
            try
            {
                foreach (string buttonName in postActions)
                {
                    try
                    {
                        ButtonInfo button = Buttons.GetIndex(buttonName);
                        try
                        {
                            Action postMethod = button.postMethod ?? throw new NullReferenceException();
                            InvokeButton(button, postMethod);
                        }
                        catch (Exception exc)
                        {
                            LogManager.LogError($"Error with mod postMethod {button.buttonText} at {exc.StackTrace}: {exc.Message}");
                        }
                    }
                    catch { }
                }
            }
            catch (Exception exc)
            {
                LogManager.LogError($"Error with postfix at {exc.StackTrace}: {exc.Message}");
            }

            postActions.Clear();
        }

        private static void InvokeButton(ButtonInfo button, Action invoke)
        {
            bool _leftPrimary = leftPrimary, _leftSecondary = leftSecondary;
            bool _rightPrimary = rightPrimary, _rightSecondary = rightSecondary;
            bool _leftGrab = leftGrab, _rightGrab = rightGrab;
            float _leftTrigger = leftTrigger, _rightTrigger = rightTrigger;
            bool _leftJoystickClick = leftJoystickClick, _rightJoystickClick = rightJoystickClick;

            if (OverwriteKeybinds && button.customBind != null)
            {
                leftPrimary = leftSecondary = rightPrimary = rightSecondary = true;
                leftGrab = rightGrab = true;
                leftTrigger = rightTrigger = 1f;
                leftJoystickClick = rightJoystickClick = true;
            }

            if (button.rebindKey != null)
            {
                float buttonAmount = button.rebindKey switch
                {
                    "A" => _rightPrimary ? 1f : 0f,
                    "B" => _rightSecondary ? 1f : 0f,
                    "X" => _leftPrimary ? 1f : 0f,
                    "Y" => _leftSecondary ? 1f : 0f,
                    "LG" => _leftGrab ? 1f : 0f,
                    "RG" => _rightGrab ? 1f : 0f,
                    "LT" => _leftTrigger,
                    "RT" => _rightTrigger,
                    "LJ" => _leftJoystickClick ? 1f : 0f,
                    "RJ" => _rightJoystickClick ? 1f : 0f,
                    _ => 0f
                };
                leftPrimary = leftSecondary = rightPrimary = rightSecondary = buttonAmount > 0.5f;
                leftGrab = rightGrab = buttonAmount > 0.5f;
                leftTrigger = rightTrigger = buttonAmount;
                leftJoystickClick = rightJoystickClick = buttonAmount > 0.5f;
            }

            try { invoke(); }
            catch (Exception exc)
            {
                LogManager.LogError($"Error with mod {button.buttonText} at {exc.StackTrace}: {exc.Message}");
            }

            if (button.rebindKey != null)
            {
                leftPrimary = _leftPrimary; leftSecondary = _leftSecondary;
                rightPrimary = _rightPrimary; rightSecondary = _rightSecondary;
                leftGrab = _leftGrab; rightGrab = _rightGrab;
                leftTrigger = _leftTrigger; rightTrigger = _rightTrigger;
                leftJoystickClick = _leftJoystickClick; rightJoystickClick = _rightJoystickClick;
            }

            if (!OverwriteKeybinds || button.customBind == null) return;
            leftPrimary = _leftPrimary; leftSecondary = _leftSecondary;
            rightPrimary = _rightPrimary; rightSecondary = _rightSecondary;
            leftGrab = _leftGrab; rightGrab = _rightGrab;
            leftTrigger = _leftTrigger; rightTrigger = _rightTrigger;
            leftJoystickClick = _leftJoystickClick; rightJoystickClick = _rightJoystickClick;
        }

        public static List<Key> lastPressedKeys = new List<Key>();
        private static readonly Dictionary<Key, HeldKeyRepeat> keyboardRepeat = new Dictionary<Key, HeldKeyRepeat>();
        public static readonly Key[] detectedKeys = {
            Key.A, Key.B, Key.C, Key.D, Key.E,
            Key.F, Key.G, Key.H, Key.I, Key.J,
            Key.K, Key.L, Key.M, Key.N, Key.O,
            Key.P, Key.Q, Key.R, Key.S, Key.T,
            Key.U, Key.V, Key.W, Key.X, Key.Y,
            Key.Z,

            Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3,
            Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7,
            Key.Digit8, Key.Digit9,

            Key.Comma, Key.Period, Key.Slash, Key.Backslash,
            Key.Minus, Key.Equals,Key.Semicolon, Key.Quote,
            Key.LeftBracket, Key.RightBracket,

            Key.Space, Key.Backspace, Key.Enter, Key.Escape
        };

        private static void UpdateKeyboard()
        {
            if (VRKeyboard != null)
            {
                if (Vector3.Distance(VRKeyboard.transform.position, GorillaTagger.Instance.bodyCollider.transform.position) > menuScale && !leftSecondary)
                {
                    VRKeyboard.transform.position = GorillaTagger.Instance.bodyCollider.transform.position;
                    VRKeyboard.transform.rotation = GorillaTagger.Instance.bodyCollider.transform.rotation;
                }
            }

            if (!inTextInput || !isKeyboardPc || UI.HasKeyboardFocus)
            {
                lastPressedKeys.Clear();
                foreach (Key key in detectedKeys)
                {
                    if (UnityInput.GetKey(key))
                    {
                        lastPressedKeys.Add(key);
                    }
                }
                keyboardRepeat.Clear();
                return;
            }

            List<Key> keysPressed = new List<Key>();
            foreach (Key key in detectedKeys)
            {
                if (UnityInput.GetKey(key))
                {
                    keysPressed.Add(key);
                    if (!keyboardRepeat.TryGetValue(key, out HeldKeyRepeat repeat))
                    {
                        repeat = new HeldKeyRepeat();
                        if (lastPressedKeys.Contains(key)) repeat.SuppressUntilRelease();
                        keyboardRepeat[key] = repeat;
                    }
                    if (!repeat.Poll(true, Time.unscaledTime, key == Key.Backspace)) continue;
                    if (key == Key.Backspace && string.IsNullOrEmpty(keyboardInput)) continue;

                    if (UnityInput.GetKey(Key.LeftCtrl))
                    {
                        switch (key)
                        {
                            case Key.A:
                                keyboardInput = "";
                                break;
                            case Key.C:
                                GUIUtility.systemCopyBuffer = keyboardInput;
                                break;
                            case Key.V:
                                keyboardInput += GUIUtility.systemCopyBuffer;
                                break;
                            case Key.Backspace:
                                if (!string.IsNullOrEmpty(keyboardInput))
                                    keyboardInput = keyboardInput[..^1];
                                break;
                        }
                    }
                    else
                    {
                        switch (key)
                        {
                            case Key.Backspace:
                                if (!string.IsNullOrEmpty(keyboardInput))
                                    keyboardInput = keyboardInput[..^1];
                                break;
                            case Key.Escape:
                                Toggle(isSearching ? "Search" : "Decline Prompt");
                                break;
                            case Key.Enter:
                                HandleSearchOrPrompt();
                                break;
                            case Key.Space:
                                keyboardInput += " ";
                                break;
                            default:
                                string keyName = key.ToString();
                                if (keyName.Length == 1)
                                {
                                    bool isShift = UnityInput.GetKey(Key.LeftShift) || UnityInput.GetKey(Key.RightShift);
                                    keyboardInput += isShift ? keyName.ToUpper() : keyName.ToLower();
                                }
                                else if (keyName.StartsWith("Digit"))
                                {
                                    keyboardInput += keyName.Replace("Digit", "");
                                }
                                break;
                        }
                    }

                    if (pcKeyboardSounds)
                        VRRig.LocalRig.PlayHandTapLocal(66, false, buttonClickVolume / 10f);

                    if (!inTextInput)
                        break;

                    pageNumber = 0;

                    ReloadMenu();
                }
                else
                {
                    keyboardRepeat.Remove(key);
                }
            }

            lastPressedKeys = keysPressed;
        }
        private static List<ButtonInfo> GetSearchResults()
        {
            IEnumerable<ButtonInfo> candidates = nonGlobalSearch && Buttons.CurrentCategoryName != "Main"
                ? Buttons.buttons[Buttons.CurrentCategoryIndex]
                : Buttons.buttons.SelectMany(category => category);

            return ModSearch.Search(candidates, keyboardInput)
                .OrderBy(button => NoRichtextTags(button.overlapText ?? button.buttonText), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void HandleSearchOrPrompt()
        {
            if (isSearching)
            {
                List<ButtonInfo> searchedMods = GetSearchResults();

                if (searchedMods.Count > 0)
                {
                    ButtonInfo button = searchedMods[0];

                    if (button.incremental)
                        ToggleIncremental(button.buttonText, UnityInput.GetKey(Key.LeftShift));
                    else
                        Toggle(button.buttonText, true);
                }
            }
            else if (CurrentPrompt != null && CurrentPrompt.IsText)
            {
                Toggle("Accept Prompt");
            }
        }

        public static void PressKeyboardKey(string key)
        {
            switch (key)
            {
                case "Space":
                    keyboardInput += " ";
                    break;
                case "Backspace":
                    if (!string.IsNullOrEmpty(keyboardInput))
                        keyboardInput = keyboardInput[..^1];
                    break;
                case "Shift":
                    shift = !shift;
                    break;
                case "CapsLock":
                    lockShift = !lockShift;
                    break;

                case "Clear":
                    keyboardInput = "";
                    break;
                case "Copy":
                    GUIUtility.systemCopyBuffer = keyboardInput;
                    break;
                case "Paste":
                    keyboardInput += GUIUtility.systemCopyBuffer;
                    break;

                default:
                    Dictionary<string, string> shiftMap = new Dictionary<string, string>
                    {
                        { "1", "!" }, { "2", "@" }, { "3", "#" }, { "4", "$" }, { "5", "%" },
                        { "6", "^" }, { "7", "&" }, { "8", "*" }, { "9", "(" }, { "0", ")" },
                        { "-", "_" }, { "=", "+" }, { "[", "{" }, { "]", "}" }, { "\\", "|" },
                        { ";", ":" }, { "'", "\"" }, { ",", "<" }, { ".", ">" }, { "/", "?" },
                        { "`", "~" }
                    };

                    bool isShifted = lockShift ^ shift;
                    string keyStr = key.ToLower();

                    if (isShifted)
                    {
                        if (shiftMap.TryGetValue(keyStr, out var value))
                            keyboardInput += value;
                        else
                            keyboardInput += keyStr.ToUpper();
                    }
                    else
                        keyboardInput += keyStr.ToLower();

                    shift = false;
                    break;
            }

            KeyboardKey.keyLookupDictionary["CapsLock"].gameObject.GetOrAddComponent<ColorChanger>().colors = buttonColors[lockShift ? 1 : 0];
            KeyboardKey.keyLookupDictionary["Shift"].gameObject.GetOrAddComponent<ColorChanger>().colors = buttonColors[shift ? 1 : 0];

            pageNumber = 0;

            ReloadMenu();
        }

        private const float ButtonCornerRatio = 0.3f;
        private const float ButtonBorderRatio = 0.035f;
        private const float ButtonThickness = 0.02f;

        private static void RoundButton(GameObject buttonObject, bool enabled)
        {
            MeshFilter filter = buttonObject.GetComponent<MeshFilter>();
            if (filter == null || menu == null)
                return;

            bool modelRow = ModelMenuActive && buttonObject.name == "Model Button";
            if (modelRow)
            {
                if (MenuVisuals.RoundButton(buttonObject, enabled))
                    return;
                buttonObject.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            float targetThickness = modelRow ? ModelButtonLayout.PixelSize * 1.5f / menu.transform.localScale.x : ButtonThickness;
            float thickness = buttonObject.transform.localScale.x;
            if (thickness > targetThickness)
            {
                float shift = (thickness - targetThickness) / 2f;

                buttonObject.transform.localScale = new Vector3(
                    targetThickness,
                    buttonObject.transform.localScale.y,
                    buttonObject.transform.localScale.z);

                buttonObject.transform.localPosition += new Vector3(shift, 0f, 0f);

                BoxCollider box = buttonObject.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.size = new Vector3(thickness / targetThickness, 1f, 1f);
                    box.center = new Vector3(-shift / targetThickness, 0f, 0f);
                }
            }

            Vector3 scale = buttonObject.transform.localScale;
            Vector3 menuScale = menu.transform.localScale;

            float width = Mathf.Abs(scale.y * menuScale.y);
            float height = Mathf.Abs(scale.z * menuScale.z);
            if (width <= 0f || height <= 0f)
                return;

            float radius = modelRow ? ModelButtonLayout.PixelSize * 16f : height * ButtonCornerRatio;
            radius = Mathf.Min(radius, Mathf.Min(width, height) * 0.45f);
            float border = modelRow ? ModelButtonLayout.PixelSize * 1.5f : height * ButtonBorderRatio;

            filter.sharedMesh = RoundedBox.Get(radius / width, radius / height);

            if (buttonColors[0].transparent || buttonColors[1].transparent)
                return;

            GameObject outline = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
            Destroy(outline.GetComponent<BoxCollider>());

            outline.name = "Outline";
            outline.layer = buttonObject.layer;
            outline.transform.SetParent(buttonObject.transform, false);
            outline.transform.localRotation = Quaternion.identity;
            outline.transform.localPosition = new Vector3(-0.3f, 0f, 0f);
            outline.transform.localScale = new Vector3(1f, 1f + border * 2f / width, 1f + border * 2f / height);

            outline.GetComponent<MeshFilter>().sharedMesh = RoundedBox.Get(
                (radius + border) / (width + border * 2f),
                (radius + border) / (height + border * 2f));

            if (modelRow)
                outline.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

            ExtGradient gradient = buttonColors[enabled ? 1 : 0].Clone();
            gradient.customColor = () => modelRow ? ModelPalette.Border(enabled) : Color.Lerp(
                buttonColors[enabled ? 1 : 0].GetCurrentColor(),
                buttonColors[enabled ? 0 : 1].GetCurrentColor(),
                enabled ? 0.18f : 0.25f);

            outline.AddComponent<ColorChanger>().colors = gradient;
        }

        private static void AddButton(float offset, int buttonIndex, ButtonInfo method)
        {
            bool modelRow = ModelButtonLayout.IsRow(buttonIndex);
            bool back = buttonIndex == -2;
            if (method != null && !method.label)
            {
                GameObject buttonObject = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
                if (!UnityInput.GetKey(Key.Q) && !isKeyboardPc)
                    buttonObject.layer = 2;

                buttonObject.GetComponent<BoxCollider>().isTrigger = true;
                buttonObject.transform.parent = menu.transform;
                buttonObject.transform.rotation = Quaternion.identity;

                buttonObject.transform.localScale = thinMenu ? new Vector3(0.09f, 0.9f, ButtonDistance * 0.74f) : new Vector3(0.09f, 1.3f, ButtonDistance * 0.74f);

                if (longmenu && buttonIndex >= PageSize)
                {
                    menuBackground.transform.localScale += new Vector3(0f, 0f, 0.1f);
                    menuBackground.transform.localPosition += new Vector3(0f, 0f, -0.05f);
                }

                buttonObject.transform.localPosition = new Vector3(0.56f, 0f, 0.28f - offset);
                if (checkMode && buttonIndex > -1)
                {
                    buttonObject.transform.localScale = new Vector3(0.09f, 0.102f, 0.08f);
                    buttonObject.transform.localPosition = thinMenu ? new Vector3(0.56f, 0.399f, 0.28f - offset) : new Vector3(0.56f, 0.599f, 0.28f - offset);
                }

                if (modelRow && !checkMode)
                {
                    buttonObject.name = "Model Button";
                    buttonObject.transform.localPosition = ModelButtonLayout.LocalPosition(buttonIndex, back);
                    buttonObject.transform.localScale = ModelButtonLayout.LocalScale(back);
                    modelJoystickButtons.Add(new KeyValuePair<int, string>(buttonIndex, method.buttonText));
                }

                ButtonCollider Button = buttonObject.AddComponent<ButtonCollider>();
                Button.relatedText = method.buttonText;

                if (incrementalButtons)
                {
                    if (method.incremental)
                    {
                        if (modelRow && !checkMode)
                        {
                            Destroy(Button);
                            Vector3 rowScale = buttonObject.transform.localScale;
                            buttonObject.transform.localScale = new Vector3(rowScale.x, rowScale.y * 0.64f, rowScale.z);
                            RenderIncrementalButton(false, offset, buttonIndex, method);
                            RenderIncrementalButton(true, offset, buttonIndex, method);
                        }
                        else if (checkMode && buttonIndex > -1)
                        {
                            Button.incremental = true;
                            Button.positive = false;

                            RenderIncrementalText(false, offset);
                            RenderIncrementalButton(true, offset, buttonIndex, method);
                        }
                        else
                        {
                            buttonObject.transform.localScale -= new Vector3(0f, 0.254f, 0f);
                            Destroy(Button);

                            RenderIncrementalButton(false, offset, buttonIndex, method);
                            RenderIncrementalButton(true, offset, buttonIndex, method);
                        }
                    }
                }

                bool shouldSwap = swapButtonColors && buttonIndex < 0 && !back;

                RoundButton(buttonObject, shouldSwap ^ method.enabled);

                if (lastClickedName != method.buttonText)
                {
                    ColorChanger colorChanger = buttonObject.AddComponent<ColorChanger>();
                    colorChanger.colors = buttonColors[shouldSwap ^ method.enabled ? 1 : 0];

                    if (joystickMenu && buttonIndex == joystickButtonSelected)
                    {
                        joystickSelectedButton = method.buttonText;

                        if (!colorChanger.colors.transparent)
                        {
                            ExtGradient gradient = colorChanger.colors.Clone();
                            gradient.SetColor(0, Color.red);

                            colorChanger.colors = gradient;
                        }
                    }
                }
                else
                    CoroutineManager.instance.StartCoroutine(ButtonClick(buttonIndex, buttonObject.GetComponent<Renderer>(),
                        buttonObject.name == "Model Button" ? (bool?)(shouldSwap ^ method.enabled) : null));

                if (buttonObject.name != "Model Button")
                    FollowMenuSettings(buttonObject, shouldSwap ? method.enabled : !method.enabled);
            }
            TextMeshPro buttonText = new GameObject
            {
                transform = { parent = canvasObj.transform }
            }.AddComponent<TextMeshPro>();

            UpdateButtonText updater = buttonText.gameObject.AddComponent<UpdateButtonText>();
            updater.Init(method, buttonIndex, offset);
            updater.UpdateText();
        }

        private static void AddSearchButton()
        {
            if (ModelMenuActive)
            {
                AddModelSearchButton();
                return;
            }
            GameObject buttonObject = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
            if (!UnityInput.GetKey(Key.Q) && !isKeyboardPc)
                buttonObject.layer = 2;

            buttonObject.GetComponent<BoxCollider>().isTrigger = true;
            buttonObject.transform.parent = menu.transform;
            buttonObject.transform.rotation = Quaternion.identity;

            buttonObject.transform.localScale = new Vector3(0.09f, 0.102f, 0.08f);
            buttonObject.transform.localPosition = thinMenu ? new Vector3(0.56f, -0.450f, -0.58f) : new Vector3(0.56f, -0.7f, -0.58f);

            buttonObject.AddComponent<ButtonCollider>().relatedText = "Search";

            ColorChanger colorChanger = buttonObject.AddComponent<ColorChanger>();
            colorChanger.colors = buttonColors[isSearching ^ !swapButtonColors ? 0 : 1];

            if (joystickMenuSearching && joystickButtonSelected == PageSize)
            {
                joystickSelectedButton = "Search";

                ExtGradient gradient = colorChanger.colors.Clone();
                gradient.SetColor(0, Color.red);

                colorChanger.colors = gradient;
            }

            FollowMenuSettings(buttonObject, isSearching ^ !swapButtonColors);

            Image searchImage = new GameObject
            {
                transform =
                {
                    parent = canvasObj.transform
                }
            }.AddComponent<Image>();
            if (searchIcon == null)
                searchIcon = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.search.png");

            if (searchMat == null)
                searchMat = new Material(searchImage.material);

            searchImage.material = searchMat;
            searchImage.material.SetTexture("_MainTex", searchIcon);
            searchImage.AddComponent<UIColorChanger>().colors = textColors[isSearching ? 2 : 1];

            RectTransform imageTransform = searchImage.GetComponent<RectTransform>();
            imageTransform.localPosition = Vector3.zero;
            imageTransform.sizeDelta = new Vector2(.03f, .03f);

            imageTransform.localPosition = thinMenu ? new Vector3(.064f, -0.35f / 2.6f, -0.58f / 2.6f) : new Vector3(.064f, -0.54444444444f / 2.6f, -0.58f / 2.6f);

            imageTransform.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

            FollowMenuSettings(searchImage);
        }

        private static void AddModelSearchButton()
        {
            Bounds panel = ModelPanelBounds;
            float size = ModelButtonLayout.PixelSize * 60f;

            Vector3 position = new Vector3(panel.max.x + ModelButtonLayout.PixelSize * 3f / menu.transform.localScale.x,
                panel.center.y - panel.size.y * (750f / 800f - 0.5f), panel.max.z - panel.size.z * 73f / 1200f);

            GameObject button = new GameObject("Model Search");
            button.transform.SetParent(menu.transform, false);
            button.transform.localPosition = position;
            button.layer = !UnityInput.GetKey(Key.Q) && !isKeyboardPc ? 2 : 0;
            BoxCollider collider = button.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(0.09f, size / menu.transform.localScale.y, size / menu.transform.localScale.z);
            collider.center = new Vector3(-0.045f, 0f, 0f);
            button.AddComponent<ButtonCollider>().relatedText = "Search";

            searchIcon ??= LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.search.png");
            Image icon = new GameObject("Search Icon").AddComponent<Image>();
            icon.transform.SetParent(canvasObj.transform, false);
            icon.sprite = Sprite.Create(searchIcon, new Rect(0f, 0f, searchIcon.width, searchIcon.height), new Vector2(0.5f, 0.5f));
            icon.rectTransform.localPosition = canvasObj.transform.InverseTransformPoint(menu.transform.TransformPoint(position));
            icon.rectTransform.localRotation = Quaternion.Euler(180f, 90f, 90f);
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            icon.AddComponent<UIColorChanger>().colors = textColors[0];
            if (hideTextOnCamera) icon.gameObject.layer = 19;
            FollowMenuSettings(icon);

            if (joystickMenuSearching)
            {
                modelJoystickButtons.Add(new KeyValuePair<int, string>(PageSize, "Search"));
                if (joystickButtonSelected == PageSize) joystickSelectedButton = "Search";
            }
        }

        private static readonly List<KeyValuePair<int, string>> modelJoystickButtons = new List<KeyValuePair<int, string>>();

        private static void MoveJoystickSelection(int direction, int legacyCount)
        {
            if (ModelMenuActive && !checkMode && CurrentPrompt == null && modelJoystickButtons.Count > 0)
            {
                int current = modelJoystickButtons.FindIndex(button => button.Key == joystickButtonSelected);
                int next = (Mathf.Max(0, current) + direction + modelJoystickButtons.Count) % modelJoystickButtons.Count;
                joystickButtonSelected = modelJoystickButtons[next].Key;
            }
            else
                joystickButtonSelected = (joystickButtonSelected + direction + legacyCount) % Mathf.Max(1, legacyCount);
        }

        private static TMP_SpriteAsset buttonSpriteSheet;
        public static TMP_SpriteAsset ButtonSpriteSheet
        {
            get
            {
                if (buttonSpriteSheet != null) return buttonSpriteSheet;
                buttonSpriteSheet = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                buttonSpriteSheet.name = "Interstellar_SpriteSheet";

                var textureList = new List<Texture2D>();
                var spriteDataList = new List<(string name, int index)>();

                void AddSprite(string name, Texture2D tex)
                {
                    spriteDataList.Add((name, textureList.Count));
                    textureList.Add(tex);
                }

                AddSprite("Favorite", LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.favorite.png"));
                AddSprite("Folder", LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.folder.png"));

                for (int i = 1; i <= 3; i++)
                {
                    AddSprite("Left" + i, LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.left{i}.png"));
                    AddSprite("Right" + i, LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.right{i}.png"));
                }

                int maxSize = 512;
                Texture2D spriteSheet = new Texture2D(maxSize, maxSize);
                Rect[] rects = spriteSheet.PackTextures(textureList.ToArray(), 2, maxSize);

                buttonSpriteSheet.spriteSheet = spriteSheet;
                buttonSpriteSheet.material = new Material(Shader.Find("TextMeshPro/Sprite"))
                {
                    mainTexture = spriteSheet
                };

                buttonSpriteSheet.spriteInfoList = new List<TMP_Sprite>();
                Traverse.Create(buttonSpriteSheet).Field("m_Version").SetValue("1.1.0");

                buttonSpriteSheet.spriteGlyphTable.Clear();
                for (int i = 0; i < spriteDataList.Count; i++)
                {
                    var rect = rects[i];

                    var glyph = new TMP_SpriteGlyph
                    {
                        index = (uint)i,
                        metrics = new GlyphMetrics(
                            width: rect.width * spriteSheet.width,
                            height: rect.height * spriteSheet.height,
                            bearingX: -(rect.width * spriteSheet.width) / 2f,
                            bearingY: rect.height * spriteSheet.height * 0.8f,
                            advance: rect.width * spriteSheet.width
                        ),
                        glyphRect = new GlyphRect(
                            x: (int)(rect.x * spriteSheet.width),
                            y: (int)(rect.y * spriteSheet.height),
                            width: (int)(rect.width * spriteSheet.width),
                            height: (int)(rect.height * spriteSheet.height)
                        ),
                        scale = 1f,
                        atlasIndex = 0
                    };
                    buttonSpriteSheet.spriteGlyphTable.Add(glyph);
                }

                buttonSpriteSheet.spriteCharacterTable.Clear();
                for (int i = 0; i < spriteDataList.Count; i++)
                {
                    var (name, _) = spriteDataList[i];

                    var character = new TMP_SpriteCharacter(0xFFFE, buttonSpriteSheet.spriteGlyphTable[i])
                    {
                        name = name,
                        scale = 1f,
                        glyphIndex = (uint)i
                    };
                    buttonSpriteSheet.spriteCharacterTable.Add(character);
                }

                buttonSpriteSheet.UpdateLookupTables();
                return buttonSpriteSheet;
            }
        }

        private static void AddDebugButton()
        {
            bool infoScreenEnabled = Buttons.GetIndex("Info Screen").enabled;

            GameObject buttonObject = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
            if (!UnityInput.GetKey(Key.Q) && !isKeyboardPc)
                buttonObject.layer = 2;

            buttonObject.GetComponent<BoxCollider>().isTrigger = true;
            buttonObject.transform.parent = menu.transform;
            buttonObject.transform.rotation = Quaternion.identity;

            buttonObject.transform.localScale = new Vector3(0.09f, 0.102f, 0.08f);
            buttonObject.transform.localPosition = thinMenu ? new Vector3(0.56f, 0.450f, -0.58f) : new Vector3(0.56f, 0.7f, -0.58f);

            buttonObject.AddComponent<ButtonCollider>().relatedText = "Info Screen";

            ColorChanger colorChanger = buttonObject.AddComponent<ColorChanger>();
            colorChanger.colors = buttonColors[infoScreenEnabled ^ swapButtonColors ? 0 : 1];

            FollowMenuSettings(buttonObject, infoScreenEnabled ^ swapButtonColors);

            Image debugImage = new GameObject
            {
                transform =
                {
                    parent = canvasObj.transform
                }
            }.AddComponent<Image>();
            if (debugIcon == null)
                debugIcon = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.debug.png");

            if (debugMat == null)
                debugMat = new Material(debugImage.material);

            debugImage.material = debugMat;
            debugImage.material.SetTexture("_MainTex", debugIcon);
            debugImage.AddComponent<UIColorChanger>().colors = textColors[1];

            RectTransform imageTransform = debugImage.GetComponent<RectTransform>();
            imageTransform.localPosition = Vector3.zero;
            imageTransform.sizeDelta = new Vector2(.03f, .03f);

            imageTransform.localPosition = thinMenu ? new Vector3(.064f, 0.35f / 2.6f, -0.58f / 2.6f) : new Vector3(.064f, 0.54444444444f / 2.6f, -0.58f / 2.6f);

            imageTransform.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

            FollowMenuSettings(debugImage);
        }

        private static void RenderIncrementalButton(bool increment, float offset, int buttonIndex, ButtonInfo method)
        {
            if (!method.label)
            {
                GameObject buttonObject = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
                if (!UnityInput.GetKey(Key.Q) && !isKeyboardPc)
                    buttonObject.layer = 2;

                buttonObject.GetComponent<BoxCollider>().isTrigger = true;
                buttonObject.transform.parent = menu.transform;
                buttonObject.transform.rotation = Quaternion.identity;

                buttonObject.transform.localScale = new Vector3(0.09f, 0.102f, ButtonDistance * 0.8f);
                buttonObject.transform.localPosition = thinMenu ? new Vector3(0.56f, 0.399f, 0.28f - offset) : new Vector3(0.56f, 0.599f, 0.28f - offset);

                ButtonCollider button = buttonObject.AddComponent<ButtonCollider>();
                button.relatedText = method.buttonText;
                button.incremental = true;
                button.positive = increment;

                if (increment)
                    buttonObject.transform.localPosition = new Vector3(buttonObject.transform.localPosition.x, -buttonObject.transform.localPosition.y, buttonObject.transform.localPosition.z);

                if (ModelButtonLayout.IsRow(buttonIndex) && !checkMode)
                {
                    buttonObject.name = "Model Button";
                    Vector3 rowScale = ModelButtonLayout.LocalScale(false);
                    Vector3 position = ModelButtonLayout.LocalPosition(buttonIndex, false);
                    position.y += rowScale.y * (increment ? -0.42f : 0.42f);
                    buttonObject.transform.localPosition = position;
                    buttonObject.transform.localScale = new Vector3(rowScale.x, rowScale.y * 0.14f, rowScale.z);
                    RoundButton(buttonObject, false);
                }

                if (lastClickedName != method.buttonText + (increment ? "+" : "-"))
                {
                    ColorChanger colorChanger = buttonObject.AddComponent<ColorChanger>();
                    colorChanger.colors = buttonColors[0];
                }
                else
                    CoroutineManager.instance.StartCoroutine(ButtonClick(buttonIndex, buttonObject.GetComponent<Renderer>()));

                FollowMenuSettings(buttonObject);
            }

            RenderIncrementalText(increment, offset, buttonIndex);
        }

        public static void RenderIncrementalText(bool increment, float offset, int buttonIndex = -1)
        {
            TextMeshPro buttonText = new GameObject
            {
                transform =
                {
                    parent = canvasObj.transform
                }
            }.AddComponent<TextMeshPro>();

            buttonText.font = activeFont;
            buttonText.text = increment ? "+" : "-";
            buttonText.richText = true;
            buttonText.fontSize = 1;
            buttonText.AddComponent<UIColorChanger>().colors = textColors[1];

            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.fontStyle = activeFontStyle;
            buttonText.enableAutoSizing = true;
            buttonText.fontSizeMin = 0;

            RectTransform textTransform = buttonText.GetComponent<RectTransform>();
            textTransform.localPosition = Vector3.zero;
            textTransform.sizeDelta = new Vector2(.2f, .03f * (ButtonDistance / 0.1f));
            if (NoAutoSizeText)
                textTransform.sizeDelta = new Vector2(9f, 0.015f);

            if (hideTextOnCamera)
                textTransform.gameObject.layer = 19;

            textTransform.localPosition = thinMenu ? new Vector3(.064f, increment ? -0.12f : 0.12f, .111f - offset / 2.6f) : new Vector3(.064f, increment ? -0.18f : 0.18f, .111f - offset / 2.6f);
            textTransform.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

            FollowMenuSettings(buttonText);
            if (ModelButtonLayout.IsRow(buttonIndex) && !checkMode)
            {
                Vector3 position = ModelButtonLayout.LocalPosition(buttonIndex, false);
                position.x += 0.045f + ModelButtonLayout.PixelSize * 2f * canvasObj.transform.localScale.x;
                position.y += ModelButtonLayout.LocalScale(false).y * (increment ? -0.42f : 0.42f);
                float width = ModelButtonLayout.RowWidth;
                textTransform.localPosition = canvasObj.transform.InverseTransformPoint(menu.transform.TransformPoint(position));
                textTransform.sizeDelta = new Vector2(width * 0.14f, ModelButtonLayout.RowHeight(false));
                buttonText.alignment = TextAlignmentOptions.Midline;
                ModelButtonLayout.StyleText(buttonText, false, false);
            }
        }

        public static void CreateReference(bool? rightHandOverride = null)
        {
            reference = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Sphere);
            reference.transform.parent = rightHandOverride ?? (rightHand || (bothHands && ControllerInputPoller.instance.rightControllerSecondaryButton)) ? GorillaTagger.Instance.leftHandTransform : GorillaTagger.Instance.rightHandTransform;
            reference.transform.localPosition = pointerOffset;
            reference.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            buttonCollider = reference.GetComponent<SphereCollider>();

            reference.AddComponent<MenuClickMarker>();
        }

        private static GameObject CreateMenuModel(out float top)
        {
            top = 0f;

            GameObject model = MenuModel.Create();
            if (model == null)
                return null;

            Transform root = model.transform;
            Transform panel = MenuModel.FindPart(root, "panel_body");
            if (panel == null)
            {
                LogManager.LogError("Menu model has no panel_body, falling back to the classic menu");
                Destroy(model);
                return null;
            }

            const float contentTop = 0.19f;
            const float contentBottom = -0.237f;
            const float thinHalfWidth = 0.15f;
            const float verticalMargin = 0.02f;
            const float sideMargin = 0.006f;
            float contentHalfWidth = thinMenu ? thinHalfWidth : 0.225f;

            Bounds panelBounds = MenuModel.GetBounds(root, panel);
            float scale = Mathf.Max(
                (contentTop - contentBottom + verticalMargin * 2f) / panelBounds.size.z,
                (thinHalfWidth + sideMargin) * 2f / panelBounds.size.y);

            float stretch = Mathf.Max(1f, (contentHalfWidth + sideMargin) * 2f / (panelBounds.size.y * scale));
            if (stretch > 1f)
            {
                foreach (string partName in new[] { "panel_body", "panel_display", "panel_back", "BarDisconnect", "BarHome" })
                {
                    Transform part = MenuModel.FindPart(root, partName);
                    if (part != null)
                        part.localScale = new Vector3(part.localScale.x, part.localScale.y * stretch, part.localScale.z);
                }

                float railShift = panelBounds.extents.y * (stretch - 1f);
                foreach (string partName in new[] { "RailPrev", "RailNext" })
                {
                    Transform part = MenuModel.FindPart(root, partName);
                    if (part != null)
                        part.localPosition += new Vector3(0f, Mathf.Sign(part.localPosition.y) * railShift, 0f);
                }
            }

            MenuModel.FindPart(root, "panel_display")?.gameObject.SetActive(false);
            MenuModel.FindPart(root, "panel_back")?.gameObject.SetActive(!disableWatermark && customWatermark == null);

            bool pageButtons = !disablePageButtons && CurrentPrompt == null && !pageScrolling;
            AddMenuModelButton(root, "RailPrev", pageButtons ? "PreviousPage" : null);
            AddMenuModelButton(root, "RailNext", pageButtons ? "NextPage" : null);
            Transform disconnectBar = AddMenuModelButton(root, "BarDisconnect", disableDisconnectButton ? null : "Disconnect");
            AddMenuModelButton(root, "BarHome", "Menu Home");

            Vector3 menuScale = menu.transform.localScale;
            Vector3 panelCenter = new Vector3(0.042f + panelBounds.extents.x * scale, 0f, (contentTop + contentBottom) / 2f);

            root.SetParent(menu.transform, false);
            root.localRotation = Quaternion.identity;
            root.localScale = new Vector3(scale / menuScale.x, scale / menuScale.y, scale / menuScale.z);
            root.localPosition = new Vector3(
                (panelCenter.x - panelBounds.center.x * scale) / menuScale.x,
                (panelCenter.y - panelBounds.center.y * scale) / menuScale.y,
                (panelCenter.z - panelBounds.center.z * scale) / menuScale.z);

            float modelTop = disconnectBar != null ? MenuModel.GetBounds(root, disconnectBar).max.z : panelBounds.max.z;
            top = panelCenter.z + (modelTop - panelBounds.center.z) * scale;

            return model;
        }

        private static Transform AddMenuModelButton(Transform root, string partName, string buttonName)
        {
            Transform part = MenuModel.FindPart(root, partName);
            if (part == null)
                return null;

            if (buttonName == null)
            {
                part.gameObject.SetActive(false);
                return null;
            }

            if (!UnityInput.GetKey(Key.Q) && !(inTextInput && isKeyboardPc))
                part.gameObject.layer = 2;

            Bounds bounds = MenuModel.GetBounds(part, part);
            BoxCollider collider = part.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = bounds.center;
            collider.size = bounds.size;

            part.gameObject.AddComponent<ButtonCollider>().relatedText = buttonName;

            if (lastClickedName == buttonName)
            {
                lastClickedName = "";
                CoroutineManager.instance.StartCoroutine(PressMenuModelPart(part));
            }

            return part;
        }

        private static IEnumerator PressMenuModelPart(Transform part)
        {
            Vector3 rest = part.localPosition;
            for (float elapsed = 0f; elapsed < 0.1f; elapsed += Time.deltaTime)
            {
                if (part == null)
                    yield break;

                part.localPosition = rest - new Vector3(Mathf.Lerp(0.005f, 0f, elapsed / 0.1f), 0f, 0f);
                yield return null;
            }

            if (part != null)
                part.localPosition = rest;
        }

        public static GameObject CreateMenu()
        {
            lastPanelPress = 0f;
            ModelMenuActive = false;
            ModelHasBackRow = false;
            modelJoystickButtons.Clear();
            menu = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);

            Destroy(menu.GetComponent<BoxCollider>());
            Destroy(menu.GetComponent<Renderer>());

            menu.transform.localScale = new Vector3(0.1f, 0.3f, 0.3825f);

            if (annoyingMode)
            {
                menu.transform.localScale = new Vector3(0.1f, Random.Range(10f, 40f) / 100f, 0.3825f);
                backgroundColor = new ExtGradient { colors = ExtGradient.GetSimpleGradient(RandomColor(), RandomColor()) };

                buttonColors[0] = new ExtGradient { colors = ExtGradient.GetSimpleGradient(RandomColor(), RandomColor()) };
                buttonColors[1] = new ExtGradient { colors = ExtGradient.GetSimpleGradient(RandomColor(), RandomColor()) };

                textColors[0] = new ExtGradient { colors = ExtGradient.GetSimpleGradient(RandomColor(), RandomColor()) };
                textColors[1] = new ExtGradient { colors = ExtGradient.GetSimpleGradient(RandomColor(), RandomColor()) };
                textColors[2] = new ExtGradient { colors = ExtGradient.GetSimpleGradient(RandomColor(), RandomColor()) };
            }

            menuBackground = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
            Destroy(menuBackground.GetComponent<BoxCollider>());

            menuBackground.transform.parent = menu.transform;
            menuBackground.transform.localPosition = new Vector3(0.50f, 0f, 0f);
            menuBackground.transform.rotation = Quaternion.identity;

            menuBackground.transform.localScale = thinMenu ? new Vector3(0.1f, 1f, 1f) : new Vector3(0.1f, 1.5f, 1f);

            float modelTop = 0f;
            GameObject menuModel = modelMenu && !longmenu ? CreateMenuModel(out modelTop) : null;
            ModelMenuActive = menuModel != null;
            if (ModelMenuActive)
                ModelPanelBounds = MenuModel.GetBounds(menu.transform, MenuModel.FindPart(menuModel.transform, "panel_body"));

            if ((innerOutline || themeType == 32) && menuModel == null)
            {
                GameObject innerOutlineSegment = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
                Destroy(innerOutlineSegment.GetComponent<BoxCollider>());
                innerOutlineSegment.transform.parent = menuBackground.transform;
                innerOutlineSegment.transform.rotation = Quaternion.identity;
                innerOutlineSegment.transform.localPosition = new Vector3(0f, -0.4840625f, 0f);
                innerOutlineSegment.transform.localScale = new Vector3(1.025f, 0.0065f, 0.98f);

                ColorChanger colorChanger = innerOutlineSegment.AddComponent<ColorChanger>();
                colorChanger.colors = buttonColors[1];

                innerOutlineSegment = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
                Destroy(innerOutlineSegment.GetComponent<BoxCollider>());
                innerOutlineSegment.transform.parent = menuBackground.transform;
                innerOutlineSegment.transform.rotation = Quaternion.identity;
                innerOutlineSegment.transform.localPosition = new Vector3(0f, 0.4840625f, 0f);
                innerOutlineSegment.transform.localScale = new Vector3(1.025f, 0.0065f, 0.98f);

                colorChanger = innerOutlineSegment.AddComponent<ColorChanger>();
                colorChanger.colors = buttonColors[1];

                innerOutlineSegment = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
                Destroy(innerOutlineSegment.GetComponent<BoxCollider>());
                innerOutlineSegment.transform.parent = menuBackground.transform;
                innerOutlineSegment.transform.rotation = Quaternion.identity;
                innerOutlineSegment.transform.localPosition = new Vector3(0f, 0f, -0.4875f);
                innerOutlineSegment.transform.localScale = new Vector3(1.025f, 0.968125f, 0.005f);

                colorChanger = innerOutlineSegment.AddComponent<ColorChanger>();
                colorChanger.colors = buttonColors[1];

                innerOutlineSegment = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
                Destroy(innerOutlineSegment.GetComponent<BoxCollider>());
                innerOutlineSegment.transform.parent = menuBackground.transform;
                innerOutlineSegment.transform.rotation = Quaternion.identity;
                innerOutlineSegment.transform.localPosition = new Vector3(0f, 0f, 0.4875f);
                innerOutlineSegment.transform.localScale = new Vector3(1.025f, 0.968125f, 0.005f);

                colorChanger = innerOutlineSegment.AddComponent<ColorChanger>();
                colorChanger.colors = buttonColors[1];
            }

            if (menuModel != null)
                menuBackground.GetComponent<Renderer>().enabled = false;
            else if (themeType == 23 || themeType == 24 || themeType == 25)
            {
                Renderer menuBackgroundRenderer = menuBackground.GetComponent<Renderer>();
                switch (themeType)
                {
                    case 23:
                        if (pride == null)
                        {
                            pride = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.pride.png");
                            pride.filterMode = FilterMode.Point;
                            pride.wrapMode = TextureWrapMode.Clamp;
                        }
                        menuBackgroundRenderer.material.shader = Shader.Find("Universal Render Pipeline/Unlit");
                        menuBackgroundRenderer.material.color = Color.white;
                        menuBackgroundRenderer.material.mainTexture = pride;
                        break;
                    case 24:
                        if (trans == null)
                        {
                            trans = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.trans.png");
                            trans.filterMode = FilterMode.Point;
                            trans.wrapMode = TextureWrapMode.Clamp;
                        }
                        menuBackgroundRenderer.material.shader = Shader.Find("Universal Render Pipeline/Unlit");
                        menuBackgroundRenderer.material.color = Color.white;
                        menuBackgroundRenderer.material.mainTexture = trans;
                        break;
                    case 25:
                        if (gay == null)
                        {
                            gay = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.mlm.png");
                            gay.filterMode = FilterMode.Point;
                            gay.wrapMode = TextureWrapMode.Clamp;
                        }
                        menuBackgroundRenderer.material.shader = Shader.Find("Universal Render Pipeline/Unlit");
                        menuBackgroundRenderer.material.color = Color.white;
                        menuBackgroundRenderer.material.mainTexture = gay;
                        break;
                }
            }
            else
            {
                if (doCustomMenuBackground)
                {
                    menuBackground.GetComponent<Renderer>().material.shader = Shader.Find("Universal Render Pipeline/Unlit");
                    menuBackground.GetComponent<Renderer>().material.color = Color.white;
                    menuBackground.GetComponent<Renderer>().material.mainTexture = customMenuBackgroundImage;
                }
                else
                {
                    ColorChanger colorChanger = menuBackground.AddComponent<ColorChanger>();
                    colorChanger.colors = menuBackgroundColor;
                }
            }

            if (menuModel == null)
                FollowMenuSettings(menuBackground, false);

            canvasObj = new GameObject();
            canvasObj.transform.parent = menu.transform;

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            if (hideTextOnCamera)
                canvasObj.layer = 19;

            CanvasScaler canvasScaler = canvasObj.AddComponent<CanvasScaler>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasScaler.dynamicPixelsPerUnit = 2500f;

            canvasObj.AddComponent<GraphicRaycaster>();

            bool wordmarkTitle = menuModel != null && !hidetitle && !doCustomName && !annoyingMode && !animatedTitle && !gradientTitle;

            if (wordmarkTitle)
            {
                Texture2D wordmarkTexture = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.wordmark.png");
                wordmarkTexture.filterMode = FilterMode.Trilinear;
                wordmarkTexture.mipMapBias = -0.35f;
                wordmarkTexture.anisoLevel = 9;
                wordmarkTexture.wrapMode = TextureWrapMode.Clamp;

                Rect[] wordmarkLines = new[] { OpaqueBounds(wordmarkTexture) };

                const float wordmarkWidth = 0.24f;
                const float wordmarkCenter = 0.169f;

                float widestLine = wordmarkLines.Max(line => line.width);
                float wordmarkUnit = wordmarkWidth / widestLine;
                float lineHeight = wordmarkLines[0].height * wordmarkUnit;
                float lineStep = lineHeight * 1.12f;
                float firstLine = wordmarkCenter + lineStep * (wordmarkLines.Length - 1) / 2f;

                for (int line = 0; line < wordmarkLines.Length; line++)
                {
                    Image wordmarkImage = new GameObject
                    {
                        name = "Interstellar Wordmark",
                        transform =
                        {
                            parent = canvasObj.transform
                        }
                    }.AddComponent<Image>();

                    wordmarkImage.sprite = BrandArtwork.CreatePaddedSprite(wordmarkTexture, wordmarkLines[line]);
                    wordmarkImage.type = Image.Type.Simple;
                    wordmarkImage.useSpriteMesh = false;
                    wordmarkImage.raycastTarget = false;
                    wordmarkImage.AddComponent<UIColorChanger>().colors = textColors[0];

                    RectTransform wordmarkTransform = wordmarkImage.GetComponent<RectTransform>();
                    wordmarkTransform.localPosition = new Vector3(0.06f, 0f, firstLine - line * lineStep);
                    wordmarkTransform.sizeDelta = wordmarkImage.sprite.rect.size * wordmarkUnit;
                    wordmarkTransform.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));
                    Vector2 cropShift = (wordmarkImage.sprite.rect.center - wordmarkLines[line].center) * wordmarkUnit;
                    wordmarkTransform.localPosition += wordmarkTransform.localRotation * new Vector3(cropShift.x, cropShift.y, 0f);

                    if (hideTextOnCamera)
                        wordmarkTransform.gameObject.layer = 19;

                    FollowMenuSettings(wordmarkImage);
                }

                TextMeshPro subtitle = new GameObject
                {
                    transform =
                    {
                        parent = canvasObj.transform
                    }
                }.AddComponent<TextMeshPro>();

                MenuTypography.Configure(subtitle);
                subtitle.text = FollowMenuSettings(noPageNumber
                    ? Buttons.CurrentCategoryName.ToUpper()
                    : $"{Buttons.CurrentCategoryName.ToUpper()}  ·  PAGE {(pageScrolling ? pageOffset : pageNumber) + 1} / {LastPage + 1}");

                subtitle.fontSize = 1;
                subtitle.characterSpacing = 7f;
                subtitle.AddComponent<UIColorChanger>().colors = textColors[0];
                subtitle.richText = true;
                subtitle.fontStyle = activeFontStyle;
                subtitle.alignment = TextAlignmentOptions.Center;
                subtitle.enableAutoSizing = true;
                subtitle.fontSizeMin = 0;

                RectTransform subtitleTransform = subtitle.GetComponent<RectTransform>();
                subtitleTransform.localPosition = new Vector3(0.06f, 0f, 0.1305f);
                subtitleTransform.sizeDelta = new Vector2(0.30f, 0.016f);
                subtitleTransform.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

                if (hideTextOnCamera)
                    subtitleTransform.gameObject.layer = 19;

                FollowMenuSettings(subtitle, 7f);
            }

            if (!hidetitle && !wordmarkTitle)
            {
                title = new GameObject
                {
                    transform =
                    {
                        parent = canvasObj.transform
                    }
                }.AddComponent<TextMeshPro>();
                title.font = activeFont;
                title.text = "<b>Interstellar</b>";

                if (doCustomName)
                    title.text = customMenuName;

                title.text = FollowMenuSettings(title.text);

                if (!noPageNumber)
                    title.text += $" <color=grey>[</color><color=white>{(pageScrolling ? pageOffset : pageNumber) + 1}</color><color=grey>]</color>";

                if (gradientTitle)
                    title.text = RichtextGradient(NoRichtextTags(title.text),
                        new[]
                        {
                            new GradientColorKey(BrightenColor(buttonColors[0].GetColor(0)), 0f),
                            new GradientColorKey(BrightenColor(buttonColors[0].GetColor(0), 0.95f), 0.5f),
                            new GradientColorKey(BrightenColor(buttonColors[0].GetColor(0)), 1f)
                        });

                if (animatedTitle)
                {
                    string targetString = doCustomName ? NoRichtextTags(customMenuName) : "Interstellar";
                    int length = (int)Mathf.PingPong(Time.time / 0.25f, targetString.Length);
                    title.text = length > 0 ? targetString[..length] : "";
                }

                title.fontSize = 1;
                title.AddComponent<UIColorChanger>().colors = textColors[0];

                title.richText = true;
                title.fontStyle = activeFontStyle;
                title.alignment = TextAlignmentOptions.Center;
                title.enableAutoSizing = true;
                title.fontSizeMin = 0;
                RectTransform component = title.GetComponent<RectTransform>();
                component.localPosition = Vector3.zero;
                component.sizeDelta = new Vector2(0.28f, 0.05f);
                if (NoAutoSizeText)
                    component.sizeDelta = new Vector2(0.28f, 0.015f);

                if (hideTextOnCamera)
                    component.gameObject.layer = 19;

                component.localPosition = new Vector3(0.06f, 0f, 0.165f);
                component.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

                FollowMenuSettings(title);
            }

            if (!backgroundColor.transparent)
            {
                if (!disableWatermark && (menuModel == null || customWatermark != null))
                {
                    watermarkImage = new GameObject
                    {
                        transform =
                        {
                            parent = canvasObj.transform
                        }
                    }.AddComponent<Image>();

                    if (watermarkMat == null)
                        watermarkMat = new Material(watermarkImage.material);

                    watermarkImage.material = watermarkMat;
                    watermarkImage.material.SetTexture("_MainTex", customWatermark ?? LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.logo.png"));

                    RectTransform imageTransform = watermarkImage.GetComponent<RectTransform>();
                    imageTransform.localPosition = Vector3.zero;
                    imageTransform.sizeDelta = new Vector2(.15f, .15f);

                    imageTransform.localPosition = new Vector3(0.04f, 0f, 0f);

                    FollowMenuSettings(watermarkImage);

                    imageTransform.localRotation = Quaternion.Euler(new Vector3(0f, 90f, 90f));

                    if (customWatermark == null)
                        watermarkImage.AddComponent<UIColorChanger>().colors = textColors[0];
                    else
                        watermarkImage.material.color = Color.white;
                }
            }

            if (!disableFpsCounter)
            {
                TextMeshPro fps = new GameObject
                {
                    transform =
                    {
                        parent = canvasObj.transform
                    }
                }.AddComponent<TextMeshPro>();
                fps.font = activeFont;

                string textToSet = ftCount ? $"FT: {Mathf.Floor(1f / lastDeltaTime * 10000f) / 10f} ms" : $"FPS: {lastDeltaTime}";
                if (hidetitle && !noPageNumber) textToSet += "      ";
                if (hidetitle && !noPageNumber) textToSet += "Page " + (pageNumber + 1);

                fps.text = FollowMenuSettings(textToSet);

                fps.AddComponent<UIColorChanger>().colors = textColors[0];
                fpsCount = fps;
                fps.fontSize = 1;
                fps.richText = true;
                fps.SafeSetFontStyle(activeFontStyle);
                fps.SafeSetAlignment(TextAlignmentOptions.Center);
                fps.SafeSetOverflowMode(TextOverflowModes.Overflow);
                fps.enableAutoSizing = true;
                fps.fontSizeMin = 0;
                RectTransform fpsTransform = fps.GetComponent<RectTransform>();
                fpsTransform.sizeDelta = NoAutoSizeText ? new Vector2(9f, 0.015f) : new Vector2(0.30f, 0.028f);
                fpsTransform.localPosition = new Vector3(0.06f, 0f, hidetitle ? 0.175f : -0.228f);
                fpsTransform.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

                if (ModelMenuActive && !hidetitle)
                {
                    float pixel = ModelButtonLayout.PixelSize;
                    Vector3 footer = new Vector3(ModelPanelBounds.max.x + pixel * 3f / menu.transform.localScale.x,
                        ModelPanelBounds.center.y, ModelPanelBounds.min.z + pixel * 24f / menu.transform.localScale.z);
                    fpsTransform.localPosition = canvasObj.transform.InverseTransformPoint(menu.transform.TransformPoint(footer));
                    fpsTransform.sizeDelta = new Vector2(ModelButtonLayout.RowWidth, pixel * 38f);
                    MenuTypography.Configure(fps);
                    fps.isOrthographic = false;
                    fps.fontSize = 0.10f * (pixel / 0.00039f);
                    fps.enableAutoSizing = false;
                    fps.textWrappingMode = TextWrappingModes.NoWrap;
                    fps.overflowMode = TextOverflowModes.Ellipsis;
                }

                if (hideTextOnCamera)
                    fpsTransform.gameObject.layer = 19;

                FollowMenuSettings(fps, ModelMenuActive ? 0f : (float?)null);
            }

            float hkbStartTime = -0.3f;
            if (menuModel != null)
                hkbStartTime = 0.28f - (modelTop + 0.005f) / menu.transform.localScale.z - ButtonDistance * 0.4f;
            else if (!disableDisconnectButton)
            {
                AddButton(-0.3f, -1, Buttons.GetIndex("Disconnect"));
                hkbStartTime -= ButtonDistance;
            }

            if (quickActions.Count > 0)
            {
                foreach (string action in quickActions.ToList())
                {
                    ButtonInfo button = Buttons.GetIndex(action);
                    if (button == null)
                    {
                        quickActions.Remove(action);
                        continue;
                    }

                    AddButton(hkbStartTime, -1, button);
                    hkbStartTime -= ButtonDistance;
                }
            }

            if (!disableSearchButton)
                AddSearchButton();

            if (enableDebugButton)
                AddDebugButton();

            if (!disablePageButtons && CurrentPrompt == null && !pageScrolling && (menuModel == null || pageButtonType != 1))
                AddPageButtons();

            if (inTextInput)
            {
                GameObject searchBoxObject = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
                if (!UnityInput.GetKey(Key.Q) && !isKeyboardPc)
                    searchBoxObject.layer = 2;

                searchBoxObject.GetComponent<BoxCollider>().isTrigger = true;
                searchBoxObject.transform.parent = menu.transform;
                searchBoxObject.transform.rotation = Quaternion.identity;

                searchBoxObject.transform.localScale = thinMenu ? new Vector3(0.09f, 0.9f, ButtonDistance * 0.8f) : new Vector3(0.09f, 1.3f, ButtonDistance * 0.8f);

                searchBoxObject.transform.localPosition = new Vector3(0.56f, 0f, 0.28f - buttonOffset * ButtonDistance);

                ColorChanger colorChanger = searchBoxObject.AddComponent<ColorChanger>();
                colorChanger.colors = buttonColors[0];

                FollowMenuSettings(searchBoxObject);

                keyboardInputObject = new GameObject
                {
                    transform =
                        {
                            parent = canvasObj.transform
                        }
                }.AddComponent<TextMeshPro>();

                keyboardInputObject.font = activeFont;
                keyboardInputObject.text = FollowMenuSettings(keyboardInput) + ((Time.time % 1f) > 0.5f ? "|" : "");

                keyboardInputObject.richText = false;
                keyboardInputObject.fontSize = 1;

                if (joystickMenu && joystickButtonSelected == 0 && themeType == 29)
                    keyboardInputObject.color = Color.red;
                else
                    keyboardInputObject.AddComponent<UIColorChanger>().colors = textColors[1];

                keyboardInputObject.alignment = TextAlignmentOptions.Center;
                keyboardInputObject.fontStyle = activeFontStyle;
                keyboardInputObject.enableAutoSizing = true;
                keyboardInputObject.fontSizeMin = 0;

                RectTransform textTransform = keyboardInputObject.GetComponent<RectTransform>();
                textTransform.localPosition = Vector3.zero;
                textTransform.sizeDelta = new Vector2(.2f, .03f * (ButtonDistance / 0.1f));
                if (NoAutoSizeText)
                    textTransform.sizeDelta = new Vector2(9f, 0.015f);

                if (hideTextOnCamera)
                    textTransform.gameObject.layer = 19;

                textTransform.localPosition = new Vector3(.064f, 0, .111f - buttonOffset * ButtonDistance / 2.6f);
                textTransform.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

                FollowMenuSettings(keyboardInputObject);
            }

            if (CurrentPrompt != null)
                RenderPrompt();
            else
            {
                int buttonIndexOffset = 0;
                ButtonInfo[] renderButtons;

                try
                {
                    if (isSearching)
                    {
                        buttonIndexOffset += 1;
                        renderButtons = GetSearchResults().ToArray();
                    }
                    else if (annoyingMode && Random.Range(1, 5) == 3)
                    {
                        ButtonInfo disconnectButton = Buttons.GetIndex("Disconnect");
                        renderButtons = Enumerable.Repeat(disconnectButton, 15).ToArray();
                    }
                    else switch (Buttons.CurrentCategoryName)
                        {
                            case "Main":
                                {
                                    List<ButtonInfo> buttons = new List<ButtonInfo>();
                                    foreach (var button in Buttons.buttons[Buttons.CurrentCategoryIndex])
                                    {
                                        if (!skipButtons.Contains(button.buttonText))
                                            buttons.Add(button);
                                    }
                                    renderButtons = buttons.ToArray();
                                    break;
                                }
                            case "Favorite Mods":
                                {
                                    foreach (var favoriteMod in favorites.Where(favoriteMod => Buttons.GetIndex(favoriteMod) == null).ToList())
                                        favorites.Remove(favoriteMod);

                                    renderButtons = StringsToInfos(favorites.ToArray());
                                    break;
                                }
                            case "Enabled Mods":
                                {
                                    List<ButtonInfo> enabledMods = new List<ButtonInfo>();
                                    int categoryIndex = 0;
                                    foreach (ButtonInfo[] buttonList in Buttons.buttons)
                                    {
                                        enabledMods.AddRange(buttonList.Where(v => v.enabled && (!hideSettings || !Buttons.categoryNames[categoryIndex].Contains("Settings"))));
                                        categoryIndex++;
                                    }
                                    enabledMods = enabledMods.OrderBy(v => v.buttonText).ToList();
                                    enabledMods.Insert(0, Buttons.GetIndex("Exit Enabled Mods"));

                                    renderButtons = enabledMods.ToArray();
                                    break;
                                }
                            default:
                                renderButtons = Buttons.buttons[Buttons.CurrentCategoryIndex];
                                break;
                        }

                    if (renderButtons.Length == 0 || (renderButtons.Length == 1 && renderButtons.FirstOrDefault().toolTip.Contains("Returns you back to the")))
                    {
                        renderButtons = renderButtons.AddToArray(
                            new ButtonInfo
                            {
                                buttonText = isSearching ? "No mods match your search." : "This category seems to be empty.",
                                label = true
                            }
                        );
                    }

                    if (ModelMenuActive && !checkMode && !isSearching)
                    {
                        ButtonInfo backButton = renderButtons.FirstOrDefault(ModelButtonLayout.IsBack);
                        if (backButton != null)
                        {
                            ModelHasBackRow = true;
                            AddButton(0f, -2, backButton);
                            renderButtons = renderButtons.Where(button => button != backButton).ToArray();
                        }
                    }

                    if (!longmenu)
                        renderButtons = renderButtons
                            .Skip(pageNumber * (PageSize - buttonIndexOffset) + pageOffset)
                            .Take(PageSize - buttonIndexOffset)
                            .ToArray();

                    for (int i = 0; i < renderButtons.Length; i++)
                        AddButton((i + buttonIndexOffset + buttonOffset) * ButtonDistance, i, renderButtons[i]);
                }
                catch (Exception ex)
                {
                    LogManager.Log($"Menu draw is erroring, returning to home page\nException: {ex}");
                    Buttons.CurrentCategoryName = "Main";
                }
            }

            if (ModelMenuActive && !checkMode && CurrentPrompt == null && joystickMenu)
            {
                modelJoystickButtons.Sort((left, right) => left.Key.CompareTo(right.Key));
                if (modelJoystickButtons.Count == 0)
                    joystickSelectedButton = null;
                else
                {
                    int selected = modelJoystickButtons.FindIndex(button => button.Key == joystickButtonSelected);
                    KeyValuePair<int, string> button = modelJoystickButtons[Mathf.Max(0, selected)];
                    joystickButtonSelected = button.Key;
                    joystickSelectedButton = button.Value;
                }
            }

            RecenterMenu();

            if (themeType == 48)
            {
                for (int i = 0; i < 15; i++)
                {
                    GameObject particle = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
                    particle.transform.position = menuBackground.transform.position;
                    particle.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

                    Destroy(particle.GetComponent<BoxCollider>());
                    Destroy(particle, 2f);

                    particle.GetComponent<Renderer>().material.shader = Shader.Find("Universal Render Pipeline/Unlit");
                    particle.GetComponent<Renderer>().material.color = Color.white;

                    if (cannabisMat == null)
                    {
                        cannabisMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                        {
                            color = Color.white
                        };

                        if (cann == null)
                            cann = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.cannabis.png");

                        cannabisMat.mainTexture = cann;

                        cannabisMat.SetFloat("_Surface", 1);
                        cannabisMat.SetFloat("_Blend", 0);
                        cannabisMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        cannabisMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                        cannabisMat.SetFloat("_ZWrite", 0);
                        cannabisMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        cannabisMat.renderQueue = (int)RenderQueue.Transparent;
                    }
                    particle.GetComponent<Renderer>().material = cannabisMat;

                    Rigidbody comp = particle.AddComponent(typeof(Rigidbody)) as Rigidbody;
                    comp.position = menuBackground.transform.position;
                    comp.linearVelocity = new Vector3(Random.Range(-3f, 3f), Random.Range(3f, 5f), Random.Range(-3f, 3f));
                    comp.angularVelocity = new Vector3(Random.Range(-3f, 3f), Random.Range(-3f, 3f), Random.Range(-3f, 3f));
                }
            }

            MenuVisuals.ApplyOverlay(menu);
            menu.transform.localScale *= scaleWithPlayer && XRSettings.isDeviceActive ? GTPlayer.Instance.scale * menuScale : menuScale;
            return menu;
        }
        public static void RecenterMenu()
        {
            bool isKeyboardCondition = UnityInput.GetKey(Key.Q) || (inTextInput && isKeyboardPc);
            if (joystickMenu)
            {
                menu.transform.position = GorillaTagger.Instance.headCollider.transform.TransformPoint(joystickMenuPositions[joystickMenuPosition]);
                menu.transform.LookAt(GorillaTagger.Instance.headCollider.transform);
                Vector3 rotModify = menu.transform.rotation.eulerAngles;
                rotModify += new Vector3(-90f, 0f, -90f);
                menu.transform.rotation = Quaternion.Euler(rotModify);
            }
            else
            {
                if (!wristMenu)
                {
                    if (oneHand)
                    {
                        menu.transform.position = GorillaTagger.Instance.headCollider.transform.TransformPoint(new Vector3(0f, -0.1f, 0.5f));
                        menu.transform.LookAt(GorillaTagger.Instance.headCollider.transform);
                        Vector3 rotModify = menu.transform.rotation.eulerAngles;
                        rotModify += new Vector3(-90f, 0f, -90f);
                        menu.transform.rotation = Quaternion.Euler(rotModify);
                    }
                    else if (chestMenu && chestMenuOpen)
                    {
                        if (chestMenuGrabbed != null)
                        {
                            if (chestMenuGrabbed.Value)
                            {
                                menu.transform.position = GorillaTagger.Instance.leftHandTransform.position;
                                menu.transform.rotation = GorillaTagger.Instance.leftHandTransform.rotation;
                            }
                            else
                            {
                                menu.transform.position = GorillaTagger.Instance.rightHandTransform.position;
                                Vector3 rotation = GorillaTagger.Instance.rightHandTransform.rotation.eulerAngles;
                                rotation += new Vector3(0f, 0f, 180f);
                                menu.transform.rotation = Quaternion.Euler(rotation);
                            }
                        }
                        else
                        {
                            menu.transform.position = GorillaTagger.Instance.bodyCollider.transform.TransformPoint(new Vector3(0f, 0f, 0.5f));
                            menu.transform.position = new Vector3(menu.transform.position.x, GorillaTagger.Instance.headCollider.transform.position.y, menu.transform.position.z);
                            menu.transform.LookAt(GorillaTagger.Instance.bodyCollider.transform);
                            menu.transform.rotation = Quaternion.Euler(0f, menu.transform.eulerAngles.y, 0f);
                            Vector3 rotModify = menu.transform.rotation.eulerAngles;
                            rotModify += new Vector3(-90f, 0f, -90f);
                            menu.transform.rotation = Quaternion.Euler(rotModify);
                        }
                    }
                    else
                    {
                        if (rightHand || (bothHands && ControllerInputPoller.instance.rightControllerSecondaryButton))
                        {
                            menu.transform.position = GorillaTagger.Instance.rightHandTransform.position;
                            Vector3 rotation = GorillaTagger.Instance.rightHandTransform.rotation.eulerAngles;
                            rotation += new Vector3(0f, 0f, 180f);
                            menu.transform.rotation = Quaternion.Euler(rotation);
                        }
                        else
                        {
                            menu.transform.position = GorillaTagger.Instance.leftHandTransform.position;
                            menu.transform.rotation = GorillaTagger.Instance.leftHandTransform.rotation;
                        }
                        if (flipMenu)
                        {
                            Vector3 rotation = menu.transform.rotation.eulerAngles;
                            rotation += new Vector3(0f, 0f, 180f);
                            menu.transform.rotation = Quaternion.Euler(rotation);
                        }
                    }
                }
                else
                {
                    menu.transform.localPosition = Vector3.zero;
                    menu.transform.localRotation = Quaternion.identity;
                    menu.transform.position = rightHand
                        ? GorillaTagger.Instance.rightHandTransform.position + new Vector3(0f, 0.3f, 0f)
                        : GorillaTagger.Instance.leftHandTransform.position + new Vector3(0f, 0.3f, 0f);

                    menu.transform.LookAt(GorillaTagger.Instance.headCollider.transform.position);
                    Vector3 rotModify = menu.transform.rotation.eulerAngles;
                    rotModify += new Vector3(-90f, 0f, -90f);
                    menu.transform.rotation = Quaternion.Euler(rotModify);
                }
            }
            if (inTextInput && !isKeyboardPc)
            {
                menu.transform.position = menuSpawnPosition.transform.position;
                menu.transform.rotation = menuSpawnPosition.transform.rotation;
                Vector3 rotModify = menu.transform.rotation.eulerAngles;
                rotModify += new Vector3(-90f, 90f, -90f);
                menu.transform.rotation = Quaternion.Euler(rotModify);
            }
            if (isKeyboardCondition)
            {
                GetObject("Shoulder Camera")?.transform.Find("CM vcam1").gameObject.SetActive(false);
                if (TPC != null)
                {
                    isOnPC = true;

                    if (!XRSettings.isDeviceActive)
                        PrivateUIRoom.instance.ToggleLevelVisibility(true);

                    Vector3[] pcPositions = {
                        TPC?.transform.position ?? GorillaTagger.Instance.headCollider.transform.position,
                        new Vector3(10f, 10f, 10f),
                        new Vector3(10f, 10f, 10f),
                        new Vector3(-67.9299f, 11.9144f, -84.2019f),
                        new Vector3(-63f, 3.634f, -65f),
                        VRRig.LocalRig.transform.position + VRRig.LocalRig.transform.forward * 1.2f
                    };

                    TPC.transform.position = pcPositions[pcbg];
                    if (pcbg != 4 && pcbg != 0)
                        TPC.transform.rotation = Quaternion.identity;

                    if (pcbg == 5)
                    {
                        if (pcBackground == null)
                        {
                            pcBackground = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
                            pcBackground.transform.localScale = new Vector3(10f, 10f, 0.01f);
                            pcBackground.transform.transform.position = TPC.transform.position + TPC.transform.forward;

                            OnMenuClosed += () => Destroy(pcBackground);
                        }

                        Color realcolor = menuBackgroundColor.GetCurrentColor();
                        pcBackground.GetComponent<Renderer>().material.color = new Color32((byte)(realcolor.r * 50), (byte)(realcolor.g * 50), (byte)(realcolor.b * 50), 255);
                    }

                    menu.transform.parent = TPC.transform;
                    menu.transform.position = TPC.transform.position + TPC.transform.forward * (modelMenu && !longmenu ? 0.78f : 0.5f);
                    menu.transform.rotation = TPC.transform.rotation * Quaternion.Euler(-90f, 90f, 0f);

                    if (reference != null)
                    {
                        if (Mouse.current != null && Mouse.current.leftButton.isPressed && !isMouseDown)
                        {
                            ButtonCollider collide = FindPointedButton();
                            if (collide != null)
                            {
                                collide.OnTriggerEnter(buttonCollider);
                            }
                        }
                        else
                            reference.transform.position = new Vector3(999f, -999f, -999f);

                        isMouseDown = Mouse.current?.leftButton.isPressed == true;
                    }
                }
            }
            else
                isOnPC = false;

            if (physicalMenu)
            {
                if (physicalOpenPosition == Vector3.zero)
                {
                    physicalOpenPosition = menu.transform.position;
                    physicalOpenRotation = menu.transform.rotation;
                }

                menu.transform.position = physicalOpenPosition;
                menu.transform.rotation = physicalOpenRotation;
            }

            if (smoothMenuPosition)
            {
                smoothTargetPosition = smoothTargetPosition == Vector3.zero ? menu.transform.position : Vector3.Lerp(smoothTargetPosition, menu.transform.position, Time.deltaTime * 10f);

                menu.transform.position = smoothTargetPosition;
            }

            if (!smoothMenuRotation) return;
            smoothTargetRotation = smoothTargetRotation == Quaternion.identity ? menu.transform.rotation : Quaternion.Lerp(smoothTargetRotation, menu.transform.rotation, Time.deltaTime * 10f);

            menu.transform.rotation = smoothTargetRotation;
        }

        public static event Action OnMenuOpened;
        public static void OpenMenu()
        {
            if (menu != null)
                return;
            isMouseDown = Mouse.current?.leftButton.isPressed == true;
            try
            {
                OnMenuOpened?.Invoke();
            }
            catch { }

            if (dynamicSounds)
                SoundManager.Play(SoundManager.DefaultSounds["Open"], global: true);

            CreateMenu();

            MenuFade.Play(menu, true);

            if (particleSpawnEffect)
            {
                for (int i = 0; i < 25; i++)
                {
                    GameObject Particle = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Sphere);
                    Particle.transform.position = menu.transform.position;
                    Particle.transform.localScale = Vector3.one * (0.025f * (scaleWithPlayer ? GTPlayer.Instance.scale : 1f));
                    Particle.AddComponent<CustomParticle>();
                    Destroy(Particle.GetComponent<Collider>());
                }
            }

            if (joystickMenu) return;
            if (reference == null)
                CreateReference();
        }

        private static readonly RaycastHit[] pointerHits = new RaycastHit[64];
        private static ButtonCollider FindPointedButton()
        {
            if (Mouse.current == null || TPC == null || menu == null || Screen.width <= 0 || Screen.height <= 0)
                return null;
            Vector2 mouse = Mouse.current.position.ReadValue();
            Ray ray = TPC.ViewportPointToRay(new Vector3(mouse.x / Screen.width, mouse.y / Screen.height, 0f));

            ButtonCollider nearest = NearestButton(Physics.RaycastNonAlloc(ray, pointerHits, 16f, ~0, QueryTriggerInteraction.Collide));
            if (nearest != null)
                return nearest;

            return NearestButton(Physics.SphereCastNonAlloc(ray, 0.004f, pointerHits, 16f, ~0, QueryTriggerInteraction.Collide));
        }

        private static ButtonCollider NearestButton(int hitCount)
        {
            ButtonCollider nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (pointerHits[i].distance >= nearestDistance || pointerHits[i].transform == null)
                    continue;

                ButtonCollider button = pointerHits[i].transform.GetComponent<ButtonCollider>();
                if (button == null || !button.isActiveAndEnabled || menu == null || !button.transform.IsChildOf(menu.transform))
                    continue;

                nearest = button;
                nearestDistance = pointerHits[i].distance;
            }

            return nearest;
        }

        public static event Action OnMenuClosed;
        public static void CloseMenu()
        {
            if (menu == null)
                return;
            try
            {
                OnMenuClosed?.Invoke();
            }
            catch { }

            GetObject("Shoulder Camera")?.transform.Find("CM vcam1").gameObject.SetActive(true);
            if (dynamicSounds)
                SoundManager.Play(SoundManager.DefaultSounds["Close"], global: true);

            try
            {
                if ((isOnPC || keyboardWithToggleButton || isKeyboardPc) && TPC != null && TPC.transform.parent.gameObject.name.Contains("CameraTablet"))
                {
                    isOnPC = false;
                    TPC.transform.position = TPC.transform.parent.position;
                    TPC.transform.rotation = TPC.transform.parent.rotation;
                }
            }
            catch { }

            smoothTargetPosition = Vector3.zero;
            smoothTargetRotation = Quaternion.identity;
            isOnPC = false;
            isMouseDown = Mouse.current?.leftButton.isPressed == true;

            MenuFade.Play(menu, false);
            menu = null;

            Destroy(reference);
            reference = null;
        }

        private static void AddPageButtons()
        {
            ExtGradient Gradient = buttonColors[swapButtonColors ? 1 : 0];

            switch (pageButtonType)
            {
                case 1:
                    CreatePageButtonPair(
                        "PreviousPage", "NextPage",
                        new Vector3(0.09f, 0.2f, 0.9f),
                        new Vector3(0.56f, thinMenu ? 0.65f : 0.9f, 0f),
                        new Vector3(0.56f, thinMenu ? -0.65f : -0.9f, 0f),
                        new Vector3(0.064f, thinMenu ? 0.195f : 0.267f, 0f),
                        new Vector3(0.064f, thinMenu ? -0.195f : -0.267f, 0f),
                        Gradient
                    );
                    break;

                case 2:
                    CreatePageButtonPair(
                        "PreviousPage", "NextPage",
                        new Vector3(0.09f, thinMenu ? 0.9f : 1.3f, ButtonDistance * 0.8f),
                        new Vector3(0.56f, 0f, 0.28f - ButtonDistance * (buttonOffset - 2)),
                        new Vector3(0.56f, 0f, 0.28f - ButtonDistance * (buttonOffset - 1)),
                        new Vector3(0.064f, 0f, 0.109f - ButtonDistance * (buttonOffset - 2) / 2.55f),
                        new Vector3(0.064f, 0f, 0.109f - ButtonDistance * (buttonOffset - 1) / 2.55f),
                        Gradient
                    );
                    break;

                case 5:
                    CreatePageButtonPair(
                        "PreviousPage", "NextPage",
                        new Vector3(0.09f, hidetitle ? 0.1f : 0.3f, 0.05f),
                        new Vector3(0.56f, (thinMenu ? 0.299f : 0.499f) + (hidetitle ? 0.1f : 0f), 0.355f + (hidetitle ? 0.1f : 0f)),
                        new Vector3(0.56f, (thinMenu ? -0.299f : -0.499f) - (hidetitle ? 0.1f : 0f), 0.355f + (hidetitle ? 0.1f : 0f)),
                        new Vector3(0.064f, (thinMenu ? 0.09f : 0.15f) + (hidetitle ? 0.035f : 0f), 0.135f + (hidetitle ? 0.0375f : 0f)),
                        new Vector3(0.064f, (thinMenu ? -0.09f : -0.15f) - (hidetitle ? 0.035f : 0f), 0.135f + (hidetitle ? 0.0375f : 0f)),
                        Gradient
                    );
                    break;

                case 6:
                    CreatePageButtonPair(
                        "PreviousPage", "NextPage",
                        new Vector3(0.09f, 0.102f, 0.08f),
                        new Vector3(0.56f, thinMenu ? 0.450f : 0.7f, -0.58f),
                        new Vector3(0.56f, thinMenu ? 0.450f : 0.7f, -0.58f) - new Vector3(0f, 0.16f, 0f),
                        new Vector3(0.064f, thinMenu ? 0.35f / 2.6f : 0.54444444444f / 2.6f, -0.58f / 2.7f),
                        new Vector3(0.064f, thinMenu ? 0.35f / 2.6f : 0.54444444444f / 2.6f, -0.58f / 2.7f) - new Vector3(0f, 0.0475f, 0f),
                        Gradient,
                        new Vector2(0.03f, 0.03f)
                    );
                    break;
            }
        }

        private static void RenderPrompt()
        {
            TextMeshPro promptText = new GameObject
            {
                transform =
                {
                    parent = canvasObj.transform
                }
            }.AddComponent<TextMeshPro>();
            promptText.font = activeFont;
            promptText.text = CurrentPrompt.Message;

            promptText.text = FollowMenuSettings(promptText.text);

            promptText.fontSize = 1;
            promptText.lineSpacing = 0.8f;
            promptText.AddComponent<UIColorChanger>().colors = textColors[0];

            promptText.richText = true;
            promptText.fontStyle = activeFontStyle;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.enableAutoSizing = true;
            promptText.fontSizeMin = 0;
            RectTransform component = promptText.GetComponent<RectTransform>();
            component.sizeDelta = new Vector2(0.28f, CurrentPrompt.IsText ? 0.25f : 0.28f);

            component.localPosition = new Vector3(0.06f, 0f, CurrentPrompt.IsText ? -0.025f : 0f);
            component.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

            FollowMenuSettings(promptText);

            joystickButtonSelected %= 2;

            {
                GameObject button = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);

                if (!UnityInput.GetKey(Key.Q) && !(inTextInput && isKeyboardPc))
                    button.layer = 2;

                button.GetComponent<BoxCollider>().isTrigger = true;
                button.transform.parent = menu.transform;
                button.transform.rotation = Quaternion.identity;
                button.transform.localScale = new Vector3(0.09f, CurrentPrompt.DeclineText == null ? 0.9f : 0.4375f, 0.08f);
                button.transform.localPosition = new Vector3(0.56f, CurrentPrompt.DeclineText == null ? 0f : 0.2375f, -0.43f);

                button.AddComponent<ButtonCollider>().relatedText = "Accept Prompt";

                if (lastClickedName != "Accept Prompt")
                {
                    ColorChanger colorChanger = button.AddComponent<ColorChanger>();
                    colorChanger.colors = buttonColors[0];

                    if (joystickMenu && joystickButtonSelected == 0)
                    {
                        joystickSelectedButton = "Accept Prompt";

                        ExtGradient gradient = colorChanger.colors.Clone();
                        gradient.SetColor(0, Color.red);

                        colorChanger.colors = gradient;
                    }
                }
                else
                    CoroutineManager.instance.StartCoroutine(ButtonClick(0, button.GetComponent<Renderer>()));

                TextMeshPro text = new GameObject { transform = { parent = canvasObj.transform } }.AddComponent<TextMeshPro>();
                text.font = activeFont;
                text.fontStyle = activeFontStyle;
                text.text = FollowMenuSettings(CurrentPrompt.AcceptText);
                text.fontSize = 1;
                text.alignment = TextAlignmentOptions.Center;
                text.enableAutoSizing = true;
                text.fontSizeMin = 0;

                text.AddComponent<UIColorChanger>().colors = textColors[1];

                RectTransform textRect = text.GetComponent<RectTransform>();
                textRect.sizeDelta = new Vector2(0.2f, 0.03f);

                if (arrowType == 11)
                    textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, textRect.sizeDelta.y * 6f);

                if (NoAutoSizeText)
                    textRect.sizeDelta = new Vector2(9f, 0.015f);

                if (hideTextOnCamera)
                    textRect.gameObject.layer = 19;

                textRect.localPosition = new Vector3(0.064f, CurrentPrompt.DeclineText != null ? 0.075f : 0f, -0.16f);
                textRect.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

                FollowMenuSettings(text);
                FollowMenuSettings(button);
            }

            if (CurrentPrompt.DeclineText == null) return;
            {
                GameObject button = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);

                if (!UnityInput.GetKey(Key.Q) && !(inTextInput && isKeyboardPc))
                    button.layer = 2;

                button.GetComponent<BoxCollider>().isTrigger = true;
                button.transform.parent = menu.transform;
                button.transform.rotation = Quaternion.identity;
                button.transform.localScale = new Vector3(0.09f, 0.4375f, 0.08f);
                button.transform.localPosition = new Vector3(0.56f, -0.2375f, -0.43f);

                button.AddComponent<ButtonCollider>().relatedText = "Decline Prompt";

                if (lastClickedName != "Decline Prompt")
                {
                    ColorChanger colorChanger = button.AddComponent<ColorChanger>();
                    colorChanger.colors = buttonColors[0];

                    if (joystickMenu && joystickButtonSelected == 1)
                    {
                        joystickSelectedButton = "Decline Prompt";

                        ExtGradient gradient = colorChanger.colors.Clone();
                        gradient.SetColor(0, Color.red);

                        colorChanger.colors = gradient;
                    }
                }
                else
                    CoroutineManager.instance.StartCoroutine(ButtonClick(1, button.GetComponent<Renderer>()));

                TextMeshPro text = new GameObject { transform = { parent = canvasObj.transform } }.AddComponent<TextMeshPro>();
                text.font = activeFont;
                text.fontStyle = activeFontStyle;
                text.text = FollowMenuSettings(CurrentPrompt.DeclineText);
                text.fontSize = 1;
                text.alignment = TextAlignmentOptions.Center;
                text.enableAutoSizing = true;
                text.fontSizeMin = 0;

                text.AddComponent<UIColorChanger>().colors = textColors[1];

                RectTransform textRect = text.GetComponent<RectTransform>();
                textRect.sizeDelta = new Vector2(0.2f, 0.03f);

                if (arrowType == 11)
                    textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, textRect.sizeDelta.y * 6f);

                if (NoAutoSizeText)
                    textRect.sizeDelta = new Vector2(9f, 0.015f);

                if (hideTextOnCamera)
                    textRect.gameObject.layer = 19;

                textRect.localPosition = new Vector3(0.064f, -0.075f, -0.16f);
                textRect.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

                FollowMenuSettings(text);
                FollowMenuSettings(button);
            }
        }

        private static void CreatePageButtonPair(string prevButtonName, string nextButtonName, Vector3 buttonScale, Vector3 prevButtonPos, Vector3 nextButtonPos, Vector3 prevTextPos, Vector3 nextTextPos, ExtGradient color, Vector2? textSize = null)
        {
            AdvancedAddButton(prevButtonName, buttonScale, prevButtonPos, prevTextPos, color, textSize, 0);
            AdvancedAddButton(nextButtonName, buttonScale, nextButtonPos, nextTextPos, color, textSize, 1);
        }

        private static GameObject AdvancedAddButton(string buttonName, Vector3 scale, Vector3 position, Vector3 textPosition, ExtGradient color, Vector2? textSize, int arrowIndex)
        {
            GameObject button = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);

            if (!UnityInput.GetKey(Key.Q) && !(inTextInput && isKeyboardPc))
                button.layer = 2;

            button.GetComponent<BoxCollider>().isTrigger = true;
            button.transform.parent = menu.transform;
            button.transform.rotation = Quaternion.identity;
            button.transform.localScale = scale;
            button.transform.localPosition = position;

            button.AddComponent<ButtonCollider>().relatedText = buttonName;

            if (lastClickedName != buttonName)
            {
                ColorChanger colorChanger = button.AddComponent<ColorChanger>();
                colorChanger.colors = color;
            }
            else
                CoroutineManager.instance.StartCoroutine(ButtonClick(-99, button.GetComponent<Renderer>()));

            TextMeshPro text = new GameObject { transform = { parent = canvasObj.transform } }.AddComponent<TextMeshPro>();
            text.font = activeFont;
            text.text = arrowTypes[arrowType][arrowIndex];
            text.fontSize = 1;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0;
            text.spriteAsset = ButtonSpriteSheet;

            text.AddComponent<UIColorChanger>().colors = textColors[1];

            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.sizeDelta = textSize ?? new Vector2(0.2f, 0.03f);

            if (arrowType == 11)
                textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, textRect.sizeDelta.y * 6f);

            if (NoAutoSizeText)
                textRect.sizeDelta = new Vector2(9f, 0.015f);

            if (hideTextOnCamera)
                textRect.gameObject.layer = 19;

            textRect.localPosition = textPosition;
            textRect.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

            FollowMenuSettings(text);
            FollowMenuSettings(button, !swapButtonColors);

            return button;
        }

        public static void OutlineMenuObject(GameObject toOut, bool shouldBeEnabled)
        {
            if (toOut.transform.parent != menu?.transform)
            {
                OutlineObject(toOut, shouldBeEnabled);
                return;
            }

            GameObject gameObject = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);

            Destroy(gameObject.GetComponent<BoxCollider>());
            gameObject.transform.parent = menu.transform;
            gameObject.transform.rotation = Quaternion.identity;
            gameObject.transform.localPosition = toOut.transform.localPosition;
            gameObject.transform.localScale = toOut.transform.localScale + new Vector3(-0.01f, 0.01f, 0.0075f);

            ColorChanger colorChanger = gameObject.AddComponent<ColorChanger>();
            colorChanger.colors = buttonColors[shouldBeEnabled ? 1 : 0];

            if (shouldRound)
                RoundMenuObject(gameObject, 0.024f);
        }

        public static void OutlineObject(GameObject toOut, bool shouldBeEnabled)
        {
            GameObject gameObject = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);

            Destroy(gameObject.GetComponent<BoxCollider>());
            gameObject.transform.parent = toOut.transform.parent;
            gameObject.transform.parent = toOut.transform.parent;
            gameObject.transform.rotation = toOut.transform.rotation;
            gameObject.transform.localPosition = toOut.transform.localPosition;
            gameObject.transform.localScale = toOut.transform.localScale + new Vector3(0.005f, 0.005f, -0.001f);

            ColorChanger colorChanger = gameObject.AddComponent<ColorChanger>();
            colorChanger.colors = buttonColors[shouldBeEnabled ? 1 : 0];
        }

        public static void OutlineCanvasObject(MaskableGraphic canvasObject)
        {
            Outline outline = canvasObject.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(0.001f, 0.001f);
            outline.useGraphicAlpha = true;
        }

        public static void RoundMenuObject(GameObject toRound, float Bevel = 0.02f)
        {
            if (toRound.transform.parent != menu?.transform)
            {
                RoundObject(toRound, Bevel);
                return;
            }

            Renderer ToRoundRenderer = toRound.GetComponent<Renderer>();
            GameObject BaseA = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
            BaseA.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            Destroy(BaseA.GetComponent<Collider>());

            BaseA.transform.parent = menu.transform;
            BaseA.transform.rotation = Quaternion.identity;
            BaseA.transform.localPosition = toRound.transform.localPosition;
            BaseA.transform.localScale = toRound.transform.localScale + new Vector3(0f, Bevel * -2.55f, 0f);

            GameObject BaseB = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
            BaseB.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            Destroy(BaseB.GetComponent<Collider>());

            BaseB.transform.parent = menu.transform;
            BaseB.transform.rotation = Quaternion.identity;
            BaseB.transform.localPosition = toRound.transform.localPosition;
            BaseB.transform.localScale = toRound.transform.localScale + new Vector3(0f, 0f, -Bevel * 2f);

            GameObject RoundCornerA = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cylinder);
            RoundCornerA.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            Destroy(RoundCornerA.GetComponent<Collider>());

            RoundCornerA.transform.parent = menu.transform;
            RoundCornerA.transform.rotation = Quaternion.identity * Quaternion.Euler(0f, 0f, 90f);

            RoundCornerA.transform.localPosition = toRound.transform.localPosition + new Vector3(0f, toRound.transform.localScale.y / 2f - Bevel * 1.275f, toRound.transform.localScale.z / 2f - Bevel);
            RoundCornerA.transform.localScale = new Vector3(Bevel * 2.55f, toRound.transform.localScale.x / 2f, Bevel * 2f);

            GameObject RoundCornerB = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cylinder);
            RoundCornerB.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            Destroy(RoundCornerB.GetComponent<Collider>());

            RoundCornerB.transform.parent = menu.transform;
            RoundCornerB.transform.rotation = Quaternion.identity * Quaternion.Euler(0f, 0f, 90f);

            RoundCornerB.transform.localPosition = toRound.transform.localPosition + new Vector3(0f, -(toRound.transform.localScale.y / 2f) + Bevel * 1.275f, toRound.transform.localScale.z / 2f - Bevel);
            RoundCornerB.transform.localScale = new Vector3(Bevel * 2.55f, toRound.transform.localScale.x / 2f, Bevel * 2f);

            GameObject RoundCornerC = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cylinder);
            RoundCornerC.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            Destroy(RoundCornerC.GetComponent<Collider>());

            RoundCornerC.transform.parent = menu.transform;
            RoundCornerC.transform.rotation = Quaternion.identity * Quaternion.Euler(0f, 0f, 90f);

            RoundCornerC.transform.localPosition = toRound.transform.localPosition + new Vector3(0f, toRound.transform.localScale.y / 2f - Bevel * 1.275f, -(toRound.transform.localScale.z / 2f) + Bevel);
            RoundCornerC.transform.localScale = new Vector3(Bevel * 2.55f, toRound.transform.localScale.x / 2f, Bevel * 2f);

            GameObject RoundCornerD = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cylinder);
            RoundCornerD.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            Destroy(RoundCornerD.GetComponent<Collider>());

            RoundCornerD.transform.parent = menu.transform;
            RoundCornerD.transform.rotation = Quaternion.identity * Quaternion.Euler(0f, 0f, 90f);

            RoundCornerD.transform.localPosition = toRound.transform.localPosition + new Vector3(0f, -(toRound.transform.localScale.y / 2f) + Bevel * 1.275f, -(toRound.transform.localScale.z / 2f) + Bevel);
            RoundCornerD.transform.localScale = new Vector3(Bevel * 2.55f, toRound.transform.localScale.x / 2f, Bevel * 2f);

            GameObject[] ToChange = {
                BaseA,
                BaseB,
                RoundCornerA,
                RoundCornerB,
                RoundCornerC,
                RoundCornerD
            };

            foreach (GameObject Changed in ToChange)
            {
                ClampColor TargetChanger = Changed.AddComponent<ClampColor>();
                TargetChanger.targetRenderer = ToRoundRenderer;
            }

            ToRoundRenderer.enabled = false;

            ColorChanger colorChanger = ToRoundRenderer.GetComponent<ColorChanger>();
            if (colorChanger)
                colorChanger.overrideTransparency = false;
        }

        public static void RoundObject(GameObject toRound, float bevel = 0.02f)
        {
            static GameObject CreatePrimitive(PrimitiveType type, Transform parent, bool rendererEnabled)
            {
                GameObject obj = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(type);
                obj.GetComponent<Renderer>().enabled = rendererEnabled;

                Collider collider = obj.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                obj.transform.SetParent(parent, false);
                return obj;
            }

            Renderer renderer = toRound.GetComponent<Renderer>();
            if (renderer == null) return;

            Transform parent = toRound.transform;
            Vector3 scale = parent.localScale;
            bool rendererEnabled = renderer.enabled;

            GameObject baseA = CreatePrimitive(PrimitiveType.Cube, parent, rendererEnabled);
            baseA.transform.localPosition = Vector3.zero;
            baseA.transform.localRotation = Quaternion.identity;
            baseA.transform.localScale = new Vector3(scale.x, scale.y - bevel * 2f, scale.z);

            GameObject baseB = CreatePrimitive(PrimitiveType.Cube, parent, rendererEnabled);
            baseB.transform.localPosition = Vector3.zero;
            baseB.transform.localRotation = Quaternion.identity;
            baseB.transform.localScale = new Vector3(scale.x, scale.y, scale.z - bevel * 2f);

            GameObject[] corners = new GameObject[4];
            Vector3[] cornerOffsets = {
                new Vector3(0f, scale.y / 2f - bevel, scale.z / 2f - bevel),
                new Vector3(0f, -scale.y / 2f + bevel, scale.z / 2f - bevel),
                new Vector3(0f, scale.y / 2f - bevel, -scale.z / 2f + bevel),
                new Vector3(0f, -scale.y / 2f + bevel, -scale.z / 2f + bevel)
            };

            for (int i = 0; i < 4; i++)
            {
                corners[i] = CreatePrimitive(PrimitiveType.Cylinder, parent, rendererEnabled);
                corners[i].transform.localPosition = cornerOffsets[i];
                corners[i].transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                corners[i].transform.localScale = new Vector3(bevel * 2f, scale.x / 2f, bevel * 2f);
            }

            GameObject[] allObjects = { baseA, baseB, corners[0], corners[1], corners[2], corners[3] };
            foreach (GameObject obj in allObjects)
            {
                ClampColor clampColor = obj.AddComponent<ClampColor>();
                clampColor.targetRenderer = renderer;
            }

            renderer.enabled = false;

            ColorChanger colorChanger = renderer.GetComponent<ColorChanger>();
            if (colorChanger != null)
                colorChanger.overrideTransparency = false;
        }

        public class PromptData
        {
            public bool IsText;
            public string Message;

            public string AcceptText;
            public string DeclineText;

            public Action AcceptAction;
            public Action DeclineAction;
        }

        public static List<PromptData> prompts = new List<PromptData>();
        public static PromptData CurrentPrompt
        {
            get
            {
                return prompts.Count > 0 ? prompts[0] : null;
            }
        }

        public static void Prompt(string Message, Action Accept = null, Action Decline = null, string AcceptButton = "Yes", string DeclineButton = "No")
        {
            Settings.DestroyKeyboard();
            keyboardInput = "";
            prompts.Add(new PromptData { Message = Message, AcceptAction = Accept, DeclineAction = Decline, AcceptText = AcceptButton, DeclineText = DeclineButton, IsText = false });

            if (menu != null && prompts.Count <= 1)
                ReloadMenu();
        }

        public static void ResolvePrompt(bool accept)
        {
            PromptData current = CurrentPrompt;
            if (current == null)
                return;
            prompts.RemoveAt(0);
            NotificationManager.ClearAllNotifications();
            if (inTextInput)
                Settings.DestroyKeyboard();
            if (accept)
                current.AcceptAction?.Invoke();
            else
                current.DeclineAction?.Invoke();
        }

        private const int MaxGradientCacheSize = 64;
        private static readonly LinkedList<(Color, Color)> gradientUsage = new LinkedList<(Color, Color)>();
        private static readonly Color[] gradientPixels = new Color[128 * 128];
        public static readonly Dictionary<(Color, Color), Texture2D> cacheGradients = new Dictionary<(Color, Color), Texture2D>();

        private static Color Quantize(Color c, float step = 1f / 64f)
        {
            return new Color(
                Mathf.Round(c.r / step) * step,
                Mathf.Round(c.g / step) * step,
                Mathf.Round(c.b / step) * step,
                Mathf.Round(c.a / step) * step
            );
        }

        public static Texture2D GetGradientTexture(Color colorA, Color colorB)
        {
            var key = (Quantize(colorA), Quantize(colorB));

            if (cacheGradients.TryGetValue(key, out Texture2D cachedTexture))
            {
                LinkedListNode<(Color, Color)> usage = gradientUsage.Last;
                if (usage == null || !usage.Value.Equals(key))
                {
                    usage = gradientUsage.Find(key);
                    if (usage != null)
                    {
                        gradientUsage.Remove(usage);
                        gradientUsage.AddLast(usage);
                    }
                    else
                        gradientUsage.AddLast(key);
                }
                return cachedTexture;
            }

            Texture2D txt2d = new Texture2D(128, 128);
            Color[] pixels = gradientPixels;

            if (horizontalGradients)
            {
                for (int y = 0; y < 128; y++)
                {
                    Color rowColor = Color.Lerp(colorA, colorB, y / 128f);
                    for (int x = 0; x < 128; x++)
                        pixels[y * 128 + x] = rowColor;
                }
            }
            else
            {
                for (int i = 0; i < 128; i++)
                {
                    Color rowColor = Color.Lerp(colorA, colorB, i / 128f);
                    for (int j = 0; j < 128; j++)
                        pixels[j * 128 + i] = rowColor;
                }
            }

            txt2d.SetPixels(pixels);
            txt2d.wrapMode = TextureWrapMode.Mirror;
            txt2d.Apply();

            cacheGradients.Add(key, txt2d);
            gradientUsage.AddLast(key);

            if (cacheGradients.Count > MaxGradientCacheSize)
            {
                var oldestKey = gradientUsage.First.Value;
                gradientUsage.RemoveFirst();

                if (cacheGradients.TryGetValue(oldestKey, out Texture2D oldTex))
                {
                    Destroy(oldTex);
                    cacheGradients.Remove(oldestKey);
                }
            }

            return txt2d;
        }

        public static void RPCProtection()
        {
            if (!NetworkSystem.Instance.InRoom)
                return;

            try
            {
                MonkeAgent.instance.rpcErrorMax = int.MaxValue;
                MonkeAgent.instance.rpcCallLimit = int.MaxValue;
                MonkeAgent.instance.logErrorMax = int.MaxValue;
                MonkeAgent.instance.userRPCCalls.Clear();

                PhotonNetwork.MaxResendsBeforeDisconnect = int.MaxValue;
                PhotonNetwork.QuickResends = int.MaxValue;

                PhotonNetwork.SendAllOutgoingCommands();
            }
            catch { LogManager.Log("RPC protection failed, are you in a lobby?"); }
        }

        public static string[] InfosToStrings(ButtonInfo[] array) =>
            array.Select(button => button.buttonText).ToArray();

        public static ButtonInfo[] StringsToInfos(string[] array) =>
            array.Select(Buttons.GetIndex).ToArray();

        public static string[] Alphabetize(string[] array)
        {
            if (array.Length <= 1)
                return array;

            string first = array[0];
            string[] others = array.Skip(1).OrderBy(s => s).ToArray();
            return new[] { first }.Concat(others).ToArray();
        }

        public static IEnumerator ButtonClick(int buttonIndex, Renderer render, bool? enabled = null)
        {
            lastClickedName = "";

            float elapsedTime = 0f;
            while (elapsedTime < 0.1f)
            {
                if (render == null)
                    yield break;

                int to = enabled.HasValue ? (enabled.Value ? 1 : 0) : buttonIndex < 0 && swapButtonColors ? 1 : 0;
                int from = 1 - to;

                render.material.color = Color.Lerp(
                    buttonColors[from].GetCurrentColor(),
                    buttonColors[to].GetCurrentColor(),
                    elapsedTime / 0.1f
                );

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (render == null)
                yield break;

            ColorChanger colorChanger = render.gameObject.AddComponent<ColorChanger>();
            colorChanger.colors = buttonColors[enabled.HasValue ? (enabled.Value ? 1 : 0) : buttonIndex < 0 && swapButtonColors ? 1 : 0];

            if (shouldRound && render.gameObject.name != "Model Button")
            {
                render.enabled = false;
                colorChanger.overrideTransparency = false;
            }

            if (!joystickMenu || buttonIndex != joystickButtonSelected) yield break;
            ExtGradient gradient = colorChanger.colors.Clone();
            gradient.SetColor(0, Color.red);

            colorChanger.colors = gradient;
        }

        private static readonly Dictionary<string, GameObject> objectPool = new Dictionary<string, GameObject>();

        public static GameObject GetObject(string find)
        {
            if (string.IsNullOrEmpty(find)) return null;
            if (objectPool.TryGetValue(find, out GameObject go))
            {
                if (go != null) return go;
                objectPool.Remove(find);
            }

            GameObject tgo = GameObject.Find(find);
            if (!tgo && find.Contains("/"))
            {
                var split = find.Split('/');
                var rootName = split[0];

                var root = GameObject.Find(rootName);

                if (root != null)
                {
                    var path = find[(rootName.Length + 1)..];
                    var tr = root.transform.Find(path);

                    if (tr != null)
                        tgo = tr.gameObject;
                }
            }
            if (tgo != null)
                objectPool.Add(find, tgo);

            return tgo;
        }

        public static GameObject audioManager;

        public static void Play2DAudio(AudioClip sound, float volume = 1f)
        {
            if (audioManager == null)
            {
                audioManager = new GameObject("2DAudioMgr");
                AudioSource temp = audioManager.AddComponent<AudioSource>();
                temp.spatialBlend = 0f;
            }
            AudioSource ausrc = audioManager.GetComponent<AudioSource>();
            ausrc.volume = volume;
            ausrc.PlayOneShot(sound);
        }

        public static string FollowMenuSettings(string input)
        {
            if (lowercaseMode)
                input = input.ToLower();

            if (uppercaseMode)
                input = input.ToUpper();

            if (!redactText) return input;
            var result = new char[input.Length];

            for (int i = 0; i < input.Length; i++)
                result[i] = input[i] == ' ' ? ' ' : '█';

            input = new string(result);

            return input;
        }

        public static void FollowMenuSettings(GameObject gameObject, bool shouldBeEnabled = true)
        {
            if (shouldOutline)
                OutlineMenuObject(gameObject, shouldBeEnabled);

            if (shouldRound)
                RoundMenuObject(gameObject);
        }

        public static void FollowMenuSettings(TMP_Text tmp, float? overlapTargetSpacing = null)
        {
            if (tmp == null)
                return;

            MenuTypography.Configure(tmp);
            float targetSpacing = overlapTargetSpacing ?? characterDistance;
            if (redactText)
                targetSpacing -= 3f;

            if (!Mathf.Approximately(tmp.characterSpacing, targetSpacing))
                tmp.characterSpacing = targetSpacing;

            if (outlineText)
            {
                const float outlineWidth = 0.2f;

                if (!Mathf.Approximately(tmp.outlineWidth, outlineWidth))
                    tmp.outlineWidth = outlineWidth;

                if (tmp.outlineColor != Color.black)
                    tmp.outlineColor = Color.black;
            }
            else if (!Mathf.Approximately(tmp.outlineWidth, 0f))
                tmp.outlineWidth = 0f;

            FontStyles targetStyle = tmp.fontStyle;

            if (underlineText)
                targetStyle |= FontStyles.Underline;

            if (smallCapsText)
                targetStyle |= FontStyles.SmallCaps;

            if (strikethroughText)
                targetStyle |= FontStyles.Strikethrough;

            if (tmp.fontStyle != targetStyle)
                tmp.fontStyle = targetStyle;
        }

        public static void FollowMenuSettings(MaskableGraphic canvasObject)
        {
            if (outlineText)
                OutlineCanvasObject(canvasObject);
        }

        public static string CurrentTimestamp()
        {
            DateTime utcNow = DateTime.UtcNow;
            return utcNow.ToString("o");
        }

        public static string ColorToHex(Color color) =>
            ColorUtility.ToHtmlStringRGB(color);

        private static readonly Regex notagsRegex = new Regex("<.*?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string NoRichtextTags(string input, string replace = "") =>
            input.IndexOf('<') < 0 ? input : notagsRegex.Replace(input, replace);

        public static bool vibrantColors;
        public static string FixTMProTags(string input)
        {
            if (vibrantColors)
                return input;

            input = input.Replace("<color=green>", "<color=#008000>");
            input = input.Replace("<color=purple>", "<color=#800080>");

            return input;
        }

        private static Gradient richtextGradientGradient = new Gradient();
        public static string RichtextGradient(string input, GradientColorKey[] Colors)
        {
            richtextGradientGradient ??= new Gradient();
            richtextGradientGradient.colorKeys = Colors;

            var sb = new System.Text.StringBuilder(input.Length * 24);
            for (int i = 0; i < input.Length; i++)
            {
                char character = input[i];
                Color characterColor = richtextGradientGradient.Evaluate((Time.time / 2f + i / 25f) % 1f);
                sb.Append("<color=#").Append(ColorToHex(characterColor)).Append('>').Append(character).Append("</color>");
            }
            return sb.ToString();
        }

        public static Color BrightenColor(Color color, float intensity = 0.5f)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            v = Mathf.Clamp01(v + (1f - v) * intensity * 1.2f);
            s = Mathf.Clamp01(s * (1f - intensity * 0.3f));
            return Color.HSVToRGB(h, s, v);
        }

        public static bool inRoomStatus;
        public static string lastRoom;
        private static bool notifiedLocalMaster;

        public static string CleanPlayerName(string input, int length = 12)
        {
            input = NoRichtextTags(input);

            if (input.Length > length)
                input = input[..(length - 1)];

            return input;
        }

        public static void CleanVisualsOnLeave(VRRig rig = null)
        {
            if (rig)
                Visuals.convertedRigs.Remove(rig);
            else
                Visuals.convertedRigs.Clear();
        }
        private static void OnJoinRoom()
        {
            NetworkSystem network = NetworkSystem.Instance;



            if (network == null || !network.InRoom || network.netState == NetSystemState.Disconnecting ||
                string.IsNullOrWhiteSpace(network.RoomName) ||
                (GorillaComputer.instance != null && GorillaComputer.instance.roomNotAllowed))
                return;
            if (inRoomStatus && string.Equals(lastRoom, network.RoomName, StringComparison.Ordinal))
                return;

            inRoomStatus = true;
            lastRoom = network.RoomName;
            notifiedLocalMaster = false;

            if (!disableRoomNotifications)
                NotificationManager.SendNotification($"<color=grey>[</color><color=blue>JOIN ROOM</color><color=grey>]</color> Room Code: {lastRoom}");

            OnMasterClientSwitch(network.MasterClient);
            RPCProtection();
        }

        private static void OnLeaveRoom()
        {
            notifiedLocalMaster = false;
            if (!inRoomStatus)
            {
                lastRoom = null;
                return;
            }

            inRoomStatus = false;
            string leftRoom = lastRoom;
            lastRoom = null;

            if (clearNotificationsOnDisconnect)
                NotificationManager.ClearAllNotifications();

            if (!disableRoomNotifications)
                NotificationManager.SendNotification($"<color=grey>[</color><color=blue>LEAVE ROOM</color><color=grey>]</color> Room Code: {leftRoom}");

            CleanVisualsOnLeave();
            RPCProtection();
        }

        private static void OnMasterClientSwitch(NetPlayer masterClient)
        {
            NetworkSystem network = NetworkSystem.Instance;
            bool localMaster = inRoomStatus && network != null && network.InRoom &&
                network.netState != NetSystemState.Disconnecting &&
                masterClient != null && masterClient.IsLocal && network.IsMasterClient;
            if (!localMaster)
            {
                notifiedLocalMaster = false;
                return;
            }
            if (notifiedLocalMaster) return;
            notifiedLocalMaster = true;

            if (!disableMasterClientNotifications)
                NotificationManager.SendNotification("<color=grey>[</color><color=purple>MASTER</color><color=grey>]</color> You are now master client.");
        }

        private static void OnPlayerJoin(NetPlayer Player)
        {
            if (Player != NetworkSystem.Instance.LocalPlayer && !disablePlayerNotifications)
                NotificationManager.SendNotification($"<color=grey>[</color><color=green>JOIN</color><color=grey>]</color> Name: {CleanPlayerName(Player.NickName)}");
        }

        private static void OnPlayerLeave(NetPlayer Player)
        {
            if (Player != NetworkSystem.Instance.LocalPlayer && !disablePlayerNotifications)
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>LEAVE</color><color=grey>]</color> Name: {CleanPlayerName(Player.NickName)}");
            var rig = Player.VRRig();
            if (rig != null)
                CleanVisualsOnLeave(rig);
        }

        public static void ReloadMenu()
        {
            if (menu != null)
            {
                menu.SetActive(false);
                Destroy(menu);
                menu = null;

                if (CurrentPrompt != null && CurrentPrompt.IsText && !inTextInput)
                    Settings.SpawnKeyboard();

                CreateMenu();
            }

            if (reference == null) return;
            Destroy(reference);
            reference = null;

            CreateReference();
        }

        private static int? noInvisLayerMask;
        private static int LayerBit(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer < 0 ? 0 : 1 << layer;
        }

        public static int NoInvisLayerMask()
        {
            noInvisLayerMask ??= ~(
                LayerBit("TransparentFX") |
                LayerBit("Ignore Raycast") |
                LayerBit("Gorilla Trigger") |
                LayerBit("Gorilla Boundary") |
                LayerBit("GorillaCosmetics") |
                LayerBit("GorillaParticle"));

            return noInvisLayerMask ?? GTPlayer.Instance.locomotionEnabledLayers;
        }

        public static void Toggle(string buttonText, bool fromMenu = false, bool ignoreForce = false)
        {
            switch (buttonText)
            {
                case "PreviousPage":
                    {
                        lastClickedName = "PreviousPage";

                        pageNumber--;
                        if (pageNumber < 0)
                            pageNumber = LastPage;
                        break;
                    }
                case "NextPage":
                    {
                        lastClickedName = "NextPage";

                        pageNumber++;
                        pageNumber %= LastPage + 1;
                        break;
                    }
                default:
                    {
                        ButtonInfo target = Buttons.GetIndex(buttonText);
                        if (target != null)
                        {
                            string newIndicator = " <color=grey>[</color><color=green>New</color><color=grey>]</color>";
                            if (target.overlapText != null && target.overlapText.Contains(newIndicator))
                            {
                                target.overlapText = target.overlapText.Replace(newIndicator, "");
                                if (target.overlapText == target.buttonText)
                                    target.overlapText = target.buttonText;
                            }

                            if (target.label)
                                return;

                            switch (fromMenu)
                            {
                                case true when !ignoreForce && menuButtonIndex != 2 && ((leftGrab && !joystickMenu) || (joystickMenu && rightJoystick.y > 0.5f && leftTrigger > 0.5f)):
                                    {
                                        if (IsBinding)
                                        {
                                            bool AlreadyBinded = false;
                                            string BindedTo = "";
                                            foreach (var Bind in ModBindings.Where(Bind => Bind.Value.Contains(target.buttonText)))
                                            {
                                                AlreadyBinded = true;
                                                BindedTo = Bind.Key;
                                                break;
                                            }

                                            if (AlreadyBinded)
                                            {
                                                target.customBind = null;
                                                ModBindings[BindedTo].Remove(target.buttonText);
                                                VRRig.LocalRig.PlayHandTapLocal(48, rightHand, 0.4f);
                                            }
                                            else
                                            {
                                                target.customBind = BindInput;
                                                ModBindings[BindInput].Add(target.buttonText);
                                                VRRig.LocalRig.PlayHandTapLocal(50, rightHand, 0.4f);
                                            }
                                        }
                                        else
                                        {
                                            if (IsRebinding)
                                            {
                                                if (target.rebindKey != null)
                                                {
                                                    target.rebindKey = null;
                                                    VRRig.LocalRig.PlayHandTapLocal(48, rightHand, 0.4f);
                                                }
                                                else
                                                {
                                                    target.rebindKey = BindInput;
                                                    VRRig.LocalRig.PlayHandTapLocal(50, rightHand, 0.4f);
                                                }
                                            }
                                            else
                                            {
                                                if (target.buttonText != "Exit Favorite Mods" && !disableFavoritingKeybind)
                                                {
                                                    if (favorites.Contains(target.buttonText))
                                                    {
                                                        favorites.Remove(target.buttonText);
                                                        VRRig.LocalRig.PlayHandTapLocal(48, rightHand, 0.4f);
                                                    }
                                                    else
                                                    {
                                                        favorites.Add(target.buttonText);
                                                        VRRig.LocalRig.PlayHandTapLocal(50, rightHand, 0.4f);
                                                    }
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case true when !ignoreForce && menuButtonIndex != 3 && leftTrigger > 0.5f && !joystickMenu:
                                    {
                                        if (!quickActions.Contains(target.buttonText))
                                        {
                                            quickActions.Add(target.buttonText);
                                            VRRig.LocalRig.PlayHandTapLocal(50, rightHand, 0.4f);
                                        }
                                        else
                                        {
                                            quickActions.Remove(target.buttonText);
                                            VRRig.LocalRig.PlayHandTapLocal(48, rightHand, 0.4f);
                                        }

                                        break;
                                    }
                                default:
                                    {
                                        if (target.isTogglable)
                                        {
                                            target.enabled = !target.enabled;
                                            if (target.enabled)
                                            {

                                                if (target.enableMethod != null)
                                                    try { target.enableMethod.Invoke(); }
                                                    catch (Exception exc)
                                                    {
                                                        LogManager.LogError(
                                                        $"Error with mod enableMethod {target.buttonText} at {exc.StackTrace}: {exc.Message}");
                                                    }
                                            }
                                            else
                                            {

                                                if (target.disableMethod != null)
                                                    try { target.disableMethod.Invoke(); }
                                                    catch (Exception exc)
                                                    {
                                                        LogManager.LogError(
                                                        $"Error with mod disableMethod {target.buttonText} at {exc.StackTrace}: {exc.Message}");
                                                    }
                                            }
                                        }
                                        else
                                        {
                                            lastClickedName = target.buttonText;


                                            if (target.method != null)
                                                try { target.method.Invoke(); }
                                                catch (Exception exc)
                                                {
                                                    LogManager.LogError(
                                                    $"Error with mod {target.buttonText} at {exc.StackTrace}: {exc.Message}");
                                                }
                                        }
                                        break;
                                    }
                            }
                        }
                        else
                            LogManager.LogError($"{buttonText} does not exist");

                        break;
                    }
            }

            ReloadMenu();
        }

        public static void ToggleIncremental(string buttonText, bool increment, bool reload = true)
        {
            ButtonInfo target = Buttons.GetIndex(buttonText);
            if (target != null)
            {
                string newIndicator = " <color=grey>[</color><color=green>New</color><color=grey>]</color>";
                if (target.overlapText != null && target.overlapText.Contains(newIndicator))
                {
                    target.overlapText = target.overlapText.Replace(newIndicator, "");
                    if (target.overlapText == target.buttonText)
                        target.overlapText = target.buttonText;
                }

                if (target.label)
                    return;

                switch (true)
                {
                    case true when menuButtonIndex != 2 && ((leftGrab && !joystickMenu) || (joystickMenu && rightJoystick.y > 0.5f && leftTrigger > 0.5f)):
                        {
                            if (IsBinding)
                            {
                                bool AlreadyBinded = false;
                                string BindedTo = "";
                                foreach (var Bind in ModBindings.Where(Bind => Bind.Value.Contains(target.buttonText)))
                                {
                                    AlreadyBinded = true;
                                    BindedTo = Bind.Key;
                                    break;
                                }

                                if (AlreadyBinded)
                                {
                                    target.customBind = null;
                                    ModBindings[BindedTo].Remove(target.buttonText);
                                    VRRig.LocalRig.PlayHandTapLocal(48, rightHand, 0.4f);

                                }
                                else
                                {
                                    target.customBind = BindInput;
                                    ModBindings[BindInput].Add(target.buttonText);
                                    VRRig.LocalRig.PlayHandTapLocal(50, rightHand, 0.4f);

                                }
                            }
                            else
                            {
                                if (IsRebinding)
                                {
                                    if (target.rebindKey != null)
                                    {
                                        target.rebindKey = null;
                                        VRRig.LocalRig.PlayHandTapLocal(48, rightHand, 0.4f);
                                    }
                                    else
                                    {
                                        target.rebindKey = BindInput;
                                        VRRig.LocalRig.PlayHandTapLocal(50, rightHand, 0.4f);
                                    }
                                }
                                else
                                {
                                    if (target.buttonText != "Exit Favorite Mods")
                                    {
                                        if (favorites.Contains(target.buttonText))
                                        {
                                            favorites.Remove(target.buttonText);
                                            VRRig.LocalRig.PlayHandTapLocal(48, rightHand, 0.4f);

                                        }
                                        else
                                        {
                                            favorites.Add(target.buttonText);
                                            VRRig.LocalRig.PlayHandTapLocal(50, rightHand, 0.4f);

                                        }
                                    }
                                }
                            }

                            break;
                        }
                    case true when menuButtonIndex != 3 && leftTrigger > 0.5f && !joystickMenu:
                        {
                            if (!quickActions.Contains(target.buttonText))
                            {
                                quickActions.Add(target.buttonText);
                                VRRig.LocalRig.PlayHandTapLocal(50, rightHand, 0.4f);

                            }
                            else
                            {
                                quickActions.Remove(target.buttonText);
                                VRRig.LocalRig.PlayHandTapLocal(48, rightHand, 0.4f);

                            }

                            break;
                        }
                    default:
                        {
                            lastClickedName = buttonText + (increment ? "+" : "-");

                            bool boost = incrementalBoost && rightGrab;
                            if (increment)
                            {

                                if (target.enableMethod != null)
                                {
                                    if (boost)
                                        for (int i = 0; i < 5; i++)
                                        {
                                            try { target.enableMethod.Invoke(); }
                                            catch (Exception exc)
                                            {
                                                LogManager.LogError($"Error with a mod's enableMethod {target.buttonText} at {exc.StackTrace}: {exc.Message}");
                                            }
                                        }
                                    else
                                        try { target.enableMethod.Invoke(); }
                                        catch (Exception exc)
                                        {
                                            LogManager.LogError($"Error with a mod's enableMethod {target.buttonText} at {exc.StackTrace}: {exc.Message}");
                                        }
                                }
                                else if (target.cycleValue != null)
                                {
                                    int steps = boost ? 5 : 1;
                                    for (int i = 0; i < steps; i++)
                                    {
                                        try { target.cycleValue.Invoke(true); }
                                        catch (Exception exc)
                                        {
                                            LogManager.LogError($"Error with a mod's cycleValue {target.buttonText} at {exc.StackTrace}: {exc.Message}");
                                        }
                                    }
                                }
                            }
                            else
                            {

                                if (target.disableMethod != null)
                                {
                                    if (boost)
                                        for (int i = 0; i < 5; i++)
                                        {
                                            try { target.disableMethod.Invoke(); }
                                            catch (Exception exc)
                                            {
                                                LogManager.LogError($"Error with a mod's disableMethod {target.buttonText} at {exc.StackTrace}: {exc.Message}");
                                            }
                                        }
                                    else
                                        try { target.disableMethod.Invoke(); }
                                        catch (Exception exc)
                                        {
                                            LogManager.LogError($"Error with a mod's disableMethod {target.buttonText} at {exc.StackTrace}: {exc.Message}");
                                        }
                                }
                                else if (target.cycleValue != null)
                                {
                                    int steps = boost ? 5 : 1;
                                    for (int i = 0; i < steps; i++)
                                    {
                                        try { target.cycleValue.Invoke(false); }
                                        catch (Exception exc)
                                        {
                                            LogManager.LogError($"Error with a mod's cycleValue {target.buttonText} at {exc.StackTrace}: {exc.Message}");
                                        }
                                    }
                                }
                            }

                            break;
                        }
                }
            }
            else
                LogManager.LogError($"{buttonText} does not exist");

            if (reload)
                ReloadMenu();
        }

        public static void UnloadMenu()
        {
            Interstellar.Mods.Room.Cleanup();
            DisableModsForUnload();
            Interstellar.Utilities.FeatureRuntime.Shutdown();
            Interstellar.Mods.World.RestoreAll();

            CustomBoardManager.CustomBoardsEnabled = false;
            CustomBoardManager.CustomBoardFonts = false;

            NetworkSystem network = NetworkSystem.Instance;
            if (network != null)
            {
                network.OnMultiplayerStarted -= OnJoinRoom;
                network.OnReturnedToSinglePlayer -= OnLeaveRoom;
                network.OnMasterClientSwitchedEvent -= OnMasterClientSwitch;
                network.OnPlayerJoined -= OnPlayerJoin;
                network.OnPlayerLeft -= OnPlayerLeave;
            }
            inRoomStatus = false;
            lastRoom = null;
            notifiedLocalMaster = false;

            if (NotificationManager.Instance != null)
            {
                Destroy(NotificationManager.Instance.canvas);
                Destroy(NotificationManager.arraylistText);
                Destroy(NotificationManager.notificationText);
                Destroy(NotificationManager.Instance.gameObject);
            }

            if (VRKeyboard != null)
            {
                Destroy(VRKeyboard);
                VRKeyboard = null;
            }

            if (CustomBoardManager.instance != null && CustomBoardManager.instance.motdTitle != null)
            {
                Destroy(CustomBoardManager.instance.motdTitle);
                CustomBoardManager.instance.motdTitle = null;
            }

            if (CustomBoardManager.instance != null && CustomBoardManager.instance.motdText != null)
            {
                Destroy(CustomBoardManager.instance.motdText);
                CustomBoardManager.instance.motdText = null;
            }

            if (menuBackground != null)
            {
                Destroy(menuBackground);
                menuBackground = null;
            }

            if (menu != null)
            {
                Destroy(menu);
                menu = null;
            }

            if (reference != null)
            {
                Destroy(reference);
                reference = null;
            }

            if (lKeyReference != null)
            {
                Destroy(lKeyReference);
                lKeyReference = null;
            }

            if (rKeyReference != null)
            {
                Destroy(rKeyReference);
                rKeyReference = null;
            }

            HasLoaded = false;

            Lockdown = true;

            PatchHandler.UnpatchAll();

            if (Bootstrapper.Loader)
                Destroy(Bootstrapper.Loader);
        }

        private static void DisableModsForUnload()
        {
            foreach (ButtonInfo button in Buttons.buttons.SelectMany(category => category))
            {
                if (button == null || !button.enabled) continue;
                button.enabled = false;
                button.firstFrame = true;
                try { button.disableMethod?.Invoke(); }
                catch (Exception error) { LogManager.LogError("Failed to disable " + button.buttonText + " during unload: " + error); }
            }
        }

        public static TMP_FontAsset activeFont = LiberationSans;
        public static FontStyles activeFontStyle = FontStyles.Bold;
        public static TMP_FontAsset LiberationSans = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        public static bool isOnPC;
        public static bool IsSteam = true;
        public static bool Lockdown;

        public static bool HasLoaded;

        public static bool thinMenu = true;
        public static bool modelMenu = true;
        public static bool ModelMenuActive { get; private set; }
        public static bool ModelHasBackRow { get; private set; }
        public static Bounds ModelPanelBounds { get; private set; }
        public static bool longmenu;
        public static bool hidetitle;
        public static bool flipMenu;
        public static bool shinyMenu;
        public static bool transparentMenu;
        public static bool crystallizeMenu;
        public static bool shouldOutline;
        public static bool outlineText;
        public static bool underlineText;
        public static bool strikethroughText;
        public static bool smallCapsText;
        public static bool innerOutline;
        public static bool shouldRound;
        public static bool isMouseDown;
        public static bool oneHand;

        public static int _pageSize = 8;
        public static int PageSize
        {
            get => _pageSize - buttonOffset;
            set => _pageSize = value;
        }

        public static int DisplayedItemCount
        {
            get
            {
                if (isSearching) return GetSearchResults().Count;

                ButtonInfo[] list = Buttons.buttons[Buttons.CurrentCategoryIndex];
                int count = list.Length;

                if (Buttons.CurrentCategoryName == "Favorite Mods")
                    count = favorites.Count;

                if (Buttons.CurrentCategoryName == "Enabled Mods")
                {
                    var enabledMods = new List<string> { "Exit Enabled Mods" };

                    for (int i = 0; i < Buttons.buttons.Length; i++)
                    {
                        var buttonList = Buttons.buttons[i];
                        var category = Buttons.categoryNames[i];

                        if (hideSettings && category.Contains("Settings"))
                            continue;

                        enabledMods.AddRange(from v in buttonList where v.enabled select v.buttonText);
                    }

                    count = enabledMods.Count - 1;
                }

                if (Buttons.CurrentCategoryName == "Main")
                {
                    foreach (var button in Buttons.buttons[Buttons.CurrentCategoryIndex])
                    {
                        if (skipButtons.Contains(button.buttonText))
                            count--;
                    }
                }

                if (ModelMenuActive && !checkMode && Buttons.CurrentCategoryName != "Favorite Mods"
                    && Buttons.CurrentCategoryName != "Enabled Mods" && list.Any(ModelButtonLayout.IsBack))
                    count--;

                return count;
            }
        }

        public static float ButtonDistance
        {
            get => 0.8f / (PageSize + buttonOffset);
        }

        public static int LastPage
        {
            get
            {
                int capacity = Mathf.Max(1, PageSize - (isSearching ? 1 : 0));
                return Mathf.Max(0, (DisplayedItemCount + capacity - 1) / capacity - 1);
            }
        }

        public static int pageOffset;
        public static int pageNumber;
        public static bool pageScrolling;
        public static bool noPageNumber;
        public static bool disablePageButtons;
        public static bool swapButtonColors;
        public static int pageButtonType = 1;
        public static float pageButtonChangeDelay;

        public static bool pcKeyboardSounds = true;
        public static int buttonClickVolume = 4;
        public static int buttonOffset = 0;
        public static int menuButtonIndex = 1;
        public static bool toggleButton;
        public static bool toggleButtonHeld;
        public static bool toggleButtonActive;
        public static bool keyboardWithToggleButton;
        public static int characterDistance;

        public static bool doButtonsVibrate = true;

        public static bool joystickMenu;
        public static bool joystickMenuSearching;

        public static bool physicalMenu;

        public static bool chestMenu;
        private static bool chestMenuOpen;
        private static bool? chestMenuGrabbed;

        private static float chestTapDelay;
        private static int chestTapCount;

        private static bool previousChestTapState;

        public static Vector3 physicalOpenPosition = Vector3.zero;
        public static Quaternion physicalOpenRotation = Quaternion.identity;
        public static Vector3 smoothTargetPosition = Vector3.zero;
        public static Quaternion smoothTargetRotation = Quaternion.identity;
        public static bool joystickOpen;
        public static bool smoothMenuPosition;
        public static bool smoothMenuRotation;
        public static int joystickButtonSelected;
        public static string joystickSelectedButton = "";
        public static float joystickDelay;
        public static float scrollDelay;

        public static int joystickMenuPosition;
        public static Vector3[] joystickMenuPositions = {
            new Vector3(0.3f, 0.2f, 1f),
            new Vector3(-0.3f, 0.2f, 1f),
            new Vector3(0.3f, -0.2f, 1f),
            new Vector3(-0.3f, -0.2f, 1f),
            new Vector3(0f, -0.1f, 0.5f),
            new Vector3(0.3f, -0.1f, 1f),
            new Vector3(-0.3f, -0.1f, 1f),
            new Vector3(0f, 0.2f, 1f),
            new Vector3(0f, -0.2f, 1f),
        };

        public static bool rightHand;
        public static bool bothHands;
        public static bool wristMenu;
        public static bool watchMenu;
        public static bool wristOpen;
        public static float wristMenuDelay;

        public static bool stackNotifications;
        public static bool hideBrackets;
        public static bool disableNotifications;
        public static bool disableMasterClientNotifications;
        public static bool disableRoomNotifications;
        public static bool disablePlayerNotifications;
        public static bool clearNotificationsOnDisconnect;
        public static bool showEnabledModsVR = true;
        public static bool advancedArraylist;
        public static bool flipArraylist;
        public static bool hideSettings;
        public static bool hideTextOnCamera;
        public static bool incrementalButtons = true;
        public static bool incrementalBoost;
        public static bool disableDisconnectButton;
        public static bool disableFpsCounter;
        public static bool disableSearchButton;
        public static bool enableDebugButton;

        public static bool ghostException;
        public static bool disableGhostview;
        public static bool legacyGhostview;
        public static bool checkMode;
        public static bool lastChecker;
        public static bool disableWatermark;
        public static bool NoAutoSizeText;

        public static bool doCustomName;
        public static string customMenuName = "Your Text Here";
        public static readonly string menuName = "<b>Interstellar</b>";
        public static bool doCustomMenuBackground;
        public static bool adaptiveButtons = true;
        public static int pcbg;

        public static bool isSearching;
        public static bool nonGlobalSearch;
        public static bool isKeyboardPc;
        public static bool inTextInput;
        public static string keyboardInput = "";

        public static int? fullModAmount;
        public static bool scaleWithPlayer;
        public static float menuScale { get => MenuSizing.Scale; set => MenuSizing.Scale = value; }
        public static int overlayScale = 30;
        public static int arraylistScale = 20;

        public static bool dynamicSounds;
        public static bool exclusivePageSounds;
        public static bool dynamicGradients;
        public static bool horizontalGradients;
        public static bool scrollingGradients;
        public static bool particleSpawnEffect;
        public static bool animatedTitle;
        public static bool gradientTitle;
        public static string lastClickedName = "";

        public const float PanelPressTime = 0.1f;
        public const float PanelPressDepth = 0.035f;

        public static float panelPressTime = -1f;
        private static float lastPanelPress;
        public static bool shouldBePC;
        public static bool leftPrimary;
        public static bool leftSecondary;
        public static bool rightPrimary;
        public static bool rightSecondary;
        public static bool leftGrab;
        public static bool rightGrab;
        public static float leftTrigger;
        public static float rightTrigger;

        private static readonly Dictionary<string, bool> Inputs = new Dictionary<string, bool>
        {
            { "A", false }, { "B", false }, { "X", false }, { "Y", false },
            { "LG", false }, { "RG", false }, { "LT", false }, { "RT", false },
            { "LJ", false }, { "RJ", false }
        };

        public static Vector2 leftJoystick = Vector2.zero;
        public static Vector2 rightJoystick = Vector2.zero;
        public static bool leftJoystickClick;
        public static bool rightJoystickClick;

        public static bool ToggleBindings = true;
        public static bool OverwriteKeybinds;
        public static bool IsBinding;
        public static string BindInput = "";
        public static bool IsRebinding;

        public static readonly Dictionary<string, List<string>> ModBindings = new Dictionary<string, List<string>> {
            { "A", new List<string>() },
            { "B", new List<string>() },
            { "X", new List<string>() },
            { "Y", new List<string>() },
            { "LG", new List<string>() },
            { "RG", new List<string>() },
            { "LT", new List<string>() },
            { "RT", new List<string>() },
            { "LJ", new List<string>() },
            { "RJ", new List<string>() },
        };

        public static readonly Dictionary<string, bool> BindStates = new Dictionary<string, bool> {
            { "A", false },
            { "B", false },
            { "X", false },
            { "Y", false },
            { "LG", false },
            { "RG", false },
            { "LT", false },
            { "RT", false },
            { "LJ", false },
            { "RJ", false },
        };

        public static readonly List<string> quickActions = new List<string>();

        public static Camera TPC;
        public static GameObject menu;
        public static GameObject menuBackground;
        public static GameObject pcBackground;
        public static GameObject reference;
        public static SphereCollider buttonCollider;
        public static GameObject canvasObj;
        public static TextMeshPro fpsCount;
        public static Image watermarkImage;
        private static float fpsAvgTime;
        private static float fpsAverageNumber;
        public static bool fpsCountTimed;
        public static bool fpsCountAverage;
        public static bool ftCount;
        public static float lastDeltaTime = 1f;
        public static TextMeshPro keyboardInputObject;
        public static TextMeshPro title;
        public static VRRig GhostRig;
        public static GameObject legacyGhostViewLeft;
        public static GameObject legacyGhostViewRight;
        public static Material GhostMaterial;
        public static Material CrystalMaterial;
        public static Material searchMat;
        public static Material watermarkMat;
        public static Material debugMat;

        public static GameObject lKeyReference;
        public static SphereCollider lKeyCollider;

        public static GameObject rKeyReference;
        public static SphereCollider rKeyCollider;

        public static GameObject VRKeyboard;
        public static GameObject menuSpawnPosition;

        public class WatchInfo
        {
            public TextMeshProUGUI text;
            public GameObject shell;
            public Image indicator;

            public void Update()
            {
                Renderer renderer = shell.GetComponent<Renderer>();
                MenuTypography.Configure(text);
                if (renderer.sharedMaterial != CustomBoardManager.BoardMaterial)
                    renderer.sharedMaterial = CustomBoardManager.BoardMaterial;
            }
        }

        public static List<WatchInfo> Watches = new List<WatchInfo>();

        public static Material watchIndicatorMat;
        public static int watchMenuIndex;

        public static Material cannabisMat;
        public static Texture2D cann;
        public static Texture2D pride;
        public static Texture2D trans;
        public static Texture2D gay;
        public static Texture2D searchIcon;
        public static Texture2D debugIcon;
        public static Texture2D customMenuBackgroundImage;
        public static Texture2D customWatermark;

        public static readonly List<string> favorites = new List<string> { "Exit Favorite Mods" };
        public static readonly List<string> skipButtons = new List<string> { };

        public static int arrowType;
        public static readonly string[][] arrowTypes = {
            new[] {"<", ">"},
            new[] {"←", "→"},
            new[] { "    <sprite name=\"Left1\">", "    <sprite name=\"Right1\">"},
            new[] {"◄", "►"},
            new[] { "    <sprite name=\"Left2\">", "    <sprite name=\"Right2\">"},
            new[] {"‹", "›"},
            new[] {"«", "»"},
            new[] { "    <sprite name=\"Left3\">", "    <sprite name=\"Right3\">"},
            new[] {"-", "+"},
            new[] {"", ""},
            new[] {"v", "ʌ"},
            new[] { "v\nv\nv\nv\nv\nv", "ʌ\nʌ\nʌ\nʌ\nʌ\nʌ" }
        };

        public static int themeType = 0;
        public static bool slowFadeColors;

        public static ExtGradient backgroundColor = new ExtGradient
        {
            colors = ExtGradient.GetSolidGradient(
                ModelPalette.Shell
            )
        };

        public static ExtGradient menuBackgroundColor = new ExtGradient
        {
            colors = ExtGradient.GetSolidGradient(
                ModelPalette.Shell
            )
        };

        public static ExtGradient[] buttonColors = {
            new ExtGradient
            {
                colors = ExtGradient.GetSolidGradient(
                    ModelPalette.Disabled
                )
            },
            new ExtGradient
            {
                colors = ExtGradient.GetSolidGradient(
                    ModelPalette.Enabled
                )
            }
        };

        public static ExtGradient[] textColors = {
            new ExtGradient
            {
                colors = ExtGradient.GetSolidGradient(ModelPalette.LightText)
            },
            new ExtGradient
            {
                colors = ExtGradient.GetSolidGradient(ModelPalette.LightText)
            },
            new ExtGradient
            {
                colors = ExtGradient.GetSolidGradient(ModelPalette.DarkText)
            }
        };

        public static Vector3 pointerOffset = new Vector3(0f, -0.1f, 0f);

        public static float timeMenuStarted = -1f;
        public static float buttonCooldown;

        public static int notificationDecayTime = 1000;

        public static bool shift;
        public static bool lockShift;

        public static bool lowercaseMode;
        public static bool uppercaseMode;
        public static bool redactText;

        public static string inputTextColor = "green";
        public static bool disableFavoritingKeybind;

        public static bool annoyingMode;
    }
}

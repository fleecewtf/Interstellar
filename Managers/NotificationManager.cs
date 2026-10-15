using Interstellar.Classes.Menu;
using Interstellar.Extensions;
using Interstellar.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using static Interstellar.Menu.Main;

namespace Interstellar.Managers
{
    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager Instance { get; private set; }

        public GameObject canvas;
        private Canvas canvasComponent;
        private CanvasScaler canvasScaler;
        private RectTransform canvasRect;
        private Camera hudCamera;
        private TMP_SpriteAsset notificationIcons;
        private bool updatingHud;
        private bool renderCallbacksRegistered;

        public static string PreviousNotifi;
        public static readonly Dictionary<string, string> information = new Dictionary<string, string>();

        public static TextMeshProUGUI notificationText;
        public static TextMeshProUGUI arraylistText;
        public static TextMeshProUGUI informationText;

        private bool hasInitialized;
        public static bool noRichText;
        public static bool soundOnError;
        private class NotificationEntry
        {
            public string BaseDisplay;
            public int Counter;
            public float ExpireTime;
            public float Duration;
        }

        private static readonly List<NotificationEntry> activeNotifications = new List<NotificationEntry>();
        private static readonly List<string> notificationLines = new List<string>(8);
        private readonly List<string> enabledModsBuffer = new List<string>();
        private readonly System.Text.StringBuilder arraylistBuilder = new System.Text.StringBuilder();

        private void Awake()
        {
            Instance = this;
            LogManager.Log("Notifications loaded");
        }

        private void Init()
        {
            NotificationIcon.Release(notificationIcons);
            notificationIcons = null;
            canvas = new GameObject("Interstellar_Notifications", typeof(RectTransform));
            canvas.transform.SetParent(transform, false);
            canvasComponent = canvas.AddComponent<Canvas>();
            canvasScaler = canvas.AddComponent<CanvasScaler>();
            canvasComponent.enabled = true;
            canvasComponent.renderMode = RenderMode.WorldSpace;
            canvasComponent.worldCamera = hudCamera;
            canvasComponent.sortingLayerID = MenuVisuals.SortingLayerId;
            canvasComponent.sortingOrder = 31990;

            canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 600f);

            GameObject notificationGroup = new GameObject("Interstellar_NotificationOverlay", typeof(RectTransform), typeof(Canvas));
            notificationGroup.transform.SetParent(canvas.transform, false);
            RectTransform groupRect = notificationGroup.GetComponent<RectTransform>();
            groupRect.anchorMin = Vector2.zero;
            groupRect.anchorMax = Vector2.one;
            groupRect.sizeDelta = Vector2.zero;
            Canvas notificationCanvas = notificationGroup.GetComponent<Canvas>();
            notificationCanvas.overrideSorting = true;
            notificationCanvas.sortingLayerID = MenuVisuals.SortingLayerId;
            notificationCanvas.sortingOrder = 32001;
            notificationText = CreateText(notificationGroup.transform,
                new Vector2(670f, 210f), 30, TextAlignmentOptions.BottomLeft);
            notificationIcons = NotificationIcon.Create();
            notificationText.spriteAsset = notificationIcons;
            notificationText.fontMaterial.renderQueue = 4051;
            notificationText.gameObject.SetActive(!disableNotifications);

            arraylistText = CreateText(canvas.transform,
                new Vector2(450f, 1000f), 20, TextAlignmentOptions.TopLeft);

            informationText = CreateText(canvas.transform,
                new Vector2(450f, 1000f), 30, TextAlignmentOptions.TopRight);
            RebuildNotificationText();
        }

        private TextMeshProUGUI CreateText(Transform parent, Vector2 size, int fontSize, TextAlignmentOptions anchor)
        {
            GameObject textObj = new GameObject("Interstellar_HudText", typeof(RectTransform));
            textObj.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();

            text.SafeSetText("");
            text.fontSize = fontSize;
            MenuTypography.Configure(text);
            text.Chams(true);
            text.rectTransform.sizeDelta = size;
            text.alignment = anchor;
            text.overflowMode = TextOverflowModes.Truncate;
            text.rectTransform.localScale = Vector3.one;
            text.rectTransform.localRotation = Quaternion.identity;
            text.raycastTarget = false;
            text.characterSpacing = 0f;

            return text;
        }

        private float updateArraylistTimer;
        private void LateUpdate()
        {
            try
            {
                if (!UpdateHudLayout())
                    return;
                RebuildNotificationText();
                UpdateNotificationExpiry();

                bool seeThrough = true;

                try
                {
                    MenuTypography.Configure(arraylistText);
                    arraylistText.SafeSetFontSize(arraylistScale);
                    arraylistText.Chams(seeThrough);

                    MenuTypography.Configure(notificationText);
                    notificationText.gameObject.SetActive(!disableNotifications);
                    notificationText.Chams(seeThrough);
                    notificationText.fontMaterial.renderQueue = 4051;

                    MenuTypography.Configure(informationText);
                    informationText.SafeSetFontSize(overlayScale);
                    informationText.Chams(seeThrough);

                    arraylistText.characterSpacing = 0f;
                    notificationText.characterSpacing = 0f;
                    informationText.characterSpacing = 0f;
                }
                catch { }

                arraylistText.alignment = flipArraylist ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
                informationText.alignment = flipArraylist ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight;

                if (information.Count > 0)
                {
                    Color targetColor = textColors[1].GetCurrentColor();

                    List<string> statsLines = information
                        .Select(item => $"<color=#{ColorToHex(targetColor)}>{item.Key}</color> <color=#{ColorToHex(textColors[1].GetColor(0))}>{item.Value}</color>")
                        .Select(line => (text: line, width: informationText.GetPreferredValues(NoRichtextTags(line)).x))
                        .OrderByDescending(t => t.width)
                        .Select(t => t.text)
                        .ToList();

                    informationText.SafeSetText(string.Join("\n", statsLines));
                    informationText.color = Color.white;
                }
                else if (!informationText.text.IsNullOrEmpty())
                    informationText.SafeSetText("");

                if (showEnabledModsVR && UnityEngine.XR.XRSettings.isDeviceActive)
                {
                    if (Time.time > updateArraylistTimer)
                    {
                        updateArraylistTimer = Time.time + (advancedArraylist ? 0.1f : 0.5f);
                        List<string> enabledMods = enabledModsBuffer;
                        enabledMods.Clear();
                        int categoryIndex = 0;

                        foreach (ButtonInfo[] buttonList in Buttons.buttons)
                        {
                            foreach (ButtonInfo button in buttonList)
                            {
                                try
                                {
                                    if (Buttons.buttons[Buttons.GetCategory("Temporary Category")].Contains(button))
                                        continue;

                                    if (!button.enabled || (hideSettings && (!hideSettings ||
                                                                             Buttons.categoryNames[categoryIndex]
                                                                                 .Contains("Settings")))) continue;
                                    string buttonText = button.overlapText ?? button.buttonText;

                                    if (inputTextColor != "green")
                                        buttonText = buttonText.Replace(" <color=grey>[</color><color=green>", " <color=grey>[</color><color=" + inputTextColor + ">");

                                    buttonText = FixTMProTags(buttonText);
                                    buttonText = FollowMenuSettings(buttonText);
                                    enabledMods.Add(buttonText);
                                }
                                catch { }
                            }
                            categoryIndex++;
                        }

                        string[] sortedMods = enabledMods
                            .Select(s => (text: s, width: arraylistText.GetPreferredValues(NoRichtextTags(s)).x))
                            .OrderByDescending(t => t.width)
                            .Select(t => t.text)
                            .ToArray();

                        var sb = arraylistBuilder;
                        sb.Clear();
                        for (int i = 0; i < sortedMods.Length; i++)
                        {
                            sb.Append(sortedMods[i]).Append('\n');
                        }
                        arraylistText.SafeSetText(sb.ToString());

                        arraylistText.color = textColors[1].GetColor(0);
                    }
                }
                else if (!arraylistText.text.IsNullOrEmpty())
                    arraylistText.SafeSetText("");

                if (lowercaseMode)
                {
                    if (!arraylistText.text.IsNullOrEmpty())
                        arraylistText.SafeSetText(arraylistText.text.ToLower());

                    if (!informationText.text.IsNullOrEmpty())
                        informationText.SafeSetText(informationText.text.ToLower());
                }

                if (uppercaseMode)
                {
                    if (!arraylistText.text.IsNullOrEmpty())
                        arraylistText.SafeSetText(arraylistText.text.ToUpper());

                    if (!informationText.text.IsNullOrEmpty())
                        informationText.SafeSetText(informationText.text.ToUpper());
                }
            }
            catch (Exception e) { LogManager.Log(e); }
        }

        private void OnEnable()
        {
            if (renderCallbacksRegistered)
                return;
            Application.onBeforeRender += RefreshHudAnchor;
            Canvas.willRenderCanvases += RefreshHudAnchor;
            Camera.onPreCull += OnCameraPreCull;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            renderCallbacksRegistered = true;
        }

        private void OnDisable()
        {
            if (renderCallbacksRegistered)
            {
                Application.onBeforeRender -= RefreshHudAnchor;
                Canvas.willRenderCanvases -= RefreshHudAnchor;
                Camera.onPreCull -= OnCameraPreCull;
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                renderCallbacksRegistered = false;
            }
            if (canvas != null)
                canvas.SetActive(false);
        }

        private static Camera CurrentHudCamera()
        {
            return !UnityEngine.XR.XRSettings.isDeviceActive && TPC != null && TPC.isActiveAndEnabled
                ? TPC : Camera.main;
        }

        private void OnCameraPreCull(Camera camera)
        {
            if (camera == hudCamera || camera == CurrentHudCamera())
                RefreshHudAnchor();
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            OnCameraPreCull(camera);
        }

        private void RefreshHudAnchor()
        {
            if (!isActiveAndEnabled || updatingHud)
                return;
            try
            {
                UpdateHudLayout();
            }
            catch (Exception error)
            {
                LogManager.Log(error);
            }
        }

        private bool UpdateHudLayout()
        {
            if (updatingHud)
                return canvas != null && canvas.activeInHierarchy;
            updatingHud = true;
            try
            {
                Camera currentCamera = CurrentHudCamera();
                if (currentCamera == null || !currentCamera.isActiveAndEnabled)
                {
                    if (canvas != null)
                        canvas.SetActive(false);
                    return false;
                }
                hudCamera = currentCamera;
                if (!hasInitialized || canvas == null)
                {
                    Init();
                    hasInitialized = true;
                }
                if (canvas.transform.parent != hudCamera.transform)
                    canvas.transform.SetParent(hudCamera.transform, false);
                canvas.SetActive(true);
                canvasComponent.worldCamera = hudCamera;
                int visibleLayer = (hudCamera.cullingMask & (1 << HudLayer)) != 0 ? HudLayer : 0;
                SetLayer(canvas, visibleLayer);
                canvasScaler.dynamicPixelsPerUnit = 2f;
                float distance = Mathf.Max(0.8f, hudCamera.nearClipPlane + 0.1f);
                float viewHeight = hudCamera.orthographic ? hudCamera.orthographicSize * 2f
                    : 2f * distance * Mathf.Tan(hudCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
                float aspect = Mathf.Max(0.25f, hudCamera.aspect);
                float canvasHeight = 1000f / aspect;
                canvasRect.sizeDelta = new Vector2(1000f, canvasHeight);
                Transform cameraTransform = hudCamera.transform;
                Vector3 cameraScale = cameraTransform.lossyScale;
                float worldScale = viewHeight * aspect / 1000f;
                canvas.transform.localPosition = cameraTransform.InverseTransformPoint(cameraTransform.position + cameraTransform.forward * distance);
                canvas.transform.localRotation = Quaternion.identity;
                canvas.transform.localScale = new Vector3(
                    worldScale / Mathf.Max(0.0001f, Mathf.Abs(cameraScale.x)),
                    worldScale / Mathf.Max(0.0001f, Mathf.Abs(cameraScale.y)),
                    worldScale / Mathf.Max(0.0001f, Mathf.Abs(cameraScale.z)));
                bool vr = UnityEngine.XR.XRSettings.isDeviceActive;
                float pixelsPerUnit = Mathf.Max(1f, hudCamera.pixelWidth) / 1000f;
                float notificationFontSize = vr ? canvasHeight * 0.016f
                    : Mathf.Clamp(20f * hudCamera.pixelHeight / 1080f, 16f, 24f) / pixelsPerUnit;
                float notificationWidth = vr ? 560f : Mathf.Min(640f, hudCamera.pixelWidth * 0.45f) / pixelsPerUnit;
                float notificationHeight = vr ? Mathf.Min(180f, canvasHeight * 0.22f)
                    : Mathf.Min(150f * hudCamera.pixelHeight / 1080f, hudCamera.pixelHeight * 0.25f) / pixelsPerUnit;
                AnchorText(notificationText, false, false, new Vector2(notificationWidth, notificationHeight));
                float margin = vr ? 24f : Mathf.Max(12f, 24f * hudCamera.pixelHeight / 1080f) / pixelsPerUnit;
                float bottomMargin = vr ? 24f : Mathf.Max(36f, 56f * hudCamera.pixelHeight / 1080f) / pixelsPerUnit;
                notificationText.rectTransform.anchoredPosition = new Vector2(margin, bottomMargin);
                notificationText.SafeSetFontSize(notificationFontSize);
                AnchorText(arraylistText, flipArraylist, true, new Vector2(450f, canvasHeight * 0.55f));
                AnchorText(informationText, !flipArraylist, true, new Vector2(450f, canvasHeight * 0.55f));
                return true;
            }
            finally
            {
                updatingHud = false;
            }
        }
        private static int hudLayer = -1;
        private static int HudLayer
        {
            get
            {
                if (hudLayer < 0)
                    hudLayer = Mathf.Max(0, LayerMask.NameToLayer("FirstPersonOnly"));

                return hudLayer;
            }
        }

        private static void SetLayer(GameObject target, int layer)
        {
            if (target == null)
                return;
            if (target.layer != layer)
                target.layer = layer;

            Transform targetTransform = target.transform;
            for (int i = 0; i < targetTransform.childCount; i++)
                SetLayer(targetTransform.GetChild(i).gameObject, layer);
        }

        private static void AnchorText(TMP_Text text, bool right, bool top, Vector2 size)
        {
            RectTransform rect = text.rectTransform;
            Vector2 anchor = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = new Vector2(right ? -24f : 24f, top ? -24f : 24f);
            rect.sizeDelta = size;
        }

        public static void SendNotification(string notificationText, int clearTime = -1)
        {
            if (clearTime < 0)
                clearTime = notificationDecayTime;

            if (disableNotifications || string.IsNullOrWhiteSpace(notificationText)) return;
            try
            {
                bool errorNotification = notificationText.Contains("<color=red>ERROR</color>");
                if (inputTextColor != "green")
                    notificationText = notificationText.Replace("<color=green>", "<color=" + inputTextColor + ">");

                notificationText = FixTMProTags(notificationText);
                notificationText = StyleNotification(notificationText);

                if (hideBrackets)
                    notificationText = notificationText.Replace("[", "").Replace("]", "");

                notificationText = notificationText.TrimEnd('\n', '\r');

                float duration = Mathf.Max(0.1f, clearTime / 1000f);
                bool ready = Instance != null && Instance.hasInitialized && Instance.canvas != null
                    && Instance.canvas.activeInHierarchy && Instance.hudCamera != null && Instance.hudCamera.isActiveAndEnabled
                    && NotificationManager.notificationText != null;
                float expireTime = ready ? Time.unscaledTime + duration : float.PositiveInfinity;
                NotificationEntry lastEntry = activeNotifications.Count > 0 ? activeNotifications[^1] : null;

                if (lastEntry != null && PreviousNotifi == notificationText && stackNotifications)
                {
                    lastEntry.Counter++;
                    lastEntry.ExpireTime = expireTime;
                    lastEntry.Duration = duration;
                }
                else
                {
                    PreviousNotifi = notificationText;

                    activeNotifications.Add(new NotificationEntry
                    {
                        BaseDisplay = notificationText,
                        Counter = 0,
                        ExpireTime = expireTime,
                        Duration = duration
                    });
                    if (activeNotifications.Count > 8)
                        activeNotifications.RemoveAt(0);
                }

                RebuildNotificationText();
                Camera soundCamera = Instance != null ? Instance.hudCamera : Camera.main;
                if (soundCamera != null && (!soundOnError || errorNotification) && Time.time > timeMenuStarted + 5f)
                    SoundManager.Play(SoundManager.DefaultSounds["Notification"], action: clip =>
                    {
                        if (soundCamera != null)
                            AudioSource.PlayClipAtPoint(clip, soundCamera.transform.position, buttonClickVolume / 10f);
                    });
            }
            catch (Exception e)
            {
                LogManager.LogError($"Notification failed.\nNotification: {notificationText}\nException: {e.Message}");
            }
        }

        private static readonly Regex tagPattern = new Regex(
            @"^<color=grey>\[</color><color=(?<tone>[^>]+)>(?<label>[^<]+)</color><color=grey>\]</color>\s*",
            RegexOptions.Compiled);

        private static string StyleNotification(string text)
        {
            Match match = tagPattern.Match(text);
            if (!match.Success)
                return text;

            return text.Substring(match.Length);
        }

        public static void ClearAllNotifications()
        {
            activeNotifications.Clear();
            PreviousNotifi = null;
            RebuildNotificationText();
        }

        private static void UpdateNotificationExpiry()
        {
            if (activeNotifications.Count == 0)
                return;

            bool changed = false;
            for (int i = activeNotifications.Count - 1; i >= 0; i--)
            {
                if (float.IsPositiveInfinity(activeNotifications[i].ExpireTime))
                    activeNotifications[i].ExpireTime = Time.unscaledTime + activeNotifications[i].Duration;
                if (Time.unscaledTime >= activeNotifications[i].ExpireTime)
                {
                    activeNotifications.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
                RebuildNotificationText();
        }

        private static void RebuildNotificationText()
        {
            if (notificationText == null)
                return;
            List<string> lines = notificationLines;
            lines.Clear();
            foreach (NotificationEntry entry in activeNotifications)
            {
                string message = entry.Counter > 0
                    ? $"{entry.BaseDisplay} <color=grey>(x{entry.Counter + 1})</color>"
                    : entry.BaseDisplay;
                if (noRichText)
                    message = NoRichtextTags(message);
                string prefix = notificationText.spriteAsset != null ? "<sprite name=\"bell\"> " : "";
                lines.Add(prefix + "<color=#A0A0A0>|</color> " + FollowMenuSettings(message));
            }

            string CombineLines()
            {
                string value = string.Join(Environment.NewLine, lines);
                return value;
            }
            string combined = CombineLines();
            while (lines.Count > 1 && notificationText.GetPreferredValues(combined,
                notificationText.rectTransform.rect.width, Mathf.Infinity).y > notificationText.rectTransform.rect.height)
            {
                lines.RemoveAt(0);
                combined = CombineLines();
            }

            notificationText.SafeSetText(combined);
            notificationText.richText = true;
        }

        private void OnDestroy()
        {
            OnDisable();
            NotificationIcon.Release(notificationIcons);
            if (canvas != null)
                Destroy(canvas);
            if (Instance != this)
                return;
            Instance = null;
            notificationText = null;
            arraylistText = null;
            informationText = null;
            activeNotifications.Clear();
            notificationLines.Clear();
            PreviousNotifi = null;
        }
    }
}

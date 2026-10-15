using Interstellar.Classes.Menu;
using Interstellar.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Interstellar.Menu.Main;
using static Interstellar.Utilities.AssetUtilities;

namespace Interstellar.Menu
{
    public class UI : MonoBehaviour
    {
        public static Texture2D watermarkImage;

        private void Awake()
        {
            uiPrefab = LoadObject<GameObject>("UI");
            if (uiPrefab == null)
                return;

            Transform canvas = uiPrefab.transform.Find("Canvas");
            if (canvas == null)
                return;

            watermark = canvas.Find("Watermark")?.GetComponent<Image>();
            canvas.Find("VersionLabel")?.gameObject.SetActive(false);
            roomStatus = canvas.Find("RoomStatus")?.GetComponent<TextMeshProUGUI>();
            arraylist = canvas.Find("Arraylist")?.GetComponent<TextMeshProUGUI>();
            canvas.Find("ControlUI")?.gameObject.SetActive(false);

            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                MenuTypography.Configure(text);
                text.characterSpacing = 0f;
            }

            debugUI = canvas.Find("DebugUI")?.gameObject;
            if (debugUI != null)
                debugUI.AddComponent<UIDragWindow>();

            templateLine = debugUI?.transform.Find("Lines/Line")?.gameObject;

            TMP_InputField inputField = debugUI?.transform.Find("TextInput")?.gameObject.GetComponent<TMP_InputField>();

            if (inputField != null)
            {
                inputField.fontAsset = MenuTypography.Font;
                inputField.onSelect.AddListener(_ => { focusedOnDebug = true; HasKeyboardFocus = true; });
                inputField.onDeselect.AddListener(_ => { focusedOnDebug = false; HasKeyboardFocus = false; });

                inputField.onSubmit.AddListener((string text) =>
                {
                    if (!string.IsNullOrWhiteSpace(text))
                        HandleDebugCommand(text);

                    inputField.text = string.Empty;
                });
            }

            textObjects = new List<TextMeshProUGUI>
            {
                canvas.Find("HideMessage")?.GetComponent<TextMeshProUGUI>()
            }.Where(textObject => textObject != null).ToList();

            imageObjects = new List<Image>
            {
                debugUI?.transform.Find("TextInput")?.GetComponent<Image>(),
                debugUI?.transform.Find("Lines")?.GetComponent<Image>()
            }.Where(imageObject => imageObject != null).ToList();

            if (watermark != null)
            {
                watermark.material = null;
                watermark.type = Image.Type.Simple;
                watermark.preserveAspect = true;
                watermark.useSpriteMesh = false;
                watermark.raycastTarget = false;
            }

            watermarkImage = BrandArtwork.ModelLogoTexture
                ?? LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.logo.png");

            if (watermarkImage != null)
            {
                watermarkImage.filterMode = FilterMode.Trilinear;
                watermarkImage.anisoLevel = 1;
                watermarkImage.wrapMode = TextureWrapMode.Clamp;
                watermarkImage.Apply(true, false);
            }

            if (!Bootstrapper.FirstLaunch)
            {
                GameObject closeMessage = uiPrefab.transform.Find("Canvas")?.Find("HideMessage")?.gameObject;
                closeMessage?.SetActive(false);
            }

            Update();
        }

        private bool isOpen = true;
        private bool focusedOnDebug;
        public static bool HasKeyboardFocus { get; private set; }

        private GameObject uiPrefab;
        private GameObject debugUI;

        private Image watermark;
        private TextMeshProUGUI roomStatus;
        private TextMeshProUGUI arraylist;

        private List<TextMeshProUGUI> textObjects = new List<TextMeshProUGUI>();
        private List<Image> imageObjects = new List<Image>();

        private float uiUpdateDelay;

        private bool lastInRoom;
        private string lastRoomName;
        private bool roomStatusInitialized;

        private void Update()
        {
            if (uiPrefab == null)
                return;
            HasKeyboardFocus = isOpen && debugUI != null && debugUI.activeInHierarchy && focusedOnDebug;

            if (Keyboard.current?.backslashKey.wasPressedThisFrame == true)
                ToggleGUI();

            if (isOpen)
            {
                uiPrefab.SetActive(true);

                if (Keyboard.current?.backquoteKey.wasPressedThisFrame == true)
                    ToggleDebug();

                Color guiColor = textColors[1].GetCurrentColor();

                if (roomStatus != null)
                    roomStatus.color = guiColor;

                if (arraylist != null)
                    arraylist.color = guiColor;

                if (watermark != null)
                {
                    watermark.color = guiColor;
                    watermark.gameObject.SetActive(!disableWatermark);
                }

                MenuTypography.Configure(roomStatus);
                MenuTypography.Configure(arraylist);

                foreach (var textObject in textObjects)
                {
                    if (textObject == null)
                        continue;

                    textObject.color = textColors[1].GetCurrentColor();
                    MenuTypography.Configure(textObject);
                }

                foreach (var imageObject in imageObjects)
                {
                    if (imageObject != null)
                        imageObject.color = buttonColors[0].GetCurrentColor();
                }

                NetworkSystem network = NetworkSystem.Instance;
                bool inRoom = Interstellar.Mods.Room.IsConnected(network);
                string roomName = inRoom ? network.RoomName : "";

                if (!roomStatusInitialized || inRoom != lastInRoom || roomName != lastRoomName)
                {
                    roomStatusInitialized = true;
                    lastInRoom = inRoom;
                    lastRoomName = roomName;
                    roomStatus.SafeSetText(FollowMenuSettings(!inRoom ? "Not connected to room" : "Connected to room ") + roomName);
                }

                if (debugUI != null && debugUI.activeSelf)
                {
                    Image debugImage = debugUI.GetComponent<Image>();
                    if (debugImage != null)
                        debugImage.color = backgroundColor.GetCurrentColor();

                    List<TextMeshProUGUI> debugTextObjects = new List<TextMeshProUGUI>
                    {
                        debugUI.transform.Find("Title")?.GetComponent<TextMeshProUGUI>(),
                        debugUI.transform.Find("TextInput/Text Area/Text")?.GetComponent<TextMeshProUGUI>(),
                        debugUI.transform.Find("TextInput/Text Area/Placeholder")?.GetComponent<TextMeshProUGUI>()
                    }.Where(textObject => textObject != null).ToList();

                    Transform lines = debugUI.transform.Find("Lines");
                    if (lines != null)
                        debugTextObjects.AddRange(lines.GetComponentsInChildren<TextMeshProUGUI>());

                    foreach (var textObject in debugTextObjects)
                    {
                        textObject.color = textColors[1].GetCurrentColor();
                        MenuTypography.Configure(textObject);
                    }

                    TextMeshProUGUI title = debugUI.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
                    if (title != null)
                        title.color = textColors[0].GetCurrentColor();
                }

                if (!(Time.time > uiUpdateDelay)) return;

                Texture2D watermarkTexture = customWatermark ?? watermarkImage;

                if (watermark != null && watermarkTexture != null && (watermark.sprite == null || watermark.sprite.texture == null || watermark.sprite.texture != watermarkTexture))
                    ApplyWatermark(watermarkTexture);

                if (flipArraylist)
                {
                    if (arraylist != null)
                    {
                        arraylist.rectTransform.anchoredPosition = new Vector2(-837.5001f, -523f);
                        arraylist.rectTransform.anchorMin = new Vector2(1f, 1f);
                        arraylist.rectTransform.anchorMax = new Vector2(1f, 1f);

                        arraylist.alignment = TextAlignmentOptions.TopRight;
                    }
                }
                else
                {
                    if (arraylist != null)
                    {
                        arraylist.rectTransform.anchoredPosition = new Vector2(837.5001f, -523f);
                        arraylist.rectTransform.anchorMin = new Vector2(0f, 1f);
                        arraylist.rectTransform.anchorMax = new Vector2(0f, 1f);

                        arraylist.alignment = TextAlignmentOptions.TopLeft;
                    }
                }

                uiUpdateDelay = Time.time + (advancedArraylist ? 0.1f : 0.5f);

                List<string> enabledMods = new List<string>();
                int categoryIndex = 0;

                foreach (ButtonInfo[] buttonList in Buttons.buttons)
                {
                    foreach (ButtonInfo button in buttonList)
                    {
                        try
                        {
                            if (button == null || !button.enabled || (hideSettings && Buttons.categoryNames[categoryIndex].Contains("Settings")))
                                continue;

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
                    .Select(s => (text: s, width: arraylist != null ? arraylist.GetPreferredValues(NoRichtextTags(s)).x : s.Length))
                    .OrderByDescending(t => t.width)
                    .Select(t => t.text)
                    .ToArray();

                string modListText = "";
                for (int i = 0; i < sortedMods.Length; i++)
                {
                    if (advancedArraylist)
                        modListText += (flipArraylist ?
                             $"<mark=#{ColorToHex(backgroundColor.GetCurrentColor(i * -0.1f))}C0> {sortedMods[i]} </mark><mark=#{ColorToHex(buttonColors[1].GetCurrentColor(i * -0.1f))}> </mark>" :
                             $"<mark=#{ColorToHex(buttonColors[1].GetCurrentColor(i * -0.1f))}> </mark><mark=#{ColorToHex(backgroundColor.GetCurrentColor(i * -0.1f))}C0> {sortedMods[i]} </mark>") + "\n";
                    else
                        modListText += sortedMods[i] + "\n";
                }

                arraylist.SafeSetText(modListText);
            }
            else
                uiPrefab.SetActive(false);
        }

        private const float watermarkHeight = 86f;
        private const float watermarkMargin = 18f;
        private Sprite hudWatermarkSprite;
        private Material hudWatermarkMaterial;

        private void ApplyWatermark(Texture2D texture)
        {
            bool modelLogo = texture == BrandArtwork.ModelLogoTexture;
            Rect bounds = modelLogo ? BrandArtwork.ModelLogoBounds : OpaqueBounds(texture);
            float padding = Mathf.Max(4f, Mathf.Ceil(Mathf.Max(bounds.width, bounds.height) * 0.02f));
            Rect paddedBounds = Rect.MinMaxRect(Mathf.Max(0f, bounds.xMin - padding),
                Mathf.Max(0f, bounds.yMin - padding), Mathf.Min(texture.width, bounds.xMax + padding),
                Mathf.Min(texture.height, bounds.yMax + padding));

            if (hudWatermarkSprite != null)
                Destroy(hudWatermarkSprite);
            hudWatermarkSprite = Sprite.Create(texture, paddedBounds, new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            watermark.sprite = hudWatermarkSprite;
            watermark.type = Image.Type.Simple;
            watermark.useSpriteMesh = false;
            if (hudWatermarkMaterial != null)
                Destroy(hudWatermarkMaterial);
            hudWatermarkMaterial = modelLogo ? BrandArtwork.CreateLogoMaterial() : null;
            watermark.material = hudWatermarkMaterial;

            RectTransform watermarkTransform = watermark.rectTransform;
            watermarkTransform.anchorMin = new Vector2(1f, 0f);
            watermarkTransform.anchorMax = new Vector2(1f, 0f);
            watermarkTransform.pivot = new Vector2(1f, 0f);
            float artworkScale = watermarkHeight / Mathf.Max(1f, bounds.height);
            watermarkTransform.sizeDelta = paddedBounds.size * artworkScale;
            watermarkTransform.anchoredPosition = new Vector2(-watermarkMargin, watermarkMargin);
        }

        private void OnDestroy()
        {
            HasKeyboardFocus = false;
            if (uiPrefab != null)
                Destroy(uiPrefab);
            if (hudWatermarkSprite != null)
                Destroy(hudWatermarkSprite);
            if (hudWatermarkMaterial != null)
                Destroy(hudWatermarkMaterial);
        }

        private void ToggleGUI()
        {
            isOpen = !isOpen;
            focusedOnDebug = false;
            HasKeyboardFocus = false;

            GameObject closeMessage = uiPrefab?.transform.Find("Canvas")?.Find("HideMessage")?.gameObject;
            closeMessage?.SetActive(false);
        }

        private void ToggleDebug()
        {
            if (debugUI == null)
                return;

            if (debugUI.activeSelf)
            {
                debugUI.SetActive(false);
                focusedOnDebug = false;
                HasKeyboardFocus = false;
            }
            else
            {
                if (dynamicSounds)
                    LoadSoundFromResource("Audio/Menu/console.ogg", clip => clip.Play(buttonClickVolume / 10f));

                debugUI.SetActive(true);
            }
        }

        private GameObject templateLine;
        public void DebugPrint(string text)
        {
            if (debugUI == null || templateLine == null || !debugUI.activeSelf)
                return;

            Transform lines = debugUI.transform.Find("Lines");
            if (lines == null)
                return;

            GameObject line = Instantiate(templateLine, lines, false);
            line.SetActive(true);

            TextMeshProUGUI lineText = line.GetComponent<TextMeshProUGUI>();
            if (lineText != null)
            {
                MenuTypography.Configure(lineText);
                lineText.characterSpacing = 0f;
                lineText.text = text;
            }

            if (lines.childCount > 14)
                Destroy(lines.GetChild(1).gameObject);
        }

        public void HandleDebugCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            string[] args = command.Split(' ');
            string commandName = args[0].ToLower();

            switch (commandName)
            {
                case "print":
                    {
                        DebugPrint(args.Skip(1).Join(" "));
                        break;
                    }
                case "beta":
                    {
                        PluginInfo.BetaBuild = args.Length > 1 && args[1].ToLower() == "true";
                        DebugPrint($"PluginInfo.BetaBuild is now {PluginInfo.BetaBuild}");
                        break;
                    }
                case "prompt":
                    {
                        MatchCollection matches = Regex.Matches(args.Skip(1).Join(" "), @"\[(.*?)\]");
                        List<string> results = matches.Select(matches => matches.Groups).SelectMany(group => group).Select(group => group.Value).ToList();

                        string promptText = results.Count > 1 ? results[1] : args.Length > 1 ? args[1] : "Prompt text";
                        string acceptText = results.Count > 3 ? results[3] : args.Length > 2 ? args[2] : "Accept";
                        string declineText = results.Count > 5 ? results[5] : args.Length > 3 ? args[3] : "Decline";

                        Prompt(promptText, () => DebugPrint("Prompt accepted"), () => DebugPrint("Prompt declined"), acceptText, declineText);
                        DebugPrint($"Prompted user {promptText} {acceptText} {declineText}");

                        break;
                    }
                case "exit":
                case "quit":
                case "close":
                    {
                        Application.Quit();
                        break;
                    }
                default:
                    {
                        DebugPrint($"Unknown command: '{commandName}'");
                        break;
                    }
            }
        }
    }
}

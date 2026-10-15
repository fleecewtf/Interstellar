using GorillaNetworking;
using Interstellar.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using static Interstellar.Menu.Main;

namespace Interstellar.Managers
{
    public class CustomBoardManager : MonoBehaviour
    {
        public static CustomBoardManager instance;
        public void Awake()
        {
            instance = this;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            RestoreTriggerMaterials();
            foreach (GameObject board in objectBoards.Values)
                if (board != null) Destroy(board);
            objectBoards.Clear();
            RestoreMotd();
            if (instance == this) instance = null;
        }

        private static bool _customBoardsEnabled = true;
        public static bool CustomBoardsEnabled
        {
            get => _customBoardsEnabled;
            set
            {
                _customBoardsEnabled = value;
                if (instance == null) return;

                if (value)
                {
                    instance.ReloadBoards();
                    if (instance.motdTitle != null) instance.motdTitle.SetActive(true);
                    if (instance.motdText != null) instance.motdText.SetActive(true);

                    GetObject("Environment Objects/LocalObjects_Prefab/TreeRoom/motdBodyText").SetActive(false);
                    GetObject("Environment Objects/LocalObjects_Prefab/TreeRoom/motdHeadingText").SetActive(false);
                }
                else
                {
                    instance.RestoreTriggerMaterials();

                    foreach (GameObject board in instance.objectBoards.Values)
                        Destroy(board);

                    instance.objectBoards.Clear();

                    if (instance.motdTitle != null) instance.motdTitle.SetActive(false);
                    if (instance.motdText != null) instance.motdText.SetActive(false);

                    GetObject("Environment Objects/LocalObjects_Prefab/TreeRoom/motdHeadingText").SetActive(true);
                    GetObject("Environment Objects/LocalObjects_Prefab/TreeRoom/motdBodyText").SetActive(true);
                }
            }
        }

        private static readonly Dictionary<TextMeshPro, float> characterDistanceArchive = new Dictionary<TextMeshPro, float>();

        private static bool _customBoardFonts;
        public static bool CustomBoardFonts
        {
            get => _customBoardFonts;
            set
            {
                if (!value && _customBoardFonts)
                {
                    foreach (TextMeshPro txt in instance.textMeshPro)
                    {
                        if (!txt.isActiveAndEnabled) continue;

                        txt.SafeSetFont(instance.archiveGorillaTagFont);
                        txt.SafeSetFontStyle(FontStyles.Normal);

                        if (characterDistanceArchive.TryGetValue(txt, out float charDistance))
                            txt.characterSpacing = charDistance;
                    }
                }

                _customBoardFonts = value;
            }
        }

        public static bool CustomBoardTextEnabled = true;
        private static Material _boardMaterial;
        public static Material BoardMaterial
        {
            get => _boardMaterial ??= CreateBoardMaterial();
            set
            {
                _boardMaterial = value == null ? CreateBoardMaterial() : new Material(value);
                MakeOpaque(_boardMaterial);
                if (instance != null) instance.ReloadBoards();
            }
        }

        private static Material CreateBoardMaterial()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                name = "Interstellar_OpaqueBoard",
                color = Color.black
            };
            MakeOpaque(material);
            return material;
        }

        private static void MakeOpaque(Material material)
        {
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
            Color color = material.color;
            color.a = 1f;
            material.color = color;
        }

        private readonly Dictionary<JoinTriggerUITemplate, Material[]> triggerMaterials =
            new Dictionary<JoinTriggerUITemplate, Material[]>();

        private void RestoreTriggerMaterials()
        {
            foreach (KeyValuePair<JoinTriggerUITemplate, Material[]> entry in triggerMaterials)
            {
                JoinTriggerUITemplate template = entry.Key;
                if (template == null) continue;
                Material[] materials = entry.Value;
                template.ScreenBG_AbandonPartyAndSoloJoin = materials[0];
                template.ScreenBG_AlreadyInRoom = materials[1];
                template.ScreenBG_ChangingGameModeSoloJoin = materials[2];
                template.ScreenBG_Error = materials[3];
                template.ScreenBG_InPrivateRoom = materials[4];
                template.ScreenBG_LeaveRoomAndGroupJoin = materials[5];
                template.ScreenBG_LeaveRoomAndSoloJoin = materials[6];
                template.ScreenBG_NotConnectedSoloJoin = materials[7];
            }
            triggerMaterials.Clear();
            if (PhotonNetworkController.Instance != null)
                PhotonNetworkController.Instance.UpdateTriggerScreens();
        }

        private void RestoreMotd()
        {
            if (motdTitle != null) Destroy(motdTitle);
            if (motdText != null) Destroy(motdText);
            GameObject heading = GetObject("Environment Objects/LocalObjects_Prefab/TreeRoom/motdHeadingText");
            GameObject body = GetObject("Environment Objects/LocalObjects_Prefab/TreeRoom/motdBodyText");
            if (heading != null) heading.SetActive(true);
            if (body != null) body.SetActive(true);
        }

        public static bool motdTextDirty = true;
        public const string motdTemplate = "You are using Interstellar Menu {0} beta. " +
        "Report any bugs to the person who shared this build with you.\n\n" +
        "<alpha=128>Build {3}<alpha=255>";

        public GameObject motdTitle;
        public GameObject motdText;

        private TMP_FontAsset archiveGorillaTagFont;

        private string cachedMotdHeading;
        private string cachedMotdBody;
        private bool hasFoundAllBoards;
        public void ReloadBoards() =>
            hasFoundAllBoards = false;

        private void RebuildMotdText()
        {
            cachedMotdHeading = FollowMenuSettings($"Thanks for using {(doCustomName ? customMenuName : menuName)}!");
            cachedMotdBody = FollowMenuSettings(string.Format(motdTemplate, PluginInfo.Version, fullModAmount, PluginInfo.BetaBuild ? "Beta" : "Release", PluginInfo.BuildTimestamp));
            motdTextDirty = fullModAmount == null;
        }

        public void Update()
        {
            if (!CustomBoardsEnabled) return;
            if (!hasFoundAllBoards)
            {
                try
                {
                    foreach (GameObject board in objectBoards.Values)
                        Destroy(board);

                    objectBoards.Clear();

                    foreach (GorillaNetworkJoinTrigger joinTrigger in PhotonNetworkController.Instance.allJoinTriggers)
                    {
                        try
                        {
                            JoinTriggerUI ui = joinTrigger.ui;
                            JoinTriggerUITemplate temp = ui.template;

                            if (!triggerMaterials.ContainsKey(temp))
                                triggerMaterials[temp] = new[] {
                                    temp.ScreenBG_AbandonPartyAndSoloJoin, temp.ScreenBG_AlreadyInRoom,
                                    temp.ScreenBG_ChangingGameModeSoloJoin, temp.ScreenBG_Error,
                                    temp.ScreenBG_InPrivateRoom, temp.ScreenBG_LeaveRoomAndGroupJoin,
                                    temp.ScreenBG_LeaveRoomAndSoloJoin, temp.ScreenBG_NotConnectedSoloJoin
                                };

                            temp.ScreenBG_AbandonPartyAndSoloJoin = BoardMaterial;
                            temp.ScreenBG_AlreadyInRoom = BoardMaterial;
                            temp.ScreenBG_ChangingGameModeSoloJoin = BoardMaterial;
                            temp.ScreenBG_Error = BoardMaterial;
                            temp.ScreenBG_InPrivateRoom = BoardMaterial;
                            temp.ScreenBG_LeaveRoomAndGroupJoin = BoardMaterial;
                            temp.ScreenBG_LeaveRoomAndSoloJoin = BoardMaterial;
                            temp.ScreenBG_NotConnectedSoloJoin = BoardMaterial;

                            TextMeshPro text = ui.screenText;
                            if (!textMeshPro.Contains(text))
                                textMeshPro.Add(text);
                        }
                        catch { }
                    }
                    PhotonNetworkController.Instance.UpdateTriggerScreens();

                    string[] objectsWithTMPro = {
                            "Environment Objects/LocalObjects_Prefab/TreeRoom/CodeOfConductHeadingText",
                            "Environment Objects/LocalObjects_Prefab/TreeRoom/COCBodyText_TitleData",
                            "Environment Objects/LocalObjects_Prefab/TreeRoom/Data",
                            "Environment Objects/LocalObjects_Prefab/TreeRoom/FunctionSelect"
                        };
                    foreach (string objectName in objectsWithTMPro)
                    {
                        GameObject obj = GetObject(objectName);
                        if (obj != null)
                        {
                            TextMeshPro text = obj.GetComponent<TextMeshPro>();
                            if (!textMeshPro.Contains(text))
                                textMeshPro.Add(text);
                        }
                        else
                            LogManager.Log("Could not find " + objectName);
                    }

                    Transform forestTransform = GetObject("Environment Objects/LocalObjects_Prefab/Forest/ForestScoreboardAnchor/GorillaScoreBoard").transform;
                    for (int i = 0; i < forestTransform.transform.childCount; i++)
                    {
                        GameObject v = forestTransform.GetChild(i).gameObject;
                        if ((!v.name.Contains("Board Text") && !v.name.Contains("Scoreboard_OfflineText")) ||
                            !v.activeSelf) continue;
                        TextMeshPro text = v.GetComponent<TextMeshPro>();
                        if (!textMeshPro.Contains(text))
                            textMeshPro.Add(text);
                    }

                    hasFoundAllBoards = true;
                }
                catch (Exception exc)
                {
                    LogManager.LogError($"Error with board colors at {exc.StackTrace}: {exc.Message}");
                    hasFoundAllBoards = false;
                }
            }

            try
            {
                Color boardColor = backgroundColor.GetCurrentColor();
                boardColor.a = 1f;
                BoardMaterial.color = boardColor;

                if (motdTitle == null)
                {
                    GameObject motdObject = GetObject("Environment Objects/LocalObjects_Prefab/TreeRoom/motdHeadingText");
                    motdTitle = Instantiate(motdObject, motdObject.transform.parent);
                    motdObject.SetActive(false);
                }

                TextMeshPro motdHeadingText = motdTitle.GetComponent<TextMeshPro>();
                if (!textMeshPro.Contains(motdHeadingText))
                    textMeshPro.Add(motdHeadingText);

                if (motdTextDirty) RebuildMotdText();

                motdHeadingText.richText = true;
                motdHeadingText.SafeSetFontSize(100);
                motdHeadingText.SafeSetText(cachedMotdHeading);
                motdHeadingText.SafeSetFontStyle(activeFontStyle);
                motdHeadingText.SafeSetFont(activeFont);
                FollowMenuSettings(motdHeadingText, -4f);

                motdHeadingText.SafeSetText(FollowMenuSettings(motdHeadingText.text));

                motdHeadingText.color = textColors[0].GetCurrentColor();
                motdHeadingText.overflowMode = TextOverflowModes.Overflow;

                if (motdText == null)
                {
                    GameObject motdObject = GetObject("Environment Objects/LocalObjects_Prefab/TreeRoom/motdBodyText");
                    motdText = Instantiate(motdObject, motdObject.transform.parent);
                    motdObject.SetActive(false);

                    motdText.GetComponent<PlayFabTitleDataTextDisplay>().enabled = false;
                }

                TextMeshPro motdBodyText = motdText.GetComponent<TextMeshPro>();
                if (!textMeshPro.Contains(motdBodyText))
                    textMeshPro.Add(motdBodyText);

                motdBodyText.richText = true;
                motdBodyText.SafeSetFontSize(100);
                motdBodyText.color = textColors[0].GetCurrentColor();
                motdBodyText.SafeSetFontStyle(activeFontStyle);
                motdBodyText.SafeSetFont(activeFont);
                FollowMenuSettings(motdBodyText, -4f);

                if (motdTextDirty) RebuildMotdText();
                motdBodyText.SafeSetText(cachedMotdBody);
            }
            catch { }

            try
            {
                Color targetColor = textColors[0].GetCurrentColor();

                if (!CustomBoardsEnabled || !CustomBoardTextEnabled)
                    targetColor = Color.white;

                foreach (TextMeshPro txt in textMeshPro)
                {
                    if (!txt.isActiveAndEnabled) continue;

                    txt.color = targetColor;

                    if (!CustomBoardFonts) continue;
                    archiveGorillaTagFont ??= txt.font;

                    if (!characterDistanceArchive.ContainsKey(txt))
                        characterDistanceArchive[txt] = txt.characterSpacing;

                    txt.characterSpacing = 0f;

                    txt.SafeSetFont(activeFont);
                    txt.SafeSetFontStyle(activeFontStyle);
                }
            }
            catch { }
        }



        public readonly Dictionary<string, GameObject> objectBoards = new Dictionary<string, GameObject>();
        public readonly List<TextMeshPro> textMeshPro = new List<TextMeshPro>();

        public void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!CustomBoardsEnabled) return;
            if (!BoardInformations.TryGetValue(scene.name, out var config)) return;

            CreateObjectBoard(scene.name, config.GameObjectPath, config.Position, config.Rotation, config.Scale);
        }

        public void CreateObjectBoard(string scene, string gameObject, Vector3? position = null, Vector3? rotation = null, Vector3? scale = null)
        {
            try
            {
                if (objectBoards.TryGetValue(scene, out GameObject existingBoard))
                {
                    if (existingBoard != null)
                        Destroy(existingBoard);

                    objectBoards.Remove(scene);
                }

                GameObject board = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Plane);
                board.transform.parent = GetObject(gameObject).transform;
                board.transform.localPosition = position ?? new Vector3(-22.1964f, -34.9f, 0.57f);
                board.transform.localRotation = Quaternion.Euler(rotation ?? new Vector3(270f, 0f, 0f));
                board.transform.localScale = scale ?? new Vector3(21.6f, 2.4f, 22f);

                Destroy(board.GetComponent<Collider>());
                board.GetComponent<Renderer>().material = BoardMaterial;

                objectBoards.Add(scene, board);
            }
            catch (Exception e)
            {
                LogManager.LogError($"Failed to create object board for scene {scene}: {e}");
            }
        }

        private readonly struct BoardInformation
        {
            public readonly string GameObjectPath;
            public readonly Vector3 Position;
            public readonly Vector3 Rotation;
            public readonly Vector3 Scale;

            public BoardInformation(string path, Vector3 pos, Vector3 rot, Vector3 scale)
            {
                GameObjectPath = path;
                Position = pos;
                Rotation = rot;
                Scale = scale;
            }
        }

        private static readonly Dictionary<string, BoardInformation> BoardInformations = new Dictionary<string, BoardInformation>
        {
            ["Canyon2"] = new BoardInformation(
                "Canyon/CanyonScoreboardAnchor/GorillaScoreBoard",
                new Vector3(-24.5019f, -28.7746f, 0.1f),
                new Vector3(270f, 0f, 0f),
                new Vector3(21.5946f, 1f, 22.1782f)
            ),
            ["Skyjungle"] = new BoardInformation(
                "skyjungle/UI/Scoreboard/GorillaScoreBoard",
                new Vector3(-21.2764f, -32.1928f, 0f),
                new Vector3(270.2987f, 0.2f, 359.9f),
                new Vector3(21.6f, 0.1f, 20.4909f)
            ),
            ["Mountain"] = new BoardInformation(
                "Mountain/MountainScoreboardAnchor/GorillaScoreBoard",
                Vector3.zero,
                Vector3.zero,
                Vector3.one
            ),
            ["Metropolis"] = new BoardInformation(
                "MetroMain/ComputerArea/Scoreboard/GorillaScoreBoard",
                new Vector3(-25.1f, -31f, 0.1502f),
                new Vector3(270.1958f, 0.2086f, 0f),
                new Vector3(21f, 102.9727f, 21.4f)
            ),
            ["Bayou"] = new BoardInformation(
                "BayouMain/ComputerArea/GorillaScoreBoardPhysical",
                new Vector3(-28.3419f, -26.851f, 0.3f),
                new Vector3(270f, 0f, 0f),
                new Vector3(21.3636f, 38f, 21f)
            ),
            ["Beach"] = new BoardInformation(
                "BeachScoreboardAnchor/GorillaScoreBoard",
                new Vector3(-22.1964f, -33.7126f, 0.1f),
                new Vector3(270.056f, 0f, 0f),
                new Vector3(21.2f, 2f, 21.6f)
            ),
            ["Cave"] = new BoardInformation(
                "Cave_Main_Prefab/CrystalCaveScoreboardAnchor/GorillaScoreBoard",
                new Vector3(-22.1964f, -33.7126f, 0.1f),
                new Vector3(270.056f, 0f, 0f),
                new Vector3(21.2f, 2f, 21.6f)
            ),
            ["Rotating"] = new BoardInformation(
                "RotatingPermanentEntrance/UI (1)/RotatingScoreboard/RotatingScoreboardAnchor/GorillaScoreBoard",
                new Vector3(-22.1964f, -33.7126f, 0.1f),
                new Vector3(270.056f, 0f, 0f),
                new Vector3(21.2f, 2f, 21.6f)
            ),
            ["MonkeBlocks"] = new BoardInformation(
                "Environment Objects/MonkeBlocksRoomPersistent/AtticScoreBoard/AtticScoreboardAnchor/GorillaScoreBoard",
                new Vector3(-22.1964f, -24.5091f, 0.57f),
                new Vector3(270.1856f, 0.1f, 0f),
                new Vector3(21.6f, 1.2f, 20.8f)
            ),
            ["Basement"] = new BoardInformation(
                "Basement/BasementScoreboardAnchor/GorillaScoreBoard/",
                new Vector3(-22.1964f, -24.5091f, 0.57f),
                new Vector3(270.1856f, 0.1f, 0f),
                new Vector3(21.6f, 1.2f, 20.8f)
            ),
            ["City"] = new BoardInformation(
                "City_Pretty/CosmeticsScoreboardAnchor/GorillaScoreBoard",
                new Vector3(-22.1964f, -34.9f, 0.57f),
                new Vector3(270f, 0f, 0f),
                new Vector3(21.6f, 2.4f, 22f)
            )
        };

    }
}

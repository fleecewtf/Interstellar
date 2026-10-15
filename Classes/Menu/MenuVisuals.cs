using Interstellar.Extensions;
using Interstellar.Managers;
using Interstellar.Menu;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interstellar.Classes.Menu
{
    public static class MenuVisuals
    {
        public const int ShellQueue = 2990;
        public const int DisplayQueue = 2991;
        public const int ButtonQueue = 2992;
        public const int ImageQueue = 2993;
        public const int TextQueue = 2994;
        public const int SortingOrder = 0;

        public static int SortingLayerId
        {
            get => 0;
        }

        internal static void ApplySorting(Renderer renderer)
        {
            renderer.sortingLayerID = SortingLayerId;
            renderer.sortingOrder = SortingOrder;
        }

        private static AssetBundle bundle;
        private static Shader surfaceShader;
        private static Shader roundedShader;
        private static Shader textShader;
        private static Mesh quad;
        private static bool failed;

        public static bool Load()
        {
            if (surfaceShader != null && roundedShader != null && textShader != null) return true;
            if (failed) return false;
            try
            {
                using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    PluginInfo.ClientResourcePath + ".menu_visuals");
                if (stream == null) throw new FileNotFoundException("Embedded menu shader bundle is missing.");
                using MemoryStream memory = new MemoryStream();
                stream.CopyTo(memory);
                bundle = AssetBundle.LoadFromMemory(memory.ToArray());
                if (bundle == null) throw new InvalidDataException("Could not load the menu shader bundle.");
                surfaceShader = bundle.LoadAsset<Shader>("Assets/InterstellarMenu/OverlaySurface.shader");
                roundedShader = bundle.LoadAsset<Shader>("Assets/InterstellarMenu/RoundedButton.shader");
                textShader = bundle.LoadAsset<Shader>("Assets/InterstellarMenu/MenuText.shader");
                if (surfaceShader == null || roundedShader == null || textShader == null
                    || !surfaceShader.isSupported || !roundedShader.isSupported || !textShader.isSupported)
                    throw new InvalidDataException("The menu shaders are unavailable for this graphics device.");
                return true;
            }
            catch (Exception error)
            {
                failed = true;
                surfaceShader = null;
                roundedShader = null;
                textShader = null;
                LogManager.LogError("Failed to load menu rendering assets: " + error);
                return false;
            }
        }

        public static bool IsMenuShader(Shader shader) => shader != null
            && shader.name.StartsWith("Interstellar/Menu/", StringComparison.Ordinal);

        public static Material CreateImageMaterial()
        {
            if (!Load()) return null;
            return new Material(surfaceShader) { renderQueue = ImageQueue };
        }

        public static bool RoundButton(GameObject button, bool enabled)
        {
            if (!Load()) return false;
            Vector3 original = button.transform.localScale;
            Vector3 scale = Main.menu.transform.localScale;
            float width = original.y * scale.y;
            float height = original.z * scale.z;
            float padding = ModelButtonLayout.PixelSize * 8f;
            button.transform.localScale = new Vector3(original.x,
                (width + 2f * padding) / scale.y, (height + 2f * padding) / scale.z);
            BoxCollider collider = button.GetComponent<BoxCollider>();

            collider.size = new Vector3(1f, original.y / button.transform.localScale.y,
                original.z / button.transform.localScale.z);
            button.GetComponent<MeshFilter>().sharedMesh = Quad;
            Material material = new Material(roundedShader) { renderQueue = ButtonQueue };
            material.SetFloat("_ZWrite", 1f);
            material.color = Main.buttonColors[enabled ? 1 : 0].GetCurrentColor();
            material.SetVector("_RectSize", new Vector4(width, height, 0f, 0f));
            material.SetFloat("_Padding", padding);
            material.SetFloat("_GlowWidth", ModelButtonLayout.PixelSize * 6f);
            material.SetFloat("_RimStrength", enabled ? 0f : 0.002f);
            material.SetFloat("_Radius", Mathf.Min(ModelButtonLayout.PixelSize * 16f, height * 0.45f));
            material.SetFloat("_BorderWidth", ModelButtonLayout.PixelSize * 1.5f);
            button.GetComponent<Renderer>().material = material;
            SmoothButtonBorder border = button.AddComponent<SmoothButtonBorder>();
            border.material = material;
            border.enabledState = enabled;
            border.Refresh();
            return true;
        }

        private static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "Interstellar_AntialiasedButtonQuad" };
                quad.vertices = new[] { new Vector3(.5f, -.5f, -.5f), new Vector3(.5f, .5f, -.5f),
                    new Vector3(.5f, .5f, .5f), new Vector3(.5f, -.5f, .5f) };
                quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                quad.RecalculateNormals();
                quad.RecalculateBounds();
                return quad;
            }
        }

        public static void ApplyOverlay(GameObject target)
        {
            if (target == null || !Load()) return;
            MenuVisualLifetime lifetime = target.GetComponent<MenuVisualLifetime>() ?? target.AddComponent<MenuVisualLifetime>();
            Dictionary<(Material, int), Material> copies = new Dictionary<(Material, int), Material>();



            foreach (Canvas canvas in target.GetComponentsInChildren<Canvas>(true))
            {
                canvas.overrideSorting = false;
                canvas.sortingLayerID = SortingLayerId;
                canvas.sortingOrder = SortingOrder;
            }

            foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>(true))
            {
                Material material = text.fontMaterial;
                ApplyTextMaterial(material);
                lifetime.Track(material);
                MenuTextOverlay overlay = text.GetComponent<MenuTextOverlay>() ?? text.gameObject.AddComponent<MenuTextOverlay>();
                overlay.Configure(text, lifetime);
                text.ForceMeshUpdate(true);
            }

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                ApplySorting(renderer);
                if (renderer.GetComponent<TMP_Text>() != null) continue;
                if (renderer.GetComponent<TMP_SubMesh>() != null) continue;

                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material source = materials[index];
                    if (source == null) continue;
                    if (IsMenuShader(source.shader))
                    {
                        lifetime.Track(source);
                        continue;
                    }
                    int queue = source.mainTexture != null ? DisplayQueue : ShellQueue;
                    if (renderer.name == "Outline") queue = ButtonQueue - 1;
                    if (renderer.GetComponent<ButtonCollider>() != null && renderer.name != "Model Search")
                        queue = ButtonQueue;
                    if (!copies.TryGetValue((source, queue), out Material material))
                    {
                        material = CopySurface(source, queue, true);
                        copies[(source, queue)] = material;
                        lifetime.Track(material);
                    }
                    materials[index] = material;
                }
                renderer.sharedMaterials = materials;
            }

            foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TMP_Text) continue;
                Material material = CopySurface(graphic.material, ImageQueue);
                if (graphic.name == "Interstellar Wordmark") BrandArtwork.ConfigureTitleMaterial(material);
                graphic.material = material;
                lifetime.Track(material);
                if (graphic is Image image && image.sprite != null
                    && image.sprite.name == BrandArtwork.PaddedSpriteName)
                    lifetime.Track(image.sprite);
            }
        }

        internal static Material CopySurface(Material source, int queue, bool depthWrite = false)
        {
            Material material = new Material(surfaceShader) { name = source.name + " Menu Overlay", renderQueue = queue };
            material.color = source.HasProperty("_BaseColor") || source.HasProperty("_Color") ? source.color : Color.white;
            material.SetFloat("_ZWrite", depthWrite && material.color.a >= 0.999f ? 1f : 0f);
            material.mainTexture = source.mainTexture;
            if (material.mainTexture is Texture2D lettering
                && (source.name == "bar_disconnect_display_display" || source.name == "bar_home_display_display"))
                lettering.mipMapBias = -0.35f;
            material.mainTextureScale = source.mainTextureScale;
            material.mainTextureOffset = source.mainTextureOffset;
            foreach (string property in BrandArtwork.MaterialProperties)
                if (source.HasProperty(property)) material.SetFloat(property, source.GetFloat(property));
            if (source.HasProperty("_BevelStrength")) material.SetFloat("_BevelStrength", source.GetFloat("_BevelStrength"));
            if (source.HasProperty("_LetteringShift"))
            {
                material.SetVector("_LetteringShift", source.GetVector("_LetteringShift"));
                material.SetVector("_LetteringRect", source.GetVector("_LetteringRect"));
            }
            else if (source.name == "bar_disconnect_display_display" || source.name == "bar_home_display_display")
            {
                float shift = source.name == "bar_home_display_display" ? 3.5f : 2.5f;
                material.SetVector("_LetteringShift", new Vector4(shift / 800f, 2.5f / 168f, 0f, 0f));
                material.SetVector("_LetteringRect", new Vector4(0.1f, 0.28f, 0.9f, 0.72f));
            }
            ModelPalette.ApplySource(material, source.name);
            return material;
        }

        internal static void ApplyTextMaterial(Material material)
        {
            if (textShader == null && !Load()) return;
            if (textShader != null) material.shader = textShader;
            MenuTypography.ConfigureMaterial(material);
            ApplyTextBackground(material, ModelPalette.Shell);
            if (material.HasProperty("_PerspectiveFilter")) material.SetFloat("_PerspectiveFilter", 0.55f);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", (float)UnityEngine.Rendering.CullMode.Back);
            material.renderQueue = TextQueue;
        }
        public static void SetTextBackground(TMP_Text text, Color color)
        {
            if (text == null) return;
            MenuTextOverlay overlay = text.GetComponent<MenuTextOverlay>() ?? text.gameObject.AddComponent<MenuTextOverlay>();
            overlay.SetBackground(color);
        }

        internal static void ApplyTextBackground(Material material, Color color)
        {
            if (material != null && material.HasProperty("_TextBackgroundColor"))
                material.SetColor("_TextBackgroundColor", color);
        }
    }

    public class MenuTextOverlay : MonoBehaviour
    {
        private TMP_Text text;
        private MenuVisualLifetime lifetime;
        private Color background = ModelPalette.Shell;
        private readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        private readonly List<TMP_SubMesh> submeshes = new List<TMP_SubMesh>();

        public void Configure(TMP_Text target, MenuVisualLifetime owner)
        {
            if (text != null) text.OnPreRenderText -= Refresh;
            text = target;
            lifetime = owner;
            text.OnPreRenderText += Refresh;
            MenuVisuals.ApplyTextBackground(text.fontMaterial, background);
        }

        public void SetBackground(Color color)
        {
            if (background == color) return;
            background = color;
            if (text != null) MenuVisuals.ApplyTextBackground(text.fontMaterial, background);
            foreach (Material material in copies.Values)
                MenuVisuals.ApplyTextBackground(material, background);
        }

        private void Refresh(TMP_TextInfo _)
        {
            text.GetComponentsInChildren(true, submeshes);
            try
            {
                foreach (TMP_SubMesh submesh in submeshes)
                {
                    Material source = submesh.sharedMaterial;
                    if (source == null) continue;
                    if (MenuVisuals.IsMenuShader(source.shader))
                    {
                        MenuVisuals.ApplyTextBackground(source, background);
                        MenuVisuals.ApplySorting(submesh.GetComponent<Renderer>());
                        continue;
                    }
                    if (!copies.TryGetValue(source, out Material material))
                    {
                        bool sprite = source.shader.name.Contains("Sprite");
                        material = sprite ? MenuVisuals.CopySurface(source, MenuVisuals.TextQueue) : new Material(source);
                        if (!sprite)
                        {
                            MenuVisuals.ApplyTextMaterial(material);
                            MenuVisuals.ApplyTextBackground(material, background);
                        }
                        copies[source] = material;
                        lifetime.Track(material);
                    }


                    Renderer renderer = submesh.GetComponent<Renderer>();
                    renderer.sharedMaterial = material;
                    MenuVisuals.ApplySorting(renderer);
                }
            }
            finally { submeshes.Clear(); }
        }

        private void OnDestroy()
        {
            if (text != null) text.OnPreRenderText -= Refresh;
        }
    }

    public class SmoothButtonBorder : MonoBehaviour
    {
        private static readonly int BorderColor = Shader.PropertyToID("_BorderColor");
        private static readonly int GlowOpacity = Shader.PropertyToID("_GlowOpacity");
        public Material material;
        public bool enabledState;
        public void Refresh()
        {
            if (material == null) return;
            material.SetColor(BorderColor, ModelPalette.Border(enabledState));
            material.SetFloat(GlowOpacity, enabledState ? 0.008f : 0.0045f);
        }
        private void LateUpdate() => Refresh();
    }

    public class MenuVisualLifetime : MonoBehaviour
    {
        private readonly HashSet<Material> materials = new HashSet<Material>();
        private readonly HashSet<Sprite> sprites = new HashSet<Sprite>();
        public void Track(Material material) { if (material != null) materials.Add(material); }
        public void Track(Sprite sprite) { if (sprite != null) sprites.Add(sprite); }
        private void OnDestroy()
        {
            foreach (Material material in materials) if (material != null) Destroy(material);
            foreach (Sprite sprite in sprites) if (sprite != null) Destroy(sprite);
            materials.Clear();
            sprites.Clear();
        }
    }
}

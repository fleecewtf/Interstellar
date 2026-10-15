using Interstellar.Utilities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Interstellar.Classes.Menu
{
    public static class BrandArtwork
    {
        public const string PaddedSpriteName = "Interstellar_PaddedBrandArtwork";
        internal static readonly string[] MaterialProperties =
        {
            "_BrandMask", "_MaskBackground", "_MaskWhite", "_Monochrome",
            "_ToneExponent", "_ToneWhitePoint", "_Cull"
        };

        private static Texture2D modelLogoTexture;



        public static Texture2D ModelLogoTexture
        {
            get
            {
                if (!MenuVisuals.Load()) return null;
                if (modelLogoTexture != null) return modelLogoTexture;
                Texture2D texture = AssetUtilities.LoadTextureFromResource(
                    PluginInfo.ClientResourcePath + ".model_logo.png");


                if (texture == null || texture.width != 800 || texture.height != 1200) return null;
                modelLogoTexture = texture;
                modelLogoTexture.filterMode = FilterMode.Trilinear;
                modelLogoTexture.anisoLevel = 1;
                modelLogoTexture.wrapMode = TextureWrapMode.Clamp;
                modelLogoTexture.Apply(true, false);
                return modelLogoTexture;
            }
        }



        public static Rect ModelLogoBounds => new Rect(180f, 469f, 440f, 262f);

        public static Sprite CreatePaddedSprite(Texture2D texture, Rect visibleBounds, int padding = 8)
        {
            float left = Mathf.Max(0f, Mathf.Floor(visibleBounds.xMin) - padding);
            float bottom = Mathf.Max(0f, Mathf.Floor(visibleBounds.yMin) - padding);
            float right = Mathf.Min(texture.width, Mathf.Ceil(visibleBounds.xMax) + padding);
            float top = Mathf.Min(texture.height, Mathf.Ceil(visibleBounds.yMax) + padding);
            Sprite sprite = Sprite.Create(texture, new Rect(left, bottom, right - left, top - bottom),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = PaddedSpriteName;
            return sprite;
        }

        public static void ConfigureTitleMaterial(Material material)
        {
            if (material == null) return;
            material.SetFloat("_BrandMask", 1f);
            material.SetFloat("_Monochrome", 0f);
        }

        public static Material CreateLogoMaterial()
        {
            if (ModelLogoTexture == null) return null;
            Material material = MenuVisuals.CreateImageMaterial();
            if (material == null) return null;
            material.name = "Interstellar Clean Model Logo";
            ConfigureLogoMaterial(material);
            material.SetFloat("_ZTest", (float)CompareFunction.Always);
            material.renderQueue = (int)RenderQueue.Overlay + 30;
            return material;
        }

        public static void ConfigureLogoMaterial(Material material)
        {
            if (material == null) return;
            material.SetFloat("_BrandMask", 2f);
            material.SetFloat("_MaskBackground", 22f / 255f);
            material.SetFloat("_MaskWhite", 251f / 255f);
            material.SetFloat("_Monochrome", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.mainTexture = ModelLogoTexture;
            material.mainTextureScale = new Vector2(1f, -1f);
            material.mainTextureOffset = new Vector2(0f, 1f);
        }
    }
}

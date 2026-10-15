using Interstellar.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace Interstellar.Classes.Menu
{
    public static class NotificationIcon
    {
        public static TMP_SpriteAsset Create()
        {
            Texture2D texture = AssetUtilities.LoadTextureFromResource(PluginInfo.ClientResourcePath + ".notification_bell.png");
            if (texture == null)
                return null;
            texture.filterMode = FilterMode.Trilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 1;
            texture.Apply(true, false);
            TMP_SpriteAsset asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            typeof(TMP_SpriteAsset).GetProperty(nameof(TMP_SpriteAsset.version)).SetValue(asset, "1.1.0");
            asset.name = "Interstellar_NotificationBell";
            asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(asset.name);
            asset.spriteSheet = texture;
            TMP_SpriteGlyph glyph = new TMP_SpriteGlyph
            {
                index = 0,
                metrics = new GlyphMetrics(24f, 24f, 0f, 20f, 26f),
                glyphRect = new GlyphRect(0, 0, texture.width, texture.height),
                scale = 1f,
                atlasIndex = 0
            };
            TMP_SpriteCharacter character = new TMP_SpriteCharacter(0xE000, glyph)
            {
                name = "bell",
                scale = 0.9f
            };
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(character);
            Material material = MenuVisuals.CreateImageMaterial();
            if (material == null)
            {
                Shader shader = Shader.Find("TextMeshPro/Sprite");
                if (shader != null)
                    material = new Material(shader);
            }
            if (material != null)
            {
                material.name = "Interstellar_NotificationBellOverlay";
                material.mainTexture = texture;
                material.renderQueue = 4051;
                if (material.HasProperty("_BrandMask"))
                    material.SetFloat("_BrandMask", 1f);
                if (material.HasProperty("_Cull"))
                    material.SetFloat("_Cull", 0f);
                if (material.HasProperty("_ZTestMode"))
                    material.SetFloat("_ZTestMode", 8f);
                if (material.HasProperty("_ZTest"))
                    material.SetFloat("_ZTest", 8f);
                asset.material = material;
            }
            asset.UpdateLookupTables();
            return asset;
        }

        public static void Release(TMP_SpriteAsset asset)
        {
            if (asset == null)
                return;
            if (asset.material != null)
                Object.Destroy(asset.material);
            Object.Destroy(asset);
        }
    }
}

using Interstellar.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace Interstellar.Classes.Menu
{


    public static class MenuTypography
    {
        private static AssetBundle bundle;
        private static TMP_FontAsset font;
        private static bool attempted;

        public static FontStyles Style => FontStyles.Bold;

        public static TMP_FontAsset Font
        {
            get
            {
                if (!attempted)
                {
                    attempted = true;
                    try
                    {
                        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                            PluginInfo.ClientResourcePath + ".menu_typography");
                        if (stream == null) throw new FileNotFoundException("Embedded menu typography bundle is missing.");
                        using MemoryStream memory = new MemoryStream();
                        stream.CopyTo(memory);
                        bundle = AssetBundle.LoadFromMemory(memory.ToArray());
                        if (bundle == null) throw new InvalidDataException("Could not load the menu typography bundle.");
                        font = bundle.LoadAsset<TMP_FontAsset>("Assets/InterstellarTypography/MenuSans.asset");
                        if (font == null || font.atlasTexture == null)
                            throw new InvalidDataException("High-resolution menu font is missing its atlas.");

                        Shader nativeShader = Shader.Find("TextMeshPro/Mobile/Distance Field")
                            ?? Shader.Find("TextMeshPro/Distance Field");
                        if (nativeShader != null) font.material.shader = nativeShader;
                        CleanMaterial(font.material);
                        foreach (Texture2D atlas in font.atlasTextures)
                        {
                            if (atlas == null) continue;
                            atlas.filterMode = FilterMode.Bilinear;
                            atlas.wrapMode = TextureWrapMode.Clamp;
                            atlas.anisoLevel = 1;
                        }

                        TMP_FontAsset fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                        font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                        if (fallback != null && fallback != font && !font.fallbackFontAssetTable.Contains(fallback))
                            font.fallbackFontAssetTable.Add(fallback);
                    }
                    catch (Exception error)
                    {
                        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                        LogManager.LogError("Failed to load high-resolution menu typography: " + error);
                    }
                }
                return font;
            }
        }

        public static void Configure(TMP_Text text)
        {
            if (text == null) return;
            TMP_FontAsset selected = Font;
            if (selected != null && text.font != selected)
            {
                text.font = selected;
                text.fontSharedMaterial = selected.material;
            }
            text.fontStyle = Style;
            text.extraPadding = true;



            CleanMaterial(text.fontMaterial);
        }

        private static void CleanMaterial(Material material)
        {
            if (material == null) return;
            Set(material, "_FaceDilate", 0f);
            Set(material, "_OutlineWidth", 0f);
            Set(material, "_OutlineSoftness", 0f);
            Set(material, "_UnderlayDilate", 0f);
            Set(material, "_UnderlaySoftness", 0f);
            Set(material, "_GlowInner", 0f);
            Set(material, "_GlowOuter", 0f);
            Set(material, "_Sharpness", 0.35f);
            material.DisableKeyword("OUTLINE_ON");
            material.DisableKeyword("UNDERLAY_ON");
            material.DisableKeyword("UNDERLAY_INNER");
            material.DisableKeyword("GLOW_ON");
        }

        private static void Set(Material material, string name, float value)
        {
            if (material.HasProperty(name) && !Mathf.Approximately(material.GetFloat(name), value))
                material.SetFloat(name, value);
        }

        public static void ConfigureMaterial(Material material) => CleanMaterial(material);
    }
}

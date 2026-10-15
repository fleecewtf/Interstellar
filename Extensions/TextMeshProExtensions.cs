using Interstellar.Utilities;
using System;
using TMPro;
using UnityEngine;

namespace Interstellar.Extensions
{
    public static class TextMeshProExtensions
    {
        public static void SafeSetText(this TMP_Text tmp, string text)
        {
            if (tmp == null)
                return;

            if (tmp.text != text)
                tmp.text = text;
        }

        public static void SafeSetFont(this TMP_Text tmp, TMP_FontAsset font)
        {
            if (tmp == null)
                return;

            if (font == null)
                return;

            if (tmp.font == null || tmp.font.hashCode != font.hashCode)
                tmp.font = font;
        }

        public static void SafeSetFontSize(this TMP_Text tmp, float size)
        {
            if (tmp == null)
                return;

            if (Math.Abs(tmp.fontSize - size) > 0.01f)
                tmp.fontSize = size;
        }

        public static void SafeSetFontStyle(this TMP_Text tmp, FontStyles style)
        {
            if (tmp == null)
                return;

            if (tmp.fontStyle != style)
                tmp.fontStyle = style;
        }

        public static void SafeSetWrappingMode(this TMP_Text tmp, TextWrappingModes mode)
        {
            if (tmp == null)
                return;
            if (tmp.textWrappingMode != mode)
                tmp.textWrappingMode = mode;
        }

        public static void SafeSetOverflowMode(this TMP_Text tmp, TextOverflowModes mode)
        {
            if (tmp == null)
                return;
            if (tmp.overflowMode != mode)
                tmp.overflowMode = mode;
        }
        public static void SafeSetAlignment(this TMP_Text tmp, TextAlignmentOptions mode)
        {
            if (tmp == null)
                return;
            if (tmp.alignment != mode)
                tmp.alignment = mode;
        }

        private static Shader _tmpShader;
        public static Shader TmpShader
        {
            get
            {
                if (_tmpShader == null)
                    _tmpShader = AssetUtilities.LoadAsset<Shader>("TMP_SDF-Mobile Overlay");

                return _tmpShader;
            }
        }

        public static void Chams(this TMP_Text tmp, bool seeThrough)
        {
            if (tmp == null)
                return;

            Shader target = seeThrough ? TmpShader : tmp.font?.material?.shader;
            if (target == null)
                return;

            var mat = tmp.fontMaterial;
            if (mat != null && mat.shader != target)
                mat.shader = target;
        }
    }
}

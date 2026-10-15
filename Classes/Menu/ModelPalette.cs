using UnityEngine;

namespace Interstellar.Classes.Menu
{

    public static class ModelPalette
    {
        public static readonly Color Shell = Color.black;
        public static readonly Color Disabled = new Color32(5, 5, 5, 255);
        public static readonly Color Enabled = new Color32(243, 243, 243, 255);
        public static readonly Color LightText = new Color32(243, 243, 243, 255);
        public static readonly Color DarkText = new Color32(9, 9, 9, 255);
        public static readonly Color DisabledHint = new Color32(150, 150, 150, 255);
        public static readonly Color EnabledHint = new Color32(90, 90, 90, 255);
        public static readonly Color DisabledBorder = new Color32(22, 22, 22, 255);

        public static Color Border(bool enabled) => enabled ? Enabled : DisabledBorder;
        public static Color Hint(bool enabled) => enabled ? EnabledHint : DisabledHint;

        public static void ApplySource(Material material, string sourceName)
        {
            if (sourceName == "shell_black" || sourceName == "shell_graphite")
            {
                material.color = Shell;
                if (material.HasProperty("_BevelStrength")) material.SetFloat("_BevelStrength", 0.024f);
            }
            else if (sourceName != null && sourceName.EndsWith("_display_display", System.StringComparison.Ordinal))
            {



                if (material.HasProperty("_Monochrome")) material.SetFloat("_Monochrome", 1f);
                if (material.HasProperty("_ToneExponent")) material.SetFloat("_ToneExponent",
                    sourceName == "bar_home_display_display" ? 1.55f : 1.4f);
                if (material.HasProperty("_ToneWhitePoint")) material.SetFloat("_ToneWhitePoint", 0.9f);
            }
        }
    }
}

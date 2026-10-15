using Interstellar.Menu;
using TMPro;
using UnityEngine;

namespace Interstellar.Classes.Menu
{

    public static class ModelButtonLayout
    {
        public const float ArtworkHeight = 1200f;
        public const float RowWidthRatio = 720f / 800f;
        public const float LabelInsetRatio = 32f / 720f;
        public const float HintWidthRatio = 0.22f;

        public static bool IsRow(int index) => Main.ModelMenuActive && (index >= 0 || index == -2);

        public static bool IsBack(ButtonInfo button) => button != null && !button.label
            && !button.isTogglable && button.buttonText.StartsWith("Exit ")
            && button.toolTip.StartsWith("Returns you back to the");



        public static float PixelSize => Main.ModelPanelBounds.size.z / Main.canvasObj.transform.localScale.z / ArtworkHeight;

        public static float RowHeight(bool back) => PixelSize * 88f
            * (back ? 1f : Mathf.Min(1f, 8f / Mathf.Max(1, Main.PageSize)));

        public static Vector3 LocalPosition(int index, bool back)
        {
            Bounds panel = Main.ModelPanelBounds;
            float step = Main.PageSize > 8 ? 700f / (Main.PageSize - 1) : 100f;
            float artworkY = back ? 296f : (Main.ModelHasBackRow || Main.isSearching ? 410f : 296f) + index * step;
            float face = panel.max.x + PixelSize * Main.canvasObj.transform.localScale.x;
            return new Vector3(face - 0.045f, panel.center.y, panel.max.z - artworkY / ArtworkHeight * panel.size.z);
        }

        public static Vector3 LocalScale(bool back) => new Vector3(0.09f,
            Main.ModelPanelBounds.size.y * RowWidthRatio, RowHeight(back) * Main.canvasObj.transform.localScale.z);

        public static Vector3 TextPosition(int index, bool back)
        {
            Vector3 local = LocalPosition(index, back);
            local.x += 0.045f + PixelSize * 2f * Main.canvasObj.transform.localScale.x;
            return Main.canvasObj.transform.InverseTransformPoint(Main.menu.transform.TransformPoint(local));
        }

        public static float RowWidth => Main.ModelPanelBounds.size.y / Main.canvasObj.transform.localScale.y * RowWidthRatio;

        public static void StyleText(TMP_Text text, bool hint, bool back)
        {
            MenuTypography.Configure(text);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;


            text.isOrthographic = false;
            float size = (hint ? 0.108f : back ? 0.14f : 0.16f) * (PixelSize / 0.00039f)
                * (back ? 1f : Mathf.Min(1f, 8f / Mathf.Max(1, Main.PageSize)));
            text.fontSize = size;
            text.fontSizeMax = size;
            text.fontSizeMin = size * 0.8f;
            text.enableAutoSizing = true;
            Main.FollowMenuSettings(text, back ? 21.5f : hint ? 6f : 0f);
        }
    }
}

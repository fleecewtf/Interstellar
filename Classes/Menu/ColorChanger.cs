using GorillaExtensions;
using Interstellar.Menu;
using UnityEngine;

namespace Interstellar.Classes.Menu
{
    public class ColorChanger : MonoBehaviour
    {
        public void Start()
        {
            if (colors == null)
            {
                Destroy(this);
                return;
            }

            targetRenderer = GetComponent<Renderer>();

            if (colors.IsFlat())
            {
                Update();
                Destroy(this);
                return;
            }

            Update();
        }

        public void Update()
        {
            targetRenderer.enabled = overrideTransparency ?? !colors.transparent;

            if (colors.transparent)
                return;

            if (MenuVisuals.IsMenuShader(targetRenderer.sharedMaterial.shader))
            {
                bool spatialGradient = Main.dynamicGradients && !colors.IsFlat();
                targetRenderer.sharedMaterial.color = spatialGradient ? Color.white : colors.GetCurrentColor();
                if (spatialGradient)
                {
                    targetRenderer.sharedMaterial.mainTexture = Main.GetGradientTexture(colors.GetColor(0), colors.GetColor(1));
                    if (Main.scrollingGradients)
                        gameObject.GetOrAddComponent<ScrollMaterial>();
                }
            }
            else if (!Main.dynamicGradients)
                targetRenderer.sharedMaterial.color = colors.GetCurrentColor();
            else
            {
                if (colors.IsFlat())
                    targetRenderer.sharedMaterial.color = colors.GetColor(0);
                else
                {
                    if (targetRenderer.sharedMaterial.shader.name != "Universal Render Pipeline/Unlit" && targetRenderer.sharedMaterial.mainTexture == null)
                    {
                        targetRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                        {
                            mainTexture = Main.GetGradientTexture(colors.GetColor(0), colors.GetColor(1))
                        };

                        if (Main.scrollingGradients)
                            gameObject.GetOrAddComponent<ScrollMaterial>();
                    }
                }
            }

            if (!Main.transparentMenu) return;
            Color color = targetRenderer.sharedMaterial.color;
            color.a = 0.5f;
            targetRenderer.material.color = color;
        }

        public Renderer targetRenderer;
        public ExtGradient colors;
        public bool? overrideTransparency;
    }
}

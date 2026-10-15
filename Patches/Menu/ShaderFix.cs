using UnityEngine;
using UnityEngine.Rendering;
using static Interstellar.Menu.Main;

namespace Interstellar.Patches.Menu
{
    public static class ShaderFix
    {
        public static GameObject CreatePrimitive(PrimitiveType type)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer == null) return primitive;
            if (crystallizeMenu && CrystalMaterial != null)
                renderer.material = new Material(CrystalMaterial);
            else if (transparentMenu)
            {
                Material material = renderer.material;
                material.shader = Shader.Find(shinyMenu ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");

                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
                renderer.material.shader = Shader.Find(shinyMenu ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");

            renderer.material.color = backgroundColor.GetColor(0);
            return primitive;
        }
    }
}

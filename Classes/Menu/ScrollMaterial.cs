using Interstellar.Menu;
using UnityEngine;

namespace Interstellar.Classes.Menu
{
    public class ScrollMaterial : MonoBehaviour
    {
        private Renderer renderer;

        void Awake()
        {
            renderer = GetComponent<Renderer>();
            Update();
        }

        void Update()
        {
            float offset = Main.slowFadeColors ? Time.time / 10f : Time.time;
            Vector4 st = new Vector4(1, 1, offset, offset);
            Material mat = renderer.sharedMaterial;
            mat.SetVector(MenuVisuals.IsMenuShader(mat.shader) ? "_MainTex_ST" : "_BaseMap_ST", st);
        }
    }
}

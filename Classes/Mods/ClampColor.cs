using Interstellar.Classes.Menu;
using Interstellar.Managers;
using UnityEngine;

namespace Interstellar.Classes.Mods
{
    public class ClampColor : MonoBehaviour
    {
        public void Start()
        {
            targetRenderer.gameObject.GetComponent<ColorChanger>()?.Start();

            gameObjectRenderer = GetComponent<Renderer>();
            Update();
        }

        public void Update()
        {
            if (gameObjectRenderer.sharedMaterial.shader != targetRenderer.sharedMaterial.shader)
            {
                LogManager.Log("Creating new material");
                gameObjectRenderer.material = new Material(targetRenderer.sharedMaterial.shader);
            }

            if (targetRenderer.sharedMaterial.mainTexture != null && gameObjectRenderer.sharedMaterial.mainTexture != targetRenderer.sharedMaterial.mainTexture)
                gameObjectRenderer.material.mainTexture = targetRenderer.sharedMaterial.mainTexture;

            gameObjectRenderer.material.color = targetRenderer.sharedMaterial.color;
        }

        public Renderer gameObjectRenderer;
        public Renderer targetRenderer;
    }
}

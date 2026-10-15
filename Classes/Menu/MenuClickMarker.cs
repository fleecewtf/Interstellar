using Interstellar.Menu;
using UnityEngine;
using UnityEngine.Rendering;

namespace Interstellar.Classes.Menu
{
    public sealed class MenuClickMarker : MonoBehaviour
    {
        private Renderer dot;
        private Material material;

        private void Awake()
        {
            Renderer original = GetComponent<Renderer>();
            if (original != null) original.enabled = false;
            material = MenuVisuals.CreateImageMaterial();
            if (material == null) return;
            GameObject marker = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Interstellar_MenuFingerContact";
            marker.layer = 2;
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one * 0.35f;
            Collider collider = marker.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            material.color = Color.white;
            material.renderQueue = MenuVisuals.TextQueue + 1;
            dot = marker.GetComponent<Renderer>();
            dot.sharedMaterial = material;
            dot.shadowCastingMode = ShadowCastingMode.Off;
            dot.receiveShadows = false;
            MenuVisuals.ApplySorting(dot);
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (dot != null) dot.enabled = Main.menu != null && Main.menu.activeInHierarchy && !Main.shouldBePC;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}

using Interstellar.Menu;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Interstellar.Classes.Menu
{
    public class MenuFade : MonoBehaviour
    {
        public const float Duration = 0.12f;

        private const string TransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";

        private class Snapshot
        {
            public Material material;
            public float surface;
            public float srcBlend;
            public float dstBlend;
            public float zWrite;
            public int queue;
            public bool keyword;
            public float alpha;
            public int holders;
        }

        private static readonly Dictionary<Material, Snapshot> active = new Dictionary<Material, Snapshot>();

        private readonly List<Snapshot> held = new List<Snapshot>();
        private readonly List<Graphic> graphics = new List<Graphic>();
        private readonly List<TMP_Text> texts = new List<TMP_Text>();
        private readonly List<Collider> colliders = new List<Collider>();
        private readonly List<ButtonCollider> buttons = new List<ButtonCollider>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Material> materials = new List<Material>();
        private readonly HashSet<Material> seen = new HashSet<Material>();

        private bool fadeIn;
        private float elapsed;
        private bool running;

        public static void Play(GameObject target, bool fadeIn)
        {
            if (target == null)
                return;

            MenuFade fade = target.GetComponent<MenuFade>() ?? target.AddComponent<MenuFade>();

            if (!fadeIn)
            {
                try
                {
                    target.GetComponentsInChildren(true, fade.colliders);
                    foreach (Collider collider in fade.colliders)
                        collider.enabled = false;
                    target.GetComponentsInChildren(true, fade.buttons);
                    foreach (ButtonCollider button in fade.buttons)
                        button.enabled = false;
                }
                finally
                {
                    fade.colliders.Clear();
                    fade.buttons.Clear();
                }
            }

            fade.Collect();
            fade.fadeIn = fadeIn;
            fade.elapsed = 0f;
            fade.running = true;
            fade.ApplyAlpha(fadeIn ? 0f : 1f);
        }

        private void Collect()
        {
            Release();

            GetComponentsInChildren(true, texts);
            GetComponentsInChildren(true, graphics);
            for (int index = graphics.Count - 1; index >= 0; index--)
            {
                if (graphics[index] is TMP_Text)
                    graphics.RemoveAt(index);
            }

            seen.Clear();
            try
            {
                GetComponentsInChildren(true, renderers);
                foreach (Renderer render in renderers)
                {

                    if (render.GetComponent<TMP_Text>() != null || render.GetComponent<TMP_SubMesh>() != null)
                        continue;

                    render.GetSharedMaterials(materials);
                    foreach (Material material in materials)
                    {
                        if (material != null && material.HasProperty("_SrcBlend") && seen.Add(material))
                            held.Add(Acquire(material));
                    }
                }
            }
            finally
            {
                renderers.Clear();
                materials.Clear();
                seen.Clear();
            }
        }

        private void LateUpdate()
        {
            if (!running)
                return;

            elapsed += Time.deltaTime;

            if (elapsed < Duration)
            {
                ApplyAlpha(fadeIn ? elapsed / Duration : 1f - elapsed / Duration);
                return;
            }

            running = false;

            if (!fadeIn)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            ApplyAlpha(1f);
            Release();
        }

        private void OnDestroy() =>
            Release();

        private void ApplyAlpha(float alpha)
        {
            foreach (Snapshot snapshot in held)
            {
                if (snapshot.material == null)
                    continue;

                Color color = snapshot.material.color;
                color.a = alpha;
                snapshot.material.color = color;
            }

            foreach (Graphic graphic in graphics)
            {
                if (graphic != null)
                    graphic.canvasRenderer.SetAlpha(alpha);
            }

            foreach (TMP_Text text in texts)
            {
                if (text != null)
                    text.alpha = alpha;
            }
        }

        private static Snapshot Acquire(Material material)
        {
            if (active.TryGetValue(material, out Snapshot existing))
            {
                existing.holders++;
                return existing;
            }

            Snapshot snapshot = new Snapshot
            {
                material = material,
                queue = material.renderQueue,
                keyword = material.IsKeywordEnabled(TransparentKeyword),
                alpha = material.color.a,
                surface = material.HasProperty("_Surface") ? material.GetFloat("_Surface") : 0f,
                srcBlend = material.GetFloat("_SrcBlend"),
                dstBlend = material.HasProperty("_DstBlend") ? material.GetFloat("_DstBlend") : (float)BlendMode.Zero,
                zWrite = material.HasProperty("_ZWrite") ? material.GetFloat("_ZWrite") : 1f,
                holders = 1
            };

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword(TransparentKeyword);
            material.renderQueue = MenuVisuals.IsMenuShader(material.shader)
                ? snapshot.queue : (int)RenderQueue.Transparent;

            active[material] = snapshot;
            return snapshot;
        }

        private void Release()
        {
            foreach (Snapshot snapshot in held)
            {
                if (--snapshot.holders > 0)
                    continue;

                Material material = snapshot.material;
                active.Remove(material);

                if (material == null)
                    continue;

                material.SetFloat("_Surface", snapshot.surface);
                material.SetFloat("_SrcBlend", snapshot.srcBlend);
                material.SetFloat("_DstBlend", snapshot.dstBlend);
                material.SetFloat("_ZWrite", snapshot.zWrite);

                if (snapshot.keyword)
                    material.EnableKeyword(TransparentKeyword);
                else
                    material.DisableKeyword(TransparentKeyword);

                material.renderQueue = snapshot.queue;

                Color restored = material.color;
                restored.a = snapshot.surface > 0.5f || snapshot.keyword ? snapshot.alpha : 1f;
                material.color = restored;
            }

            held.Clear();
        }
    }
}

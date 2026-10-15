using System;
using System.Collections.Generic;
using System.Text;
using GorillaGameModes;
using Interstellar.Classes.Menu;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using Object = UnityEngine.Object;

namespace Interstellar.Mods
{

    public static class PlayerVisuals
    {
        private sealed class Visual
        {
            internal VRRig Rig;
            internal GameObject Root;
            internal Transform RootTransform;
            internal GameObject NameGroup;
            internal TextMeshPro Name;
            internal GameObject Voice;
            internal Transform VoiceTransform;
            internal readonly Transform[] Bars = new Transform[3];
            internal GorillaSpeakerLoudness Speaker;
            internal LineRenderer Tracer;
            internal float VoiceAmount;
            internal string RawName;
            internal float NextNameRefresh;
            internal int Actor;
            internal string UserId;
        }

        private static bool names;
        private static bool voices;
        private static bool tracers;
        private static GameObject holder;
        private static Material plateMaterial;
        private static Material whiteMaterial;
        private static Material lineMaterial;
        private static Material textMaterial;
        private static Mesh roundedMesh;
        private static Mesh squareMesh;
        private static readonly Dictionary<VRRig, Visual> visuals = new Dictionary<VRRig, Visual>();
        private static readonly HashSet<VRRig> seen = new HashSet<VRRig>();
        private static readonly List<VRRig> stale = new List<VRRig>();

        public static void EnableNameTags() => names = true;
        public static void EnableVoiceIndicators() => voices = true;
        public static void EnableAutomaticTracers() => tracers = true;
        public static void DisableNameTags() { names = false; HideDisabled(); }
        public static void DisableVoiceIndicators() { voices = false; HideDisabled(); }
        public static void DisableAutomaticTracers() { tracers = false; HideDisabled(); }

        private static void HideDisabled()
        {
            foreach (Visual visual in visuals.Values)
            {
                if (!names && visual.NameGroup != null) visual.NameGroup.SetActive(false);
                if (!voices && visual.Voice != null) visual.Voice.SetActive(false);
                if (!tracers && visual.Tracer != null) visual.Tracer.enabled = false;
            }
            if (!names && !voices && !tracers) ClearRuntime();
        }

        public static void LateUpdate()
        {
            if ((!names && !voices && !tracers) || NetworkSystem.Instance == null || !NetworkSystem.Instance.InRoom
                || GorillaTagger.Instance == null || GorillaTagger.Instance.headCollider == null)
            {
                if (holder != null || visuals.Count != 0) ClearRuntime();
                return;
            }
            Transform view = GorillaTagger.Instance.headCollider.transform;
            if (Interstellar.Menu.Main.shouldBePC && Interstellar.Menu.Main.TPC != null)
                view = Interstellar.Menu.Main.TPC.transform;
            Vector3 viewPosition = view.position;
            Vector3 viewUp = view.up;
            float now = Time.unscaledTime;
            float deltaTime = Time.unscaledDeltaTime;
            EnsureResources();
            seen.Clear();
            GorillaGameManager manager = GameMode.ActiveGameMode;
            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (!CurrentRemoteRig(rig)) continue;
                seen.Add(rig);
                if (visuals.TryGetValue(rig, out Visual prior)
                    && (prior.Actor != rig.Creator.ActorNumber || prior.UserId != rig.Creator.UserId))
                {
                    DestroyVisual(prior);
                    visuals.Remove(rig);
                }
                if (!visuals.TryGetValue(rig, out Visual visual))
                {
                    visual = CreateVisual(rig);
                    visuals.Add(rig, visual);
                }
                UpdateVisual(visual, viewPosition, viewUp, now, deltaTime, manager);
            }
            stale.Clear();
            foreach (KeyValuePair<VRRig, Visual> entry in visuals)
                if (entry.Key == null || !seen.Contains(entry.Key)) stale.Add(entry.Key);
            foreach (VRRig rig in stale)
            {
                Visual visual = visuals[rig];
                DestroyVisual(visual);
                visuals.Remove(rig);
            }
        }

        private static void DestroyVisual(Visual visual)
        {
            if (visual.Root != null) Object.Destroy(visual.Root);
            if (visual.Tracer != null) Object.Destroy(visual.Tracer.gameObject);
        }

        internal static bool CurrentRemoteRig(VRRig rig)
        {
            if (rig == null || rig.isLocal || !rig.gameObject.activeInHierarchy || rig.Creator == null
                || NetworkSystem.Instance == null || !NetworkSystem.Instance.InRoom) return false;
            NetPlayer current = NetworkSystem.Instance.GetPlayer(rig.Creator.ActorNumber);
            return current != null && !current.IsLocal && !string.IsNullOrEmpty(current.UserId)
                && current.UserId == rig.Creator.UserId;
        }

        private static void UpdateVisual(Visual visual, Vector3 viewPosition, Vector3 viewUp, float now, float deltaTime, GorillaGameManager manager)
        {
            VRRig rig = visual.Rig;
            float scale = float.IsNaN(rig.scaleFactor) || float.IsInfinity(rig.scaleFactor)
                ? 1f : Mathf.Clamp(rig.scaleFactor, 0.05f, 20f);
            Transform head = rig.headMesh != null ? rig.headMesh.transform : rig.transform;
            visual.RootTransform.position = head.position + Vector3.up * (0.30f * scale);
            Vector3 away = visual.RootTransform.position - viewPosition;
            if (away.sqrMagnitude > 0.00001f) visual.RootTransform.rotation = Quaternion.LookRotation(away, viewUp);
            Vector3 rootScale = Vector3.one * (0.25f * scale);
            if (!visual.RootTransform.localScale.Equals(rootScale)) visual.RootTransform.localScale = rootScale;
            if (visual.NameGroup.activeSelf != names) visual.NameGroup.SetActive(names);
            if (names && now >= visual.NextNameRefresh)
            {
                visual.NextNameRefresh = now + 0.2f;
                string raw = rig.Creator.NickName ?? string.Empty;
                if (visual.RawName != raw) { visual.RawName = raw; visual.Name.text = SanitizeName(raw); }
            }
            float amount = voices && visual.Speaker != null && visual.Speaker.IsSpeaking
                ? Mathf.Clamp01(visual.Speaker.LoudnessNormalized) : 0f;
            visual.VoiceAmount = Mathf.MoveTowards(visual.VoiceAmount, amount, deltaTime * 8f);
            bool showVoice = voices && (amount > 0.001f || visual.VoiceAmount > 0.015f);
            if (visual.Voice.activeSelf != showVoice) visual.Voice.SetActive(showVoice);
            if (visual.Voice.activeSelf)
            {
                visual.VoiceTransform.localPosition = names ? new Vector3(1.59f, 0f, 0f) : Vector3.zero;
                for (int index = 0; index < visual.Bars.Length; index++)
                    visual.Bars[index].localScale = new Vector3(0.055f,
                        (index == 1 ? 0.13f : 0.09f) + visual.VoiceAmount * (index == 1 ? 0.16f : 0.10f), 1f);
            }
            bool showTracer = tracers && ShouldTrace(manager, rig) && GorillaTagger.Instance.rightHandTransform != null;
            if (visual.Tracer.enabled != showTracer) visual.Tracer.enabled = showTracer;
            if (showTracer)
            {
                float width = Mathf.Clamp(0.008f * scale, 0.002f, 0.04f);
                if (visual.Tracer.startWidth != width) visual.Tracer.startWidth = width;
                if (visual.Tracer.endWidth != width) visual.Tracer.endWidth = width;
                visual.Tracer.SetPosition(0, GorillaTagger.Instance.rightHandTransform.position);
                visual.Tracer.SetPosition(1, rig.transform.position);
            }
        }

        private static bool ShouldTrace(GorillaGameManager manager, VRRig rig)
        {
            NetPlayer local = NetworkSystem.Instance.LocalPlayer;
            if (manager == null || local == null) return false;
            if (manager is GorillaHuntManager hunt)
                return hunt.GetTargetOf(local) == rig.Creator || hunt.IsTargetOf(rig.Creator, local);
            if (manager is GorillaTagManager infection)
                return infection.currentInfected.Count == 0 || infection.IsInfected(local) != infection.IsInfected(rig.Creator);
            return true;
        }

        internal static string SanitizeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Player";
            StringBuilder result = new StringBuilder(32);
            bool wasSpace = true;
            for (int index = 0; index < value.Length && result.Length < 28; index++)
            {
                char current = value[index];
                if (char.IsControl(current) || (current >= '\u202a' && current <= '\u202e')
                    || (current >= '\u2066' && current <= '\u2069')) continue;
                if (char.IsWhiteSpace(current))
                {
                    if (!wasSpace) { result.Append(' '); wasSpace = true; }
                    continue;
                }
                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]) || result.Length > 26) continue;
                    result.Append(current).Append(value[++index]);
                }
                else if (!char.IsLowSurrogate(current)) result.Append(current);
                wasSpace = false;
            }
            string cleaned = result.ToString().Trim();
            return cleaned.Length == 0 ? "Player" : cleaned;
        }

        private static Visual CreateVisual(VRRig rig)
        {
            Visual visual = new Visual { Rig = rig, Speaker = rig.GetComponent<GorillaSpeakerLoudness>(),
                Actor = rig.Creator.ActorNumber, UserId = rig.Creator.UserId };
            visual.Root = new GameObject("Interstellar_PlayerIndicator");
            visual.RootTransform = visual.Root.transform;
            visual.RootTransform.SetParent(holder.transform, false);
            visual.NameGroup = new GameObject("Name");
            visual.NameGroup.transform.SetParent(visual.RootTransform, false);
            CreateMesh("Plate", visual.NameGroup.transform, roundedMesh, plateMaterial, new Vector3(2.8f, 0.50f, 1f), new Vector3(0f, 0f, 0.008f));
            GameObject text = new GameObject("Text");
            text.transform.SetParent(visual.NameGroup.transform, false);
            visual.Name = text.AddComponent<TextMeshPro>();
            if (MenuTypography.Font != null) visual.Name.font = MenuTypography.Font;
            if (textMaterial != null) visual.Name.fontSharedMaterial = textMaterial;
            visual.Name.color = Color.white;
            visual.Name.fontStyle = FontStyles.Bold;
            visual.Name.fontSize = 3f;
            visual.Name.textWrappingMode = TextWrappingModes.NoWrap;
            visual.Name.overflowMode = TextOverflowModes.Ellipsis;
            visual.Name.alignment = TextAlignmentOptions.Center;
            visual.Name.richText = false;
            visual.Name.rectTransform.sizeDelta = new Vector2(2.45f, 0.45f);
            visual.Name.margin = Vector4.zero;
            visual.Voice = new GameObject("Speaking");
            visual.VoiceTransform = visual.Voice.transform;
            visual.VoiceTransform.SetParent(visual.RootTransform, false);
            CreateMesh("Plate", visual.VoiceTransform, roundedMesh, plateMaterial, new Vector3(0.46f, 0.50f, 1f), new Vector3(0f, 0f, 0.008f));
            for (int index = 0; index < visual.Bars.Length; index++)
                visual.Bars[index] = CreateMesh("VoiceBar", visual.VoiceTransform, squareMesh, whiteMaterial,
                    new Vector3(0.055f, 0.14f, 1f), new Vector3((index - 1) * 0.095f, 0f, 0f));
            visual.Voice.SetActive(false);
            GameObject line = new GameObject("Interstellar_PlayerTracer");
            line.transform.SetParent(holder.transform, false);
            visual.Tracer = line.AddComponent<LineRenderer>();
            visual.Tracer.sharedMaterial = lineMaterial;
            visual.Tracer.positionCount = 2;
            visual.Tracer.useWorldSpace = true;
            visual.Tracer.numCapVertices = 4;
            visual.Tracer.shadowCastingMode = ShadowCastingMode.Off;
            visual.Tracer.receiveShadows = false;
            visual.Tracer.startColor = visual.Tracer.endColor = new Color(1f, 1f, 1f, 0.65f);
            visual.Tracer.enabled = false;
            return visual;
        }

        private static Transform CreateMesh(string name, Transform parent, Mesh mesh, Material material, Vector3 scale, Vector3 position)
        {
            GameObject item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false);
            item.transform.localScale = scale;
            item.transform.localPosition = position;
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return item.transform;
        }

        private static void EnsureResources()
        {
            if (holder != null) return;
            holder = new GameObject("Interstellar_PlayerVisuals");
            Object.DontDestroyOnLoad(holder);
            plateMaterial = CreateMaterial(new Color(0.015f, 0.015f, 0.015f, 0.94f), false);
            whiteMaterial = CreateMaterial(Color.white, false);
            lineMaterial = CreateMaterial(Color.white, true);
            TMP_FontAsset font = MenuTypography.Font != null ? MenuTypography.Font : TMP_Settings.defaultFontAsset;
            if (font != null)
            {
                textMaterial = new Material(font.material) { name = "Interstellar_NameText" };
                if (textMaterial.HasProperty("_FaceColor")) textMaterial.SetColor("_FaceColor", Color.white);
                MenuTypography.ConfigureMaterial(textMaterial);
            }
            roundedMesh = BuildPlate(0.16f);
            squareMesh = BuildPlate(0f);
        }

        internal static Material CreateMaterial(Color color, bool throughWalls)
        {



            Shader shader = throughWalls
                ? Shader.Find("GUI/Text Shader") ?? Shader.Find("Hidden/Internal-Colored")
                : Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("A local visual shader is unavailable.");
            Material material = new Material(shader) { name = "Interstellar_LocalVisual", color = color,
                renderQueue = (int)(throughWalls ? RenderQueue.Overlay : RenderQueue.Transparent) };
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", Texture2D.whiteTexture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
            if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)(throughWalls ? CompareFunction.Always : CompareFunction.LessEqual));
            return material;
        }

        private static Mesh BuildPlate(float radius)
        {
            const int steps = 6;
            int edgeCount = 4 * (steps + 1);
            Vector3[] vertices = new Vector3[edgeCount + 1];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[edgeCount * 3];
            for (int corner = 0; corner < 4; corner++)
            {
                float angle = corner * Mathf.PI * 0.5f;
                Vector2 center = new Vector2(corner == 0 || corner == 3 ? 0.5f - radius : -0.5f + radius,
                    corner < 2 ? 0.5f - radius : -0.5f + radius);
                for (int step = 0; step <= steps; step++)
                {
                    int index = corner * (steps + 1) + step;
                    float theta = angle + step / (float)steps * Mathf.PI * 0.5f;
                    vertices[index + 1] = center + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * radius;
                    uv[index + 1] = (Vector2)vertices[index + 1] + Vector2.one * 0.5f;
                    triangles[index * 3] = 0;
                    triangles[index * 3 + 1] = index + 1;
                    triangles[index * 3 + 2] = (index + 1) % edgeCount + 1;
                }
            }
            Mesh mesh = new Mesh { name = "Interstellar_RoundedIndicator", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void ClearRuntime()
        {
            if (holder != null) Object.Destroy(holder);
            holder = null;
            visuals.Clear();
            seen.Clear();
            stale.Clear();
            if (plateMaterial != null) Object.Destroy(plateMaterial);
            if (whiteMaterial != null) Object.Destroy(whiteMaterial);
            if (lineMaterial != null) Object.Destroy(lineMaterial);
            if (textMaterial != null) Object.Destroy(textMaterial);
            if (roundedMesh != null) Object.Destroy(roundedMesh);
            if (squareMesh != null) Object.Destroy(squareMesh);
            plateMaterial = whiteMaterial = lineMaterial = textMaterial = null;
            roundedMesh = squareMesh = null;
        }

        public static void Shutdown()
        {
            names = voices = tracers = false;
            ClearRuntime();
        }
    }
}

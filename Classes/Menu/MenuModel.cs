using Interstellar.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Valve.Newtonsoft.Json.Linq;

namespace Interstellar.Classes.Menu
{
    public static class MenuModel
    {
        private const string ResourceName = PluginInfo.ClientResourcePath + ".interstellar_menu.glb";

        private static readonly Matrix4x4 Axes = new Matrix4x4(
            new Vector4(0f, -1f, 0f, 0f),
            new Vector4(0f, 0f, 1f, 0f),
            new Vector4(1f, 0f, 0f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        private class Node
        {
            public string name;
            public Vector3 position;
            public Quaternion rotation = Quaternion.identity;
            public Vector3 scale = Vector3.one;
            public Mesh mesh;
            public Material material;
            public readonly List<Node> children = new List<Node>();
        }

        private static List<Node> roots;
        private static bool loadFailed;

        public static bool Load()
        {
            if (roots != null)
                return true;

            if (loadFailed)
                return false;

            try
            {
                byte[] glb;
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                {
                    if (stream == null)
                        throw new FileNotFoundException($"Embedded resource {ResourceName} is missing");

                    using MemoryStream memory = new MemoryStream();
                    stream.CopyTo(memory);
                    glb = memory.ToArray();
                }

                roots = new GlbReader(glb).ReadScene();
                return true;
            }
            catch (Exception exc)
            {
                loadFailed = true;
                LogManager.LogError($"Failed to load the menu model, falling back to the classic menu: {exc}");
                return false;
            }
        }

        public static GameObject Create()
        {
            if (!Load())
                return null;

            GameObject root = new GameObject("Interstellar_MenuModel");
            foreach (Node node in roots)
                Spawn(node, root.transform);

            return root;
        }

        public static Transform FindPart(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name)
                    return child;

                Transform found = FindPart(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        public static Bounds GetBounds(Transform root, Transform part)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            Bounds bounds = default;
            bool found = false;

            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                Bounds meshBounds = filter.sharedMesh.bounds;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    corner = matrix.MultiplyPoint3x4(corner);

                    if (found)
                        bounds.Encapsulate(corner);
                    else
                    {
                        bounds = new Bounds(corner, Vector3.zero);
                        found = true;
                    }
                }
            }

            return bounds;
        }

        private static void Spawn(Node node, Transform parent)
        {
            GameObject gameObject = new GameObject(node.name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = node.position;
            gameObject.transform.localRotation = node.rotation;
            gameObject.transform.localScale = node.scale;

            if (node.mesh != null)
            {
                gameObject.AddComponent<MeshFilter>().sharedMesh = node.mesh;
                gameObject.AddComponent<MeshRenderer>().sharedMaterial = node.material;
            }

            foreach (Node child in node.children)
                Spawn(child, gameObject.transform);
        }

        private static Vector3 ToMenuSpace(float x, float y, float z) =>
            new Vector3(z, -x, y);

        private class GlbReader
        {
            private readonly byte[] glb;
            private readonly int binStart = -1;
            private readonly JObject gltf;

            private readonly Dictionary<int, List<KeyValuePair<Mesh, Material>>> meshes = new Dictionary<int, List<KeyValuePair<Mesh, Material>>>();
            private readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
            private readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
            private Material defaultMaterial;

            public GlbReader(byte[] glb)
            {
                this.glb = glb;

                if (glb.Length < 20 || BitConverter.ToUInt32(glb, 0) != 0x46546C67)
                    throw new InvalidDataException("Not a binary glTF (.glb) file");

                int offset = 12;
                while (offset + 8 <= glb.Length)
                {
                    int chunkLength = BitConverter.ToInt32(glb, offset);
                    uint chunkType = BitConverter.ToUInt32(glb, offset + 4);

                    if (chunkType == 0x4E4F534A)
                        gltf = JObject.Parse(Encoding.UTF8.GetString(glb, offset + 8, chunkLength));
                    else if (chunkType == 0x004E4942)
                        binStart = offset + 8;

                    offset += 8 + chunkLength;
                }

                if (gltf == null || binStart < 0)
                    throw new InvalidDataException("GLB is missing its JSON or binary chunk");
            }

            public List<Node> ReadScene()
            {
                int sceneIndex = (int?)gltf["scene"] ?? 0;
                List<Node> sceneRoots = new List<Node>();

                foreach (JToken nodeIndex in (JArray)gltf["scenes"][sceneIndex]["nodes"])
                    sceneRoots.Add(ReadNode((int)nodeIndex));

                return sceneRoots;
            }

            private Node ReadNode(int index)
            {
                JObject json = (JObject)gltf["nodes"][index];
                Node node = new Node { name = (string)json["name"] ?? $"node{index}" };

                Matrix4x4 local = Matrix4x4.identity;
                if (json["matrix"] is JArray matrix)
                {
                    for (int i = 0; i < 16; i++)
                        local[i % 4, i / 4] = (float)matrix[i];
                }
                else
                {
                    Vector3 translation = json["translation"] is JArray t ? new Vector3((float)t[0], (float)t[1], (float)t[2]) : Vector3.zero;
                    Quaternion rotation = json["rotation"] is JArray r ? new Quaternion((float)r[0], (float)r[1], (float)r[2], (float)r[3]) : Quaternion.identity;
                    Vector3 scale = json["scale"] is JArray s ? new Vector3((float)s[0], (float)s[1], (float)s[2]) : Vector3.one;
                    local = Matrix4x4.TRS(translation, rotation, scale);
                }

                local = Axes * local * Axes.transpose;
                node.position = local.GetColumn(3);
                node.rotation = local.rotation;
                node.scale = local.lossyScale;

                if (json["mesh"] != null)
                {
                    List<KeyValuePair<Mesh, Material>> primitives = ReadMesh((int)json["mesh"]);
                    for (int i = 0; i < primitives.Count; i++)
                    {
                        Node target = node;
                        if (i > 0)
                        {
                            target = new Node { name = $"{node.name}_{i}" };
                            node.children.Add(target);
                        }

                        target.mesh = primitives[i].Key;
                        target.material = primitives[i].Value;
                    }
                }

                if (json["children"] is JArray children)
                {
                    foreach (JToken child in children)
                        node.children.Add(ReadNode((int)child));
                }

                return node;
            }

            private List<KeyValuePair<Mesh, Material>> ReadMesh(int index)
            {
                if (meshes.TryGetValue(index, out List<KeyValuePair<Mesh, Material>> cached))
                    return cached;

                JObject json = (JObject)gltf["meshes"][index];
                List<KeyValuePair<Mesh, Material>> primitives = new List<KeyValuePair<Mesh, Material>>();

                foreach (JObject primitive in (JArray)json["primitives"])
                {
                    if (((int?)primitive["mode"] ?? 4) != 4)
                        throw new NotSupportedException("Only triangle meshes are supported");

                    Mesh mesh = ReadPrimitive(primitive, (string)json["name"] ?? $"mesh{index}");
                    primitives.Add(new KeyValuePair<Mesh, Material>(mesh, ReadMaterial((int?)primitive["material"])));
                }

                meshes[index] = primitives;
                return primitives;
            }

            private Mesh ReadPrimitive(JObject primitive, string name)
            {
                JObject attributes = (JObject)primitive["attributes"];

                float[] positions = ReadFloats((int)attributes["POSITION"], 3);
                int vertexCount = positions.Length / 3;

                Vector3[] vertices = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                    vertices[i] = ToMenuSpace(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);

                Mesh mesh = new Mesh { name = name };
                if (vertexCount > ushort.MaxValue)
                    mesh.indexFormat = IndexFormat.UInt32;

                mesh.vertices = vertices;

                if (attributes["NORMAL"] != null)
                {
                    float[] normals = ReadFloats((int)attributes["NORMAL"], 3);
                    Vector3[] converted = new Vector3[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                        converted[i] = ToMenuSpace(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2]);

                    mesh.normals = converted;
                }

                if (attributes["TEXCOORD_0"] != null)
                {
                    float[] uvs = ReadFloats((int)attributes["TEXCOORD_0"], 2);
                    Vector2[] converted = new Vector2[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                        converted[i] = new Vector2(uvs[i * 2], 1f - uvs[i * 2 + 1]);

                    mesh.uv = converted;
                }

                int[] indices;
                if (primitive["indices"] != null)
                    indices = ReadIndices((int)primitive["indices"]);
                else
                {
                    indices = new int[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                        indices[i] = i;
                }

                for (int i = 0; i + 2 < indices.Length; i += 3)
                    (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);

                mesh.triangles = indices;

                if (attributes["NORMAL"] == null)
                    mesh.RecalculateNormals();

                mesh.RecalculateBounds();
                return mesh;
            }

            private Material ReadMaterial(int? index)
            {
                if (index == null)
                    return defaultMaterial ??= new Material(FindShader(false)) { name = "Interstellar_MenuModelDefault" };

                if (materials.TryGetValue(index.Value, out Material cached))
                    return cached;

                JObject json = (JObject)gltf["materials"][index.Value];
                JObject pbr = json["pbrMetallicRoughness"] as JObject;
                bool unlit = json["extensions"]?["KHR_materials_unlit"] != null;

                Material material = new Material(FindShader(unlit)) { name = (string)json["name"] ?? $"material{index}" };

                if (pbr?["baseColorFactor"] is JArray factor)
                    material.color = new Color((float)factor[0], (float)factor[1], (float)factor[2], (float)factor[3]).gamma;
                else
                    material.color = Color.white;

                if (pbr?["baseColorTexture"]?["index"] != null)
                    material.mainTexture = ReadTexture((int)pbr["baseColorTexture"]["index"]);

                if (!unlit)
                {
                    material.SetFloat("_Metallic", (float?)pbr?["metallicFactor"] ?? 1f);
                    material.SetFloat("_Smoothness", 1f - ((float?)pbr?["roughnessFactor"] ?? 1f));
                }

                if (((string)json["alphaMode"] ?? "OPAQUE") != "OPAQUE")
                {
                    material.SetFloat("_Surface", 1);
                    material.SetFloat("_Blend", 0);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                    material.renderQueue = (int)RenderQueue.Transparent - 1;
                }

                materials[index.Value] = material;
                return material;
            }

            private static Shader FindShader(bool unlit) =>
                (unlit ? null : Shader.Find("Universal Render Pipeline/Lit")) ?? Shader.Find("Universal Render Pipeline/Unlit");

            private Texture2D ReadTexture(int index)
            {
                if (textures.TryGetValue(index, out Texture2D cached))
                    return cached;

                JObject json = (JObject)gltf["textures"][index];
                JObject image = (JObject)gltf["images"][(int)json["source"]];

                if (image["bufferView"] == null)
                    throw new NotSupportedException("Only images embedded in the .glb are supported");

                JObject view = GetBufferView((int)image["bufferView"]);
                byte[] bytes = new byte[(int)view["byteLength"]];
                Buffer.BlockCopy(glb, binStart + ((int?)view["byteOffset"] ?? 0), bytes, 0, bytes.Length);

                Texture2D texture = new Texture2D(2, 2) { name = $"Interstellar_MenuModel{index}" };
                texture.LoadImage(bytes, true);
                texture.filterMode = FilterMode.Trilinear;

                int wrap = json["sampler"] != null ? (int?)gltf["samplers"][(int)json["sampler"]]["wrapS"] ?? 10497 : 10497;
                texture.wrapMode = wrap switch
                {
                    33071 => TextureWrapMode.Clamp,
                    33648 => TextureWrapMode.Mirror,
                    _ => TextureWrapMode.Repeat
                };

                textures[index] = texture;
                return texture;
            }

            private JObject GetBufferView(int index)
            {
                JObject view = (JObject)gltf["bufferViews"][index];
                if (((int?)view["buffer"] ?? 0) != 0)
                    throw new NotSupportedException("Only the .glb's own binary chunk is supported as a buffer");

                return view;
            }

            private void GetAccessorData(JObject accessor, int elementSize, out int start, out int stride)
            {
                if (accessor["bufferView"] == null)
                    throw new NotSupportedException("Sparse accessors are not supported");

                JObject view = GetBufferView((int)accessor["bufferView"]);
                start = binStart + ((int?)view["byteOffset"] ?? 0) + ((int?)accessor["byteOffset"] ?? 0);
                stride = (int?)view["byteStride"] ?? elementSize;
            }

            private float[] ReadFloats(int accessorIndex, int components)
            {
                JObject accessor = (JObject)gltf["accessors"][accessorIndex];
                if ((int)accessor["componentType"] != 5126)
                    throw new NotSupportedException("Only float vertex attributes are supported");

                int count = (int)accessor["count"];
                GetAccessorData(accessor, components * 4, out int start, out int stride);

                float[] values = new float[count * components];
                for (int i = 0; i < count; i++)
                {
                    for (int c = 0; c < components; c++)
                        values[i * components + c] = BitConverter.ToSingle(glb, start + i * stride + c * 4);
                }

                return values;
            }

            private int[] ReadIndices(int accessorIndex)
            {
                JObject accessor = (JObject)gltf["accessors"][accessorIndex];
                int componentType = (int)accessor["componentType"];
                int size = componentType switch
                {
                    5121 => 1,
                    5123 => 2,
                    5125 => 4,
                    _ => throw new NotSupportedException($"Unsupported index type {componentType}")
                };

                int count = (int)accessor["count"];
                GetAccessorData(accessor, size, out int start, out int stride);

                int[] indices = new int[count];
                for (int i = 0; i < count; i++)
                {
                    int position = start + i * stride;
                    indices[i] = size switch
                    {
                        1 => glb[position],
                        2 => BitConverter.ToUInt16(glb, position),
                        _ => (int)BitConverter.ToUInt32(glb, position)
                    };
                }

                return indices;
            }
        }
    }
}

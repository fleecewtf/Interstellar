using System.Collections.Generic;
using UnityEngine;

namespace Interstellar.Classes.Menu
{
    public static class RoundedBox
    {
        private const int CornerSegments = 6;

        private static readonly Dictionary<long, Mesh> cache = new Dictionary<long, Mesh>();

        public static Mesh Get(float radiusY, float radiusZ)
        {
            radiusY = Mathf.Clamp(radiusY, 0.0005f, 0.5f);
            radiusZ = Mathf.Clamp(radiusZ, 0.0005f, 0.5f);

            long key = ((long)Mathf.RoundToInt(radiusY * 20000f) << 24) | (long)Mathf.RoundToInt(radiusZ * 20000f);
            if (cache.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;

            Mesh mesh = Build(radiusY, radiusZ);
            cache[key] = mesh;

            return mesh;
        }

        private static Mesh Build(float radiusY, float radiusZ)
        {
            int ringCount = (CornerSegments + 1) * 4;
            Vector2[] ring = new Vector2[ringCount];

            float[] centreY = { 0.5f - radiusY, -0.5f + radiusY, -0.5f + radiusY, 0.5f - radiusY };
            float[] centreZ = { 0.5f - radiusZ, 0.5f - radiusZ, -0.5f + radiusZ, -0.5f + radiusZ };

            int index = 0;
            for (int corner = 0; corner < 4; corner++)
            {
                for (int step = 0; step <= CornerSegments; step++)
                {
                    float angle = (corner * 90f + step * (90f / CornerSegments)) * Mathf.Deg2Rad;

                    ring[index++] = new Vector2(
                        centreY[corner] + Mathf.Cos(angle) * radiusY,
                        centreZ[corner] + Mathf.Sin(angle) * radiusZ);
                }
            }

            List<Vector3> vertices = new List<Vector3> { new Vector3(0.5f, 0f, 0f) };
            List<Vector2> uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };

            const int frontCentre = 0;
            int frontRing = 1;

            for (int i = 0; i < ringCount; i++)
            {
                vertices.Add(new Vector3(0.5f, ring[i].x, ring[i].y));
                uvs.Add(new Vector2(ring[i].x + 0.5f, ring[i].y + 0.5f));
            }

            int backCentre = vertices.Count;
            vertices.Add(new Vector3(-0.5f, 0f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            int backRing = vertices.Count;

            for (int i = 0; i < ringCount; i++)
            {
                vertices.Add(new Vector3(-0.5f, ring[i].x, ring[i].y));
                uvs.Add(new Vector2(ring[i].x + 0.5f, ring[i].y + 0.5f));
            }

            List<int> triangles = new List<int>();

            for (int i = 0; i < ringCount; i++)
            {
                int next = (i + 1) % ringCount;

                triangles.Add(frontCentre);
                triangles.Add(frontRing + i);
                triangles.Add(frontRing + next);

                triangles.Add(backCentre);
                triangles.Add(backRing + next);
                triangles.Add(backRing + i);

                triangles.Add(frontRing + i);
                triangles.Add(backRing + i);
                triangles.Add(backRing + next);

                triangles.Add(frontRing + i);
                triangles.Add(backRing + next);
                triangles.Add(frontRing + next);
            }

            Mesh mesh = new Mesh { name = "Interstellar_RoundedBox" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            if (Vector3.Dot(mesh.normals[frontCentre], Vector3.right) < 0f)
            {
                triangles.Reverse();
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

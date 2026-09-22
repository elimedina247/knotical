using System.Collections.Generic;
using UnityEngine;

namespace Knotical
{
    public static class CloudMeshBuilder
    {
        private static readonly float Phi = (1f + Mathf.Sqrt(5f)) * 0.5f;

        private static readonly Vector3[] IcoVertices =
        {
            new Vector3(-1f, Phi, 0f), new Vector3(1f, Phi, 0f), new Vector3(-1f, -Phi, 0f), new Vector3(1f, -Phi, 0f),
            new Vector3(0f, -1f, Phi), new Vector3(0f, 1f, Phi), new Vector3(0f, -1f, -Phi), new Vector3(0f, 1f, -Phi),
            new Vector3(Phi, 0f, -1f), new Vector3(Phi, 0f, 1f), new Vector3(-Phi, 0f, -1f), new Vector3(-Phi, 0f, 1f),
        };

        private static readonly int[] IcoTriangles =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        public static Mesh Build(System.Random rng, float size)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();

            int blobs = rng.Next(3, 6);
            for (int b = 0; b < blobs; b++)
            {
                bool main = b == 0;
                float radius = size * (main ? 0.5f : Lerp(rng, 0.25f, 0.4f));
                Vector3 centre = main
                    ? Vector3.zero
                    : new Vector3(Lerp(rng, -0.55f, 0.55f) * size, -Lerp(rng, 0f, 0.15f) * size, Lerp(rng, -0.3f, 0.3f) * size);
                AddBlob(vertices, colors, rng, centre, radius);
            }

            var normals = new Vector3[vertices.Count];
            var triangles = new int[vertices.Count];
            for (int i = 0; i < vertices.Count; i += 3)
            {
                Vector3 n = Vector3.Cross(vertices[i + 1] - vertices[i], vertices[i + 2] - vertices[i]).normalized;
                normals[i] = normals[i + 1] = normals[i + 2] = n;
                triangles[i] = i;
                triangles[i + 1] = i + 1;
                triangles[i + 2] = i + 2;
            }

            var mesh = new Mesh { name = "Cloud" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBlob(List<Vector3> vertices, List<Color> colors, System.Random rng, Vector3 centre, float radius)
        {
            var points = new List<Vector3>(IcoVertices);
            var faces = new List<int>(IcoTriangles);
            Subdivide(points, faces);

            var jitter = new float[points.Count];
            for (int i = 0; i < points.Count; i++) jitter[i] = Lerp(rng, 0.78f, 1.12f);

            var squash = new Vector3(1f, 0.55f, 1f);
            for (int i = 0; i < faces.Count; i++)
            {
                int index = faces[i];
                Vector3 dir = points[index].normalized;
                Vector3 p = centre + Vector3.Scale(dir * (radius * jitter[index]), squash);
                vertices.Add(p);
                float shade = Mathf.Lerp(0.82f, 1f, Mathf.InverseLerp(-radius * 0.55f, radius * 0.3f, p.y - centre.y));
                colors.Add(new Color(shade, shade, shade, 1f));
            }
        }

        private static void Subdivide(List<Vector3> points, List<int> faces)
        {
            var cache = new Dictionary<long, int>();
            var result = new List<int>(faces.Count * 4);
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Midpoint(points, cache, a, b);
                int bc = Midpoint(points, cache, b, c);
                int ca = Midpoint(points, cache, c, a);
                result.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            faces.Clear();
            faces.AddRange(result);
        }

        private static int Midpoint(List<Vector3> points, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            points.Add(((points[a] + points[b]) * 0.5f).normalized * points[a].magnitude);
            index = points.Count - 1;
            cache[key] = index;
            return index;
        }

        private static float Lerp(System.Random rng, float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
    }
}

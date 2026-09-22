using UnityEngine;
using UnityEngine.Rendering;

namespace Knotical
{
    public static class TerrainMeshBuilder
    {
        public static Mesh Build(Heightmap map, LevelSettings s)
        {
            int n = map.Size;
            int quads = (n - 1) * (n - 1);
            var vertices = new Vector3[quads * 6];
            var normals = new Vector3[quads * 6];
            var colors = new Color[quads * 6];
            var triangles = new int[quads * 6];

            int v = 0;
            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    Vector3 a = Corner(map, x, z);
                    Vector3 b = Corner(map, x + 1, z);
                    Vector3 c = Corner(map, x, z + 1);
                    Vector3 d = Corner(map, x + 1, z + 1);

                    v = Triangle(vertices, normals, colors, v, a, c, b, s);
                    v = Triangle(vertices, normals, colors, v, b, c, d, s);
                }
            }

            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;

            var mesh = new Mesh
            {
                name = "LevelTerrain",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Corner(Heightmap map, int x, int z)
        {
            Vector2 p = map.CellCentre(x, z);
            return new Vector3(p.x, map[x, z], p.y);
        }

        private static int Triangle(Vector3[] vertices, Vector3[] normals, Color[] colors, int v, Vector3 a, Vector3 b, Vector3 c, LevelSettings s)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            Color color = ColourFor((a.y + b.y + c.y) / 3f, s);

            vertices[v] = a;
            vertices[v + 1] = b;
            vertices[v + 2] = c;
            for (int i = 0; i < 3; i++)
            {
                normals[v + i] = normal;
                colors[v + i] = color;
            }
            return v + 3;
        }

        public static Color ColourFor(float h, LevelSettings s)
        {
            if (s == null) return Color.gray;
            if (h > s.RockHeight) return s.Rock;
            if (h > 1.5f) return Color.Lerp(s.Grass, s.Rock, Mathf.InverseLerp(s.RockHeight * 0.5f, s.RockHeight, h));
            if (h > -1.5f) return s.Sand;
            return Color.Lerp(s.Sand, s.SeaFloor, Mathf.Clamp01(-h / s.MaxDepth));
        }
    }
}

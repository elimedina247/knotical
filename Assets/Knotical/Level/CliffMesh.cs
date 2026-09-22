using UnityEngine;

namespace Knotical
{
    public static class CliffMesh
    {
        private static readonly Vector3[] FaceNormals =
        {
            Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
        };

        public static Mesh Build()
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var heights = new Vector2[24];
            var triangles = new int[36];

            int v = 0;
            int t = 0;
            foreach (Vector3 normal in FaceNormals)
            {
                Vector3 tangent = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                Vector3 centre = normal * 0.5f;

                vertices[v] = centre - tangent * 0.5f - bitangent * 0.5f;
                vertices[v + 1] = centre + tangent * 0.5f - bitangent * 0.5f;
                vertices[v + 2] = centre + tangent * 0.5f + bitangent * 0.5f;
                vertices[v + 3] = centre - tangent * 0.5f + bitangent * 0.5f;

                for (int i = 0; i < 4; i++)
                {
                    normals[v + i] = normal;
                    heights[v + i] = new Vector2(0f, vertices[v + i].y + 0.5f);
                }

                triangles[t++] = v;
                triangles[t++] = v + 1;
                triangles[t++] = v + 2;
                triangles[t++] = v;
                triangles[t++] = v + 2;
                triangles[t++] = v + 3;
                v += 4;
            }

            var mesh = new Mesh { name = "CliffBlock" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(1, heights);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

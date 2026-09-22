using UnityEngine;

namespace Knotical
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Sail : MonoBehaviour
    {
        [Min(0.5f)] public float Width = 5.2f;
        [Min(0.5f)] public float Drop = 5f;
        [Range(0f, 0.6f)] public float Camber = 0.3f;
        [Range(0f, 0.3f)] public float FootTaper = 0.06f;
        [Range(2, 32)] public int Columns = 12;
        [Range(2, 32)] public int Rows = 10;
        [Range(0f, 1f)] public float MinFill = 0.25f;

        private SailRig rig;
        private MeshFilter filter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Vector3[] vertices;
        private int[] triangles;
        private float lastDeployment = -1f;
        private float lastBelly = -1f;

        private void Awake()
        {
            rig = GetComponentInParent<SailRig>();
            filter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            mesh = new Mesh { name = "Sail" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            BuildTopology();
        }

        private void LateUpdate()
        {
            float deployment = rig != null ? rig.Deployment : 1f;
            float belly = 0f;
            if (rig != null && deployment > 0f)
            {
                float along = Vector3.Dot(rig.ApparentWindVelocity, transform.forward);
                float fill = Mathf.Lerp(MinFill, 1f, rig.Pressure);
                belly = Mathf.Sign(along == 0f ? 1f : along) * fill;
            }

            meshRenderer.enabled = deployment > 0.02f;
            if (!meshRenderer.enabled) return;
            if (Mathf.Abs(deployment - lastDeployment) < 0.0005f && Mathf.Abs(belly - lastBelly) < 0.0005f) return;

            lastDeployment = deployment;
            lastBelly = belly;
            Shape(deployment, belly);
        }

        private void BuildTopology()
        {
            int cols = Columns + 1;
            int rows = Rows + 1;
            int count = cols * rows;
            vertices = new Vector3[count * 2];
            triangles = new int[Columns * Rows * 12];

            int t = 0;
            for (int side = 0; side < 2; side++)
            {
                int offset = side * count;
                for (int r = 0; r < Rows; r++)
                {
                    for (int c = 0; c < Columns; c++)
                    {
                        int a = offset + r * cols + c;
                        int b = a + 1;
                        int d = a + cols;
                        int e = d + 1;
                        if (side == 0)
                        {
                            triangles[t++] = a; triangles[t++] = b; triangles[t++] = d;
                            triangles[t++] = b; triangles[t++] = e; triangles[t++] = d;
                        }
                        else
                        {
                            triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
                            triangles[t++] = b; triangles[t++] = d; triangles[t++] = e;
                        }
                    }
                }
            }

            Shape(0f, 0f);
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void Shape(float deployment, float belly)
        {
            int cols = Columns + 1;
            int rows = Rows + 1;
            int count = cols * rows;
            float hang = Mathf.Max(deployment, 0.001f);

            for (int r = 0; r < rows; r++)
            {
                float v = (float)r / Rows;
                float hung = Mathf.Min(v, hang);
                float local = hung / hang;
                float y = -hung * Drop;
                float halfWidth = Width * 0.5f * (1f - FootTaper * hung);
                for (int c = 0; c < cols; c++)
                {
                    float u = (float)c / Columns;
                    float x = (u - 0.5f) * 2f * halfWidth;
                    float bulge = belly * Camber * Width * Mathf.Sin(u * Mathf.PI) * Mathf.Sin(local * Mathf.PI) * hang;
                    var p = new Vector3(x, y, bulge);
                    int i = r * cols + c;
                    vertices[i] = p;
                    vertices[i + count] = p;
                }
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}

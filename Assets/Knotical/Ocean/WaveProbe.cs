using UnityEngine;

namespace Knotical
{
    public static class WaveProbe
    {
        public struct Result
        {
            public float MaxPositionError;
            public float MaxNormalAngle;
            public int WorstIndex;
            public Vector3[] GpuSurface;
            public Vector3[] GpuNormals;
        }

        public static readonly Vector2[] DefaultPoints =
        {
            new Vector2(0f, 0f),
            new Vector2(3.2f, -7.9f),
            new Vector2(-14.6f, 11.1f),
            new Vector2(19.5f, 18.25f),
            new Vector2(-42.75f, -33.9f),
            new Vector2(250f, -180f),
            new Vector2(3000f, -2500f),
        };

        public static Result Run(ComputeShader shader, Vector2[] points)
        {
            int count = points.Length;
            int kernel = shader.FindKernel("Probe");

            OceanUniforms.Push();
            OceanUniforms.Apply(shader);

            var pointBuffer = new ComputeBuffer(count, sizeof(float) * 2);
            var surfaceBuffer = new ComputeBuffer(count, sizeof(float) * 3);
            var normalBuffer = new ComputeBuffer(count, sizeof(float) * 3);

            try
            {
                pointBuffer.SetData(points);
                shader.SetBuffer(kernel, "_ProbePoints", pointBuffer);
                shader.SetBuffer(kernel, "_ProbeSurface", surfaceBuffer);
                shader.SetBuffer(kernel, "_ProbeNormals", normalBuffer);
                shader.SetInt("_ProbeCount", count);
                shader.Dispatch(kernel, Mathf.CeilToInt(count / 64f), 1, 1);

                var result = new Result
                {
                    GpuSurface = new Vector3[count],
                    GpuNormals = new Vector3[count],
                };
                surfaceBuffer.GetData(result.GpuSurface);
                normalBuffer.GetData(result.GpuNormals);

                for (int i = 0; i < count; i++)
                {
                    float positionError = Vector3.Distance(result.GpuSurface[i], Ocean.GetSurfacePoint(points[i]));
                    float normalAngle = Vector3.Angle(result.GpuNormals[i], Ocean.GetNormal(points[i]));

                    if (positionError > result.MaxPositionError)
                    {
                        result.MaxPositionError = positionError;
                        result.WorstIndex = i;
                    }

                    result.MaxNormalAngle = Mathf.Max(result.MaxNormalAngle, normalAngle);
                }

                return result;
            }
            finally
            {
                pointBuffer.Release();
                surfaceBuffer.Release();
                normalBuffer.Release();
            }
        }
    }
}

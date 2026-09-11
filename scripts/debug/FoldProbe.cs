using System.Collections.Generic;
using Godot;


namespace Knotical.Debug;

[GlobalClass]
public partial class FoldProbe : Node
{
    private const float Gravity = 9.81f;

    public override void _Ready()
    {
        Knotical.Ocean.Ocean ocean = Knotical.Ocean.Ocean.Instance;
        if (ocean == null)
        {
            GD.Print("foldprobe FAIL: no ocean");
            GetTree().Quit();
            return;
        }

        var folds = new List<float>();
        var heights = new List<float>();
        float maxFold = 0f;
        float maxHeightAtMaxFold = 0f;
        int bins = 20;
        var histogram = new int[bins];

        for (float t = 0f; t < 120f; t += 0.5f)
        {
            for (float z = 0f; z < 300f; z += 3f)
            {
                for (float x = 0f; x < 300f; x += 3f)
                {
                    Fold(ocean, new Vector2(x, z), t, out float fold, out float height);
                    folds.Add(fold);
                    heights.Add(height);
                    histogram[Mathf.Clamp((int)(fold * bins), 0, bins - 1)]++;
                    if (fold > maxFold)
                    {
                        maxFold = fold;
                        maxHeightAtMaxFold = height;
                    }
                }
            }
        }

        folds.Sort();
        GD.Print($"foldprobe samples={folds.Count} sig={ocean.SignificantHeight:0.00} waves={ocean.Waves.Length}");
        GD.Print($"foldprobe max={maxFold:0.000} (height {maxHeightAtMaxFold:0.00}) " +
            $"p50={folds[folds.Count / 2]:0.000} p90={folds[(int)(folds.Count * 0.9)]:0.000} " +
            $"p99={folds[(int)(folds.Count * 0.99)]:0.000} p999={folds[(int)(folds.Count * 0.999)]:0.000} " +
            $"p9999={folds[(int)(folds.Count * 0.9999)]:0.000}");
        for (int i = 0; i < bins; i++)
        {
            GD.Print($"foldprobe bin {i / (float)bins:0.00}-{(i + 1) / (float)bins:0.00}: {histogram[i] / (float)folds.Count * 100f:0.000}%");
        }
        GetTree().Quit();
    }

    private static void Fold(Knotical.Ocean.Ocean ocean, Vector2 p, float t, out float fold, out float height)
    {
        Vector4[] waves = ocean.Waves;
        float[] steepness = ocean.Steepness;
        float[] phases = ocean.Phases;
        float jxx = 0f, jxz = 0f, jzz = 0f;
        height = 0f;

        for (int i = 0; i < waves.Length; i++)
        {
            Vector4 w = waves[i];
            var dir = new Vector2(w.X, w.Y);
            float k = Mathf.Tau / w.W;
            float omega = Mathf.Sqrt(Gravity * k);
            float phase = k * dir.Dot(p) - omega * t + phases[i];
            float s = Mathf.Sin(phase);
            height += w.Z * s;
            float qak = steepness[i] / waves.Length * s;
            jxx += qak * dir.X * dir.X;
            jxz += qak * dir.X * dir.Y;
            jzz += qak * dir.Y * dir.Y;
        }

        float jacobian = (1f - jxx) * (1f - jzz) - jxz * jxz;
        fold = Mathf.Clamp(1f - jacobian, 0f, 1f);
    }
}

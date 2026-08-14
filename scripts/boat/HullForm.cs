using System.Collections.Generic;
using Godot;

namespace Knotical.Boat;

public sealed class HullForm
{
    public float Length = 17f;
    public float Beam = 6.2f;
    public float Draft = 2.1f;
    public float Freeboard = 2.3f;
    public float BowSheerRise = 1.6f;
    public float SternSheerRise = 1.2f;
    public float SheerPower = 2.4f;
    public float Rocker = 1.75f;
    public float RockerPower = 2.6f;
    public float BowSharpness = 2.2f;
    public float SternSharpness = 3f;
    public float TransomWidth = 0.55f;
    public float TransomRake = 0.6f;
    public float StemRake = 0f;
    public float StemPower = 3f;
    public float BilgeFullness = 0.42f;

    public float PlanFactor(float t)
    {
        float m = Mathf.Abs(t * 2f - 1f);
        float sharpness = t > 0.5f ? BowSharpness : SternSharpness;
        float f = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(m, sharpness)), 0.65f);
        if (t < 0.5f) f = Mathf.Lerp(f, 1f, TransomWidth * m * m);
        return f;
    }

    public float RakeZ(float t, float v)
    {
        if (t < 0.5f)
        {
            float m = 1f - t * 2f;
            return TransomRake * v * m * m * m;
        }

        float n = t * 2f - 1f;
        return -StemRake * v * Mathf.Pow(n, StemPower);
    }

    public float SectionFactor(float v) => Mathf.Pow(Mathf.Clamp(v, 0f, 1f), BilgeFullness);

    public float KeelY(float t)
    {
        float m = Mathf.Abs(t * 2f - 1f);
        return -Draft + Rocker * Mathf.Pow(m, RockerPower);
    }

    public float SheerY(float t)
    {
        float m = Mathf.Abs(t * 2f - 1f);
        float rise = t > 0.5f ? BowSheerRise : SternSheerRise;
        return Freeboard + rise * Mathf.Pow(m, SheerPower);
    }

    public float ZAt(float t) => Mathf.Lerp(Length * 0.5f, -Length * 0.5f, t);

    public float VAtHeight(float t, float y)
    {
        float keel = KeelY(t);
        float sheer = SheerY(t);
        if (sheer - keel < 0.001f) return 0f;
        return Mathf.Clamp((y - keel) / (sheer - keel), 0f, 1f);
    }

    public float VolumeBelow(float y, float offset, int stations, int levels)
    {
        int nt = Mathf.Max(8, stations);
        int nv = Mathf.Max(8, levels);
        float slice = Length / nt;
        float total = 0f;

        for (int i = 0; i < nt; i++)
        {
            float t = (i + 0.5f) / nt;
            float keel = KeelY(t);
            float span = SheerY(t) - keel;
            if (span <= 0f) continue;

            float widest = Beam * 0.5f * PlanFactor(t);
            float step = span / nv;
            float area = 0f;

            for (int j = 0; j < nv; j++)
            {
                float v = (j + 0.5f) / nv;
                if (keel + span * v > y) break;
                area += 2f * Mathf.Max(0.02f, widest * SectionFactor(v) + offset) * step;
            }

            total += area * slice;
        }

        return total;
    }

    public Vector3 Shell(float t, float v, float offset, float side)
    {
        float y = Mathf.Lerp(KeelY(t), SheerY(t), v);
        float half = Mathf.Max(0.02f, Beam * 0.5f * PlanFactor(t) * SectionFactor(v) + offset);
        return new Vector3(half * side, y, ZAt(t) + RakeZ(t, v));
    }

    public Vector3[] Ring(float v, float offset, int stations)
    {
        int count = Mathf.Max(3, stations);
        var points = new Vector3[count * 2];
        for (int j = 0; j < count; j++)
        {
            float t = (float)j / (count - 1);
            Vector3 starboard = Shell(t, v, offset, 1f);
            points[j] = starboard;
            points[count * 2 - 1 - j] = new Vector3(-starboard.X, starboard.Y, starboard.Z);
        }
        return points;
    }

    public Vector3[] RingAtHeight(float y, float offset, int stations)
    {
        int count = Mathf.Max(3, stations);
        var points = new Vector3[count * 2];
        for (int j = 0; j < count; j++)
        {
            float t = (float)j / (count - 1);
            Vector3 starboard = Shell(t, VAtHeight(t, y), offset, 1f);
            points[j] = new Vector3(starboard.X, y, starboard.Z);
            points[count * 2 - 1 - j] = new Vector3(-starboard.X, y, starboard.Z);
        }
        return points;
    }

    public void BuildSolid(int stations, int rings, float offset, out Vector3[] vertices, out int[] indices)
    {
        int count = Mathf.Max(3, stations);
        int levels = Mathf.Max(2, rings);
        int loop = count * 2;
        float bias = Mathf.Clamp(1f / Mathf.Max(BilgeFullness, 0.05f), 0.5f, 6f);

        var points = new List<Vector3>(levels * loop + count * 4);
        for (int r = 0; r < levels; r++)
        {
            float v = Mathf.Pow((float)r / (levels - 1), bias);
            Vector3[] ring = Ring(v, offset, count);
            for (int j = 0; j < loop; j++) points.Add(ring[j]);
        }

        var tris = new List<int>((levels - 1) * loop * 6 + count * 24);

        for (int r = 0; r < levels - 1; r++)
        {
            for (int j = 0; j < loop; j++)
            {
                int next = (j + 1) % loop;
                int a = r * loop + j;
                int b = r * loop + next;
                int c = (r + 1) * loop + next;
                int d = (r + 1) * loop + j;
                bool straddles = j == count - 1 || j == loop - 1;
                Quad(points, tris, a, b, c, d, straddles, j >= count);
            }
        }

        for (int j = 0; j < count - 1; j++)
        {
            int keel = j;
            int sheer = (levels - 1) * loop + j;
            Quad(points, tris, keel, loop - 1 - keel, loop - 2 - keel, keel + 1, true, false);
            Quad(points, tris, sheer, sheer + 1, sheer + loop - 2 - 2 * j, sheer + loop - 1 - 2 * j, true, false);
        }

        vertices = points.ToArray();
        indices = tris.ToArray();

        float volume = 0f;
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 a = vertices[indices[i]];
            Vector3 b = vertices[indices[i + 1]];
            Vector3 c = vertices[indices[i + 2]];
            volume += a.Dot(b.Cross(c));
        }

        if (volume >= 0f) return;

        for (int i = 0; i < indices.Length; i += 3)
        {
            (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
        }
    }

    private static void Quad(List<Vector3> points, List<int> tris, int a, int b, int c, int d, bool straddles, bool mirrored)
    {
        if (straddles)
        {
            points.Add((points[a] + points[b] + points[c] + points[d]) * 0.25f);
            int m = points.Count - 1;
            tris.Add(a); tris.Add(b); tris.Add(m);
            tris.Add(b); tris.Add(c); tris.Add(m);
            tris.Add(c); tris.Add(d); tris.Add(m);
            tris.Add(d); tris.Add(a); tris.Add(m);
            return;
        }

        if (mirrored)
        {
            tris.Add(a); tris.Add(b); tris.Add(d);
            tris.Add(b); tris.Add(c); tris.Add(d);
            return;
        }

        tris.Add(a); tris.Add(b); tris.Add(c);
        tris.Add(a); tris.Add(c); tris.Add(d);
    }
}

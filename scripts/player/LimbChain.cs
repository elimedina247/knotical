using Godot;

namespace Knotical.Player;

public sealed class LimbChain
{
    public Vector3[] Points = System.Array.Empty<Vector3>();
    public Vector3[] Previous = System.Array.Empty<Vector3>();
    public float SegmentLength = 0.1f;
    public float Gravity = 20f;
    public float Damping = 0.2f;
    public float Stiffness = 0.8f;
    public int Iterations = 8;

    public int Links => Points.Length - 1;
    public float Span => SegmentLength * Links;
    public Vector3 Tip => Points[Points.Length - 1];

    public void Build(int segments, float length, Vector3 root, Vector3 direction)
    {
        int count = Mathf.Max(1, segments) + 1;
        SegmentLength = length / (count - 1);
        Points = new Vector3[count];
        Previous = new Vector3[count];
        Place(root, direction);
    }

    public void Place(Vector3 root, Vector3 direction)
    {
        Vector3 step = direction.Normalized() * SegmentLength;
        for (int i = 0; i < Points.Length; i++)
        {
            Points[i] = root + step * i;
            Previous[i] = Points[i];
        }
    }

    public Vector3 Reachable(Vector3 root, Vector3 target)
    {
        Vector3 span = target - root;
        float distance = span.Length();
        float limit = Span * 0.995f;

        return distance <= limit || distance < 1e-5f ? target : root + span * (limit / distance);
    }

    public void Step(float dt, Vector3 root, bool pinned, Vector3 target)
    {
        if (pinned) target = Reachable(root, target);

        int last = Points.Length - 1;
        float retain = Mathf.Pow(Mathf.Clamp(Damping, 0.001f, 1f), dt);
        Vector3 fall = Vector3.Down * (Gravity * dt * dt);

        for (int i = 1; i < Points.Length; i++)
        {
            Vector3 velocity = (Points[i] - Previous[i]) * retain;
            Previous[i] = Points[i];
            Points[i] += velocity + fall;
        }

        for (int pass = 0; pass < Iterations; pass++)
        {
            Points[0] = root;
            if (pinned) Points[last] = target;

            for (int i = 0; i < last; i++)
            {
                bool lockLow = i == 0;
                bool lockHigh = pinned && i + 1 == last;
                if (lockLow && lockHigh) continue;

                Vector3 link = Points[i + 1] - Points[i];
                float distance = link.Length();
                if (distance < 1e-5f) continue;

                Vector3 push = link * ((distance - SegmentLength) / distance * Stiffness);
                if (lockLow) Points[i + 1] -= push;
                else if (lockHigh) Points[i] += push;
                else
                {
                    Points[i] += push * 0.5f;
                    Points[i + 1] -= push * 0.5f;
                }
            }
        }

        Points[0] = root;
        if (pinned) Points[last] = target;

        Contract();
    }

    public void Contract()
    {
        for (int i = 0; i < Points.Length - 1; i++)
        {
            Vector3 link = Points[i + 1] - Points[i];
            float distance = link.Length();
            if (distance <= SegmentLength) continue;

            Points[i + 1] = Points[i] + link * (SegmentLength / distance);
        }
    }

    public void Straighten(float amount)
    {
        if (amount <= 0f || Points.Length < 3) return;

        int last = Points.Length - 1;
        Vector3 root = Points[0];
        Vector3 tip = Points[last];

        float taut = Span > 1e-5f ? Mathf.Clamp(root.DistanceTo(tip) / Span, 0f, 1f) : 0f;
        float blend = amount * taut;
        if (blend <= 0f) return;

        for (int i = 1; i < last; i++)
        {
            Points[i] = Points[i].Lerp(root.Lerp(tip, (float)i / last), blend);
            Previous[i] = Points[i];
        }
    }

    public void PushOutside(Vector3 a, Vector3 b, float radius)
    {
        Vector3 axis = b - a;
        float span = axis.LengthSquared();

        for (int i = 1; i < Points.Length; i++)
        {
            float t = span < 1e-6f ? 0f : Mathf.Clamp((Points[i] - a).Dot(axis) / span, 0f, 1f);
            Vector3 nearest = a + axis * t;
            Vector3 offset = Points[i] - nearest;
            float distance = offset.Length();
            if (distance >= radius) continue;
            Points[i] = distance < 1e-5f
                ? nearest + Vector3.Right * radius
                : nearest + offset * (radius / distance);
        }
    }
}

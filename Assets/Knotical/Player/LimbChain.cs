using UnityEngine;

namespace Knotical
{
    public sealed class LimbChain
    {
        public Vector3[] Points = System.Array.Empty<Vector3>();
        public Vector3[] Previous = System.Array.Empty<Vector3>();
        public float[] Lengths = System.Array.Empty<float>();
        public float Gravity = 20f;
        public float Damping = 0.2f;
        public float Stiffness = 0.8f;
        public int Iterations = 8;

        public int Links => Lengths.Length;
        public Vector3 Tip => Points[Points.Length - 1];

        public float Span
        {
            get
            {
                float total = 0f;
                foreach (float length in Lengths) total += length;
                return total;
            }
        }

        public void Build(float[] lengths, Vector3 root, Vector3 direction)
        {
            Lengths = (float[])lengths.Clone();
            Points = new Vector3[Lengths.Length + 1];
            Previous = new Vector3[Points.Length];
            Place(root, direction);
        }

        public void Place(Vector3 root, Vector3 direction)
        {
            Vector3 step = direction.normalized;
            Vector3 at = root;
            Points[0] = at;
            Previous[0] = at;
            for (int i = 0; i < Lengths.Length; i++)
            {
                at += step * Lengths[i];
                Points[i + 1] = at;
                Previous[i + 1] = at;
            }
        }

        public Vector3 Reachable(Vector3 root, Vector3 target)
        {
            Vector3 span = target - root;
            float distance = span.magnitude;
            float limit = Span * 0.995f;
            return distance <= limit || distance < 1e-5f ? target : root + span * (limit / distance);
        }

        public void Step(float dt, Vector3 root, bool pinned, Vector3 target)
        {
            if (pinned) target = Reachable(root, target);

            int last = Points.Length - 1;
            float retain = Mathf.Pow(Mathf.Clamp(Damping, 0.001f, 1f), dt);
            Vector3 fall = Vector3.down * (Gravity * dt * dt);

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
                    float distance = link.magnitude;
                    if (distance < 1e-5f) continue;

                    Vector3 push = link * ((distance - Lengths[i]) / distance * Stiffness);
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
                float distance = link.magnitude;
                if (distance <= Lengths[i]) continue;
                Points[i + 1] = Points[i] + link * (Lengths[i] / distance);
            }
        }

        public void Straighten(float amount)
        {
            if (amount <= 0f || Points.Length < 3) return;

            int last = Points.Length - 1;
            Vector3 root = Points[0];
            Vector3 tip = Points[last];
            float span = Span;

            float taut = span > 1e-5f ? Mathf.Clamp01(Vector3.Distance(root, tip) / span) : 0f;
            float blend = amount * taut;
            if (blend <= 0f) return;

            float along = 0f;
            for (int i = 1; i < last; i++)
            {
                along += Lengths[i - 1];
                Points[i] = Vector3.Lerp(Points[i], Vector3.Lerp(root, tip, along / span), blend);
                Previous[i] = Points[i];
            }
        }

        public void PushOutside(Vector3 a, Vector3 b, float radius)
        {
            Vector3 axis = b - a;
            float span = axis.sqrMagnitude;

            for (int i = 1; i < Points.Length; i++)
            {
                float t = span < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(Points[i] - a, axis) / span);
                Vector3 nearest = a + axis * t;
                Vector3 offset = Points[i] - nearest;
                float distance = offset.magnitude;
                if (distance >= radius) continue;
                Points[i] = distance < 1e-5f
                    ? nearest + Vector3.right * radius
                    : nearest + offset * (radius / distance);
            }
        }
    }
}

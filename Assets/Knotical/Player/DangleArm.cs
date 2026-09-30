using UnityEngine;

namespace Knotical
{
    public class DangleArm : MonoBehaviour
    {
        [Range(0f, 60f)] public float Gravity = 16f;
        [Range(0.01f, 1f)] public float Damping = 0.45f;
        [Range(0.1f, 1f)] public float Stiffness = 0.8f;
        [Range(0.02f, 0.6f)] public float ReachTime = 0.12f;
        [Range(0.02f, 0.6f)] public float RelaxTime = 0.2f;
        [Range(0.01f, 1f)] public float RigidDamping = 0.04f;
        [Range(0f, 2f)] public float ElbowOut = 0.6f;
        [Range(0f, 0.3f)] public float ClearancePad = 0.02f;
        public Vector3 RootShift;

        private readonly LimbChain chain = new LimbChain();
        private LimbPart upper;
        private LimbPart lower;
        private Transform torso;
        private float spineLow;
        private float spineHigh;
        private float clearance;
        private float side = 1f;
        private float rigid;
        private bool gripping;
        private Vector3 target;

        private Vector3 RootPoint => torso != null
            ? transform.position + torso.TransformVector(RootShift)
            : transform.position;

        public bool IsGripping => gripping;
        public Vector3 Tip => chain.Points.Length > 0 ? chain.Tip : transform.position;
        public Vector3 Root => RootPoint;
        public float Rigidity => rigid;
        public float Length => chain.Span;

        public void Grip(Vector3 worldTarget)
        {
            gripping = true;
            target = worldTarget;
        }

        public void Release()
        {
            gripping = false;
        }

        private void Awake()
        {
            Rigidbody body = GetComponentInParent<Rigidbody>();
            torso = body != null ? body.transform : transform.root;

            Transform upperT = transform.childCount > 0 ? transform.GetChild(0) : null;
            Transform lowerT = upperT != null && upperT.childCount > 0 ? upperT.GetChild(0) : null;
            if (upperT == null || lowerT == null)
            {
                enabled = false;
                return;
            }

            side = torso.InverseTransformPoint(transform.position).x < 0f ? -1f : 1f;
            Vector3 outward = torso.right * side;
            upper = LimbPart.From(upperT, outward);
            lower = LimbPart.From(lowerT, outward);

            MeasureTorso();
            chain.Build(new[] { upper.Length, lower.Length }, RootPoint, Vector3.down);
        }

        private void MeasureTorso()
        {
            var filter = transform.parent != null ? transform.parent.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                spineLow = 0.6f;
                spineHigh = 0.95f;
                clearance = 0.27f;
                return;
            }

            Bounds b = filter.sharedMesh.bounds;
            Transform t = filter.transform;
            Vector3 lo = torso.InverseTransformPoint(t.TransformPoint(b.min));
            Vector3 hi = torso.InverseTransformPoint(t.TransformPoint(b.max));
            float radius = Mathf.Max(Mathf.Abs(hi.x - lo.x), Mathf.Abs(hi.z - lo.z)) * 0.5f;
            float bottom = Mathf.Min(lo.y, hi.y);
            float top = Mathf.Max(lo.y, hi.y);
            spineLow = bottom + radius;
            spineHigh = Mathf.Max(spineLow, top - radius);
            clearance = radius + upper.Length * 0.15f + ClearancePad;
        }

        private void FixedUpdate()
        {
            if (upper == null) return;

            float dt = Time.fixedDeltaTime;
            rigid = gripping
                ? Mathf.Clamp01(rigid + dt / Mathf.Max(ReachTime, 1e-3f))
                : Mathf.Clamp01(rigid - dt / Mathf.Max(RelaxTime, 1e-3f));

            chain.Gravity = Gravity * (1f - rigid);
            chain.Damping = Mathf.Lerp(Damping, RigidDamping, rigid);
            chain.Stiffness = Mathf.Lerp(Stiffness, 1f, rigid);

            chain.Straighten(rigid);
            chain.PushOutside(
                torso.TransformPoint(0f, spineLow, 0f),
                torso.TransformPoint(0f, spineHigh, 0f),
                clearance * (1f - rigid));

            chain.Step(dt, RootPoint, gripping, target);

            if (rigid > 0f)
            {
                chain.Points[1] = Vector3.Lerp(chain.Points[1], Elbow(chain.Points[0], chain.Points[2]), rigid);
                chain.Previous[1] = chain.Points[1];
            }

            Vector3 pole = Pole();
            upper.Aim(chain.Points[0], chain.Points[1], pole);
            lower.Aim(chain.Points[1], chain.Points[2], pole);
        }

        private Vector3 Elbow(Vector3 root, Vector3 tip)
        {
            Vector3 span = tip - root;
            float distance = span.magnitude;
            float a = upper.Length;
            float b = lower.Length;
            Vector3 pole = Pole();

            if (distance < 1e-4f) return root + pole * a;

            Vector3 direction = span / distance;
            if (distance >= a + b) return root + direction * a;

            float along = Mathf.Clamp((a * a - b * b + distance * distance) / (2f * distance), -a, a);
            float outward = Mathf.Sqrt(Mathf.Max(a * a - along * along, 0f));

            pole -= direction * Vector3.Dot(pole, direction);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.Cross(direction, Vector3.up);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.down;

            return root + direction * along + pole.normalized * outward;
        }

        private Vector3 Pole()
        {
            return (torso.rotation * new Vector3(side * ElbowOut, -1f, 0f)).normalized;
        }
    }
}

using UnityEngine;

namespace Knotical
{
    [RequireComponent(typeof(Rigidbody))]
    public class Hull : MonoBehaviour
    {
        [Range(0f, 2f)] public float ForwardDrag = 0.05f;
        [Range(0f, 1f)] public float ForwardDragQuadratic = 0.08f;
        [Range(0f, 6f)] public float SideDrag = 1.5f;
        [Range(0f, 4f)] public float SideDragQuadratic = 1f;
        [Min(0.1f)] public float Draft = 0.8f;
        public Vector3 BowKeel = new Vector3(0f, -0.8f, 4.5f);
        public Vector3 SternKeel = new Vector3(0f, -0.8f, -4.5f);

        public float Headway { get; private set; }
        public float Slip { get; private set; }
        public float Wetness { get; private set; }

        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            Vector3 ahead = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (ahead.sqrMagnitude < 0.0001f) return;
            ahead.Normalize();
            Vector3 side = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            Vector3 planar = body.linearVelocity - Ocean.GetFlow(transform.position);
            planar.y = 0f;
            Headway = Vector3.Dot(planar, ahead);
            Slip = Vector3.Dot(planar, side);

            float mass = body.mass;
            float bowWet = Resist(BowKeel, side, mass);
            float sternWet = Resist(SternKeel, side, mass);
            Wetness = 0.5f * (bowWet + sternWet);

            float drag = ForwardDrag * Headway + ForwardDragQuadratic * Headway * Mathf.Abs(Headway);
            body.AddForce(-ahead * (mass * Wetness * drag), ForceMode.Force);
        }

        private float Resist(Vector3 local, Vector3 side, float mass)
        {
            Vector3 p = transform.TransformPoint(local);
            float wet = Mathf.Clamp01((Ocean.GetHeight(p) - p.y) / Draft);
            if (wet <= 0f) return 0f;

            float s = Vector3.Dot(body.GetPointVelocity(p) - Ocean.GetFlow(p), side);
            float drag = SideDrag * s + SideDragQuadratic * s * Mathf.Abs(s);
            body.AddForceAtPosition(-side * (mass * wet * 0.5f * drag), p, ForceMode.Force);
            return wet;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.TransformPoint(BowKeel), 0.3f);
            Gizmos.DrawWireSphere(transform.TransformPoint(SternKeel), 0.3f);
        }
    }
}

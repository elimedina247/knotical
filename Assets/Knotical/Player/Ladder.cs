using System.Collections.Generic;
using UnityEngine;

namespace Knotical
{
    [RequireComponent(typeof(BoxCollider))]
    public class Ladder : MonoBehaviour
    {
        private static readonly List<Ladder> Live = new List<Ladder>();

        public Vector3 Face = Vector3.right;
        [Range(0f, 1f)] public float StandOff = 0.3f;
        [Range(0f, 2f)] public float TopStep = 0.5f;
        [Range(0.1f, 2f)] public float Capture = 0.5f;
        [Range(0f, 2f)] public float TopReach = 0.7f;

        private BoxCollider box;
        private Rigidbody carrier;

        public Rigidbody Carrier => carrier;
        public Vector3 Facing => transform.TransformDirection(FaceAxis);
        public float Bottom => box.center.y - box.size.y * 0.5f;
        public float Rim => box.center.y + box.size.y * 0.5f;
        public float Top => Rim + TopStep;

        private Vector3 FaceAxis => Cardinal(Face);
        private Vector3 SideAxis => Vector3.Cross(Vector3.up, FaceAxis);
        private float FaceHalf => Vector3.Dot(box.size * 0.5f, Abs(FaceAxis));
        private float SideHalf => Vector3.Dot(box.size * 0.5f, Abs(SideAxis));
        private float RailOut => FaceHalf + StandOff;

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
            carrier = GetComponentInParent<Rigidbody>();
        }

        private void OnEnable() => Live.Add(this);

        private void OnDisable() => Live.Remove(this);

        public Vector3 Rail(float height)
        {
            Vector3 local = box.center + FaceAxis * RailOut;
            local.y = height;
            return transform.TransformPoint(local);
        }

        public float Height(Vector3 world) => transform.InverseTransformPoint(world).y;

        public bool Covers(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            Vector3 offset = local - box.center;
            if (Mathf.Abs(Vector3.Dot(offset, SideAxis)) > SideHalf + Capture) return false;
            if (local.y < Bottom - Capture || local.y > Top + Capture) return false;

            float outward = Vector3.Dot(offset, FaceAxis);
            if (outward > RailOut + Capture) return false;
            return local.y > Rim ? outward > -TopReach : outward > 0f;
        }

        public static Ladder Nearest(Vector3 world)
        {
            Ladder best = null;
            float nearest = float.MaxValue;
            foreach (Ladder ladder in Live)
            {
                if (!ladder.Covers(world)) continue;
                float reach = (ladder.Rail(ladder.Height(world)) - world).sqrMagnitude;
                if (reach >= nearest) continue;
                nearest = reach;
                best = ladder;
            }
            return best;
        }

        private static Vector3 Cardinal(Vector3 v)
        {
            float x = Mathf.Abs(v.x);
            float z = Mathf.Abs(v.z);
            if (x < 1e-4f && z < 1e-4f) return Vector3.right;
            return x >= z ? new Vector3(Mathf.Sign(v.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}

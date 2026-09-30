using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Knotical
{
    public sealed class LimbPart
    {
        public Transform Transform;
        public float Length;
        public Vector3 LocalAxis;
        public Vector3 LocalSide;

        public static LimbPart From(Transform part, Vector3 worldSideAtRest)
        {
            var filter = part.GetComponent<MeshFilter>();
            Bounds bounds = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.down * 0.1f, new Vector3(0.05f, 0.2f, 0.05f));

            Vector3 center = bounds.center;
            int axis = 0;
            for (int i = 1; i < 3; i++)
            {
                if (Mathf.Abs(center[i]) > Mathf.Abs(center[axis])) axis = i;
            }

            Vector3 localAxis = Vector3.zero;
            localAxis[axis] = Mathf.Sign(center[axis]);
            float length = bounds.size[axis];

            Vector3 localSide = Quaternion.Inverse(part.rotation) * worldSideAtRest;
            localSide -= localAxis * Vector3.Dot(localSide, localAxis);
            if (localSide.sqrMagnitude < 1e-6f)
            {
                localSide = Vector3.zero;
                localSide[(axis + 1) % 3] = 1f;
            }

            return new LimbPart
            {
                Transform = part,
                Length = Mathf.Max(length, 1e-3f),
                LocalAxis = localAxis,
                LocalSide = localSide.normalized,
            };
        }

        public void Aim(Vector3 from, Vector3 to, Vector3 sideHint)
        {
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 1e-8f) return;

            Vector3 hint = sideHint - direction.normalized * Vector3.Dot(sideHint, direction.normalized);
            if (hint.sqrMagnitude < 1e-6f) hint = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;

            Quaternion world = Quaternion.LookRotation(direction, hint);
            Quaternion local = Quaternion.LookRotation(LocalAxis, LocalSide);
            Transform.SetPositionAndRotation(from, world * Quaternion.Inverse(local));
        }
    }

    public static class CrewRig
    {
        public static Transform FindBySuffix(Transform root, string suffix)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith(suffix)) return t;
            }
            return null;
        }

        public static Vector3 EyePointLocal(Transform root)
        {
            Transform left = FindBySuffix(root, "Eye_L");
            Transform right = FindBySuffix(root, "Eye_R");
            if (left == null || right == null) return new Vector3(0f, 0.9f, 0f);
            Vector3 mid = (left.position + right.position) * 0.5f;
            return root.InverseTransformPoint(mid);
        }

        public static bool IsLimb(Transform t)
        {
            for (Transform step = t; step != null; step = step.parent)
            {
                if (step.name.Contains("_Shoulder_") || step.name.Contains("_Hip_")) return true;
            }
            return false;
        }

        public static void Conceal(Transform crewRoot, bool hide)
        {
            ShadowCastingMode mode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (Renderer renderer in crewRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (IsLimb(renderer.transform)) continue;
                renderer.shadowCastingMode = mode;
            }
        }

        public static List<Collider> OwnColliders(Component any)
        {
            var list = new List<Collider>();
            Rigidbody body = any.GetComponentInParent<Rigidbody>();
            if (body != null) list.AddRange(body.GetComponentsInChildren<Collider>(true));
            return list;
        }
    }
}

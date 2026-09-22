using System.Collections.Generic;
using UnityEngine;

namespace Knotical
{
    public class KenneySails : MonoBehaviour
    {
        [Range(0f, 1f)] public float FirstSailAt = 0.05f;
        [Range(0f, 1f)] public float LastSailAt = 0.55f;
        [Range(0f, 90f)] public float MaxSwingDeg = 60f;
        [Range(0.1f, 20f)] public float SwingResponse = 2.5f;

        public float SwingDeg { get; private set; }

        private struct Pivot
        {
            public Transform Transform;
            public Quaternion Rest;
            public Vector3 Axis;
            public float RestDeg;
        }

        private SailRig rig;
        private Renderer[] sails = new Renderer[0];
        private Pivot[] sailPivots = new Pivot[0];
        private Pivot[] flagPivots = new Pivot[0];
        private float flagDeg = 180f;
        private float baseDeg;

        private void Awake()
        {
            rig = GetComponent<SailRig>();
            var found = new List<Renderer>();
            var flags = new List<Pivot>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                string lower = r.name.ToLowerInvariant();
                if (lower.StartsWith("sail")) found.Add(r);
                else if (lower.StartsWith("flag")) flags.Add(MakeFlagPivot(r));
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            sails = found.ToArray();
            sailPivots = new Pivot[sails.Length];
            for (int i = 0; i < sails.Length; i++) sailPivots[i] = MakePivot(sails[i].transform);
            flagPivots = flags.ToArray();
        }

        private Pivot MakePivot(Transform t)
        {
            return new Pivot
            {
                Transform = t,
                Rest = t.localRotation,
                Axis = t.parent.InverseTransformDirection(transform.up),
            };
        }

        private Pivot MakeFlagPivot(Renderer r)
        {
            Pivot p = MakePivot(r.transform);
            Vector3 offset = transform.InverseTransformPoint(r.bounds.center) - transform.InverseTransformPoint(r.transform.position);
            offset.y = 0f;
            p.RestDeg = offset.sqrMagnitude < 1e-6f ? 180f : Vector3.SignedAngle(Vector3.forward, offset, Vector3.up);
            return p;
        }

        private void LateUpdate()
        {
            float deployment = rig != null ? rig.Deployment : 1f;
            for (int i = 0; i < sails.Length; i++)
            {
                float threshold = sails.Length == 1 ? FirstSailAt : Mathf.Lerp(FirstSailAt, LastSailAt, i / (sails.Length - 1f));
                sails[i].enabled = deployment >= threshold;
            }

            if (rig == null) return;
            Vector3 apparent = rig.ApparentWindVelocity;
            if (apparent.sqrMagnitude < 0.01f) return;

            float offForward = Vector3.SignedAngle(transform.forward, apparent, transform.up);
            float fromAhead = Mathf.Abs(offForward);
            if (baseDeg == 0f && fromAhead > 100f) baseDeg = 180f;
            else if (baseDeg == 180f && fromAhead < 80f) baseDeg = 0f;
            float yard = baseDeg + Mathf.Clamp(Mathf.DeltaAngle(baseDeg, offForward), -MaxSwingDeg, MaxSwingDeg);

            float blend = 1f - Mathf.Exp(-SwingResponse * Time.deltaTime);
            SwingDeg = Mathf.LerpAngle(SwingDeg, yard, blend);
            flagDeg = Mathf.LerpAngle(flagDeg, offForward, blend);

            foreach (Pivot p in sailPivots) p.Transform.localRotation = Quaternion.AngleAxis(SwingDeg, p.Axis) * p.Rest;
            foreach (Pivot p in flagPivots) p.Transform.localRotation = Quaternion.AngleAxis(Mathf.DeltaAngle(p.RestDeg, flagDeg + 180f), p.Axis) * p.Rest;
        }
    }
}

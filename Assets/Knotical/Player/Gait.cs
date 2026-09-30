using UnityEngine;

namespace Knotical
{
    [RequireComponent(typeof(PlayerBody))]
    public class Gait : MonoBehaviour
    {
        [Range(0.1f, 1.5f)] public float StrideLength = 0.5f;
        [Range(0.05f, 0.5f)] public float StepTime = 0.11f;
        [Range(0f, 0.4f)] public float StepHeight = 0.1f;
        [Range(0f, 0.4f)] public float LeadTime = 0.11f;
        [Range(0f, 60f)] public float DangleGravity = 18f;
        [Range(0.01f, 1f)] public float DangleDamping = 0.3f;
        [Range(0.1f, 1f)] public float DangleStiffness = 0.85f;
        [Range(0.2f, 4f)] public float KickRate = 1.9f;
        [Range(0f, 0.4f)] public float KickAmplitude = 0.16f;
        [Range(0f, 1f)] public float KickTrail = 0.8f;

        private sealed class Leg
        {
            public Transform Hip;
            public LimbPart Thigh;
            public LimbPart Shin;
            public Transform Foot;
            public Quaternion FootRest;
            public float FootLift;
            public LimbChain Chain = new LimbChain();
            public Transform Deck;
            public Vector3 PlantLocal;
            public Vector3 PlantWorld;
            public bool HasPlant;
            public bool Stepping;
            public float StepBlend;
            public Vector3 StepFrom;
        }

        private PlayerBody body;
        private readonly Leg[] legs = new Leg[2];
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private System.Collections.Generic.HashSet<Collider> own;
        private float kick;

        private void Awake()
        {
            body = GetComponent<PlayerBody>();
            own = new System.Collections.Generic.HashSet<Collider>(GetComponentsInChildren<Collider>(true));
            legs[0] = Wire("_Hip_L");
            legs[1] = Wire("_Hip_R");
        }

        private Leg Wire(string suffix)
        {
            Transform hip = CrewRig.FindBySuffix(transform, suffix);
            if (hip == null || hip.childCount == 0) return null;
            Transform upper = hip.GetChild(0);
            if (upper.childCount == 0) return null;
            Transform lower = upper.GetChild(0);
            Transform foot = lower.childCount > 0 ? lower.GetChild(0) : null;

            var leg = new Leg
            {
                Hip = hip,
                Thigh = LimbPart.From(upper, transform.forward),
                Shin = LimbPart.From(lower, transform.forward),
                Foot = foot,
            };

            if (foot != null)
            {
                leg.FootRest = Quaternion.Inverse(transform.rotation) * foot.rotation;
                leg.FootLift = FootHeight(foot) + 0.005f;
            }

            leg.Chain.Build(new[] { leg.Thigh.Length, leg.Shin.Length }, hip.position, Vector3.down);
            return leg;
        }

        private static float FootHeight(Transform foot)
        {
            var filter = foot.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return 0.08f;
            Bounds b = filter.sharedMesh.bounds;
            Vector3 down = Quaternion.Inverse(foot.rotation) * Vector3.down;
            int axis = 0;
            for (int i = 1; i < 3; i++) if (Mathf.Abs(down[i]) > Mathf.Abs(down[axis])) axis = i;
            return down[axis] > 0f ? Mathf.Max(b.max[axis], 0f) : Mathf.Max(-b.min[axis], 0f);
        }

        private void FixedUpdate()
        {
            if (body == null) return;

            float dt = Time.fixedDeltaTime;
            bool swimming = body.IsSwimming;
            bool footing = body.IsGrounded && !swimming;
            Quaternion facing = body.FlatFacing;
            Vector3 forward = facing * Vector3.forward;

            float effort = 0f;
            if (swimming)
            {
                effort = Mathf.Clamp01(body.PlanarVelocity.magnitude / Mathf.Max(body.SwimSpeed, 0.1f));
                kick = Mathf.Repeat(kick + 2f * Mathf.PI * KickRate * (0.45f + 0.55f * effort) * dt, 2f * Mathf.PI);
            }

            for (int i = 0; i < 2; i++)
            {
                Leg leg = legs[i];
                if (leg == null) continue;

                Vector3 hip = leg.Hip.position;

                if (footing) Stand(leg, legs[1 - i], hip, forward, dt);
                else if (swimming) Flutter(leg, i, hip, forward, effort);
                else Dangle(leg, hip, dt);

                Vector3 knee = leg.Chain.Points[1];
                Vector3 ankle = leg.Chain.Points[2];

                leg.Thigh.Aim(hip, knee, forward);
                leg.Shin.Aim(knee, ankle, forward);

                if (leg.Foot == null) continue;

                if (swimming)
                {
                    Vector3 shin = (ankle - knee).normalized;
                    Quaternion toes = Quaternion.FromToRotation(Vector3.down, shin);
                    leg.Foot.SetPositionAndRotation(ankle, toes * facing * leg.FootRest);
                }
                else
                {
                    leg.Foot.SetPositionAndRotation(ankle, facing * leg.FootRest);
                }
            }
        }

        private void Flutter(Leg leg, int index, Vector3 hip, Vector3 forward, float effort)
        {
            leg.HasPlant = false;
            leg.Stepping = false;

            Vector3 planar = body.PlanarVelocity;
            Vector3 heading = planar.sqrMagnitude > 0.04f ? planar.normalized : forward;
            Vector3 rest = (Vector3.down - heading * (KickTrail * effort)).normalized;
            float amplitude = KickAmplitude * (0.55f + 0.45f * effort);
            float span = leg.Chain.Span;

            Vector3 ankle = hip + rest * (span * 0.94f) + heading * (Mathf.Sin(kick + index * Mathf.PI) * amplitude);
            Pose(leg, hip, ankle, forward);
        }

        private void Stand(Leg leg, Leg other, Vector3 hip, Vector3 forward, float dt)
        {
            Vector3 ground = Probe(leg, hip);
            Vector3 lead = body.PlanarVelocity * LeadTime;
            if (lead.magnitude > StrideLength) lead = lead.normalized * StrideLength;
            Vector3 home = ground + lead;

            if (!leg.HasPlant) Plant(leg, ground);

            Vector3 planted = PlantedWorld(leg);

            if (leg.Stepping)
            {
                leg.StepBlend = Mathf.Min(leg.StepBlend + dt / Mathf.Max(StepTime, 1e-3f), 1f);
                float t = Mathf.SmoothStep(0f, 1f, leg.StepBlend);
                Vector3 foot = Vector3.Lerp(leg.StepFrom, home, t) + Vector3.up * (StepHeight * Mathf.Sin(leg.StepBlend * Mathf.PI));

                if (leg.StepBlend >= 1f)
                {
                    leg.Stepping = false;
                    Plant(leg, home);
                    foot = home;
                }

                Pose(leg, hip, foot + Vector3.up * leg.FootLift, forward);
                return;
            }

            Vector3 error = home - planted;
            error -= Vector3.up * error.y;
            float drift = error.magnitude;

            float speed = Mathf.Max(body.PlanarVelocity.magnitude, 0.5f);
            float near = StrideLength * 0.5f;
            float far = near + speed * StepTime * 1.3f;
            bool free = other == null || !other.Stepping;

            if ((free && drift > near) || drift > far)
            {
                leg.Stepping = true;
                leg.StepBlend = 0f;
                leg.StepFrom = planted;
            }

            Pose(leg, hip, planted + Vector3.up * leg.FootLift, forward);
        }

        private void Dangle(Leg leg, Vector3 hip, float dt)
        {
            leg.HasPlant = false;
            leg.Stepping = false;

            LimbChain chain = leg.Chain;
            chain.Gravity = DangleGravity;
            chain.Damping = DangleDamping;
            chain.Stiffness = DangleStiffness;
            chain.Step(dt, hip, false, Vector3.zero);
        }

        private void Plant(Leg leg, Vector3 world)
        {
            leg.HasPlant = true;
            leg.PlantWorld = world;
            leg.Deck = body.Deck;
            leg.PlantLocal = leg.Deck != null ? leg.Deck.InverseTransformPoint(world) : world;
        }

        private Vector3 PlantedWorld(Leg leg)
        {
            if (leg.Deck != null) leg.PlantWorld = leg.Deck.TransformPoint(leg.PlantLocal);
            if (leg.Deck != body.Deck) Plant(leg, leg.PlantWorld);
            return leg.PlantWorld;
        }

        private Vector3 Probe(Leg leg, Vector3 hip)
        {
            own ??= new System.Collections.Generic.HashSet<Collider>(GetComponentsInChildren<Collider>(true));
            float span = leg.Chain.Span;
            Vector3 from = hip + Vector3.up * 0.2f;
            float reach = span + 0.55f;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, hits, reach, ~0, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            Vector3 point = hip + Vector3.down * (span * 0.95f);
            for (int i = 0; i < count; i++)
            {
                if (own.Contains(hits[i].collider) || hits[i].distance >= best) continue;
                best = hits[i].distance;
                point = hits[i].point;
            }
            return point;
        }

        private void Pose(Leg leg, Vector3 hip, Vector3 ankle, Vector3 forward)
        {
            Vector3 knee = Bend(leg, hip, ankle, forward);

            LimbChain chain = leg.Chain;
            chain.Previous[0] = chain.Points[0];
            chain.Previous[1] = chain.Points[1];
            chain.Previous[2] = chain.Points[2];
            chain.Points[0] = hip;
            chain.Points[1] = knee;
            chain.Points[2] = ankle;
        }

        private static Vector3 Bend(Leg leg, Vector3 hip, Vector3 ankle, Vector3 forward)
        {
            float a = leg.Thigh.Length;
            float b = leg.Shin.Length;
            Vector3 span = ankle - hip;
            float distance = span.magnitude;
            if (distance < 1e-4f) return hip + forward * a;

            Vector3 direction = span / distance;
            if (distance >= a + b) return hip + direction * a;

            float along = Mathf.Clamp((a * a - b * b + distance * distance) / (2f * distance), -a, a);
            float outward = Mathf.Sqrt(Mathf.Max(a * a - along * along, 0f));

            Vector3 pole = forward - direction * Vector3.Dot(forward, direction);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.up - direction * Vector3.Dot(Vector3.up, direction);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.right;

            return hip + direction * along + pole.normalized * outward;
        }
    }
}

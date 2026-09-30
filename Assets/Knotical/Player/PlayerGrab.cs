using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Knotical
{
    [RequireComponent(typeof(PlayerBody))]
    public class PlayerGrab : MonoBehaviour
    {
        [Range(0.2f, 2.5f)] public float Reach = 0.62f;
        [Range(0.3f, 4f)] public float AimDistance = 1.2f;
        [Range(0.02f, 0.5f)] public float CastRadius = 0.09f;
        [Range(0.5f, 20f)] public float MaxPullSpeed = 6f;
        [Range(0.05f, 1f)] public float Correction = 0.2f;
        [Range(0f, 20f)] public float SwingDamping = 3f;
        [Range(500f, 80000f)] public float BreakForce = 6500f;
        public bool Breaks;
        [Range(1f, 100f)] public float CarryMassRatio = 8f;
        [Range(0.4f, 2.5f)] public float CarryDistance = 1.1f;
        [Range(0.2f, 3f)] public float CarrySlip = 1f;
        [Range(5f, 200f)] public float CarryAccel = 30f;
        [Range(0.3f, 3f)] public float PushReach = 1.1f;
        [Range(5f, 400f)] public float PushImpulse = 140f;
        [Range(1f, 30f)] public float PushSpeedCap = 9f;
        [Range(0f, 1f)] public float PushRecoil = 0.35f;
        [Range(0.1f, 2f)] public float PushCooldown = 0.5f;
        [Range(0.05f, 0.6f)] public float PushTime = 0.22f;
        [Range(0f, 1f)] public float LazyReach = 0.6f;
        [Range(0.05f, 0.6f)] public float HandSpread = 0.24f;
        [Range(0f, 0.3f)] public float HangSpread = 0.05f;
        [Range(0f, 0.15f)] public float HangStack = 0.045f;
        [Range(0.1f, 2f)] public float DropPause = 0.45f;
        [Range(1f, 3f)] public float LetGoBeyond = 1.6f;
        public bool ShowMarker = true;
        [Range(0.01f, 0.2f)] public float MarkerRadius = 0.025f;
        public Color MarkerColor = new Color(0.25f, 0.66f, 0.96f);
        public Transform Pivot;
        public DangleArm ArmLeft;
        public DangleArm ArmRight;

        public Vector2 Twist;
        public int Shift;

        private PlayerBody player;
        private Rigidbody body;
        private HashSet<Collider> own;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Transform marker;
        private bool held;
        private bool shove;
        private bool attached;
        private bool rearm;
        private float pause;
        private Transform node;
        private Rigidbody nodeBody;
        private Rigidbody carried;
        private readonly List<Collider> carriedColliders = new List<Collider>();
        private IGrabbable handle;
        private Vector3 local;
        private Vector3 normal = Vector3.up;
        private Vector3 lastAnchor;
        private Vector3 lastChest;
        private float span;
        private float load;
        private bool engaged;
        private float hang;
        private float pushing;
        private float pushWait;
        private Vector3 pushAt;

        public float Load => load;
        public bool IsGripping => attached;
        public bool HandsEngaged => engaged;
        public bool CapturesInput => attached && handle != null && handle.CapturesInput;

        public Vector3 ChestWorld => ArmLeft != null && ArmRight != null
            ? (ArmLeft.Root + ArmRight.Root) * 0.5f
            : transform.TransformPoint(0f, 0.9f, 0f);

        private void Awake()
        {
            player = GetComponent<PlayerBody>();
            body = GetComponent<Rigidbody>();
            own = new HashSet<Collider>(GetComponentsInChildren<Collider>(true));
            if (Pivot == null) Pivot = player.CameraPivot;

            if (ArmLeft == null || ArmRight == null)
            {
                foreach (DangleArm arm in GetComponentsInChildren<DangleArm>(true))
                {
                    bool left = transform.InverseTransformPoint(arm.transform.position).x < 0f;
                    if (left && ArmLeft == null) ArmLeft = arm;
                    else if (!left && ArmRight == null) ArmRight = arm;
                }
            }
        }

        private void Start()
        {
            BuildMarker();
        }

        private void BuildMarker()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GrabMarker";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * (MarkerRadius * 2f);
            var renderer = go.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var material = new Material(shader);
                material.color = MarkerColor;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", MarkerColor);
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            marker = go.transform;
        }

        private void Update()
        {
            Mouse m = Mouse.current;
            bool captured = player.InputCaptured && !player.JustCaptured;
            held = captured && m != null && m.leftButton.isPressed;
            if (captured && m != null && m.rightButton.wasPressedThisFrame) shove = true;
        }

        public void Tick(float dt)
        {
            pause = Mathf.Max(pause - dt, 0f);
            pushWait = Mathf.Max(pushWait - dt, 0f);
            pushing = Mathf.Max(pushing - dt, 0f);
            if (!held) rearm = false;

            if (shove)
            {
                shove = false;
                if (pushWait <= 0f)
                {
                    if (!attached) Push();
                    else if (carried != null) Throw();
                }
            }

            if (!held || rearm || pause > 0f)
            {
                engaged = false;
                Release();
                Pose(false, dt);
                return;
            }

            engaged = true;
            if (!attached) Catch();
            Pose(true, dt);
        }

        private void Catch()
        {
            float reach = Grasp();
            Vector3 chest = ChestWorld;
            Vector3 tip = chest + Aim(chest) * reach;

            bool found = Sweep(chest, tip, true, out RaycastHit hit);
            if (!found) found = Sweep(chest, chest + Vector3.up * reach, true, out hit);
            if (!found) found = Sweep(chest, chest + Vector3.down * reach, true, out hit);
            if (!found) return;

            Vector3 point = hit.point;
            handle = hit.collider.GetComponentInParent<IGrabbable>();
            if (handle != null) point = handle.Attach(point);
            else if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.rigidbody.mass < OwnMass() * CarryMassRatio)
            {
                carried = hit.rigidbody;
                carriedColliders.Clear();
                carriedColliders.AddRange(carried.GetComponentsInChildren<Collider>());
                IgnoreCarried(true);
            }

            attached = true;
            node = hit.collider.transform;
            nodeBody = hit.rigidbody;
            normal = hit.normal;
            local = node.InverseTransformPoint(point);
            lastAnchor = point;
            lastChest = chest;
            span = Mathf.Min((point - chest).magnitude, reach);
            load = 0f;
        }

        private void IgnoreCarried(bool ignore)
        {
            own ??= new HashSet<Collider>(GetComponentsInChildren<Collider>(true));
            foreach (Collider mine in own)
            {
                foreach (Collider theirs in carriedColliders)
                {
                    if (mine != null && theirs != null) Physics.IgnoreCollision(mine, theirs, ignore);
                }
            }
        }

        private void Pose(bool isHeld, float dt)
        {
            if (!attached || node == null)
            {
                if (pushing > 0f)
                {
                    Vector3 side = Pivot.right * (HandSpread * 0.5f);
                    Mark(null);
                    ArmLeft?.Grip(pushAt - side);
                    ArmRight?.Grip(pushAt + side);
                    return;
                }

                ArmLeft?.Release();

                if (!isHeld)
                {
                    Mark(null);
                    ArmRight?.Release();
                    return;
                }

                Vector3 chest = ChestWorld;
                Vector3 tip = chest + Aim(chest) * Grasp();
                Mark(tip);
                ArmRight?.Grip(Vector3.Lerp(chest, tip, LazyReach));
                return;
            }

            Vector3 n = normal;
            Vector3 anchor;
            if (carried != null) anchor = CarryAnchor(out n);
            else anchor = handle != null ? lastAnchor : node.TransformPoint(local);

            Mark(anchor);

            Vector3 across = Flatten(Pivot.right, n);
            if (across == Vector3.zero) across = Pivot.right;

            float hanging = Hanging(anchor, dt);

            if (handle != null)
            {
                Vector3 lift = anchor - ChestWorld;
                lift = lift.sqrMagnitude > 1e-6f ? lift.normalized : Vector3.up;
                Vector3 offset = across * (HandSpread * (1f - hanging)) + lift * (HangStack * hanging);
                ArmLeft?.Grip(anchor - offset);
                ArmRight?.Grip(anchor + offset);
                return;
            }

            float spread = Mathf.Lerp(HandSpread, HangSpread, hanging);
            ArmLeft?.Grip(anchor - across * spread);
            ArmRight?.Grip(anchor + across * spread);
        }

        private Vector3 CarryAnchor(out Vector3 n)
        {
            Vector3 chest = ChestWorld;
            Vector3 center = carried.worldCenterOfMass;
            Vector3 back = chest - center;
            n = back.sqrMagnitude > 1e-6f ? back.normalized : Vector3.up;

            Vector3 delta = center - chest;
            float distance = delta.magnitude;
            Vector3 point = center + n * CastRadius;
            if (distance < 1e-4f) return point;

            int count = Physics.SphereCastNonAlloc(chest, CastRadius, delta / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].rigidbody != carried) continue;
                float d = (hits[i].point - chest).sqrMagnitude;
                if (d >= nearest) continue;
                nearest = d;
                point = hits[i].point;
                n = hits[i].normal;
            }
            return point;
        }

        private void Push()
        {
            pushWait = PushCooldown;
            pushing = PushTime;

            Vector3 chest = ChestWorld;
            Vector3 aim = Aim(chest);
            pushAt = chest + aim * PushReach;

            if (!Sweep(chest, pushAt, false, out RaycastHit hit)) return;

            pushAt = hit.point;
            float impulse = PushImpulse;
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                impulse = Mathf.Min(PushImpulse, hit.rigidbody.mass * PushSpeedCap);
                hit.rigidbody.AddForceAtPosition(aim * impulse, hit.point, ForceMode.Impulse);
            }

            body.AddForce(aim * (-impulse * PushRecoil), ForceMode.Impulse);
        }

        private void Throw()
        {
            if (carried == null) return;

            pushWait = PushCooldown;
            pushing = PushTime;

            Vector3 chest = ChestWorld;
            Vector3 aim = Aim(chest);
            Rigidbody thrown = carried;
            pushAt = chest + aim * PushReach;

            Drop();

            float impulse = Mathf.Min(PushImpulse, thrown.mass * PushSpeedCap);
            thrown.AddForce(aim * impulse, ForceMode.Impulse);
            body.AddForce(aim * (-impulse * PushRecoil), ForceMode.Impulse);
        }

        private float Hanging(Vector3 anchor, float dt)
        {
            float want = 0f;
            Vector3 lift = anchor - ChestWorld;
            if (!player.IsGrounded && lift.sqrMagnitude > 1e-6f)
            {
                want = Threshold(0.4f, 0.85f, Vector3.Dot(lift.normalized, Vector3.up));
            }
            hang = Mathf.MoveTowards(hang, want, dt / 0.15f);
            return hang;
        }

        private static float Threshold(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(edge1 - edge0, 1e-5f));
            return t * t * (3f - 2f * t);
        }

        private void Mark(Vector3? at)
        {
            if (marker == null) return;
            bool show = ShowMarker && at.HasValue;
            marker.gameObject.SetActive(show);
            if (show) marker.position = at.Value;
        }

        private float OwnMass() => body != null && body.mass > 0f ? body.mass : 70f;

        private static Vector3 Flatten(Vector3 axis, Vector3 n)
        {
            Vector3 flat = axis - n * Vector3.Dot(axis, n);
            return flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.zero;
        }

        public void Drop()
        {
            pause = DropPause;
            rearm = true;
            Release();
        }

        private void Release()
        {
            if (!attached) return;

            handle?.Detach();

            if (carried != null)
            {
                IgnoreCarried(false);
                carried = null;
                carriedColliders.Clear();
            }

            attached = false;
            node = null;
            nodeBody = null;
            handle = null;
            load = 0f;
        }

        public void Apply(float dt)
        {
            load = 0f;
            if (!attached) return;

            if (node == null)
            {
                Release();
                return;
            }

            Vector3 chest = ChestWorld;
            Vector3 anchor;

            if (handle != null)
            {
                anchor = handle.Track(new GrabHold
                {
                    Point = lastAnchor + (chest - lastChest),
                    Chest = chest,
                    Aim = Aim(chest),
                    Twist = Twist,
                    Shift = Shift,
                }, dt);
            }
            else
            {
                anchor = node.TransformPoint(local);
            }

            Twist = Vector2.zero;
            Shift = 0;
            lastChest = chest;
            lastAnchor = anchor;

            if (carried != null)
            {
                Carry(chest, dt);
                return;
            }

            if (handle != null && !handle.Anchors)
            {
                if (Vector3.Distance(chest, anchor) > Grasp() * LetGoBeyond) Release();
                return;
            }

            Vector3 spanVector = anchor - chest;
            float distance = spanVector.magnitude;
            if (distance < 1e-4f) return;

            Vector3 direction = spanVector / distance;
            Vector3 relative = body.linearVelocity - AnchorVelocity(nodeBody, anchor);
            float closing = Vector3.Dot(relative, direction);
            float error = distance - span;
            if (error < 0f) return;

            body.linearVelocity -= (relative - direction * closing) * Mathf.Min(SwingDamping * dt, 1f);

            float want = Mathf.Min(error * Correction / dt, MaxPullSpeed);
            if (closing >= want) return;

            Rigidbody other = nodeBody != null && !nodeBody.isKinematic ? nodeBody : null;
            float invSelf = 1f / OwnMass();
            float invOther = other != null && other.mass > 0f ? 1f / other.mass : 0f;
            float invSum = invSelf + invOther;
            if (invSum <= 0f) return;

            float impulse = (want - closing) / invSum;
            body.linearVelocity += direction * (impulse * invSelf);
            other?.AddForceAtPosition(direction * -impulse, anchor, ForceMode.Impulse);

            load = Mathf.Min(impulse / (dt * Mathf.Max(BreakForce, 1f)), 1f);
            if (Breaks && load >= 1f) Release();
        }

        private void Carry(Vector3 chest, float dt)
        {
            if (carried == null)
            {
                Release();
                return;
            }

            Vector3 target = chest + Aim(chest) * CarryDistance;
            Vector3 to = target - carried.worldCenterOfMass;
            float distance = to.magnitude;

            if (distance > CarryDistance + CarrySlip)
            {
                Release();
                return;
            }

            Vector3 correction = distance > 1e-4f
                ? to / distance * Mathf.Min(distance * Correction / dt, MaxPullSpeed)
                : Vector3.zero;

            Vector3 change = body.linearVelocity + correction - carried.linearVelocity;
            carried.AddForce(Vector3.ClampMagnitude(change, CarryAccel * dt) * carried.mass, ForceMode.Impulse);
            carried.angularVelocity *= Mathf.Max(1f - SwingDamping * dt, 0f);

            load = Mathf.Clamp01(carried.mass / (OwnMass() * CarryMassRatio));
        }

        private float Grasp()
        {
            float arms = Mathf.Max(ArmLeft != null ? ArmLeft.Length : 0f, ArmRight != null ? ArmRight.Length : 0f);
            return arms > 1e-3f ? Mathf.Min(Reach, arms) : Reach;
        }

        private Vector3 Aim(Vector3 chest)
        {
            Vector3 forward = Pivot != null ? Pivot.forward : transform.forward;
            Vector3 origin = Pivot != null ? Pivot.position : chest;
            Vector3 spanVector = origin + forward * AimDistance - chest;
            return spanVector.sqrMagnitude > 1e-6f ? spanVector.normalized : forward;
        }

        private bool Sweep(Vector3 from, Vector3 to, bool preferHandles, out RaycastHit best)
        {
            best = default;
            own ??= new HashSet<Collider>(GetComponentsInChildren<Collider>(true));
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1e-4f) return false;

            int count = Physics.SphereCastNonAlloc(from, CastRadius, delta / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);

            int chosen = -1;
            int grip = -1;
            float nearest = float.MaxValue;
            float handled = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance <= 0f || own.Contains(hit.collider)) continue;

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    chosen = i;
                }

                if (!preferHandles || hit.distance >= handled) continue;
                if (hit.collider.GetComponentInParent<IGrabbable>() == null) continue;

                handled = hit.distance;
                grip = i;
            }

            if (grip >= 0) chosen = grip;
            if (chosen < 0) return false;

            best = hits[chosen];
            return true;
        }

        private static Vector3 AnchorVelocity(Rigidbody rigid, Vector3 at)
        {
            return rigid != null ? rigid.GetPointVelocity(at) : Vector3.zero;
        }
    }
}

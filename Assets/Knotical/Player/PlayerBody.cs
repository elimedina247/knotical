using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Knotical
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerBody : MonoBehaviour
    {
        [Range(0.5f, 8f)] public float WalkSpeed = 3.2f;
        [Range(1f, 60f)] public float Acceleration = 18f;
        [Range(100f, 4000f)] public float MaxFootingForce = 1100f;
        [Range(0f, 1f)] public float AirControl = 0.1f;
        [Range(0f, 8f)] public float CarryTime = 2f;
        [Range(1f, 10f)] public float JumpSpeed = 4.2f;
        [Range(0f, 90f)] public float GripHeel = 12f;
        [Range(0f, 90f)] public float SlipHeel = 38f;
        [Range(0f, 1f)] public float MinTraction = 0.15f;
        [Range(10f, 85f)] public float FloorMaxAngle = 55f;
        [Range(0.01f, 1f)] public float FootingSmooth = 0.25f;
        [Range(0f, 0.6f)] public float FootingGrace = 0.15f;
        [Range(0.01f, 1f)] public float MouseSensitivity = 0.12f;
        [Range(40f, 89f)] public float PitchLimit = 85f;
        [Range(0.05f, 1f)] public float SwimEntry = 0.35f;
        [Range(1f, 2f)] public float Buoyancy = 1.15f;
        [Range(0.4f, 1.2f)] public float DiveBuoyancy = 0.85f;
        [Range(0f, 20f)] public float WaterDrag = 3.5f;
        [Range(0.2f, 6f)] public float SwimSpeed = 2.2f;
        [Range(1f, 40f)] public float SwimAcceleration = 10f;
        [Range(0.2f, 4f)] public float SwimVertical = 1.4f;
        [Range(0f, 5f)] public float StrokeKick = 2.2f;
        [Range(0f, 1f)] public float SwellFilter = 0.35f;
        [Range(0.05f, 2f)] public float SwellFast = 0.5f;
        [Range(1f, 30f)] public float SwellSlow = 10f;
        [Range(0f, 0.4f)] public float SwellLimit = 0.09f;
        [Range(0.5f, 5f)] public float ClimbSpeed = 1.8f;
        [Range(0f, 1f)] public float ClimbEnter = 0.35f;
        [Range(1f, 20f)] public float ClimbSnap = 5f;
        [Range(0f, 6f)] public float ClimbStepOff = 2.2f;
        [Range(0f, 2f)] public float ClimbRearm = 0.4f;
        [Range(0f, 30f)] public float GripFovPull = 6f;
        [Range(0f, 1f)] public float GripDrag = 1f;
        public Vector3 EyeOffset = new Vector3(0f, 0.92f, 0.15f);
        public Transform CameraPivot;
        public Camera View;
        public Transform CrewRoot;
        public PlayerGrab Grab;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private HashSet<Collider> own;
        private float baseFov;
        private float fovBlend;
        private float swellFast;
        private float swellSlow;
        private float swellBlend;
        private bool swellReady;
        private Vector3 deckVelocity;
        private float deckYawRate;
        private Vector3 groundNormal = Vector3.up;
        private Vector3 wish;
        private Vector2 stick;
        private Ladder ladder;
        private float rung;
        private float climbWait;
        private bool grounded;
        private float airborneFor;
        private float submersion;
        private float waterRise;
        private bool swimming;
        private float stroke;
        private bool jump;
        private float yaw;
        private float pitch;
        private bool wantsCapture = true;
        private bool captured;
        private Transform deck;

        public bool IsGrounded => grounded;
        public bool IsSwimming => swimming;
        public bool IsClimbing => ladder != null;
        public float Submersion => submersion;
        public Vector3 DeckVelocity => deckVelocity;
        public Transform Deck => deck;
        public Rigidbody Body => body;
        public bool InputCaptured => captured;
        public bool JustCaptured { get; private set; }
        public Quaternion FlatFacing => Quaternion.Euler(0f, yaw, 0f);
        public float ShipHeel => deck != null ? Vector3.Angle(deck.up, Vector3.up) : 0f;
        public float DeckHeel => Vector3.Angle(groundNormal, Vector3.up);

        public float Traction
        {
            get
            {
                float fade = Mathf.InverseLerp(GripHeel, Mathf.Max(SlipHeel, GripHeel + 0.01f), ShipHeel);
                return Mathf.Lerp(1f, MinTraction, Mathf.Clamp01(fade));
            }
        }

        public Vector3 PlanarVelocity
        {
            get
            {
                Vector3 relative = body.linearVelocity - deckVelocity;
                return relative - Vector3.up * relative.y;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            own = new HashSet<Collider>(GetComponentsInChildren<Collider>(true));
            yaw = transform.eulerAngles.y;

            if (CameraPivot == null) CameraPivot = transform.Find("CameraPivot");
            if (View == null) View = GetComponentInChildren<Camera>();
            if (Grab == null) Grab = GetComponent<PlayerGrab>();
            if (View != null) baseFov = View.fieldOfView;
            if (CrewRoot != null) CrewRig.Conceal(CrewRoot, true);
        }

        private void Start()
        {
            Capture();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus && wantsCapture) Capture();
        }

        private void Capture()
        {
            wantsCapture = true;
            captured = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            JustCaptured = false;
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;

            if (k != null && k.escapeKey.wasPressedThisFrame)
            {
                wantsCapture = false;
                captured = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (m != null && !captured && m.leftButton.wasPressedThisFrame)
            {
                Capture();
                JustCaptured = true;
            }

            bool held = Grab != null && Grab.CapturesInput;

            if (captured && m != null)
            {
                Vector2 delta = m.delta.ReadValue();
                if (held) Grab.Twist += delta;
                else
                {
                    yaw += delta.x * MouseSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * MouseSensitivity, -PitchLimit, PitchLimit);
                }
            }

            Vector2 move = Vector2.zero;
            stroke = 0f;
            if (captured && k != null)
            {
                bool forward = k.wKey.isPressed || k.upArrowKey.isPressed;
                bool back = k.sKey.isPressed || k.downArrowKey.isPressed;
                bool left = k.aKey.isPressed || k.leftArrowKey.isPressed;
                bool right = k.dKey.isPressed || k.rightArrowKey.isPressed;

                if (held)
                {
                    if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) Grab.Shift += 1;
                    if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) Grab.Shift -= 1;
                }
                else
                {
                    move.x = (right ? 1f : 0f) - (left ? 1f : 0f);
                    move.y = (forward ? 1f : 0f) - (back ? 1f : 0f);
                }

                if (k.spaceKey.wasPressedThisFrame) jump = true;
                stroke = (k.spaceKey.isPressed ? 1f : 0f) - (k.leftCtrlKey.isPressed ? 1f : 0f);
            }

            stick = move;
            wish = Vector3.ClampMagnitude(FlatFacing * new Vector3(move.x, 0f, move.y), 1f);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            SampleWater();
            SampleFooting(dt);
            SampleClimb(dt);
            if (grounded || ladder != null) yaw += deckYawRate * dt;
            body.MoveRotation(FlatFacing);
            Launch();

            if (Grab != null)
            {
                Grab.Tick(dt);
                Grab.Apply(dt);
            }

            if (ladder != null)
            {
                ApplyClimb(dt);
                return;
            }

            if (swimming)
            {
                ApplyBuoyancy();
                ApplySwim();
                return;
            }

            ApplyWalk();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            if (CameraPivot != null)
            {
                CameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
                CameraPivot.position = transform.TransformPoint(EyeOffset) + Vector3.up * Swell(dt);
            }

            if (View == null) return;

            float grip = Grab != null ? Grab.Load : 0f;
            fovBlend = Mathf.MoveTowards(fovBlend, grip, dt / 0.3f);
            View.fieldOfView = baseFov - GripFovPull * fovBlend;
        }

        private float Swell(float dt)
        {
            float level = deck != null ? deck.position.y : 0f;

            if (!swellReady)
            {
                swellFast = level;
                swellSlow = level;
                swellReady = true;
            }

            swellFast = Mathf.Lerp(swellFast, level, 1f - Mathf.Exp(-dt / Mathf.Max(SwellFast, 1e-3f)));
            swellSlow = Mathf.Lerp(swellSlow, level, 1f - Mathf.Exp(-dt / Mathf.Max(SwellSlow, 1e-3f)));

            float want = grounded && !swimming ? SwellFilter : 0f;
            swellBlend = Mathf.MoveTowards(swellBlend, want, dt / 0.25f);

            float limit = Mathf.Max(SwellLimit, 1e-4f);
            float raw = -(swellFast - swellSlow) * swellBlend;
            return limit * (float)System.Math.Tanh(raw / limit);
        }

        private void SampleWater()
        {
            Vector3 at = transform.position;
            float surface = Ocean.GetHeight(at);
            waterRise = Ocean.GetVerticalVelocity(new Vector2(at.x, at.z));
            submersion = Mathf.Clamp01((surface - at.y) / Mathf.Max(capsule.height, 0.1f));
            swimming = submersion > (swimming ? SwimEntry * 0.6f : SwimEntry);
        }

        private void SampleFooting(float dt)
        {
            own ??= new HashSet<Collider>(GetComponentsInChildren<Collider>(true));
            Vector3 center = transform.TransformPoint(capsule.center);
            float radius = capsule.radius * 0.9f;
            float reach = capsule.height * 0.5f - radius + 0.15f;
            int count = Physics.SphereCastNonAlloc(center, radius, Vector3.down, hits, reach, ~0, QueryTriggerInteraction.Ignore);
            float standable = Mathf.Cos(FloorMaxAngle * Mathf.Deg2Rad);

            int best = -1;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (own.Contains(hit.collider)) continue;
                Vector3 n = hit.distance > 0f ? hit.normal : Vector3.up;
                if (n.y < standable) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                best = i;
            }

            if (best >= 0)
            {
                RaycastHit hit = hits[best];
                airborneFor = 0f;
                grounded = true;
                Vector3 point = hit.distance > 0f ? hit.point : center + Vector3.down * reach;
                deckVelocity = hit.rigidbody != null ? hit.rigidbody.GetPointVelocity(point) : Vector3.zero;
                deckYawRate = hit.rigidbody != null ? hit.rigidbody.angularVelocity.y * Mathf.Rad2Deg : 0f;
                Vector3 sensed = hit.distance > 0f ? hit.normal : Vector3.up;
                float ease = 1f - Mathf.Exp(-dt / Mathf.Max(FootingSmooth, 1e-3f));
                groundNormal = Vector3.Lerp(groundNormal, sensed, ease).normalized;
                Transform floor = hit.rigidbody != null ? hit.rigidbody.transform : hit.collider.transform;
                if (floor != deck)
                {
                    deck = floor;
                    swellReady = false;
                }
                return;
            }

            airborneFor += dt;
            if (airborneFor < FootingGrace) return;

            grounded = false;
            deckYawRate = 0f;
            deckVelocity = Vector3.Lerp(deckVelocity, Vector3.zero, 1f - Mathf.Exp(-dt / Mathf.Max(CarryTime, 1e-3f)));
            groundNormal = Vector3.Lerp(groundNormal, Vector3.up, 1f - Mathf.Exp(-dt / Mathf.Max(FootingSmooth, 1e-3f))).normalized;
        }

        private void Launch()
        {
            if (!jump || ladder != null) return;
            jump = false;

            Vector3 velocity = body.linearVelocity;

            if (swimming)
            {
                velocity.y += Mathf.Max(StrokeKick * submersion - (velocity.y - waterRise), 0f);
                body.linearVelocity = velocity;
                return;
            }

            if (!grounded) return;

            velocity.y += Mathf.Max(JumpSpeed - (velocity.y - deckVelocity.y), 0f);
            body.linearVelocity = velocity;
            grounded = false;
            airborneFor = FootingGrace;
        }

        private void SampleClimb(float dt)
        {
            climbWait = Mathf.Max(climbWait - dt, 0f);

            if (ladder != null)
            {
                if (ladder.isActiveAndEnabled)
                {
                    grounded = false;
                    swimming = false;
                    deckYawRate = ladder.Carrier != null ? ladder.Carrier.angularVelocity.y * Mathf.Rad2Deg : 0f;
                    return;
                }
                LetGo(Vector3.zero);
                return;
            }

            if (climbWait > 0f || wish.sqrMagnitude < 1e-4f) return;

            Ladder found = Ladder.Nearest(transform.position);
            if (found == null) return;

            float height = Mathf.Clamp(found.Height(transform.position), found.Bottom, found.Rim);
            Vector3 toward = found.Rail(height) - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 1e-4f && Vector3.Dot(wish.normalized, toward.normalized) < ClimbEnter) return;

            ladder = found;
            rung = height;
            grounded = false;
            swimming = false;
            body.useGravity = false;
        }

        private void ApplyClimb(float dt)
        {
            if (jump)
            {
                jump = false;
                LetGo(-ladder.Facing * ClimbStepOff + Vector3.up * (JumpSpeed * 0.5f));
                return;
            }

            rung += stick.y * ClimbSpeed * dt;

            if (rung >= ladder.Top)
            {
                LetGo(-ladder.Facing * ClimbStepOff + Vector3.up * (JumpSpeed * 0.25f));
                return;
            }

            if (rung <= ladder.Bottom && stick.y < 0f)
            {
                LetGo(Vector3.zero);
                return;
            }

            rung = Mathf.Max(rung, ladder.Bottom);
            Vector3 target = ladder.Rail(rung);
            Vector3 carry = ladder.Carrier != null ? ladder.Carrier.GetPointVelocity(target) : Vector3.zero;
            Vector3 correction = Vector3.ClampMagnitude((target - transform.position) / dt, ClimbSnap);

            deckVelocity = carry;
            groundNormal = Vector3.up;
            body.linearVelocity = carry + correction;
        }

        private void LetGo(Vector3 push)
        {
            ladder = null;
            body.useGravity = true;
            climbWait = ClimbRearm;
            airborneFor = FootingGrace;
            grounded = false;
            if (push.sqrMagnitude > 1e-6f) body.linearVelocity = deckVelocity + push;
        }

        private void ApplyBuoyancy()
        {
            float buoy = Mathf.Lerp(Buoyancy, DiveBuoyancy, Mathf.Max(-stroke, 0f));
            Vector3 lift = -Physics.gravity * (body.mass * submersion * buoy);
            float rise = body.linearVelocity.y - waterRise;
            lift.y -= rise * (WaterDrag * body.mass * submersion);
            body.AddForce(lift, ForceMode.Force);
        }

        private void ApplySwim()
        {
            Vector3 velocity = body.linearVelocity;
            Vector3 planar = velocity - Vector3.up * velocity.y;
            Vector3 force = (wish * SwimSpeed - planar) * (SwimAcceleration * body.mass * submersion);

            if (stroke != 0f)
            {
                float want = waterRise + stroke * SwimVertical;
                force.y = (want - velocity.y) * (SwimAcceleration * body.mass * submersion);
            }

            body.AddForce(Vector3.ClampMagnitude(force, MaxFootingForce), ForceMode.Force);
        }

        private void ApplyWalk()
        {
            Vector3 ground = grounded ? groundNormal : Vector3.up;
            Vector3 relative = body.linearVelocity - deckVelocity;
            Vector3 planar = relative - ground * Vector3.Dot(relative, ground);

            Vector3 aim = wish - ground * Vector3.Dot(wish, ground);
            if (aim.sqrMagnitude > 1e-6f) aim = aim.normalized * wish.magnitude;
            Vector3 target = aim * WalkSpeed;

            Vector3 force = (target - planar) * (Acceleration * body.mass);

            if (grounded) force = Vector3.ClampMagnitude(force, MaxFootingForce * Traction);
            else force *= AirControl;

            float hands = 1f - (Grab != null ? Grab.Load : 0f) * GripDrag;
            body.AddForce(force * Mathf.Max(hands, 0f), ForceMode.Force);
        }
    }
}

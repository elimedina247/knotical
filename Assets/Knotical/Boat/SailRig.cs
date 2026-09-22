using UnityEngine;

namespace Knotical
{
    [RequireComponent(typeof(Rigidbody), typeof(BoatMotor))]
    public class SailRig : MonoBehaviour
    {
        [Min(0f)] public float SailAcceleration = 8f;
        [Min(0.1f)] public float ReferenceWind = 9f;
        [Range(0f, 1f)] public float ApparentWind = 0.3f;
        [Range(0f, 2f)] public float HaulRate = 0.35f;

        [Range(0f, 1f)] public float Leeway = 0.35f;
        [Range(20f, 60f)] public float DriveNoGoDeg = 35f;
        [Range(0f, 0.5f)] public float NoGoDrive = 0.05f;
        [Range(0f, 1f)] public float CloseHauledDrive = 0.35f;
        [Range(0f, 1f)] public float ReachDrive = 0.8f;
        public Vector3 SailForceLocalPosition = new Vector3(0f, 3f, 0f);

        public float Deployment { get; private set; }
        public float TargetDeployment { get; private set; }
        public float Pressure { get; private set; }
        public Vector3 ApparentWindVelocity { get; private set; }
        public float OffWindDeg { get; private set; } = 180f;
        public float Polar { get; private set; } = 1f;
        public float HeelDeg { get; private set; }
        public float CanvasForce { get; private set; }

        private Rigidbody body;
        private BoatMotor motor;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            motor = GetComponent<BoatMotor>();
        }

        private void Update()
        {
            TargetDeployment = motor.SailSetting;
            Deployment = Mathf.MoveTowards(Deployment, TargetDeployment, HaulRate * Time.deltaTime);
        }

        private void FixedUpdate()
        {
            Vector2 breeze2 = Wind.Velocity;
            var breeze = new Vector3(breeze2.x, 0f, breeze2.y);
            Vector3 apparent = breeze - body.linearVelocity * ApparentWind;
            apparent.y = 0f;
            ApparentWindVelocity = apparent;

            Vector3 ahead = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (ahead.sqrMagnitude < 0.0001f) return;
            ahead.Normalize();

            float speed = apparent.magnitude;
            OffWindDeg = breeze.sqrMagnitude < 0.01f ? 180f : Vector3.Angle(ahead, -breeze);
            Polar = ComputePolar(OffWindDeg, breeze.sqrMagnitude);
            Pressure = Mathf.Clamp01(speed / ReferenceWind);
            HeelDeg = Mathf.Asin(Mathf.Clamp(transform.right.y, -1f, 1f)) * Mathf.Rad2Deg;

            if (Deployment <= 0f || speed <= 0.01f)
            {
                CanvasForce = 0f;
                return;
            }

            float ratio = speed / ReferenceWind;
            Vector3 canvas = apparent / speed * (body.mass * SailAcceleration * ratio * ratio * Deployment);
            Vector3 side = canvas - ahead * Vector3.Dot(canvas, ahead);
            CanvasForce = canvas.magnitude;
            body.AddForce(ahead * (canvas.magnitude * Polar), ForceMode.Force);
            body.AddForceAtPosition(side * Leeway, transform.TransformPoint(SailForceLocalPosition), ForceMode.Force);
        }

        private float ComputePolar(float offWind, float breezeSq)
        {
            if (breezeSq < 0.01f) return 1f;

            float shape = offWind <= 90f
                ? Mathf.Lerp(CloseHauledDrive, ReachDrive, Mathf.SmoothStep(0f, 1f, (offWind - DriveNoGoDeg) / Mathf.Max(90f - DriveNoGoDeg, 1f)))
                : Mathf.Lerp(ReachDrive, 1f, Mathf.SmoothStep(0f, 1f, (offWind - 90f) / 90f));

            float gate = Mathf.SmoothStep(0f, 1f, (offWind - (DriveNoGoDeg - 6f)) / 12f);
            return Mathf.Lerp(NoGoDrive, shape, gate);
        }
    }
}

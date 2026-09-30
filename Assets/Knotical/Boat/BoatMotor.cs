using UnityEngine;

namespace Knotical
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatMotor : MonoBehaviour
    {
        public const int MinGear = -1;
        public const int MaxGear = 3;

        [Range(0f, 10f)] public float PropellerAcceleration = 2f;
        [Range(0f, 1f)] public float PutterThrottle = 0.25f;
        [Range(0f, 10f)] public float PropWash = 4f;
        [Range(0f, 1f)] public float RudderLift = 0.08f;
        [Range(1f, 30f)] public float RudderMaxFlow = 10f;
        public Vector3 RudderLocalPosition = new Vector3(0f, -0.6f, -6f);

        public int Gear { get; private set; }
        public float Throttle { get; set; }
        public float RudderAngle { get; private set; }
        public float ForwardSpeed { get; private set; }
        public bool RudderSubmerged { get; private set; }

        public float SailSetting => Gear switch
        {
            2 => 0.5f,
            3 => 1f,
            _ => 0f,
        };

        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public void SetGear(int gear)
        {
            Gear = Mathf.Clamp(gear, MinGear, MaxGear);
            Throttle = Gear == 0 ? 0f : Mathf.Sign(Gear) * PutterThrottle;
        }

        public void SetRudder(float angle)
        {
            RudderAngle = Mathf.Clamp(angle, -1f, 1f);
        }

        private void FixedUpdate()
        {
            Vector3 ahead = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (ahead.sqrMagnitude < 0.0001f) return;
            ahead.Normalize();
            Vector3 side = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            Vector3 rudderWorld = transform.TransformPoint(RudderLocalPosition);
            RudderSubmerged = rudderWorld.y < Ocean.GetHeight(rudderWorld);
            ForwardSpeed = Vector3.Dot(body.linearVelocity, ahead);

            if (!RudderSubmerged) return;

            float mass = body.mass;
            if (Throttle != 0f)
            {
                body.AddForceAtPosition(ahead * (mass * PropellerAcceleration * Throttle), rudderWorld, ForceMode.Force);
            }

            if (RudderAngle != 0f)
            {
                float flow = Mathf.Clamp(ForwardSpeed + PropWash * Throttle, -RudderMaxFlow, RudderMaxFlow);
                float lift = RudderLift * RudderAngle * flow * Mathf.Abs(flow);
                body.AddForceAtPosition(-side * (mass * lift), rudderWorld, ForceMode.Force);
            }
        }
    }
}

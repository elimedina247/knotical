using UnityEngine;
using UnityEngine.InputSystem;

namespace Knotical
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatMotor : MonoBehaviour
    {
        [Min(0f)] public float PropellerStrength = 60000f;
        [Min(0f)] public float RudderStrength = 40000f;
        [Min(0f)] public float MaxSpeed = 8f;
        public Vector3 MotorLocalPosition = new Vector3(0f, -0.8f, -6f);
        public bool PlayerControlled = true;

        public float Throttle { get; set; }
        public float Steer { get; set; }
        public float ForwardSpeed { get; private set; }
        public bool MotorSubmerged { get; private set; }

        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (!PlayerControlled) return;

            Keyboard k = Keyboard.current;
            if (k == null) return;

            float throttle = 0f;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) throttle += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) throttle -= 1f;

            float steer = 0f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) steer += 1f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) steer -= 1f;

            Throttle = throttle;
            Steer = steer;
        }

        private void FixedUpdate()
        {
            Vector3 motorWorld = transform.TransformPoint(MotorLocalPosition);
            MotorSubmerged = motorWorld.y < Ocean.GetHeight(motorWorld);
            ForwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);

            if (!MotorSubmerged) return;

            bool underLimit = Mathf.Abs(ForwardSpeed) < MaxSpeed;
            bool reversing = Throttle < 0f && ForwardSpeed > 0f;
            if (Throttle != 0f && (underLimit || reversing))
            {
                Vector3 thrustDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                body.AddForceAtPosition(thrustDir * (PropellerStrength * Throttle), motorWorld, ForceMode.Force);
            }

            if (Steer != 0f && Mathf.Abs(ForwardSpeed) > 0.2f)
            {
                float authority = Mathf.Clamp(Mathf.Abs(ForwardSpeed) / MaxSpeed, 0.2f, 1f) * Mathf.Sign(ForwardSpeed);
                body.AddTorque(Vector3.up * (RudderStrength * Steer * authority), ForceMode.Force);
            }
        }
    }
}

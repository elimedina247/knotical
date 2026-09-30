using UnityEngine;

namespace Knotical
{
    [RequireComponent(typeof(BoatMotor))]
    public class Helm : MonoBehaviour
    {
        public Transform Wheel;
        public Transform Rudder;
        [Range(90f, 1080f)] public float WheelLockDeg = 540f;
        [Range(10f, 60f)] public float RudderLockDeg = 35f;
        [Range(0.1f, 5f)] public float DegreesPerPixel = 1.2f;

        public bool Held { get; private set; }
        public float WheelAngle { get; private set; }

        private BoatMotor motor;
        private Quaternion wheelRest;
        private Quaternion rudderRest;
        private Vector3 wheelAxis;
        private Vector3 rudderAxis;

        private void Awake()
        {
            motor = GetComponent<BoatMotor>();
            if (Wheel != null)
            {
                wheelRest = Wheel.localRotation;
                wheelAxis = Wheel.parent.InverseTransformDirection(transform.forward);
            }
            if (Rudder != null)
            {
                rudderRest = Rudder.localRotation;
                rudderAxis = Rudder.parent.InverseTransformDirection(transform.up);
            }
        }

        public void Grip(bool held)
        {
            Held = held;
        }

        public void Twist(float pixels)
        {
            if (pixels == 0f) return;
            float angle = Mathf.Clamp(WheelAngle + pixels * DegreesPerPixel, -WheelLockDeg, WheelLockDeg);
            motor.SetRudder(angle / WheelLockDeg);
            WheelAngle = angle;
        }

        public void Shift(int steps)
        {
            if (steps != 0) motor.SetGear(motor.Gear + steps);
        }

        private void Update()
        {
            WheelAngle = motor.RudderAngle * WheelLockDeg;
            if (Wheel != null) Wheel.localRotation = Quaternion.AngleAxis(WheelAngle, wheelAxis) * wheelRest;
            if (Rudder != null) Rudder.localRotation = Quaternion.AngleAxis(-motor.RudderAngle * RudderLockDeg, rudderAxis) * rudderRest;
        }
    }
}

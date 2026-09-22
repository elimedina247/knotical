using UnityEngine;
using UnityEngine.InputSystem;

namespace Knotical
{
    [RequireComponent(typeof(BoatMotor))]
    public class Helm : MonoBehaviour
    {
        public Transform Wheel;
        public Transform Rudder;
        public Transform Stand;
        public Transform Helmsman;
        [Range(0.5f, 6f)] public float Reach = 2.5f;
        [Range(90f, 1080f)] public float WheelLockDeg = 540f;
        [Range(10f, 60f)] public float RudderLockDeg = 35f;
        [Range(0.1f, 5f)] public float DegreesPerPixel = 1.2f;
        [Min(0.1f)] public float GrabRadius = 0.6f;

        public bool Dragging { get; private set; }
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

        private void Update()
        {
            ReadMouse();

            WheelAngle = motor.RudderAngle * WheelLockDeg;
            if (Wheel != null) Wheel.localRotation = Quaternion.AngleAxis(WheelAngle, wheelAxis) * wheelRest;
            if (Rudder != null) Rudder.localRotation = Quaternion.AngleAxis(-motor.RudderAngle * RudderLockDeg, rudderAxis) * rudderRest;
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Wheel == null) return;

            if (Dragging)
            {
                if (!mouse.leftButton.isPressed)
                {
                    Dragging = false;
                    return;
                }
                float delta = mouse.delta.ReadValue().x * DegreesPerPixel;
                motor.SetRudder((WheelAngle + delta) / WheelLockDeg);
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (!WithinReach()) return;

            Camera camera = Camera.main;
            if (camera == null) return;
            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (RayHitsSphere(ray, Wheel.position, GrabRadius)) Dragging = true;
        }

        private bool WithinReach()
        {
            if (Helmsman == null || Stand == null) return true;
            return Vector3.Distance(Helmsman.position, Stand.position) <= Reach;
        }

        private static bool RayHitsSphere(Ray ray, Vector3 center, float radius)
        {
            Vector3 toCenter = center - ray.origin;
            float along = Vector3.Dot(toCenter, ray.direction);
            if (along < 0f) return false;
            Vector3 closest = ray.origin + ray.direction * along;
            return (closest - center).sqrMagnitude <= radius * radius;
        }
    }
}

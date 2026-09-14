using UnityEngine;
using UnityEngine.InputSystem;

namespace Knotical
{
    public class ChaseCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0f, 7f, -22f);
        public Vector3 LookOffset = new Vector3(0f, 1.5f, 6f);
        [Range(0.5f, 20f)] public float PositionResponse = 4f;
        [Range(0.5f, 20f)] public float RotationResponse = 6f;
        [Range(0.02f, 1f)] public float OrbitSensitivity = 0.25f;
        [Range(0.2f, 4f)] public float MinZoom = 0.3f;
        [Range(1f, 8f)] public float MaxZoom = 3f;

        private float orbitYaw;
        private float orbitPitch;
        private float zoom = 1f;

        private void LateUpdate()
        {
            if (Target == null) return;

            ReadMouse();

            Vector3 flatForward = Vector3.ProjectOnPlane(Target.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 1e-4f) flatForward = Vector3.forward;
            Quaternion yaw = Quaternion.LookRotation(flatForward, Vector3.up);
            Quaternion orbit = yaw * Quaternion.Euler(orbitPitch, orbitYaw, 0f);

            Vector3 desired = Target.position + orbit * (Offset * zoom);
            float dt = Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-PositionResponse * dt));

            Vector3 lookAt = Target.position + yaw * LookOffset;
            Quaternion wanted = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-RotationResponse * dt));
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                orbitYaw += delta.x * OrbitSensitivity;
                orbitPitch = Mathf.Clamp(orbitPitch - delta.y * OrbitSensitivity, -15f, 60f);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                zoom = Mathf.Clamp(zoom * Mathf.Exp(-scroll * 0.001f), MinZoom, MaxZoom);
            }
        }
    }
}

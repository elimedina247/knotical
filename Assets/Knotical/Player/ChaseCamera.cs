using UnityEngine;

namespace Knotical
{
    public class ChaseCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0f, 7f, -22f);
        public Vector3 LookOffset = new Vector3(0f, 1.5f, 6f);
        [Range(0.5f, 20f)] public float PositionResponse = 4f;
        [Range(0.5f, 20f)] public float RotationResponse = 6f;

        private void LateUpdate()
        {
            if (Target == null) return;

            Vector3 flatForward = Vector3.ProjectOnPlane(Target.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 1e-4f) flatForward = Vector3.forward;
            Quaternion yaw = Quaternion.LookRotation(flatForward, Vector3.up);

            Vector3 desired = Target.position + yaw * Offset;
            float dt = Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-PositionResponse * dt));

            Vector3 lookAt = Target.position + yaw * LookOffset;
            Quaternion wanted = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-RotationResponse * dt));
        }
    }
}

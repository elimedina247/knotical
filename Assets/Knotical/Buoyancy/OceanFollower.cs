using UnityEngine;

namespace Knotical
{
    [RequireComponent(typeof(Rigidbody))]
    public class OceanFollower : MonoBehaviour
    {
        public float Draft = 0.8f;
        public float Length = 12f;
        public float Width = 4f;
        [Range(0f, 3f)] public float ShortWaveFilter = 1f;
        [Range(0.5f, 20f)] public float Response = 2.5f;

        private Rigidbody body;
        private Vector3 anchor;
        private float yaw;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            anchor = transform.position;
            yaw = transform.eulerAngles.y;
        }

        private void Start()
        {
            Place(1f);
        }

        private void FixedUpdate()
        {
            Place(1f - Mathf.Exp(-Response * Time.fixedDeltaTime));
        }

        private void Place(float blend)
        {
            float cutoff = ShortWaveFilter * Mathf.Max(Length, Width);
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = heading * Vector3.forward * (Length * 0.5f);
            Vector3 right = heading * Vector3.right * (Width * 0.5f);

            float bow = Height(anchor + forward, cutoff);
            float stern = Height(anchor - forward, cutoff);
            float starboard = Height(anchor + right, cutoff);
            float port = Height(anchor - right, cutoff);

            float level = 0.25f * (bow + stern + starboard + port);
            float pitch = Mathf.Atan2(stern - bow, Length) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(starboard - port, Width) * Mathf.Rad2Deg;

            var targetPosition = new Vector3(anchor.x, level - Draft, anchor.z);
            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, roll);

            body.MovePosition(Vector3.Lerp(body.position, targetPosition, blend));
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, blend));
        }

        private static float Height(Vector3 point, float cutoff) =>
            Ocean.GetHeight(new Vector2(point.x, point.z), cutoff);
    }
}

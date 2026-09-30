using UnityEngine;

namespace Knotical
{
    public class WheelGrip : MonoBehaviour, IGrabbable
    {
        private Helm helm;
        private Vector3 local;

        public bool Anchors => false;

        public bool CapturesInput => true;

        private void Awake()
        {
            helm = GetComponentInParent<Helm>();
        }

        public Vector3 Attach(Vector3 worldPoint)
        {
            local = transform.InverseTransformPoint(worldPoint);
            if (helm != null) helm.Grip(true);
            return worldPoint;
        }

        public Vector3 Track(GrabHold hold, float dt)
        {
            if (helm != null)
            {
                helm.Twist(hold.Twist.x);
                helm.Shift(hold.Shift);
            }
            return transform.TransformPoint(local);
        }

        public void Detach()
        {
            if (helm != null) helm.Grip(false);
        }
    }
}

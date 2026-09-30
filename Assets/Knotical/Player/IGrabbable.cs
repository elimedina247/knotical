using UnityEngine;

namespace Knotical
{
    public struct GrabHold
    {
        public Vector3 Point;
        public Vector3 Chest;
        public Vector3 Aim;
        public Vector2 Twist;
        public int Shift;
    }

    public interface IGrabbable
    {
        bool Anchors { get; }

        bool CapturesInput { get; }

        Vector3 Attach(Vector3 worldPoint);

        Vector3 Track(GrabHold hold, float dt);

        void Detach();
    }
}

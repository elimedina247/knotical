using UnityEngine;

namespace Knotical
{
    [CreateAssetMenu(menuName = "Knotical/Wind Settings", fileName = "WindSettings")]
    public class WindSettings : ScriptableObject
    {
        [Range(0f, 35f)] public float BaseSpeed = 9f;
        [Range(0f, 360f)] public float BaseDirectionDeg = 35f;
        [Range(0f, 90f)] public float VeerDeg = 22f;
        [Range(0f, 1f)] public float Gustiness = 0.35f;
        [Range(0.1f, 20f)] public float TimeScale = 1f;
    }
}

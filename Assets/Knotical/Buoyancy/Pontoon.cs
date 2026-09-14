using UnityEngine;

namespace Knotical
{
    public class Pontoon : MonoBehaviour
    {
        private void OnDrawGizmosSelected()
        {
            var buoyancy = GetComponentInParent<Buoyancy>();
            if (buoyancy == null) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, buoyancy.Radius);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Knotical
{
    public class KenneySails : MonoBehaviour
    {
        [Range(0f, 1f)] public float FirstSailAt = 0.05f;
        [Range(0f, 1f)] public float LastSailAt = 0.55f;

        private SailRig rig;
        private Renderer[] sails = new Renderer[0];

        private void Awake()
        {
            rig = GetComponent<SailRig>();
            var found = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.ToLowerInvariant().StartsWith("sail")) found.Add(r);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            sails = found.ToArray();
        }

        private void LateUpdate()
        {
            float deployment = rig != null ? rig.Deployment : 1f;
            for (int i = 0; i < sails.Length; i++)
            {
                float threshold = sails.Length == 1 ? FirstSailAt : Mathf.Lerp(FirstSailAt, LastSailAt, i / (sails.Length - 1f));
                sails[i].enabled = deployment >= threshold;
            }
        }
    }
}

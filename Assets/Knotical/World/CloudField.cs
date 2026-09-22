using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Knotical
{
    public class CloudField : MonoBehaviour
    {
        [Range(0, 200)] public int Count = 45;
        [Range(200f, 4000f)] public float Extent = 1500f;
        public Vector2 Altitude = new Vector2(170f, 280f);
        public Vector2 Size = new Vector2(50f, 130f);
        [Range(0f, 3f)] public float DriftFactor = 0.7f;
        public int Seed = 7;
        public Material Material;

        private readonly List<Transform> clouds = new List<Transform>();
        private readonly List<Mesh> meshes = new List<Mesh>();

        private void Start() => Build();

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            meshes.Clear();
        }

        private void Update()
        {
            Vector2 wind = Wind.Velocity * (DriftFactor * Time.deltaTime);
            var drift = new Vector3(wind.x, 0f, wind.y);
            foreach (Transform cloud in clouds)
            {
                Vector3 p = cloud.position + drift;
                if (p.x > Extent) p.x -= 2f * Extent;
                if (p.x < -Extent) p.x += 2f * Extent;
                if (p.z > Extent) p.z -= 2f * Extent;
                if (p.z < -Extent) p.z += 2f * Extent;
                cloud.position = p;
            }
        }

        private void Build()
        {
            var rng = new System.Random(Seed);
            for (int i = 0; i < Count; i++)
            {
                float size = Mathf.Lerp(Size.x, Size.y, (float)rng.NextDouble());
                Mesh mesh = CloudMeshBuilder.Build(rng, size);
                meshes.Add(mesh);

                var go = new GameObject($"Cloud{i}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(
                    Mathf.Lerp(-Extent, Extent, (float)rng.NextDouble()),
                    Mathf.Lerp(Altitude.x, Altitude.y, (float)rng.NextDouble()),
                    Mathf.Lerp(-Extent, Extent, (float)rng.NextDouble()));
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                clouds.Add(go.transform);
            }
        }
    }
}

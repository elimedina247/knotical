using UnityEngine;

namespace Knotical
{
    public class FoamEmitter : MonoBehaviour
    {
        [Range(4, 96)] public int Points = 24;
        [Range(0f, 60f)] public float RingRate = 0.8f;
        [Range(0f, 60f)] public float WakeRate = 8f;
        [Range(0.1f, 12f)] public float Size = 1.5f;
        [Range(0.5f, 20f)] public float Life = 5f;
        [Range(0f, 1f)] public float Strength = 0.35f;
        [Range(0f, 3f)] public float Drift = 0.25f;
        [Range(0.2f, 1.5f)] public float WakeWidth = 0.9f;

        private ParticleSystem stamps;
        private Collider[] colliders;
        private Rigidbody body;
        private float[] accumulators;
        private float wakeAccumulator;

        private void Start()
        {
            colliders = GetComponentsInChildren<Collider>();
            body = GetComponentInParent<Rigidbody>();
            accumulators = new float[Points];
            CreateStamps();
        }

        private void Update()
        {
            if (colliders.Length == 0 || stamps == null) return;
            if (accumulators.Length != Points) accumulators = new float[Points];

            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);

            var centre = new Vector2(bounds.center.x, bounds.center.z);
            float waterY = Ocean.GetHeight(bounds.center);
            if (bounds.min.y > waterY + 0.5f || bounds.max.y < waterY - 0.5f) return;

            float dt = Time.deltaTime;
            EmitRing(bounds, centre, waterY, dt);
            EmitWake(bounds, dt);
        }

        private void EmitRing(Bounds bounds, Vector2 centre, float waterY, float dt)
        {
            float reach = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.5f + 1f;

            for (int k = 0; k < Points; k++)
            {
                float angle = (k + 0.5f) / Points * 2f * Mathf.PI;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var origin = new Vector3(centre.x + dir.x * reach, waterY, centre.y + dir.z * reach);
                var ray = new Ray(origin, -dir);

                if (!Hit(ray, reach, out RaycastHit hit)) continue;

                accumulators[k] += RingRate * dt;
                int count = Mathf.FloorToInt(accumulators[k]);
                accumulators[k] -= count;

                for (int n = 0; n < count; n++) Emit(hit.point, dir * Drift, Size);
            }
        }

        private void EmitWake(Bounds bounds, float dt)
        {
            if (body == null) return;

            Vector3 planar = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float speed = planar.magnitude;
            if (speed < 0.3f) return;

            Vector3 forward = planar / speed;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float halfLength = Mathf.Abs(bounds.extents.x * forward.x) + Mathf.Abs(bounds.extents.z * forward.z);
            float halfWidth = (Mathf.Abs(bounds.extents.x * side.x) + Mathf.Abs(bounds.extents.z * side.z)) * WakeWidth;
            Vector3 stern = bounds.center - forward * (halfLength * 0.85f);

            wakeAccumulator += WakeRate * speed * dt;
            int count = Mathf.FloorToInt(wakeAccumulator);
            wakeAccumulator -= count;

            float sizeBoost = Mathf.Lerp(1f, 1.5f, Mathf.Clamp01(speed / 8f));
            for (int n = 0; n < count; n++)
            {
                float lateral = Random.Range(-halfWidth, halfWidth);
                Vector3 point = stern + side * lateral - forward * Random.Range(0f, 1.5f);
                Emit(point, -forward * (Drift * 0.5f), Size * sizeBoost);
            }
        }

        private bool Hit(Ray ray, float reach, out RaycastHit best)
        {
            best = default;
            float bestDistance = float.MaxValue;
            foreach (Collider c in colliders)
            {
                if (!c.enabled || c.isTrigger) continue;
                if (c.Raycast(ray, out RaycastHit hit, reach) && hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = hit;
                }
            }
            return bestDistance < float.MaxValue;
        }

        private void Emit(Vector3 point, Vector3 velocity, float size)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(point.x, Ocean.SeaLevel, point.z),
                velocity = velocity * Random.Range(0.5f, 1f),
                startSize = size * Random.Range(0.7f, 1.3f),
                startLifetime = Life * Random.Range(0.7f, 1.3f),
                startColor = new Color(1f, 1f, 1f, Strength * Random.Range(0.6f, 1f)),
            };
            stamps.Emit(emit, 1);
        }

        private void CreateStamps()
        {
            int layer = FoamCapture.Layer;
            var go = new GameObject("FoamStamps");
            go.transform.SetParent(null, false);
            if (layer >= 0) go.layer = layer;

            stamps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = stamps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4000;
            main.startSpeed = 0f;
            main.playOnAwake = true;
            main.loop = true;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = stamps.emission;
            emission.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colour = stamps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = stamps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = new Material(Shader.Find("Knotical/FoamStamp"));
        }

        private void OnDestroy()
        {
            if (stamps != null) Destroy(stamps.gameObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Knotical
{
    [RequireComponent(typeof(Rigidbody))]
    public class Buoyancy : MonoBehaviour
    {
        private const float Gravity = 9.81f;
        private const float WaterDensity = 1025f;

        public static readonly List<Buoyancy> Active = new List<Buoyancy>();

        [Range(0.05f, 4f)] public float Radius = 0.5f;
        [Range(0.1f, 10f)] public float Coefficient = 1f;
        [Min(0f)] public float MaxForce;
        [Range(0f, 8f)] public float DampingFactor1 = 1.2f;
        [Range(0f, 4f)] public float DampingFactor2 = 0.5f;
        [Range(0f, 8f)] public float DragCoefficient = 0.8f;
        [Range(0f, 8f)] public float DragCoefficient2 = 0.5f;
        [Range(0.5f, 30f)] public float MaxDragSpeed = 8f;
        [Range(0f, 8f)] public float AngularDrag = 0.8f;
        [Range(0f, 1f)] public float TiltResponse = 1f;
        [Range(0f, 3f)] public float ShortWaveFilter;
        public bool ApplyWaveNormal;
        [Range(0f, 20f)] public float WaveNormalGain = 3f;
        public bool SnapToWaterOnActivation;
        [Range(1, 8)] public int SnapToWaterIterations = 3;
        public bool DrawDebug;

        private Rigidbody body;
        private Transform[] pontoons = new Transform[0];
        private Vector3[] world = new Vector3[0];
        private float[] water = new float[0];
        private float[] wet = new float[0];
        private Vector3[] force = new Vector3[0];
        private float lever = 0.5f;
        private float footprint;
        private bool snapDone;

        public Rigidbody Body => body;
        public int ProbeCount => pontoons.Length;
        public float Wetness { get; private set; }

        public void GetProbe(int i, out Vector3 worldPosition, out float wetness, out Vector3 appliedForce, out float span)
        {
            worldPosition = world[i];
            wetness = wet[i];
            appliedForce = force[i];
            span = Radius;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            Pontoon[] found = GetComponentsInChildren<Pontoon>();
            pontoons = new Transform[Mathf.Max(found.Length, 1)];
            for (int i = 0; i < found.Length; i++) pontoons[i] = found[i].transform;
            if (found.Length == 0) pontoons[0] = transform;

            int count = pontoons.Length;
            world = new Vector3[count];
            water = new float[count];
            wet = new float[count];
            force = new Vector3[count];

            var local = new Vector2[count];
            float spread = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = body.transform.InverseTransformPoint(pontoons[i].position);
                local[i] = new Vector2(p.x, p.z);
                spread += local[i].magnitude;
            }
            lever = Mathf.Max(spread / count, 0.25f);

            float reach = 0f;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    reach = Mathf.Max(reach, Vector2.Distance(local[i], local[j]));
                }
            }
            footprint = reach + 2f * Radius;

            snapDone = !SnapToWaterOnActivation;
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        private void FixedUpdate()
        {
            if (!snapDone) SnapToWater();

            int count = pontoons.Length;
            float pontoonVolume = 4f / 3f * Mathf.PI * Radius * Radius * Radius;
            float capacity = WaterDensity * pontoonVolume * Coefficient;
            float cutoff = ShortWaveFilter * footprint;

            float mean = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = pontoons[i].position;
                world[i] = p;
                water[i] = Ocean.GetHeight(new Vector2(p.x, p.z), cutoff);
                mean += water[i];
            }
            mean /= count;

            float wetSum = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = world[i];
                float level = mean + (water[i] - mean) * TiltResponse;
                float s = Mathf.Clamp01((level - (p.y - Radius)) / (2f * Radius));
                wet[i] = s;

                if (s <= 0f)
                {
                    force[i] = Vector3.zero;
                    continue;
                }

                wetSum += s;

                Vector3 velocity = body.GetPointVelocity(p);

                Vector3 f = Vector3.up * (Gravity * capacity * s);

                f.y -= (DampingFactor1 * velocity.y + DampingFactor2 * velocity.y * Mathf.Abs(velocity.y))
                    * s * capacity;

                var planar = new Vector3(velocity.x, 0f, velocity.z);
                float blend = Mathf.Min(planar.magnitude / MaxDragSpeed, 1f);
                f -= planar * ((DragCoefficient + DragCoefficient2 * blend) * s * capacity);

                if (MaxForce > 0f && f.magnitude > MaxForce) f = f.normalized * MaxForce;

                force[i] = f;
                body.AddForceAtPosition(f, p, ForceMode.Force);
            }

            Wetness = wetSum / count;
            if (Wetness <= 0f) return;

            float inertia = body.mass * lever * lever;

            if (AngularDrag > 0f)
            {
                body.AddTorque(-body.angularVelocity * (AngularDrag * Wetness * inertia), ForceMode.Force);
            }

            if (ApplyWaveNormal && WaveNormalGain > 0f)
            {
                Vector3 origin = body.position;
                Vector3 up = body.transform.up;
                Vector3 normal = Ocean.GetNormal(new Vector2(origin.x, origin.z), cutoff);
                Vector3 lean = Vector3.Lerp(Vector3.up, normal, TiltResponse).normalized;
                body.AddTorque(Vector3.Cross(up, lean) * (WaveNormalGain * Wetness * inertia), ForceMode.Force);
            }
        }

        private void SnapToWater()
        {
            snapDone = true;

            int count = pontoons.Length;
            float totalVolume = 4f / 3f * Mathf.PI * Radius * Radius * Radius * count;
            float rest = Mathf.Clamp01(body.mass / (WaterDensity * Mathf.Max(totalVolume, 0.0001f) * Coefficient));
            float restDepth = 2f * Radius * rest - Radius;

            for (int iter = 0; iter < SnapToWaterIterations; iter++)
            {
                float waterMean = 0f;
                float pontoonMean = 0f;

                for (int i = 0; i < count; i++)
                {
                    Vector3 p = pontoons[i].position;
                    waterMean += Ocean.GetHeight(new Vector2(p.x, p.z));
                    pontoonMean += p.y;
                }

                waterMean /= count;
                pontoonMean /= count;

                body.position += Vector3.up * (waterMean - restDepth - pontoonMean);
                body.transform.position = body.position;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void OnDrawGizmos()
        {
            if (DrawDebug) DrawProbes();
        }

        private void OnDrawGizmosSelected()
        {
            if (!DrawDebug) DrawProbes();
        }

        private void DrawProbes()
        {
            if (!Application.isPlaying) return;

            for (int i = 0; i < world.Length; i++)
            {
                Gizmos.color = Color.Lerp(new Color(1f, 1f, 1f, 0.3f), new Color(0.2f, 0.6f, 1f, 0.9f), wet[i]);
                Gizmos.DrawWireSphere(world[i], Radius);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(world[i], world[i] + force[i] / Mathf.Max(body.mass * Gravity, 1f));
            }
        }
    }
}

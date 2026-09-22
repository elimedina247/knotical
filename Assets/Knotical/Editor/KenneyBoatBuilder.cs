using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Knotical.Editor
{
    public static class KenneyBoatBuilder
    {
        public const string PrefabPath = "Assets/Knotical/Boat/Boat.prefab";
        public const string ModelPath = "Assets/Knotical/Art/Models/Kenney/ship-large.fbx";
        public const string MaterialPath = "Assets/Knotical/World/KenneyShip.mat";
        public const string ColormapPath = "Assets/Knotical/Art/Models/Kenney/Textures/colormap.png";

        private const string HullMeshPath = "Assets/Knotical/Boat/HullCollider.asset";
        private const float Draft = 0.8f;
        private const float BlockCoefficient = 0.55f;
        private const float PontoonRadius = 0.9f;
        private const float WaterDensity = 1025f;

        private static readonly Vector2[] PontoonLayout =
        {
            new Vector2(0.55f, -0.70f), new Vector2(0.68f, -0.18f), new Vector2(0.68f, 0.30f), new Vector2(0.35f, 0.82f),
        };

        [MenuItem("Knotical/Build Boat Prefab")]
        public static void BuildPrefabMenu() => BuildPrefab();

        public static GameObject BuildPrefab()
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null) throw new InvalidOperationException($"missing {ModelPath}");

            var root = new GameObject("Boat");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                ApplyMaterial(model, LoadOrCreateMaterial());

                MeshFilter hull = HullFilter(model);
                if (BowAtNegativeZ(hull)) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Bounds bounds = ModelBounds(model);
                model.transform.localPosition = new Vector3(0f, -Draft - bounds.min.y, 0f);
                bounds = ModelBounds(model);

                float length = bounds.size.z;
                float width = bounds.size.x;
                float mass = length * width * Draft * BlockCoefficient * WaterDensity;
                Func<float, float> deckAt = DeckSampler(hull, length);
                float mainDeck = deckAt(0f);

                var body = root.AddComponent<Rigidbody>();
                body.mass = mass;
                body.centerOfMass = new Vector3(0f, -0.4f, -0.2f);
                body.linearDamping = 0f;
                body.angularDamping = 0f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;

                BuildHullCollider(root, hull, mainDeck + 0.3f);
                BuildPontoons(root, length, width, mass);
                Transform attach = BuildAttachments(root, length, deckAt);

                var motor = root.AddComponent<BoatMotor>();
                motor.RudderLocalPosition = attach.Find("rudder").localPosition;

                var keel = root.AddComponent<Hull>();
                keel.Draft = Draft;
                keel.BowKeel = new Vector3(0f, -Draft, length * 0.35f);
                keel.SternKeel = new Vector3(0f, -Draft, -length * 0.35f);

                var foam = root.AddComponent<FoamEmitter>();
                foam.Points = 32;
                foam.Size = 1.0f;
                foam.RingRate = 0.6f;
                foam.WakeRate = 14f;
                foam.Life = 3f;
                foam.Strength = 0.6f;
                foam.WakeWidth = 0.7f;
                foam.Stern = attach.Find("stern");

                var rig = root.AddComponent<SailRig>();
                rig.SailForceLocalPosition = new Vector3(0f, bounds.max.y * 0.35f, 0f);
                root.AddComponent<KenneySails>();

                var stand = new GameObject("HelmStand");
                stand.transform.SetParent(root.transform, false);
                stand.transform.localPosition = attach.Find("wheel").localPosition;

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"KenneyBoatBuilder: wrote {PrefabPath} ({length:F1} x {width:F1} m, {mass:F0} kg, main deck y={mainDeck:F2}, aft deck y={deckAt(-length * 0.3f):F2})");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static Material LoadOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            material = new Material(Shader.Find("Knotical/TexturedLit"));
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(ColormapPath));
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        public static Bounds ModelBounds(GameObject model)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 local = model.transform.parent != null
                    ? model.transform.parent.worldToLocalMatrix * filter.transform.localToWorldMatrix
                    : model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 v in filter.sharedMesh.vertices)
                {
                    Vector3 p = local.MultiplyPoint3x4(v);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        public static bool IsCanvas(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.StartsWith("sail") || lower.StartsWith("flag");
        }

        private static MeshFilter HullFilter(GameObject model)
        {
            MeshFilter best = null;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || IsCanvas(filter.name)) continue;
                if (best == null || filter.sharedMesh.vertexCount > best.sharedMesh.vertexCount) best = filter;
            }
            if (best == null) throw new InvalidOperationException("no hull mesh in model");
            return best;
        }

        private static bool BowAtNegativeZ(MeshFilter hull)
        {
            Bounds b = hull.sharedMesh.bounds;
            float cut = b.min.y + b.size.y * 0.25f;
            float aftWidth = 0f, foreWidth = 0f;
            int aft = 0, fore = 0;
            foreach (Vector3 v in hull.sharedMesh.vertices)
            {
                if (v.y > cut) continue;
                if (v.z > b.center.z + b.size.z * 0.3f) { foreWidth += Mathf.Abs(v.x); fore++; }
                else if (v.z < b.center.z - b.size.z * 0.3f) { aftWidth += Mathf.Abs(v.x); aft++; }
            }
            if (aft == 0 || fore == 0) return false;
            bool bowNegative = aftWidth / aft < foreWidth / fore;
            Debug.Log($"KenneyBoatBuilder: mean half width fore {foreWidth / fore:F2} aft {aftWidth / aft:F2}, bow at {(bowNegative ? "-Z, rotating 180" : "+Z")}");
            return bowNegative;
        }

        private static Func<float, float> DeckSampler(MeshFilter hull, float length)
        {
            var probe = hull.gameObject.AddComponent<MeshCollider>();
            Physics.SyncTransforms();

            var samples = new List<(float z, float y)>();
            foreach (float z in new[] { -0.45f, -0.3f, -0.15f, 0f, 0.15f, 0.3f, 0.45f })
            {
                float best = float.MaxValue;
                foreach (float x in new[] { -0.9f, 0f, 0.9f })
                {
                    var ray = new Ray(new Vector3(x, 50f, z * length), Vector3.down);
                    foreach (RaycastHit hit in Physics.RaycastAll(ray, 100f))
                    {
                        if (hit.collider == probe && hit.point.y > 0.2f) best = Mathf.Min(best, hit.point.y);
                    }
                }
                if (best < float.MaxValue) samples.Add((z, best));
            }

            UnityEngine.Object.DestroyImmediate(probe);
            if (samples.Count == 0) throw new InvalidOperationException("no deck found above the waterline");

            return fractionZ =>
            {
                (float z, float y) nearest = samples[0];
                foreach ((float z, float y) s in samples)
                {
                    if (Mathf.Abs(s.z - fractionZ) < Mathf.Abs(nearest.z - fractionZ)) nearest = s;
                }
                return nearest.y;
            };
        }

        private static void ApplyMaterial(GameObject model, Material material)
        {
            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private static void BuildHullCollider(GameObject root, MeshFilter hull, float cutY)
        {
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * hull.transform.localToWorldMatrix;
            var points = new List<Vector3>();
            foreach (Vector3 v in hull.sharedMesh.vertices)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(v);
                if (p.y <= cutY) points.Add(p);
            }

            var mesh = new Mesh { name = "HullCollider" };
            mesh.SetVertices(points);
            var triangles = new List<int>();
            for (int i = 1; i + 1 < points.Count; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Path.GetDirectoryName(HullMeshPath));
            AssetDatabase.CreateAsset(mesh, HullMeshPath);

            var go = new GameObject("HullCollider");
            go.transform.SetParent(root.transform, false);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
        }

        private static void BuildPontoons(GameObject root, float length, float width, float mass)
        {
            int count = PontoonLayout.Length * 2;
            float volume = 4f / 3f * Mathf.PI * PontoonRadius * PontoonRadius * PontoonRadius;

            var buoyancy = root.AddComponent<Buoyancy>();
            buoyancy.Radius = PontoonRadius;
            buoyancy.Coefficient = 2f * mass / (WaterDensity * count * volume);
            buoyancy.DampingFactor1 = 1.0f;
            buoyancy.DampingFactor2 = 0.4f;
            buoyancy.DragCoefficient = 0.05f;
            buoyancy.DragCoefficient2 = 0f;
            buoyancy.MaxDragSpeed = 12f;
            buoyancy.AngularDrag = 1.0f;
            buoyancy.TiltResponse = 0.45f;
            buoyancy.ShortWaveFilter = 1.0f;
            buoyancy.SnapToWaterOnActivation = true;

            var group = new GameObject("Pontoons");
            group.transform.SetParent(root.transform, false);
            for (int i = 0; i < PontoonLayout.Length; i++)
            {
                Vector2 f = PontoonLayout[i];
                foreach (float side in new[] { -1f, 1f })
                {
                    var p = new GameObject($"Pontoon{i}{(side < 0 ? "L" : "R")}");
                    p.transform.SetParent(group.transform, false);
                    p.transform.localPosition = new Vector3(side * f.x * width * 0.5f, 0f, f.y * length * 0.5f);
                    p.AddComponent<Pontoon>();
                }
            }
        }

        private static Transform BuildAttachments(GameObject root, float length, Func<float, float> deckAt)
        {
            float half = length * 0.5f;
            var group = new GameObject("Attach");
            group.transform.SetParent(root.transform, false);

            Add(group, "stern", new Vector3(0f, 0f, -half));
            Add(group, "rudder", new Vector3(0f, -0.6f, -half + 0.6f));
            Add(group, "wheel", new Vector3(0f, deckAt(-0.3f), -half + 2.2f));
            Add(group, "player_spawn", new Vector3(0.9f, deckAt(-0.15f), -half + 3.6f));
            Add(group, "cargo", new Vector3(0f, deckAt(0.15f), 0.8f));
            return group.transform;
        }

        private static void Add(GameObject group, string name, Vector3 localPosition)
        {
            var point = new GameObject(name);
            point.transform.SetParent(group.transform, false);
            point.transform.localPosition = localPosition;
        }
    }
}

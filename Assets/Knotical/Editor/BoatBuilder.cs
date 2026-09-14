using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Knotical.Editor
{
    public static class BoatBuilder
    {
        public const string PrefabPath = "Assets/Knotical/Boat/BoatPlaceholder.prefab";

        private const string ModelPath = "Assets/Knotical/Art/Models/boat_placeholder.fbx";
        private const string SpecPath = "Assets/Knotical/Art/Models/boat_placeholder.json";
        private const string MaterialPath = "Assets/Knotical/Art/Models/BoatPlaceholder.mat";
        private const string ShaderName = "Knotical/VertexColorLit";

        private const float ReferenceMass = 4000f;
        private const float ReferenceCoefficient = 0.103f;

        [Serializable]
        private class Spec
        {
            public float stern_y, bow_y, bow_tip_y, half_beam, draft, main_deck, quarter_deck, qd_y, bulwark, wall;
            public float mast_y, mast_top, helm_y, length, beam;
            public Step[] steps;
            public Wall[] walls;
            public float[] hull_points_flat;
            public float[] deck_outline_flat;
            public float[] quarter_outline_flat;
            public Definition definition;
        }

        [Serializable]
        private class Definition
        {
            public float volume, mass, waterline_length, waterline_beam, pontoon_radius;
            public float[] com;
            public Attach[] pontoons;
            public Attach[] attachments;
        }

        [Serializable] private class Attach { public string name; public float x, y, z, r; }

        [Serializable] private class Step { public float y, z_top, depth; }
        [Serializable] private class Wall { public float x, y, z0, z1, len, yaw; }

        [MenuItem("Knotical/Build Placeholder Boat Prefab")]
        public static void BuildPrefabMenu() => BuildPrefab();

        public static GameObject BuildPrefab()
        {
            Spec spec = JsonUtility.FromJson<Spec>(File.ReadAllText(SpecPath));
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null) throw new InvalidOperationException($"missing {ModelPath}");

            var root = new GameObject("BoatPlaceholder");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                OrientModel(model, spec);
                ApplyMaterial(model);

                Definition def = spec.definition;
                if (def == null || def.pontoons == null || def.pontoons.Length == 0) throw new InvalidOperationException("boat definition missing; rerun tools/blender/build_boat.py");

                var body = root.AddComponent<Rigidbody>();
                body.mass = def.mass;
                body.centerOfMass = ToUnity(def.com[0], def.com[1], def.com[2]);
                body.linearDamping = 0f;
                body.angularDamping = 0f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;

                BuildColliders(root, spec);
                BuildPontoons(root, def);
                Transform attach = BuildAttachments(root, def);

                var motor = root.AddComponent<BoatMotor>();
                var foam = root.AddComponent<FoamEmitter>();
                foam.Points = 32;
                foam.Size = 1.1f;
                foam.RingRate = 0.6f;
                foam.WakeRate = 14f;
                foam.Life = 3f;
                foam.Strength = 0.6f;
                foam.WakeWidth = 0.7f;
                motor.MotorLocalPosition = attach.Find("rudder").localPosition;
                foam.Stern = attach.Find("stern");
                motor.PropellerStrength = def.mass * 2f;
                motor.RudderStrength = def.mass * 1.5f;

                var stand = new GameObject("HelmStand");
                stand.transform.SetParent(root.transform, false);
                stand.transform.localPosition = new Vector3(0f, spec.quarter_deck, spec.helm_y);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"BoatBuilder: wrote {PrefabPath} ({spec.length} x {spec.beam} m, {def.mass} kg from {def.volume} m3, {def.pontoons.Length} pontoons r={def.pontoon_radius})");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void OrientModel(GameObject model, Spec spec)
        {
            Bounds bounds = ModelBounds(model);
            float bowReach = Mathf.Abs(spec.bow_tip_y);
            float sternReach = Mathf.Abs(spec.stern_y) + 1f;
            bool bowAtPositiveZ = bounds.max.z > sternReach && bounds.max.z > Mathf.Abs(bounds.min.z);
            bool bowAtNegativeZ = -bounds.min.z > sternReach && -bounds.min.z >= bounds.max.z;

            if (bowAtNegativeZ && !bowAtPositiveZ)
            {
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Debug.Log($"BoatBuilder: model bow imported at -Z, rotated 180. bounds {bounds.min} .. {bounds.max}");
            }
            else
            {
                Debug.Log($"BoatBuilder: model bow at +Z. bounds {bounds.min} .. {bounds.max} (expected bow reach {bowReach})");
            }
        }

        private static Bounds ModelBounds(GameObject model)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                Matrix4x4 local = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3 min = local.MultiplyPoint3x4(b.min);
                Vector3 max = local.MultiplyPoint3x4(b.max);
                var world = new Bounds(min, Vector3.zero);
                world.Encapsulate(max);
                if (first) { bounds = world; first = false; }
                else bounds.Encapsulate(world);
            }
            return bounds;
        }

        private static void ApplyMaterial(GameObject model)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader == null) throw new InvalidOperationException($"Shader {ShaderName} not found");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private static Vector3 ToUnity(float x, float yAlong, float zUp) => new Vector3(x, zUp, yAlong);

        private static void BuildColliders(GameObject root, Spec spec)
        {
            var colliders = new GameObject("Colliders");
            colliders.transform.SetParent(root.transform, false);

            AddConvex(colliders, "Hull", FlatToPoints(spec.hull_points_flat, 3));
            AddConvex(colliders, "MainDeck", DeckSlab(spec.deck_outline_flat, spec.main_deck));
            AddConvex(colliders, "QuarterDeck", DeckSlab(spec.quarter_outline_flat, spec.quarter_deck));

            float rampFront = spec.steps[0].y + spec.steps[0].depth * 0.5f;
            AddConvex(colliders, "Ramp", new List<Vector3>
            {
                ToUnity(-0.7f, rampFront, spec.main_deck), ToUnity(0.7f, rampFront, spec.main_deck),
                ToUnity(-0.7f, spec.qd_y, spec.quarter_deck), ToUnity(0.7f, spec.qd_y, spec.quarter_deck),
                ToUnity(-0.7f, spec.qd_y, spec.main_deck), ToUnity(0.7f, spec.qd_y, spec.main_deck),
            });


            AddBox(colliders, "Mast", ToUnity(0f, spec.mast_y, (spec.main_deck + spec.mast_top) * 0.5f),
                new Vector3(0.34f, spec.mast_top - spec.main_deck, 0.34f));

            AddBox(colliders, "HelmStand", ToUnity(0f, spec.helm_y, spec.quarter_deck + 0.45f),
                new Vector3(0.22f, 0.9f, 0.18f));

            for (int i = 0; i < spec.walls.Length; i++)
            {
                Wall w = spec.walls[i];
                var box = AddBox(colliders, $"Wall{i}", ToUnity(w.x, w.y, (w.z0 + w.z1) * 0.5f),
                    new Vector3(spec.wall, w.z1 - w.z0, w.len));
                box.transform.localRotation = Quaternion.Euler(0f, w.yaw * Mathf.Rad2Deg, 0f);
            }
        }

        private static List<Vector3> FlatToPoints(float[] flat, int stride)
        {
            var points = new List<Vector3>(flat.Length / stride);
            for (int i = 0; i + 2 < flat.Length; i += stride)
            {
                points.Add(ToUnity(flat[i], flat[i + 1], flat[i + 2]));
            }
            return points;
        }

        private static List<Vector3> DeckSlab(float[] outlineFlat, float deckZ)
        {
            var points = new List<Vector3>();
            for (int i = 0; i + 1 < outlineFlat.Length; i += 2)
            {
                float x = Mathf.Abs(outlineFlat[i]);
                float y = outlineFlat[i + 1];
                foreach (float side in new[] { -1f, 1f })
                {
                    points.Add(ToUnity(side * x, y, deckZ));
                    points.Add(ToUnity(side * x, y, deckZ - 0.25f));
                }
            }
            return points;
        }

        private static void AddConvex(GameObject parent, string name, List<Vector3> points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);

            var mesh = new Mesh { name = name + "Hull" };
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

            string meshPath = $"Assets/Knotical/Boat/Colliders/{name}.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath));
            AssetDatabase.CreateAsset(mesh, meshPath);

            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
        }

        private static GameObject AddBox(GameObject parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = centre;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            return go;
        }

        private static Transform BuildAttachments(GameObject root, Definition def)
        {
            var group = new GameObject("Attach");
            group.transform.SetParent(root.transform, false);
            foreach (Attach a in def.attachments)
            {
                var point = new GameObject(a.name);
                point.transform.SetParent(group.transform, false);
                point.transform.localPosition = ToUnity(a.x, a.y, a.z);
            }
            return group.transform;
        }

        private static void BuildPontoons(GameObject root, Definition def)
        {
            var buoyancy = root.AddComponent<Buoyancy>();
            buoyancy.Radius = def.pontoon_radius;
            buoyancy.Coefficient = ReferenceCoefficient * def.mass / ReferenceMass;
            buoyancy.DampingFactor1 = 1.0f;
            buoyancy.DampingFactor2 = 0.4f;
            buoyancy.DragCoefficient = 0.35f;
            buoyancy.DragCoefficient2 = 0.4f;
            buoyancy.MaxDragSpeed = 12f;
            buoyancy.AngularDrag = 1.0f;
            buoyancy.TiltResponse = 0.45f;
            buoyancy.ShortWaveFilter = 1.0f;
            buoyancy.SnapToWaterOnActivation = true;
            buoyancy.DrawDebug = true;

            var group = new GameObject("Pontoons");
            group.transform.SetParent(root.transform, false);
            foreach (Attach a in def.pontoons)
            {
                var p = new GameObject(a.name);
                p.transform.SetParent(group.transform, false);
                p.transform.localPosition = ToUnity(a.x, a.y, a.z);
                p.AddComponent<Pontoon>();
            }
        }
    }
}

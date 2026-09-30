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
        public const string ModelPath = "Assets/Knotical/Art/Models/Ship/ship.fbx";
        public const string MaterialPath = "Assets/Knotical/World/KenneyShip.mat";
        public const string ColormapPath = "Assets/Knotical/Art/Models/Kenney/Textures/colormap.png";

        private const string HullMeshPath = "Assets/Knotical/Boat/HullCollider.asset";
        public const float ModelScale = 2f;
        private const float Draft = 0.8f * ModelScale;
        private const float BlockCoefficient = 0.55f;
        private const float PontoonRadius = 0.9f * ModelScale;
        private const float WaterDensity = 1025f;
        private const float WallThickness = 0.12f * ModelScale;
        private const float RampThickness = 0.25f * ModelScale;
        private const float RailBin = 0.55f * ModelScale;

        private static readonly Vector2[] PontoonLayout =
        {
            new Vector2(0.55f, -0.70f), new Vector2(0.68f, -0.18f), new Vector2(0.68f, 0.30f), new Vector2(0.35f, 0.82f),
        };

        private static readonly string[] BoxParts =
        {
            "Forecastle", "ForecastleRail", "MastBase_Main", "MastBase_Mizzen", "BowspritBase", "Balcony_Floor", "CrowsNest_Floor",
            "SideStep_L", "SideStep_R", "Ladder_L", "Ladder_R",
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
                model.transform.localScale = Vector3.one * ModelScale;
                ApplyMaterial(model, LoadOrCreateMaterial());

                MeshFilter hull = HullFilter(model);
                if (BowAtNegativeZ(hull)) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Bounds bounds = ModelBounds(model);
                model.transform.localPosition = new Vector3(0f, -Draft - bounds.min.y, 0f);
                bounds = ModelBounds(model);

                float length = bounds.size.z;
                float width = bounds.size.x;
                float mass = length * width * Draft * BlockCoefficient * WaterDensity;
                float mainDeck = Marker(root, model, "Deck_Main").y;
                float aftDeck = Marker(root, model, "Deck_Quarter").y;

                var body = root.AddComponent<Rigidbody>();
                body.mass = mass;
                body.centerOfMass = new Vector3(0f, -0.4f, -0.2f) * ModelScale;
                body.linearDamping = 0f;
                body.angularDamping = 0f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;

                BuildHullCollider(root, hull, mainDeck + 0.02f * ModelScale);
                BuildPontoons(root, length, width, mass);
                Transform attach = BuildAttachments(root, model);
                int deckColliders = BuildDeckColliders(root, model);

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

                var helm = root.AddComponent<Helm>();
                helm.Wheel = FindChild(model, "Wheel");
                helm.Rudder = FindChild(model, "Rudder");
                BuildWheelGrip(helm.Wheel);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                MeshCollider check = prefab.GetComponentInChildren<MeshCollider>(true);
                if (check == null || check.sharedMesh == null) throw new InvalidOperationException("prefab hull collider lost its mesh");
                Debug.Log($"KenneyBoatBuilder: wrote {PrefabPath} ({length:F1} x {width:F1} m, {mass:F0} kg, main deck y={mainDeck:F2}, aft deck y={aftDeck:F2}, {deckColliders} deck colliders)");
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
                if (filter.name.Equals("Hull", StringComparison.OrdinalIgnoreCase)) return filter;
                if (best == null || filter.sharedMesh.vertexCount > best.sharedMesh.vertexCount) best = filter;
            }
            if (best == null) throw new InvalidOperationException("no hull mesh in model");
            return best;
        }

        private static Transform FindChild(GameObject model, string name)
        {
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            throw new InvalidOperationException($"model has no child named {name}");
        }

        private static Vector3 Marker(GameObject root, GameObject model, string name)
        {
            return root.transform.InverseTransformPoint(FindChild(model, name).position);
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
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(HullMeshPath, ImportAssetOptions.ForceUpdate);
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(HullMeshPath);
            if (mesh == null) throw new InvalidOperationException($"failed to reload {HullMeshPath}");

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

        private static Transform BuildAttachments(GameObject root, GameObject model)
        {
            var group = new GameObject("Attach");
            group.transform.SetParent(root.transform, false);

            Add(group, "stern", Marker(root, model, "Attach_Stern"));
            Add(group, "rudder", Marker(root, model, "Attach_Rudder"));
            Add(group, "wheel", Marker(root, model, "Attach_Wheel"));
            Add(group, "player_spawn", Marker(root, model, "Attach_PlayerSpawn"));
            Add(group, "cargo", Marker(root, model, "Attach_Cargo"));
            Add(group, "map_table", Marker(root, model, "Attach_MapTable"));
            return group.transform;
        }

        private static void Add(GameObject group, string name, Vector3 localPosition)
        {
            var point = new GameObject(name);
            point.transform.SetParent(group.transform, false);
            point.transform.localPosition = localPosition;
        }

        private static int BuildDeckColliders(GameObject root, GameObject model)
        {
            var group = new GameObject("Colliders");
            group.transform.SetParent(root.transform, false);

            Bounds? door = null;
            float? stairsTop = null;
            float? aftDoorHalf = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Door_Aft") aftDoorHalf = t.lossyScale.x;
            }
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                if (filter.name == "DoorFrame_Front") door = BoundsOf(LocalPoints(root, filter));
                if (filter.name == "CabinStairs")
                {
                    List<Vector3> dense = DenseAlongZ(LocalPoints(root, filter), 0.15f * ModelScale);
                    if (dense.Count >= 4) stairsTop = BoundsOf(dense).min.z;
                }
            }

            int count = 0;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                string name = filter.name;
                List<Vector3> points = LocalPoints(root, filter);

                if (name.StartsWith("Bulwark") || name.StartsWith("Rail_")) count += RingBoxes(group, name, EdgePoints(root, filter, 0.1f * ModelScale), RailBin);
                else if (name == "WheelPedestal") count += RingBoxes(group, name, EdgePoints(root, filter, 0.05f * ModelScale), 0.2f * ModelScale);
                else if (name.Contains("Stairs")) count += Ramps(group, name, points);
                else if (name == "Cabin") count += CabinWalls(group, points, door, stairsTop, aftDoorHalf);
                else if (name.StartsWith("Mast_")) count += MastCapsule(group, name, points);
                else if (Array.IndexOf(BoxParts, name) >= 0) count += BoxFromBounds(group, name, BoundsOf(points));
            }

            BuildLadders(group);
            return count;
        }

        private static void BuildLadders(GameObject group)
        {
            var steps = new List<BoxCollider>();
            foreach (Transform child in group.transform)
            {
                if (!child.name.StartsWith("Col_SideStep")) continue;
                var step = child.GetComponent<BoxCollider>();
                if (step != null) steps.Add(step);
            }

            foreach (Transform child in group.transform)
            {
                if (!child.name.StartsWith("Col_Ladder")) continue;
                var box = child.GetComponent<BoxCollider>();
                if (box == null) continue;

                bool alongX = box.size.x <= box.size.z;
                Vector3 at = child.localPosition;
                float sign = alongX ? Mathf.Sign(at.x) : Mathf.Sign(at.z);
                if (sign == 0f) sign = 1f;

                var ladder = child.gameObject.AddComponent<Ladder>();
                ladder.Face = alongX ? new Vector3(sign, 0f, 0f) : new Vector3(0f, 0f, sign);

                float rim = at.y + box.size.y * 0.5f;
                foreach (BoxCollider step in steps)
                {
                    Vector3 stepAt = step.transform.localPosition;
                    if (Mathf.Sign(stepAt.x) != Mathf.Sign(at.x) || Mathf.Abs(stepAt.z - at.z) > box.size.z) continue;
                    ladder.TopStep = Mathf.Max(stepAt.y + step.size.y * 0.5f - rim, 0.2f);
                }
            }
        }

        private static void BuildWheelGrip(Transform wheel)
        {
            var filter = wheel.GetComponent<MeshFilter>();
            var box = wheel.gameObject.AddComponent<BoxCollider>();
            if (filter != null && filter.sharedMesh != null)
            {
                Bounds b = filter.sharedMesh.bounds;
                box.center = b.center;
                box.size = Vector3.Max(b.size, Vector3.one * 0.12f);
            }
            wheel.gameObject.AddComponent<WheelGrip>();
        }

        private static List<Vector3> LocalPoints(GameObject root, MeshFilter filter)
        {
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var points = new List<Vector3>(filter.sharedMesh.vertexCount);
            foreach (Vector3 v in filter.sharedMesh.vertices) points.Add(toRoot.MultiplyPoint3x4(v));
            return points;
        }

        private static List<Vector3> EdgePoints(GameObject root, MeshFilter filter, float step)
        {
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Vector3[] vertices = filter.sharedMesh.vertices;
            int[] triangles = filter.sharedMesh.triangles;
            var points = new List<Vector3>();
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    Vector3 a = toRoot.MultiplyPoint3x4(vertices[triangles[i + e]]);
                    Vector3 b = toRoot.MultiplyPoint3x4(vertices[triangles[i + (e + 1) % 3]]);
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / step));
                    for (int k = 0; k <= n; k++) points.Add(Vector3.Lerp(a, b, (float)k / n));
                }
            }
            return points;
        }

        private static Bounds BoundsOf(List<Vector3> points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 p in points) bounds.Encapsulate(p);
            return bounds;
        }

        private static int BoxFromBounds(GameObject group, string name, Bounds bounds)
        {
            var go = new GameObject("Col_" + name);
            go.transform.SetParent(group.transform, false);
            go.transform.localPosition = bounds.center;
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.Max(bounds.size, Vector3.one * (0.05f * ModelScale));
            return 1;
        }

        private static int RingBoxes(GameObject group, string name, List<Vector3> points, float binSize)
        {
            Bounds all = BoundsOf(points);
            int bins = Mathf.Max(1, Mathf.CeilToInt(all.size.z / binSize));
            float width = all.size.z / bins;
            int made = 0;

            for (int bin = 0; bin < bins; bin++)
            {
                float z0 = all.min.z + bin * width;
                float z1 = z0 + width;
                foreach (float side in new[] { -1f, 1f })
                {
                    var part = new List<Vector3>();
                    foreach (Vector3 p in points)
                    {
                        if (p.z < z0 - 1e-4f || p.z > z1 + 1e-4f) continue;
                        if (side < 0f ? p.x > 0f : p.x < 0f) continue;
                        part.Add(p);
                    }
                    if (part.Count < 2) continue;
                    made += FittedBox(group, $"{name}_{bin}{(side < 0 ? "L" : "R")}", part);
                }
            }
            return made;
        }

        private static int FittedBox(GameObject group, string name, List<Vector3> part, int depth = 0)
        {
            Bounds b = BoundsOf(part);
            if (b.size.y < 0.05f * ModelScale) return 0;

            Vector3 forward = RunDirection(part, b, Vector3.forward);
            Vector3 alternative = RunDirection(part, b, Vector3.right);
            float thickness = Thickness(part, b, forward);
            float otherThickness = Thickness(part, b, alternative);
            if (otherThickness < thickness) { forward = alternative; thickness = otherThickness; }

            if (thickness > 0.35f * ModelScale && depth < 2 && part.Count >= 4)
            {
                int made = 0;
                int index = 0;
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        var quadrant = new List<Vector3>();
                        foreach (Vector3 p in part)
                        {
                            if ((sx < 0f ? p.x > b.center.x : p.x < b.center.x) || (sz < 0f ? p.z > b.center.z : p.z < b.center.z)) continue;
                            quadrant.Add(p);
                        }
                        if (quadrant.Count >= 2) made += FittedBox(group, $"{name}_{index}", quadrant, depth + 1);
                        index++;
                    }
                }
                if (made > 0) return made;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float minF = float.MaxValue, maxF = float.MinValue, minR = float.MaxValue, maxR = float.MinValue;
            foreach (Vector3 p in part)
            {
                Vector3 q = p - b.center;
                float f = Vector3.Dot(q, forward);
                float r = Vector3.Dot(q, right);
                minF = Mathf.Min(minF, f); maxF = Mathf.Max(maxF, f);
                minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r);
            }

            var go = new GameObject("Col_" + name);
            go.transform.SetParent(group.transform, false);
            go.transform.localPosition = b.center + forward * ((minF + maxF) * 0.5f) + right * ((minR + maxR) * 0.5f);
            go.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.Max(new Vector3(maxR - minR, b.size.y, maxF - minF), new Vector3(0.1f, 0.05f, 0.1f) * ModelScale);
            return 1;
        }

        private static Vector3 RunDirection(List<Vector3> part, Bounds b, Vector3 axis)
        {
            float span = Vector3.Dot(b.size, axis);
            float edge = Mathf.Max(span * 0.2f, 0.02f * ModelScale);
            float low = Vector3.Dot(b.min, axis);
            float high = Vector3.Dot(b.max, axis);
            Vector3 near = Vector3.zero, far = Vector3.zero;
            int nearCount = 0, farCount = 0;
            foreach (Vector3 p in part)
            {
                float a = Vector3.Dot(p, axis);
                if (a <= low + edge) { near += p; nearCount++; }
                if (a >= high - edge) { far += p; farCount++; }
            }
            if (nearCount == 0 || farCount == 0) return axis;
            Vector3 d = far / farCount - near / nearCount;
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : axis;
        }

        private static float Thickness(List<Vector3> part, Bounds b, Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float minR = float.MaxValue, maxR = float.MinValue;
            foreach (Vector3 p in part)
            {
                float r = Vector3.Dot(p - b.center, right);
                minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r);
            }
            return maxR - minR;
        }
        private static int Ramps(GameObject group, string name, List<Vector3> points)
        {
            var left = new List<Vector3>();
            var right = new List<Vector3>();
            var middle = new List<Vector3>();
            foreach (Vector3 p in points)
            {
                if (p.x < -0.3f * ModelScale) left.Add(p);
                else if (p.x > 0.3f * ModelScale) right.Add(p);
                else middle.Add(p);
            }

            bool split = left.Count >= 4 && right.Count >= 4 && middle.Count < points.Count / 10;
            if (!split) return Ramp(group, name, points);
            return Ramp(group, name + "_L", left) + Ramp(group, name + "_R", right);
        }

        private static int Ramp(GameObject group, string name, List<Vector3> points)
        {
            List<Vector3> dense = DenseAlongZ(points, 0.15f * ModelScale);
            if (dense.Count < 4) return 0;
            Bounds b = BoundsOf(dense);
            if (b.size.z < 0.05f * ModelScale || b.size.y < 0.05f * ModelScale) return BoxFromBounds(group, name, b);

            float yAtMin = MaxYNear(dense, b.min.z, 0.25f * ModelScale);
            float yAtMax = MaxYNear(dense, b.max.z, 0.25f * ModelScale);
            bool risesTowardMax = yAtMax >= yAtMin;
            float x = b.center.x;
            float zLow = risesTowardMax ? b.min.z : b.max.z;
            float zFar = risesTowardMax ? b.max.z : b.min.z;
            float yTop = b.max.y;
            float zTop = zFar;
            foreach (Vector3 p in dense)
            {
                if (p.y < yTop - 0.03f * ModelScale || Mathf.Abs(p.z - zFar) > 0.5f * ModelScale) continue;
                zTop = risesTowardMax ? Mathf.Min(zTop, p.z) : Mathf.Max(zTop, p.z);
            }

            int made = 0;
            if (Mathf.Abs(zFar - zTop) > 0.02f * ModelScale)
            {
                made += BoxFromBounds(group, name + "_Top", FromTo(b.min.x, yTop - RampThickness, Mathf.Min(zTop, zFar), b.max.x, yTop, Mathf.Max(zTop, zFar)));
            }

            float run = Mathf.Abs(zTop - zLow) + Mathf.Abs(zFar - zTop);
            Vector3 low = new Vector3(x, b.min.y, risesTowardMax ? zTop - run : zTop + run);
            Vector3 high = new Vector3(x, yTop, zTop);
            Vector3 along = high - low;
            float length = along.magnitude;
            if (length < 0.05f * ModelScale) return made;
            Vector3 direction = along / length;
            Vector3 up = new Vector3(0f, direction.z, -direction.y);
            if (up.y < 0f) up = -up;
            up.Normalize();

            var go = new GameObject("Col_" + name);
            go.transform.SetParent(group.transform, false);
            go.transform.localPosition = (low + high) * 0.5f - up * (RampThickness * 0.5f);
            go.transform.localRotation = Quaternion.LookRotation(direction, up);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(b.size.x, 0.1f * ModelScale), RampThickness, length);
            return made + 1;
        }
        private static List<Vector3> DenseAlongZ(List<Vector3> points, float bin)
        {
            Bounds b = BoundsOf(points);
            int bins = Mathf.Max(1, Mathf.CeilToInt(b.size.z / bin));
            var counts = new int[bins];
            foreach (Vector3 p in points) counts[Mathf.Min((int)((p.z - b.min.z) / bin), bins - 1)]++;
            var dense = new List<Vector3>();
            foreach (Vector3 p in points)
            {
                if (counts[Mathf.Min((int)((p.z - b.min.z) / bin), bins - 1)] >= 2) dense.Add(p);
            }
            return dense;
        }

        private static float MaxYNear(List<Vector3> points, float z, float tolerance)
        {
            float best = float.MinValue;
            foreach (Vector3 p in points)
            {
                if (Mathf.Abs(p.z - z) <= tolerance) best = Mathf.Max(best, p.y);
            }
            return best == float.MinValue ? 0f : best;
        }

        private static int CabinWalls(GameObject group, List<Vector3> points, Bounds? door, float? stairsTop, float? aftDoorHalf)
        {
            Bounds b = BoundsOf(points);
            float t = WallThickness;
            float roof = 0.15f * ModelScale;
            float floor = b.min.y;
            float top = b.max.y - roof;
            float split = stairsTop ?? b.min.z;
            int made = 0;

            float frontMin = b.min.x;
            float frontMax = b.max.x;
            bool narrow = false;
            foreach (Vector3 p in points)
            {
                if (p.z < b.max.z - 0.25f * ModelScale || p.y < floor + 0.3f * ModelScale) continue;
                if (!narrow) { frontMin = p.x; frontMax = p.x; narrow = true; }
                frontMin = Mathf.Min(frontMin, p.x);
                frontMax = Mathf.Max(frontMax, p.x);
            }
            bool stepped = stairsTop.HasValue && frontMax - frontMin < b.size.x - 0.5f * ModelScale;
            if (!stepped) { frontMin = b.min.x; frontMax = b.max.x; split = b.max.z; }

            made += BoxFromBounds(group, "Cabin_Roof", FromTo(b.min.x, top, b.min.z, b.max.x, b.max.y, split));
            if (stepped) made += BoxFromBounds(group, "Cabin_RoofFore", FromTo(frontMin, top, split, frontMax, b.max.y, b.max.z));
            made += BoxFromBounds(group, "Cabin_WallL", FromTo(b.min.x, floor, b.min.z, b.min.x + t, top, b.max.z));
            made += BoxFromBounds(group, "Cabin_WallR", FromTo(b.max.x - t, floor, b.min.z, b.max.x, top, b.max.z));
            if (stepped)
            {
                made += BoxFromBounds(group, "Cabin_WallInnerL", FromTo(frontMin, floor, split, frontMin + t, top, b.max.z));
                made += BoxFromBounds(group, "Cabin_WallInnerR", FromTo(frontMax - t, floor, split, frontMax, top, b.max.z));
            }

            float backFloor = b.max.z;
            float slantTop = top;
            foreach (Vector3 p in points)
            {
                if (p.y <= floor + 0.05f * ModelScale) backFloor = Mathf.Min(backFloor, p.z);
                if (p.z <= b.min.z + 0.01f * ModelScale) slantTop = Mathf.Min(slantTop, p.y);
            }
            float aftMin = aftDoorHalf.HasValue ? -aftDoorHalf.Value - 0.02f * ModelScale : 0f;
            float aftMax = aftDoorHalf.HasValue ? aftDoorHalf.Value + 0.02f * ModelScale : 0f;
            var lowFrom = new Vector3(0f, floor, backFloor);
            var lowTo = new Vector3(0f, slantTop, b.min.z);
            if (aftDoorHalf.HasValue)
            {
                made += SlantedWall(group, "Cabin_WallBackLowL", b.min.x, aftMin, lowFrom, lowTo, t);
                made += SlantedWall(group, "Cabin_WallBackLowR", aftMax, b.max.x, lowFrom, lowTo, t);
                made += BoxFromBounds(group, "Cabin_WallBackHighL", FromTo(b.min.x, slantTop, b.min.z, aftMin, top, b.min.z + t));
                made += BoxFromBounds(group, "Cabin_WallBackHighR", FromTo(aftMax, slantTop, b.min.z, b.max.x, top, b.min.z + t));
            }
            else
            {
                made += SlantedWall(group, "Cabin_WallBackLow", b.min.x, b.max.x, lowFrom, lowTo, t);
                made += BoxFromBounds(group, "Cabin_WallBackHigh", FromTo(b.min.x, slantTop, b.min.z, b.max.x, top, b.min.z + t));
            }

            float doorMin = door.HasValue ? door.Value.min.x - 0.02f * ModelScale : -0.48f * ModelScale;
            float doorMax = door.HasValue ? door.Value.max.x + 0.02f * ModelScale : 0.48f * ModelScale;
            made += BoxFromBounds(group, "Cabin_WallFrontL", FromTo(frontMin, floor, b.max.z - t, doorMin, top, b.max.z));
            made += BoxFromBounds(group, "Cabin_WallFrontR", FromTo(doorMax, floor, b.max.z - t, frontMax, top, b.max.z));
            return made;
        }

        private static int SlantedWall(GameObject group, string name, float x0, float x1, Vector3 from, Vector3 to, float thickness)
        {
            if (x1 - x0 < 0.02f * ModelScale) return 0;
            Vector3 along = to - from;
            float length = along.magnitude;
            if (length < 0.02f * ModelScale) return 0;
            Vector3 direction = along / length;
            Vector3 normal = new Vector3(0f, -direction.z, direction.y);
            if (normal.z < 0f) normal = -normal;
            normal.Normalize();

            var go = new GameObject("Col_" + name);
            go.transform.SetParent(group.transform, false);
            Vector3 middle = (from + to) * 0.5f;
            middle.x = (x0 + x1) * 0.5f;
            go.transform.localPosition = middle + normal * (thickness * 0.5f);
            go.transform.localRotation = Quaternion.LookRotation(direction, normal);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(x1 - x0, thickness, length);
            return 1;
        }
        private static Bounds FromTo(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            var bounds = new Bounds(new Vector3(x0, y0, z0), Vector3.zero);
            bounds.Encapsulate(new Vector3(x1, y1, z1));
            return bounds;
        }

        private static int MastCapsule(GameObject group, string name, List<Vector3> points)
        {
            Bounds b = BoundsOf(points);
            var go = new GameObject("Col_" + name);
            go.transform.SetParent(group.transform, false);
            go.transform.localPosition = b.center;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.height = b.size.y;
            capsule.radius = Mathf.Max(b.size.x, b.size.z) * 0.5f;
            return 1;
        }
    }
}

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Knotical.Editor
{
    public static class PlayerBuilder
    {
        public const string PrefabPath = "Assets/Knotical/Player/Player.prefab";
        public const string CrewPath = "Assets/Knotical/Crew/Pill.prefab";
        private const string MaterialPath = "Assets/Knotical/Player/Frictionless.asset";
        private const float Mass = 70f;

        [MenuItem("Knotical/Build Player Prefab")]
        public static void BuildPrefabMenu() => BuildPrefab();

        public static GameObject BuildPrefab()
        {
            var crewAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CrewPath);
            if (crewAsset == null) throw new InvalidOperationException($"missing {CrewPath}");

            var root = new GameObject("Player");
            try
            {
                var crew = (GameObject)PrefabUtility.InstantiatePrefab(crewAsset);
                crew.name = "Crew";
                crew.transform.SetParent(root.transform, false);

                Bounds torso = TorsoBounds(root, crew);
                float height = torso.max.y;
                float radius = Mathf.Max(torso.extents.x, torso.extents.z);

                var body = root.AddComponent<Rigidbody>();
                body.mass = Mass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = radius;
                capsule.height = height;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);
                capsule.material = LoadOrCreateFrictionless();

                var player = root.AddComponent<PlayerBody>();
                var grab = root.AddComponent<PlayerGrab>();
                root.AddComponent<Gait>();
                player.CrewRoot = crew.transform;
                player.Grab = grab;
                player.EyeOffset = CrewRig.EyePointLocal(root.transform);

                foreach (string side in new[] { "L", "R" })
                {
                    Transform shoulder = CrewRig.FindBySuffix(crew.transform, "_Shoulder_" + side);
                    if (shoulder == null) throw new InvalidOperationException($"crew has no shoulder {side}");
                    var arm = shoulder.gameObject.AddComponent<DangleArm>();
                    arm.RootShift = new Vector3(0f, -0.22f, 0.12f);
                    if (side == "L") grab.ArmLeft = arm;
                    else grab.ArmRight = arm;
                }

                var pivot = new GameObject("CameraPivot");
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = player.EyeOffset;

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(pivot.transform, false);
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 20000f;
                camera.fieldOfView = 70f;
                cameraObject.AddComponent<AudioListener>();
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

                player.CameraPivot = pivot.transform;
                player.View = camera;
                grab.Pivot = pivot.transform;

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"PlayerBuilder: wrote {PrefabPath} (capsule {height:F2} m tall, radius {radius:F2}, eye at {player.EyeOffset})");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Bounds TorsoBounds(GameObject root, GameObject crew)
        {
            Transform body = CrewRig.FindBySuffix(crew.transform, "_Body");
            var filter = body != null ? body.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) return new Bounds(new Vector3(0f, 0.58f, 0f), new Vector3(0.48f, 1.16f, 0.48f));

            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (Vector3 v in filter.sharedMesh.vertices)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(v);
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                else bounds.Encapsulate(p);
            }
            return bounds;
        }

        private static PhysicsMaterial LoadOrCreateFrictionless()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialPath);
            if (material != null) return material;

            material = new PhysicsMaterial("Frictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}

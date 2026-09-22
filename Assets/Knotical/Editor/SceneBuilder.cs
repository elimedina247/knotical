using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Knotical.Editor
{
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Knotical/Scenes/Main.unity";
        private const string OceanSettingsPath = "Assets/Knotical/Ocean/OceanSettings.asset";
        private const string VolumePath = "Assets/Knotical/World/MainVolume.asset";
        private const string CrateMaterialPath = "Assets/Knotical/World/Crate.mat";
        private const string SkyMaterialPath = "Assets/Knotical/World/Sky.mat";
        private const string StandInMaterialPath = "Assets/Knotical/World/StandIn.mat";
        private const string FillerModelFolder = "Assets/Knotical/Art/Models/Kenney/";
        private const string WindSettingsPath = "Assets/Knotical/Wind/WindSettings.asset";
        private const string MaterialPath = "Assets/Knotical/Shaders/OceanSurface.mat";
        private const string ProbePath = "Assets/Knotical/Shaders/OceanProbe.compute";
        private const string ShaderName = "Knotical/OceanSurface";
        private const string FlipbookPath = "Assets/Knotical/Art/Textures/fft_water_normals_8x8.png";
        private const string LevelSettingsPath = "Assets/Knotical/Level/LevelSettings.asset";
        private const string TerrainMaterialPath = "Assets/Knotical/World/Terrain.mat";
        private const string CliffMaterialPath = "Assets/Knotical/World/Cliff.mat";
        private const string CloudMaterialPath = "Assets/Knotical/World/Cloud.mat";
        private const string DayNightSettingsPath = "Assets/Knotical/World/DayNightSettings.asset";

        [MenuItem("Knotical/Build Main Scene")]
        public static void BuildMain()
        {
            OceanSettings oceanSettings = LoadOrCreate<OceanSettings>(OceanSettingsPath);
            WindSettings windSettings = LoadOrCreate<WindSettings>(WindSettingsPath);
            Material material = LoadOrCreateMaterial();
            var flipbook = AssetDatabase.LoadAssetAtPath<Texture2D>(FlipbookPath);
            material.SetTexture("_DetailFlipbook", flipbook);
            material.SetFloat("_DetailEnabled", flipbook != null ? 1f : 0f);
            EditorUtility.SetDirty(material);
            var probe = AssetDatabase.LoadAssetAtPath<ComputeShader>(ProbePath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var clock = new GameObject("WorldClock").AddComponent<WorldClock>();

            var surfaceObject = new GameObject("OceanSurface");
            surfaceObject.AddComponent<MeshFilter>();
            var renderer = surfaceObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var surface = surfaceObject.AddComponent<OceanSurface>();
            SetReference(surface, "probeShader", probe);
            surfaceObject.AddComponent<PlanarReflection>();

            GameObject boatPrefab = KenneyBoatBuilder.BuildPrefab();
            var boat = (GameObject)PrefabUtility.InstantiatePrefab(boatPrefab);
            boat.name = "Boat";
            boat.transform.position = new Vector3(0f, 1f, 0f);
            PlaceStandIn(boat, "player_spawn");
            PlaceStandIn(boat, "cargo");

            new GameObject("FoamCapture").AddComponent<FoamCapture>();
            BuildLevel();
            BuildClouds();
            BuildCrates();
            BuildFillerShips();
            new GameObject("Hud").AddComponent<Hud>();
            new GameObject("Map").AddComponent<MapView>();

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 8f, -25f);
                camera.transform.LookAt(new Vector3(0f, 0f, 30f));
                camera.farClipPlane = 20000f;
                UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                var chase = camera.gameObject.AddComponent<ChaseCamera>();
                chase.Target = boat.transform;
            }

            var light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(16f, 160f, 0f);
                RenderSettings.sun = light;
                BuildDayNight(light);
            }

            RenderSettings.skybox = LoadOrCreateSky();
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0005f;
            RenderSettings.fogColor = new Color(0.74f, 0.85f, 0.94f);

            var volume = new GameObject("Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = BuildVolumeProfile();

            SetReference(clock, "ocean", AssetDatabase.LoadAssetAtPath<OceanSettings>(OceanSettingsPath));
            SetReference(clock, "wind", AssetDatabase.LoadAssetAtPath<WindSettings>(WindSettingsPath));
            if (new SerializedObject(clock).FindProperty("ocean").objectReferenceValue == null)
            {
                Debug.LogError("SceneBuilder: WorldClock ocean reference did not stick");
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"SceneBuilder: wrote {ScenePath}");
        }

        internal static LevelGenerator BuildLevel()
        {
            LevelSettings settings = LoadOrCreate<LevelSettings>(LevelSettingsPath);
            Material terrain = LoadOrCreateTinted(TerrainMaterialPath, Color.white);
            Material cliff = LoadOrCreateCliff();

            var generator = new GameObject("Level").AddComponent<LevelGenerator>();
            SetReference(generator, "settings", settings);
            SetReference(generator, "terrainMaterial", terrain);
            SetReference(generator, "blockMaterial", cliff);
            return generator;
        }

        internal static DayNightCycle BuildDayNight(Light sun)
        {
            DayNightSettings settings = LoadOrCreate<DayNightSettings>(DayNightSettingsPath);
            sun.name = "Sun";

            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.None;
            moon.intensity = 0f;
            moon.color = settings.MoonColor;

            var cycle = new GameObject("DayNight").AddComponent<DayNightCycle>();
            SetReference(cycle, "settings", settings);
            SetReference(cycle, "sun", sun);
            SetReference(cycle, "moon", moon);
            return cycle;
        }

        internal static CloudField BuildClouds()
        {
            var clouds = new GameObject("Clouds").AddComponent<CloudField>();
            clouds.Material = LoadOrCreateTinted(CloudMaterialPath, Color.white);
            return clouds;
        }

        private static void BuildCrates()
        {
            Material material = LoadOrCreateTinted(CrateMaterialPath, new Color(0.62f, 0.42f, 0.24f));
            Vector3[] pontoons = { new Vector3(-0.35f, 0f, -0.35f), new Vector3(0.35f, 0f, -0.35f), new Vector3(-0.35f, 0f, 0.35f), new Vector3(0.35f, 0f, 0.35f) };
            for (int i = 0; i < 3; i++)
            {
                GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = $"Crate{i}";
                crate.transform.position = new Vector3(-14f - i * 4f, 1f, -4f + i * 6f);
                crate.transform.localScale = Vector3.one * 1.2f;
                crate.GetComponent<MeshRenderer>().sharedMaterial = material;
                var body = crate.AddComponent<Rigidbody>();
                body.mass = 600f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                var buoyancy = crate.AddComponent<Buoyancy>();
                buoyancy.Radius = 0.6f;
                foreach (Vector3 p in pontoons)
                {
                    var pontoon = new GameObject("Pontoon");
                    pontoon.transform.SetParent(crate.transform, false);
                    pontoon.transform.localPosition = p;
                    pontoon.AddComponent<Pontoon>();
                }
                var emitter = crate.AddComponent<FoamEmitter>();
                emitter.Points = 12;
                emitter.Size = 0.9f;
                emitter.RingRate = 1.2f;
                emitter.WakeRate = 6f;
                emitter.Life = 2f;
                emitter.Strength = 0.55f;
                emitter.WakeWidth = 0.8f;
            }
        }

        private static void BuildFillerShips()
        {
            Material material = KenneyBoatBuilder.LoadOrCreateMaterial();
            PlaceFillerShip("ship-large", new Vector3(38f, 0f, 55f), 115f, material);
            PlaceFillerShip("ship-small", new Vector3(-42f, 0f, 72f), -35f, material);
        }

        private static void PlaceFillerShip(string model, Vector3 position, float yaw, Material material)
        {
            string path = FillerModelFolder + model + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogWarning($"SceneBuilder: filler model missing at {path}");
                return;
            }

            var ship = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            ship.name = "Filler_" + model;
            ship.transform.position = position;
            ship.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            foreach (MeshRenderer renderer in ship.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }

            foreach (MeshFilter filter in ship.GetComponentsInChildren<MeshFilter>())
            {
                if (KenneyBoatBuilder.IsCanvas(filter.name)) continue;
                filter.gameObject.AddComponent<MeshCollider>();
            }

            Bounds bounds = KenneyBoatBuilder.ModelBounds(ship);
            var body = ship.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var follower = ship.AddComponent<OceanFollower>();
            follower.Length = bounds.size.z;
            follower.Width = bounds.size.x;
            Debug.Log($"SceneBuilder: {ship.name} bounds {bounds.min} .. {bounds.max} ({bounds.size.z:F1} x {bounds.size.x:F1} m)");
        }

        private static void PlaceStandIn(GameObject boat, string attachName)
        {
            Transform attach = boat.transform.Find("Attach/" + attachName);
            if (attach == null)
            {
                Debug.LogWarning($"SceneBuilder: boat has no attach point {attachName}");
                return;
            }
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "PlayerStandIn_" + attachName;
            Object.DestroyImmediate(capsule.GetComponent<CapsuleCollider>());
            capsule.transform.SetParent(boat.transform, false);
            capsule.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            capsule.transform.localPosition = attach.localPosition + new Vector3(0f, 0.9f, 0f);
            capsule.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateTinted(StandInMaterialPath, new Color(0.25f, 0.55f, 0.95f));
        }

        private static Material LoadOrCreateSky()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("Knotical/Sky"));
            AssetDatabase.CreateAsset(material, SkyMaterialPath);
            return material;
        }

        internal static Material LoadOrCreateCliff()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(CliffMaterialPath);
            Shader shader = Shader.Find("Knotical/Cliff");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, CliffMaterialPath);
            }
            if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Material LoadOrCreateTinted(string path, Color tint)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Knotical/VertexColorLit"));
            material.SetColor("_Tint", tint);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static VolumeProfile BuildVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.25f);
            bloom.threshold.Override(1.1f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            var adjust = profile.Add<ColorAdjustments>(true);
            adjust.saturation.Override(18f);
            adjust.contrast.Override(10f);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material LoadOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                throw new System.InvalidOperationException($"Shader {ShaderName} not found");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

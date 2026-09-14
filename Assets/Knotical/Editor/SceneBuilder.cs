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
        private const string RockMaterialPath = "Assets/Knotical/World/Rock.mat";
        private const string SkyMaterialPath = "Assets/Knotical/World/Sky.mat";
        private const string WindSettingsPath = "Assets/Knotical/Wind/WindSettings.asset";
        private const string MaterialPath = "Assets/Knotical/Shaders/OceanSurface.mat";
        private const string ProbePath = "Assets/Knotical/Shaders/OceanProbe.compute";
        private const string ShaderName = "Knotical/OceanSurface";
        private const string FlipbookPath = "Assets/Knotical/Art/Textures/fft_water_normals_8x8.png";

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

            GameObject boatPrefab = BoatBuilder.BuildPrefab();
            var boat = (GameObject)PrefabUtility.InstantiatePrefab(boatPrefab);
            boat.name = "Boat";
            boat.transform.position = new Vector3(0f, 1f, 0f);

            new GameObject("FoamCapture").AddComponent<FoamCapture>();
            BuildCrates();
            BuildRock();

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
                emitter.Size = 0.6f;
                emitter.RingRate = 1.2f;
                emitter.WakeRate = 3f;
                emitter.Life = 4f;
                emitter.Strength = 0.35f;
            }
        }

        private static void BuildRock()
        {
            Material material = LoadOrCreateTinted(RockMaterialPath, new Color(0.45f, 0.42f, 0.36f));
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.position = new Vector3(70f, -3f, 90f);
            rock.transform.localScale = new Vector3(34f, 9f, 26f);
            rock.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(rock.GetComponent<SphereCollider>());
            rock.AddComponent<MeshCollider>();
            var emitter = rock.AddComponent<FoamEmitter>();
            emitter.Points = 64;
            emitter.Size = 3f;
            emitter.RingRate = 0.8f;
            emitter.Strength = 0.35f;
            emitter.WakeRate = 0f;
            emitter.Life = 6f;
            emitter.Drift = 0.8f;
        }

        private static Material LoadOrCreateSky()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("Knotical/Sky"));
            AssetDatabase.CreateAsset(material, SkyMaterialPath);
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

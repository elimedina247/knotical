using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Knotical
{
    public enum OceanDebugView
    {
        Off = 0,
        Grey = 1,
        Normals = 2,
        Height = 3,
        Fold = 4,
        Foam = 5,
        Depth = 6,
    }

    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class OceanSurface : MonoBehaviour
    {
        private static readonly int DebugViewId = Shader.PropertyToID("_DebugView");
        private static readonly int InnerExtentId = Shader.PropertyToID("_InnerExtent");
        private static readonly int ShallowColorId = Shader.PropertyToID("_ShallowColor");
        private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
        private static readonly int DepthFadeId = Shader.PropertyToID("_DepthFade");
        private static readonly int FoamColorId = Shader.PropertyToID("_FoamColor");
        private static readonly int SssColorId = Shader.PropertyToID("_SssColor");
        private static readonly int SunDirId = Shader.PropertyToID("_KnoticalSunDir");

        [SerializeField] private OceanDebugView debugView = OceanDebugView.Off;
        [SerializeField, Range(16, 512)] private int cellsPerLevel = 256;
        [SerializeField] private float[] levelExtents = { 256f, 1024f, 4096f, 8192f };
        [SerializeField, Range(0f, 64f)] private float shortWaveFadeStart = 12f;
        [SerializeField, Range(0f, 128f)] private float shortWaveFadeEnd = 32f;
        [SerializeField] private ComputeShader probeShader;

        private readonly List<MeshRenderer> levels = new List<MeshRenderer>();
        private readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
        private MeshRenderer rootRenderer;
        private Mesh grid;
        private int builtCells;

        public OceanDebugView DebugView
        {
            get => debugView;
            set => debugView = value;
        }

        public float CentreSpacing => levelExtents.Length > 0 ? levelExtents[0] * 2f / cellsPerLevel : 0f;

        private void OnEnable()
        {
            rootRenderer = GetComponent<MeshRenderer>();
            rootRenderer.enabled = false;
            BuildLevels();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            DestroyLevels();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying && Ocean.WaveSet.Count == 0)
            {
                var clock = FindAnyObjectByType<WorldClock>();
                if (clock != null) clock.ConfigureOcean();
            }

            if (levels.Count != levelExtents.Length || builtCells != cellsPerLevel) BuildLevels();

            OceanUniforms.Push();
            OceanUniforms.SetFade(shortWaveFadeStart, shortWaveFadeEnd);
            Light sun = RenderSettings.sun;
            if (sun != null) Shader.SetGlobalVector(SunDirId, -sun.transform.forward);
            PushBlocks();
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            Vector3 eye = cam.transform.position;
            for (int i = 0; i < levels.Count; i++)
            {
                float cell = levelExtents[i] * 2f / cellsPerLevel;
                float x = Mathf.Floor(eye.x / cell) * cell;
                float z = Mathf.Floor(eye.z / cell) * cell;
                levels[i].transform.position = new Vector3(x, Ocean.SeaLevel - 0.05f * i, z);
            }
        }

        private void PushBlocks()
        {
            OceanSettings s = Ocean.Settings;
            for (int i = 0; i < levels.Count; i++)
            {
                MaterialPropertyBlock block = blocks[i];
                block.SetFloat(DebugViewId, (float)debugView);
                block.SetFloat(InnerExtentId, i == 0 ? 0f : levelExtents[i - 1] * 0.95f);
                if (s != null)
                {
                    block.SetColor(ShallowColorId, s.ShallowColor);
                    block.SetColor(DeepColorId, s.DeepColor);
                    block.SetFloat(DepthFadeId, s.ShallowDepth);
                    block.SetColor(FoamColorId, s.CrestColor);
                    block.SetColor(SssColorId, s.SssColor);
                }
                levels[i].SetPropertyBlock(block);
            }
        }

        [ContextMenu("Probe GPU Parity")]
        public void ProbeParity()
        {
            if (probeShader == null)
            {
                Debug.LogWarning("OceanSurface: no probe compute shader assigned.");
                return;
            }

            WaveProbe.Result r = WaveProbe.Run(probeShader, WaveProbe.DefaultPoints);
            Debug.Log($"Ocean GPU parity: max position error {r.MaxPositionError:F5} m, max normal angle {r.MaxNormalAngle:F4} deg, worst point {WaveProbe.DefaultPoints[r.WorstIndex]}");
        }

        private void BuildLevels()
        {
            DestroyLevels();
            if (grid == null || builtCells != cellsPerLevel)
            {
                if (grid != null) DestroyMesh(grid);
                grid = BuildGrid(cellsPerLevel);
                builtCells = cellsPerLevel;
            }

            for (int i = 0; i < levelExtents.Length; i++)
            {
                var go = new GameObject($"OceanLevel{i}") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(levelExtents[i], 1f, levelExtents[i]);

                go.AddComponent<MeshFilter>().sharedMesh = grid;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = rootRenderer.sharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;

                levels.Add(renderer);
                blocks.Add(new MaterialPropertyBlock());
            }
        }

        private void DestroyLevels()
        {
            foreach (MeshRenderer level in levels)
            {
                if (level != null) DestroyUnityObject(level.gameObject);
            }
            levels.Clear();
            blocks.Clear();
        }

        private static void DestroyUnityObject(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        private static void DestroyMesh(Mesh m) => DestroyUnityObject(m);

        private static Mesh BuildGrid(int cells)
        {
            int n = cells + 1;
            var vertices = new Vector3[n * n];
            var triangles = new int[cells * cells * 6];

            for (int j = 0; j < n; j++)
            {
                float z = j / (float)cells * 2f - 1f;
                for (int i = 0; i < n; i++)
                {
                    float x = i / (float)cells * 2f - 1f;
                    vertices[j * n + i] = new Vector3(x, 0f, z);
                }
            }

            int t = 0;
            for (int j = 0; j < cells; j++)
            {
                for (int i = 0; i < cells; i++)
                {
                    int a = j * n + i;
                    int b = a + 1;
                    int c = a + n;
                    int d = c + 1;

                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = b;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = d;
                }
            }

            var mesh = new Mesh
            {
                name = "OceanGrid",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave,
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2.2f, 1000f, 2.2f));
            return mesh;
        }
    }
}

using UnityEngine;

namespace Knotical
{
    [DefaultExecutionOrder(-900)]
    public class LevelGenerator : MonoBehaviour
    {
        [SerializeField] private LevelSettings settings;
        [SerializeField] private LevelProfile profile;
        [SerializeField] private int seed = 1;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private Material blockMaterial;
        [SerializeField] private GameObject blockPrefab;

        private const string GeneratedName = "Generated";

        private Transform root;
        private Mesh terrainMesh;
        private DepthMap depthMap;
        private LevelSettings rolled;
        private OceanSettings oceanBase;
        private OceanSettings rolledOcean;

        public LevelData Data { get; private set; }
        public Heightmap Heightmap => Data?.Heightmap;
        public LevelSettings Settings => settings;
        public LevelProfile Profile => profile;
        public int Seed => seed;
        public LevelSettings Active => rolled != null ? rolled : settings;
        public OceanSettings ActiveOcean => rolledOcean != null ? rolledOcean : Ocean.Settings;
        public int BlockCount { get; private set; }
        public int Generation { get; private set; }
        public bool HasGenerated => transform.Find(GeneratedName) != null;

        private void Awake() => Generate();

        private void OnDestroy() => Clear();

        [ContextMenu("Generate")]
        public void Generate()
        {
            Clear();
            if (settings == null)
            {
                Debug.LogWarning("LevelGenerator: no LevelSettings assigned");
                return;
            }

            if (profile != null) rolled = profile.Roll(settings, seed);
            RollOcean();
            LevelSettings active = Active;
            Data = LevelBuilder.Build(active);
            depthMap = Data.Heightmap.ToDepthMap(active.ShoalDepth);
            Ocean.DepthMap = depthMap;

            root = new GameObject(GeneratedName).transform;
            root.SetParent(transform, false);
            root.gameObject.hideFlags = HideFlags.DontSave;

            BuildTerrain();
            PlaceBlocks();
            MarkDontSave(root);
            Generation++;
            Debug.Log($"LevelGenerator: seed {active.Seed}, {Data.LandCells} land cells of {Data.Land.Length}, {BlockCount} blocks");
        }

        private void RollOcean()
        {
            if (profile == null || profile.Ocean == null) return;
            if (Ocean.Settings == null) FindAnyObjectByType<WorldClock>()?.ConfigureOcean();
            if (Ocean.Settings == null) return;

            oceanBase = Ocean.Settings;
            rolledOcean = profile.Ocean.Roll(oceanBase, seed);
            Ocean.Configure(rolledOcean);
        }

        private static void MarkDontSave(Transform t)
        {
            t.gameObject.hideFlags = HideFlags.DontSave;
            foreach (Transform child in t) MarkDontSave(child);
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            if (root != null) DestroyNow(root.gameObject);
            if (terrainMesh != null) DestroyNow(terrainMesh);
            if (rolled != null) DestroyNow(rolled);
            if (rolledOcean != null)
            {
                if (Ocean.Settings == rolledOcean && oceanBase != null) Ocean.Configure(oceanBase);
                DestroyNow(rolledOcean);
            }
            DestroyStale();
            if (Ocean.DepthMap == depthMap) Ocean.DepthMap = null;
            depthMap?.Dispose();
            root = null;
            rolled = null;
            rolledOcean = null;
            oceanBase = null;
            terrainMesh = null;
            depthMap = null;
            Data = null;
            BlockCount = 0;
        }

        private void DestroyStale()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != GeneratedName) continue;
                Transform terrain = child.Find("Terrain");
                Mesh mesh = terrain != null ? terrain.GetComponent<MeshFilter>().sharedMesh : null;
                if (mesh != null && mesh.name == "LevelTerrain") DestroyNow(mesh);
                DestroyNow(child.gameObject);
            }
        }

        private void BuildTerrain()
        {
            terrainMesh = TerrainMeshBuilder.Build(Data.Heightmap, Active);
            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(root, false);
            terrain.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
            var renderer = terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMaterial != null ? terrainMaterial : FallbackMaterial(Color.white);
            terrain.AddComponent<MeshCollider>().sharedMesh = terrainMesh;
        }

        private void PlaceBlocks()
        {
            var blocks = new GameObject("Blocks").transform;
            blocks.SetParent(root, false);

            GameObject template = blockPrefab;
            bool ownsTemplate = template == null;
            if (ownsTemplate)
            {
                template = GameObject.CreatePrimitive(PrimitiveType.Cube);
                template.GetComponent<MeshFilter>().sharedMesh = CliffMesh.Build();
                template.GetComponent<MeshRenderer>().sharedMaterial =
                    blockMaterial != null ? blockMaterial : FallbackMaterial(new Color(0.55f, 0.5f, 0.45f));
            }

            LevelSettings active = Active;
            var rng = new System.Random(active.Seed * 7919 + 13);
            Heightmap map = Data.Heightmap;
            float spacing = active.BlockSpacing;
            int count = Mathf.FloorToInt(2f * active.Extent / spacing);
            float origin = -active.Extent + spacing * 0.5f;

            for (int gz = 0; gz < count; gz++)
            {
                for (int gx = 0; gx < count; gx++)
                {
                    float jx = ((float)rng.NextDouble() - 0.5f) * active.BlockJitter * spacing;
                    float jz = ((float)rng.NextDouble() - 0.5f) * active.BlockJitter * spacing;
                    var p = new Vector2(origin + gx * spacing + jx, origin + gz * spacing + jz);
                    float h = map.Sample(p);
                    float yaw = (float)rng.NextDouble() * 360f;
                    float tiltX = ((float)rng.NextDouble() - 0.5f) * 2f * active.BlockTilt;
                    float tiltZ = ((float)rng.NextDouble() - 0.5f) * 2f * active.BlockTilt;
                    float width = spacing * Mathf.Lerp(active.BlockWidthMin, active.BlockWidthMax, (float)rng.NextDouble());
                    float depth = spacing * Mathf.Lerp(active.BlockWidthMin, active.BlockWidthMax, (float)rng.NextDouble());

                    if (h < active.BlockMinHeight) continue;

                    float top = h;
                    float reach = 0.5f * Mathf.Max(width, depth);
                    top = Mathf.Max(top, map.Sample(p + new Vector2(reach, reach)));
                    top = Mathf.Max(top, map.Sample(p + new Vector2(-reach, reach)));
                    top = Mathf.Max(top, map.Sample(p + new Vector2(reach, -reach)));
                    top = Mathf.Max(top, map.Sample(p + new Vector2(-reach, -reach)));
                    float height = top + active.BlockRise + active.BlockSink;
                    GameObject block = Instantiate(template, blocks);
                    block.name = "Cliff";
                    block.transform.localPosition = new Vector3(p.x, height * 0.5f - active.BlockSink, p.y);
                    block.transform.localRotation = Quaternion.Euler(tiltX, yaw, tiltZ);
                    block.transform.localScale = new Vector3(width, height, depth);
                    BlockCount++;
                }
            }

            if (ownsTemplate) DestroyNow(template);
            StaticBatchingUtility.Combine(blocks.gameObject);
        }

        private static Material FallbackMaterial(Color tint)
        {
            var material = new Material(Shader.Find("Knotical/VertexColorLit")) { hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_Tint", tint);
            return material;
        }

        private static void DestroyNow(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}

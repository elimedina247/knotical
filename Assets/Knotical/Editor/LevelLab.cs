using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Knotical.Editor
{
    public class LevelLab : EditorWindow
    {
        private const string AutoKey = "Knotical.LevelLab.Auto";
        private const string OceanHiddenKey = "Knotical.LevelLab.OceanHidden";
        private const string FogHiddenKey = "Knotical.LevelLab.FogHidden";
        private const float PreviewHeight = 260f;

        private LevelGenerator generator;
        private OceanSurface ocean;
        private UnityEditor.Editor settingsEditor;
        private UnityEditor.Editor profileEditor;
        private UnityEditor.Editor oceanProfileEditor;
        private UnityEditor.Editor rolledEditor;
        private bool showRolled = true;
        private Texture2D preview;
        private int previewGeneration = -1;
        private bool autoRegenerate;
        private bool dirty;
        private bool regeneratePending;
        private Vector2 scroll;
        private string report = "";

        [MenuItem("Knotical/Level Lab")]
        public static void Open()
        {
            var window = GetWindow<LevelLab>("Level Lab");
            window.minSize = new Vector2(380f, 500f);
        }

        private void OnEnable()
        {
            autoRegenerate = EditorPrefs.GetBool(AutoKey, true);
            Undo.undoRedoPerformed += MarkDirty;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= MarkDirty;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            if (settingsEditor != null) DestroyImmediate(settingsEditor);
            if (profileEditor != null) DestroyImmediate(profileEditor);
            if (oceanProfileEditor != null) DestroyImmediate(oceanProfileEditor);
            if (rolledEditor != null) DestroyImmediate(rolledEditor);
            if (preview != null) DestroyImmediate(preview);
        }

        private void OnDestroy()
        {
            if (ocean != null) ocean.Hidden = false;
            SessionState.SetBool(OceanHiddenKey, false);
            SessionState.SetBool(FogHiddenKey, false);
            RenderSettings.fog = true;
            SceneView.RepaintAll();
        }

        private void OnSceneSaving(Scene scene, string path) => RenderSettings.fog = true;

        private void OnSceneSaved(Scene scene) => RenderSettings.fog = !SessionState.GetBool(FogHiddenKey, false);

        private void MarkDirty() => dirty = true;

        private void OnGUI()
        {
            if (generator == null) generator = FindAnyObjectByType<LevelGenerator>();
            if (ocean == null) ocean = FindAnyObjectByType<OceanSurface>();

            if (generator == null)
            {
                DrawMissingLevel();
                return;
            }

            LevelSettings settings = generator.Settings;
            if (settings == null)
            {
                EditorGUILayout.HelpBox("The Level object has no LevelSettings assigned.", MessageType.Warning);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawActions(settings);
            DrawView(settings);
            DrawPreview();
            DrawSettings(settings);
            EditorGUILayout.EndScrollView();

            if (dirty && autoRegenerate && GUIUtility.hotControl == 0) QueueRegenerate();
        }

        private void DrawMissingLevel()
        {
            EditorGUILayout.HelpBox("No Level object in the open scene.", MessageType.Info);
            if (GUILayout.Button("Add Level object"))
            {
                generator = SceneBuilder.BuildLevel();
                Undo.RegisterCreatedObjectUndo(generator.gameObject, "Add Level");
            }
        }

        private void DrawActions(LevelSettings settings)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate", GUILayout.Height(28f))) QueueRegenerate();
                if (GUILayout.Button("Random seed", GUILayout.Height(28f)))
                {
                    Undo.RecordObject(settings, "Random seed");
                    settings.Seed = Random.Range(0, 10000);
                    EditorUtility.SetDirty(settings);
                    QueueRegenerate();
                }
                if (GUILayout.Button("Clear", GUILayout.Height(28f)))
                {
                    generator.Clear();
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool auto = EditorGUILayout.ToggleLeft("Regenerate when a setting changes", autoRegenerate);
                if (auto != autoRegenerate)
                {
                    autoRegenerate = auto;
                    EditorPrefs.SetBool(AutoKey, auto);
                }
                if (GUILayout.Button("Copy values", GUILayout.Width(90f))) CopyValues(generator.Active);
            }

            if (report.Length > 0) EditorGUILayout.LabelField(report, EditorStyles.miniLabel);
        }

        private void DrawView(LevelSettings settings)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene view", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(ocean == null))
                {
                    bool wantHidden = SessionState.GetBool(OceanHiddenKey, false);
                    bool hidden = EditorGUILayout.ToggleLeft("Hide ocean", wantHidden);
                    if (hidden != wantHidden) SessionState.SetBool(OceanHiddenKey, hidden);
                    if (ocean != null && ocean.Hidden != hidden)
                    {
                        ocean.Hidden = hidden;
                        SceneView.RepaintAll();
                    }
                }

                bool wantFogHidden = SessionState.GetBool(FogHiddenKey, false);
                bool fogHidden = EditorGUILayout.ToggleLeft("Hide fog", wantFogHidden);
                if (fogHidden != wantFogHidden) SessionState.SetBool(FogHiddenKey, fogHidden);
                if (RenderSettings.fog == fogHidden)
                {
                    RenderSettings.fog = !fogHidden;
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Whole map, top down")) Frame(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), settings.Extent * 1.05f, true);
                if (GUILayout.Button("Whole map, angled")) Frame(Vector3.zero, Quaternion.Euler(50f, -30f, 0f), settings.Extent * 0.9f, false);
                if (GUILayout.Button("Spawn")) Frame(Vector3.zero, Quaternion.Euler(25f, 0f, 0f), 60f, false);
            }

            EditorGUILayout.LabelField("Click the preview to fly the Scene camera to that spot. Hold right mouse + WASD to move, Shift for speed.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawPreview()
        {
            Heightmap map = generator.Heightmap;
            if (map == null)
            {
                EditorGUILayout.HelpBox("Press Generate to build the level in the Scene view.", MessageType.Info);
                return;
            }

            if (preview == null || previewGeneration != generator.Generation)
            {
                if (preview != null) DestroyImmediate(preview);
                preview = MapPainter.Paint(map, 3, generator.Active, generator.transform.Find("Generated/Blocks"));
                preview.hideFlags = HideFlags.HideAndDontSave;
                previewGeneration = generator.Generation;
            }

            float side = Mathf.Min(PreviewHeight, EditorGUIUtility.currentViewWidth - 24f);
            Rect rect = GUILayoutUtility.GetRect(side, side, GUILayout.ExpandWidth(false));
            rect.x = (EditorGUIUtility.currentViewWidth - side) * 0.5f;
            GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                float u = (e.mousePosition.x - rect.x) / rect.width;
                float v = 1f - (e.mousePosition.y - rect.y) / rect.height;
                var target = new Vector3(Mathf.Lerp(-map.Extent, map.Extent, u), 0f, Mathf.Lerp(-map.Extent, map.Extent, v));
                Frame(target, Quaternion.Euler(40f, -30f, 0f), 120f, false);
                e.Use();
            }
        }

        private void DrawSettings(LevelSettings settings)
        {
            EditorGUILayout.Space();
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 120f;

            var generatorObject = new SerializedObject(generator);
            generatorObject.Update();
            EditorGUILayout.PropertyField(generatorObject.FindProperty("profile"), new GUIContent("Profile"));
            LevelProfile profile = generator.Profile;
            if (profile != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty seed = generatorObject.FindProperty("seed");
                    EditorGUILayout.PropertyField(seed, new GUIContent("Seed"));
                    if (GUILayout.Button("Reroll", GUILayout.Width(60f))) seed.intValue = Random.Range(1, 10000);
                }
            }
            if (generatorObject.ApplyModifiedProperties()) dirty = true;

            if (profile == null)
            {
                DrawInspector(ref settingsEditor, settings);
                if (GUILayout.Button("Make a profile from these settings")) CreateProfile(settings);
            }
            else
            {
                EditorGUILayout.HelpBox("Each range rolls one value per seed. Min equal to max pins the setting.", MessageType.None);
                DrawInspector(ref profileEditor, profile);

                if (profile.Ocean != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Sea: " + profile.Ocean.name, EditorStyles.boldLabel);
                    DrawInspector(ref oceanProfileEditor, profile.Ocean);
                }

                EditorGUILayout.Space();
                showRolled = EditorGUILayout.Foldout(showRolled, "Rolled values for this seed", true);
                if (showRolled && generator.Active != null && generator.Active != settings)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        if (rolledEditor == null || rolledEditor.target != generator.Active)
                        {
                            if (rolledEditor != null) DestroyImmediate(rolledEditor);
                            rolledEditor = UnityEditor.Editor.CreateEditor(generator.Active);
                        }
                        rolledEditor.OnInspectorGUI();
                    }
                }
            }

            EditorGUIUtility.labelWidth = labelWidth;
        }

        private void DrawInspector(ref UnityEditor.Editor editor, Object target)
        {
            if (editor == null || editor.target != target)
            {
                if (editor != null) DestroyImmediate(editor);
                editor = UnityEditor.Editor.CreateEditor(target);
            }

            EditorGUI.BeginChangeCheck();
            editor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) dirty = true;
        }

        private void CreateProfile(LevelSettings settings)
        {
            string path = EditorUtility.SaveFilePanelInProject("New level profile", "LevelProfile", "asset", "", "Assets/Knotical/Level");
            if (string.IsNullOrEmpty(path)) return;

            var profile = ScriptableObject.CreateInstance<LevelProfile>();
            profile.FixTo(settings);
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            var generatorObject = new SerializedObject(generator);
            generatorObject.FindProperty("profile").objectReferenceValue = profile;
            generatorObject.FindProperty("seed").intValue = settings.Seed;
            generatorObject.ApplyModifiedProperties();
            QueueRegenerate();
        }

        private static void CopyValues(LevelSettings settings)
        {
            var text = new System.Text.StringBuilder();
            foreach (System.Reflection.FieldInfo field in typeof(LevelSettings).GetFields())
            {
                object value = field.GetValue(settings);
                text.AppendLine(value is Color c ? $"{field.Name} = #{ColorUtility.ToHtmlStringRGB(c)}" : $"{field.Name} = {value}");
            }
            EditorGUIUtility.systemCopyBuffer = text.ToString();
            UnityEngine.Debug.Log($"LevelSettings {settings.name}" + System.Environment.NewLine + text);
        }

        private static void Frame(Vector3 pivot, Quaternion rotation, float size, bool ortho)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.LookAt(pivot, rotation, size, ortho, false);
            view.Repaint();
        }

        private void QueueRegenerate()
        {
            dirty = false;
            if (regeneratePending) return;
            regeneratePending = true;
            EditorApplication.delayCall += Regenerate;
        }

        private void Regenerate()
        {
            regeneratePending = false;
            if (generator == null) return;

            var watch = Stopwatch.StartNew();
            generator.Generate();
            watch.Stop();

            LevelData data = generator.Data;
            float land = data != null ? data.LandCells / (float)data.Land.Length : 0f;
            report = $"seed {generator.Active?.Seed}, {watch.ElapsedMilliseconds} ms, land {land:P0}, {generator.BlockCount} cliff blocks";
            SceneView.RepaintAll();
            Repaint();
        }
    }

    [InitializeOnLoad]
    internal static class LevelLabPlayModeGuard
    {
        private const string RegenerateKey = "Knotical.LevelLab.Regenerate";

        static LevelLabPlayModeGuard() => EditorApplication.playModeStateChanged += OnChange;

        private static void OnChange(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                bool any = false;
                foreach (LevelGenerator g in Object.FindObjectsByType<LevelGenerator>())
                {
                    any |= g.HasGenerated;
                    g.Clear();
                }
                SessionState.SetBool(RegenerateKey, any);
            }
            else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RegenerateKey, false))
            {
                SessionState.SetBool(RegenerateKey, false);
                var g = Object.FindAnyObjectByType<LevelGenerator>();
                if (g != null) g.Generate();
            }
        }
    }
}

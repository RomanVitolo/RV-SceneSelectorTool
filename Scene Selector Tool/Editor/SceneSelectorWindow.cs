#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RV_SceneSelectorTool.Editor
{
    public class SceneSelectorWindow : EditorWindow
    {
        private static readonly int Color1 = Shader.PropertyToID("_Color");
       
        private const string PREF_PREFIX = "RV_SceneSelectorTool.";
        private const string HistoryKey = PREF_PREFIX + "SceneHistory";
        private const string FavoritesKey = PREF_PREFIX + "Favorites";
        private const int MaxHistory = 5;
       
        private int selectedTab;
        private readonly string[] tabNames = { "Scene Selector", "Scene Dependencies", "Documentation" };
        
        private List<EditorBuildSettingsScene> buildScenesList;
        private EditorBuildSettingsScene[] buildScenes;
        
        private List<string> allScenePaths;
       
        private Vector2 scrollPos;
        private Vector2 scrollDependenciesPos;
        private Vector2 scrollDocumentationPos;
        
        private ReorderableList reorderableList;
        
        private readonly List<string> sceneHistory = new List<string>();
       
        private readonly List<string> favorites = new List<string>();
        private readonly Dictionary<string, string> favoriteHotkeys = new Dictionary<string, string>();
        private bool editingHotkeys;
        
        private bool showMostUsed = true;
        
        private string historySearch = "";
        private string favoritesSearch = "";
        private string tagSearch = "";
        
        private bool showHistorySection = true;
        private bool showBuildSettingsSection = true;
        private bool showGroupedScenesSection = true;
        private bool showFavoritesSection = true;
       
        private readonly Dictionary<string, bool> folderFoldouts = new Dictionary<string, bool>();

        #region Scene Dependencies Fields
        
        private readonly List<string> depPrefabs = new List<string>();
        private readonly List<string> depOthers = new List<string>();

        private readonly List<ScriptDependency> scriptDependencies = new List<ScriptDependency>();

        private readonly List<PrefabDependency> scenePrefabDependencies = new List<PrefabDependency>();
        private readonly List<MaterialDependency> sceneMaterialDependencies = new List<MaterialDependency>();

        private string lastScannedScenePath = "";

        private readonly Dictionary<int, string> prefabRenameDict = new Dictionary<int, string>();
        private readonly Dictionary<int, string> materialRenameDict = new Dictionary<int, string>();

        private class ScriptDependency
        {
            public string displayName;
            public GameObject gameObjectRef;
            public MonoBehaviour componentRef;
        }

        private class PrefabDependency
        {
            public string displayName;
            public GameObject gameObjectRef;
            public GameObject PrefabAssetRef { get; set; }
        }

        private class MaterialDependency
        {
            public string displayName;
            public Material materialRef;
        }
        #endregion

        [MenuItem("RV - Template Tool/Scenes Tools/Scene Selector")]
        public static void ShowWindow()
        {
            SceneSelectorWindow window = GetWindow<SceneSelectorWindow>("Scene Selector");
            window.minSize = new Vector2(500, 400);
        }

        private void OnEnable()
        {
            RefreshScenes();
            SetupReorderableList();
            LoadHistory();
            LoadFavorites();
            ValidateHistoryAndFavorites();
            LoadFavoriteHotkeys();
        }

        #region Key Helpers
        /// <summary>
        /// Returns a unique EditorPrefs key for the given category and scene.
        /// </summary>
        private string GetKey(string category, string scenePath)
        {
            return PREF_PREFIX + category + "_" + scenePath.Replace("/", "_").Replace(".", "_");
        }
        #endregion

        #region Snapshot Functionality
        private Texture2D GetCustomPreview(string scenePath)
        {
            string key = GetKey("Preview", scenePath);
            if (EditorPrefs.HasKey(key))
            {
                string base64 = EditorPrefs.GetString(key);
                byte[] pngData = System.Convert.FromBase64String(base64);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(pngData);
                return texture;
            }
            return null;
        }

        private void CaptureAndSavePreview(string scenePath)
        {
            if (SceneView.lastActiveSceneView == null || SceneView.lastActiveSceneView.camera == null)
            {
                EditorUtility.DisplayDialog("Error", "No active SceneView available for capturing the image.", "OK");
                return;
            }
            Camera cam = SceneView.lastActiveSceneView.camera;
            int width = 128, height = 128;
            RenderTexture rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D snapshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            snapshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            snapshot.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            rt.Release();
            byte[] pngData = snapshot.EncodeToPNG();
            string base64 = System.Convert.ToBase64String(pngData);
            string key = GetKey("Preview", scenePath);
            EditorPrefs.SetString(key, base64);
            Repaint();
        }
        #endregion

        #region Scene Notes
        private string GetSceneNote(string scenePath)
        {
            string key = GetKey("SceneNote", scenePath);
            return EditorPrefs.HasKey(key) ? EditorPrefs.GetString(key) : "";
        }

        private void SetSceneNote(string scenePath, string note)
        {
            string key = GetKey("SceneNote", scenePath);
            EditorPrefs.SetString(key, note);
        }
        #endregion

        #region Scene Tags
        private string GetSceneTags(string scenePath)
        {
            string key = GetKey("SceneTags", scenePath);
            return EditorPrefs.HasKey(key) ? EditorPrefs.GetString(key) : "";
        }

        private void SetSceneTags(string scenePath, string tags)
        {
            string key = GetKey("SceneTags", scenePath);
            EditorPrefs.SetString(key, tags);
        }
        #endregion

        private Texture2D GetScenePreview(string scenePath)
        {
            Texture2D custom = GetCustomPreview(scenePath);
            if (custom != null)
                return custom;
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            Texture2D preview = AssetPreview.GetAssetPreview(sceneAsset);
            if (preview == null)
                preview = AssetPreview.GetMiniThumbnail(sceneAsset);
            if (preview == null)
                preview = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;
            return preview;
        }

        private void RefreshScenes()
        {
            buildScenes = EditorBuildSettings.scenes;
            buildScenesList = new List<EditorBuildSettingsScene>(buildScenes);
            if (reorderableList != null)
                reorderableList.list = buildScenesList;
            string[] guids = AssetDatabase.FindAssets("t:Scene");
            allScenePaths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/"))
                    continue;
                if (!allScenePaths.Contains(path))
                    allScenePaths.Add(path);
            }
        }

        #region Advanced Build Settings Options
        private void CleanBuildSettings()
        {
            buildScenesList = buildScenesList.Where(scene => allScenePaths.Contains(scene.path)).ToList();
            EditorBuildSettings.scenes = buildScenesList.ToArray();
            RefreshScenes();
        }

        private void SortBuildSettingsAlphabetically()
        {
            buildScenesList = buildScenesList.OrderBy(scene => Path.GetFileNameWithoutExtension(scene.path)).ToList();
            EditorBuildSettings.scenes = buildScenesList.ToArray();
            RefreshScenes();
        }
        #endregion

        #region Hotkeys for Favorites
        
        private void ValidateHistoryAndFavorites()
        {
            List<string> validHistory = sceneHistory.Where(scene => allScenePaths.Contains(scene)).ToList();
            if (validHistory.Count != sceneHistory.Count)
            {
                sceneHistory.Clear();
                sceneHistory.AddRange(validHistory);
                SaveHistory();
            }
           
            List<string> validFavorites = favorites.Where(scene => allScenePaths.Contains(scene)).ToList();
            if (validFavorites.Count != favorites.Count)
            {
                favorites.Clear();
                favorites.AddRange(validFavorites);
                SaveFavorites();
            }
        }
        private void LoadFavoriteHotkeys()
        {
            favoriteHotkeys.Clear();
            for (int i = 0; i < favorites.Count; i++)
            {
                string scenePath = favorites[i];
                string key = GetKey("FavHotkey", scenePath);
                if (EditorPrefs.HasKey(key))
                    favoriteHotkeys[scenePath] = EditorPrefs.GetString(key);
                else
                {
                    string defaultHotkey = (i < 9) ? (i + 1).ToString() : "";
                    favoriteHotkeys[scenePath] = defaultHotkey;
                    EditorPrefs.SetString(key, defaultHotkey);
                }
            }
        }

        private void SaveFavoriteHotkey(string scenePath, string hotkey)
        {
            string key = GetKey("FavHotkey", scenePath);
            EditorPrefs.SetString(key, hotkey);
            favoriteHotkeys[scenePath] = hotkey;
        }
        #endregion

        #region History and Favorites
        private void LoadHistory()
        {
            sceneHistory.Clear();
            string saved = EditorPrefs.GetString(HistoryKey, "");
            if (!string.IsNullOrEmpty(saved))
                sceneHistory.AddRange(saved.Split('|'));
        }

        private void SaveHistory()
        {
            string saved = string.Join("|", sceneHistory);
            EditorPrefs.SetString(HistoryKey, saved);
        }

        private void AddToHistory(string scenePath)
        {
            IncrementUsageCounter(scenePath);
            sceneHistory.Remove(scenePath);
            sceneHistory.Insert(0, scenePath);
            if (sceneHistory.Count > MaxHistory)
                sceneHistory.RemoveAt(sceneHistory.Count - 1);
            SaveHistory();
        }

        private void LoadFavorites()
        {
            favorites.Clear();
            string saved = EditorPrefs.GetString(FavoritesKey, "");
            if (!string.IsNullOrEmpty(saved))
                favorites.AddRange(saved.Split('|'));
        }

        private void SaveFavorites()
        {
            EditorPrefs.SetString(FavoritesKey, string.Join("|", favorites));
        }

        private void AddToFavorites(string scenePath)
        {
            if (!favorites.Contains(scenePath))
            {
                favorites.Add(scenePath);
                SaveFavorites();
            }
        }

        private void RemoveFromFavorites(string scenePath)
        {
            if (favorites.Contains(scenePath))
            {
                favorites.Remove(scenePath);
                SaveFavorites();
            }
        }
        #endregion

        #region Usage Counters
        private int GetUsageCounter(string scenePath)
        {
            string key = GetKey("UsageCounter", scenePath);
            return EditorPrefs.GetInt(key, 0);
        }

        private void IncrementUsageCounter(string scenePath)
        {
            string key = GetKey("UsageCounter", scenePath);
            int count = EditorPrefs.GetInt(key, 0);
            count++;
            EditorPrefs.SetInt(key, count);
        }

        private void ResetUsageCounters()
        {
            foreach (string scenePath in allScenePaths)
            {
                string key = GetKey("UsageCounter", scenePath);
                EditorPrefs.DeleteKey(key);
            }
        }
        #endregion

        #region Most Used Scenes Section
        private void DrawMostUsedSection()
        {
            showMostUsed = EditorGUILayout.Foldout(showMostUsed, "Most Used Scenes:");
            if (showMostUsed)
            {
                List<KeyValuePair<string, int>> usageList = new List<KeyValuePair<string, int>>();
                foreach (string scenePath in allScenePaths)
                {
                    int count = GetUsageCounter(scenePath);
                    if (count > 0)
                        usageList.Add(new KeyValuePair<string, int>(scenePath, count));
                }
                usageList = usageList.OrderByDescending(pair => pair.Value).ToList();
                foreach (var pair in usageList)
                {
                    string sceneName = Path.GetFileNameWithoutExtension(pair.Key);
                    if (GUILayout.Button(sceneName + " (" + pair.Value + " uses)"))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            EditorSceneManager.OpenScene(pair.Key);
                            AddToHistory(pair.Key);
                        }
                    }
                }
                if (GUILayout.Button("Reset Usage Counters", GUILayout.Width(250)))
                {
                    ResetUsageCounters();
                }
            }
        }
        #endregion

        #region ReorderableList Setup
        private void SetupReorderableList()
        {
            reorderableList = new ReorderableList(buildScenesList, typeof(EditorBuildSettingsScene), true, true, false, false)
                {
                    drawHeaderCallback = rect =>
                    {
                        EditorGUI.LabelField(rect, "Scenes in Build Settings");
                    },
                    elementHeight = 120,
                    drawElementCallback = (rect, index, _, _) =>
                    {
                        if (index < 0 || index >= buildScenesList.Count)
                            return;
                        EditorBuildSettingsScene scene = buildScenesList[index];
                        string sceneName = Path.GetFileNameWithoutExtension(scene.path);
                        float padding = 5f;
                        float previewSize = 50f;
                        float x = rect.x + padding;
                        float y = rect.y + padding;
                        Rect previewRect = new Rect(x, y, previewSize, previewSize);
                        x += previewSize + padding;
                        float labelWidth = 150f;
                        Rect labelRect = new Rect(x, y, labelWidth, 20);
                        EditorGUI.LabelField(labelRect, sceneName);
                        x += labelWidth + padding;
                        Rect toggleRect = new Rect(x, y, 60, 20);
                        bool newEnabled = EditorGUI.Toggle(toggleRect, scene.enabled);
                        if (newEnabled != scene.enabled)
                        {
                            buildScenesList[index] = new EditorBuildSettingsScene(scene.path, newEnabled);
                            EditorBuildSettings.scenes = buildScenesList.ToArray();
                        }
                        x += 60 + padding;
                        Rect openButtonRect = new Rect(x, y, 50, 20);
                        x += 55;
                        Rect additiveButtonRect = new Rect(x, y, 80, 20);
                        x += 85;
                        Rect deleteButtonRect = new Rect(x, y, 70, 20);
                        x += 75;
                        Rect captureButtonRect = new Rect(x, y, 120, 20);
                        x += 105;
                        Rect deleteCaptureButtonRect = new Rect(x, y, 120, 20);
                        x += 125;
                        Rect favButtonRect = new Rect(x, y, 80, 20);

                        Color prevColor = GUI.color;
                        GUI.color = scene.enabled ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.6f);
                        Texture2D preview = GetScenePreview(scene.path);
                        if (preview != null)
                            GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
                        else
                            EditorGUI.LabelField(previewRect, "No Preview");
                        GUI.color = prevColor;

                        if (GUI.Button(openButtonRect, "Open"))
                        {
                            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            {
                                EditorSceneManager.OpenScene(scene.path);
                                AddToHistory(scene.path);
                            }
                        }
                        Scene loadedScene = SceneManager.GetSceneByPath(scene.path);
                        if (loadedScene.isLoaded && !loadedScene.Equals(SceneManager.GetActiveScene()))
                        {
                            if (GUI.Button(additiveButtonRect, "Close Additive"))
                            {
                                EditorSceneManager.CloseScene(loadedScene, true);
                            }
                        }
                        else
                        {
                            if (GUI.Button(additiveButtonRect, "Additive"))
                            {
                                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Additive);
                                AddToHistory(scene.path);
                            }
                        }
                        if (GUI.Button(deleteButtonRect, "Remove"))
                        {
                            if (EditorUtility.DisplayDialog("Confirm Deletion",
                                    "Are you sure you want to remove the scene '" + sceneName + "' from Build Settings?", "Yes", "No"))
                            {
                                buildScenesList.RemoveAt(index);
                                EditorBuildSettings.scenes = buildScenesList.ToArray();
                                return;
                            }
                        }
                        if (GUI.Button(captureButtonRect, "Capture Preview"))
                        {
                            CaptureAndSavePreview(scene.path);
                        }
                        if (GetCustomPreview(scene.path) != null)
                        {
                            if (GUI.Button(deleteCaptureButtonRect, "Remove Preview"))
                            {
                                string key = GetKey("Preview", scene.path);
                                EditorPrefs.DeleteKey(key);
                                Repaint();
                            }
                        }
                        if (favorites.Contains(scene.path))
                        {
                            if (GUI.Button(favButtonRect, "Remove Fav"))
                            {
                                RemoveFromFavorites(scene.path);
                            }
                        }
                        else
                        {
                            if (GUI.Button(favButtonRect, "Favorite"))
                            {
                                AddToFavorites(scene.path);
                                LoadFavoriteHotkeys();
                            }
                        }
                        Rect noteRect = new Rect(rect.x + padding, rect.y + previewSize + padding + 10, rect.width - 2 * padding, 20);
                        string currentNote = GetSceneNote(scene.path);
                        EditorGUI.BeginChangeCheck();
                        string newNote = EditorGUI.TextField(noteRect, "Note:", currentNote);
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetSceneNote(scene.path, newNote);
                        }
                        Rect tagsRect = EditorGUILayout.GetControlRect();
                        string currentTags = GetSceneTags(scene.path);
                        EditorGUI.BeginChangeCheck();
                        string newTags = EditorGUI.TextField(tagsRect, "Tags:", currentTags);
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetSceneTags(scene.path, newTags);
                        }
                        if (Event.current.type == EventType.ContextClick && tagsRect.Contains(Event.current.mousePosition))
                        {
                            GenericMenu menu = new GenericMenu();
                            string[] commonTags = { "MainMenu", "Gameplay", "TestScene" };
                            foreach (var tag in commonTags)
                            {
                                menu.AddItem(new GUIContent("Add " + tag), false, () =>
                                {
                                    string tags = GetSceneTags(scene.path);
                                    if (!tags.ToLower().Contains(tag.ToLower()))
                                    {
                                        tags = string.IsNullOrEmpty(tags) ? tag : tags + ", " + tag;
                                        SetSceneTags(scene.path, tags);
                                        Repaint();
                                    }
                                });
                            }
                            menu.ShowAsContext();
                            Event.current.Use();
                        }
                    },
                    onReorderCallback = _ =>
                    {
                        EditorBuildSettings.scenes = buildScenesList.ToArray();
                        RefreshScenes();
                    }
                };
        }
        #endregion

        #region Scene Dependencies Functionality
        /// <summary>
        /// Scans the active scene for its dependencies and categorizes them.
        /// It uses AssetDatabase.GetDependencies for some assets and also scans the scene GameObjects.
        /// </summary>
        private void ScanSceneDependencies()
        {
            depPrefabs.Clear();
            depOthers.Clear();
            scriptDependencies.Clear();

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
                return;

            string scenePath = activeScene.path;
            string[] dependencies = AssetDatabase.GetDependencies(scenePath, true);
            foreach (string dep in dependencies)
            {
                if (dep == scenePath)
                    continue;

                if (dep.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    string typeName = Path.GetFileNameWithoutExtension(dep);
                    if (!depPrefabs.Contains(typeName))
                        depPrefabs.Add(typeName);
                }
                else if (
                    dep.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                    dep.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                    dep.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase) ||
                    dep.EndsWith(".tga", System.StringComparison.OrdinalIgnoreCase) ||
                    dep.EndsWith(".bmp", System.StringComparison.OrdinalIgnoreCase) ||
                    dep.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase)
                    )
                {
                    string fileName = Path.GetFileName(dep);
                    if (!depOthers.Contains(fileName))
                        depOthers.Add(fileName);
                }
                else
                {
                    var type = AssetDatabase.GetMainAssetTypeAtPath(dep);
                    if (type == typeof(Texture2D) || type == typeof(Material) || type == typeof(AudioClip))
                    {
                        string fileName = Path.GetFileName(dep);
                        if (!depOthers.Contains(fileName))
                            depOthers.Add(fileName);
                    }
                }
            }
            depPrefabs.Sort();
            depOthers.Sort();

            ScanScriptDependencies();

            ScanScenePrefabAndMaterialDependencies();
        }

        /// <summary>
        /// Recursively scans the active scene for MonoBehaviour components
        /// and adds their information (GameObject name, script file name and component reference) to the list.
        /// </summary>
        private void ScanScriptDependencies()
        {
            scriptDependencies.Clear();
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return;

            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            foreach (GameObject go in rootObjects)
            {
                ScanScriptDependenciesRecursive(go);
            }
        }

        private void ScanScriptDependenciesRecursive(GameObject go)
        {
            MonoBehaviour[] comps = go.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour comp in comps)
            {
                if (comp == null)
                    continue;

                MonoScript ms = MonoScript.FromMonoBehaviour(comp);
                if (ms != null)
                {
                    string scriptPath = AssetDatabase.GetAssetPath(ms);
                    string scriptFileName = !string.IsNullOrEmpty(scriptPath) ? Path.GetFileName(scriptPath) : ms.name + ".cs";
                    string display = go.name + " - " + scriptFileName;
                    scriptDependencies.Add(new ScriptDependency { displayName = display, gameObjectRef = go, componentRef = comp });
                }
            }
            foreach (Transform child in go.transform)
            {
                ScanScriptDependenciesRecursive(child.gameObject);
            }
        }

        /// <summary>
        /// Scans the scene recursively for prefab instances and materials used.
        /// </summary>
        private void ScanScenePrefabAndMaterialDependencies()
        {
            scenePrefabDependencies.Clear();
            sceneMaterialDependencies.Clear();

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return;

            GameObject[] roots = activeScene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                ScanScenePrefabAndMaterialRecursive(root);
            }
        }

        private void ScanScenePrefabAndMaterialRecursive(GameObject go)
        {
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (prefabAsset != null)
            {
                string prefabName = prefabAsset.name;
                string displayName = go.name + " - " + prefabName + ".prefab";
                if (scenePrefabDependencies.All(x => x.displayName != displayName))
                {
                    scenePrefabDependencies.Add(new PrefabDependency
                    {
                        displayName = displayName,
                        gameObjectRef = go,
                        PrefabAssetRef = prefabAsset
                    });
                }
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null && sceneMaterialDependencies.All(x => x.materialRef != mat))
                    {
                        sceneMaterialDependencies.Add(new MaterialDependency
                        {
                            displayName = mat.name,
                            materialRef = mat
                        });
                    }
                }
            }

            foreach (Transform child in go.transform)
            {
                ScanScenePrefabAndMaterialRecursive(child.gameObject);
            }
        }

        /// <summary>
        /// Draws the Scene Dependencies tab.
        /// </summary>
        private void DrawSceneDependencies()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
            {
                EditorGUILayout.LabelField("No active scene open.");
                return;
            }

            EditorGUILayout.LabelField("Current Scene: " + activeScene.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Path: " + activeScene.path);
            EditorGUILayout.Space();

            if (activeScene.path != lastScannedScenePath)
            {
                ScanSceneDependencies();
                lastScannedScenePath = activeScene.path;
            }

            if (GUILayout.Button("View Dependencies", GUILayout.Width(150)))
            {
                ScanSceneDependencies();
                lastScannedScenePath = activeScene.path;
            }
            EditorGUILayout.Space();

            scrollDependenciesPos = EditorGUILayout.BeginScrollView(scrollDependenciesPos);

            EditorGUILayout.LabelField("🟢 Prefabs Used (via AssetDatabase):", EditorStyles.boldLabel);
            if (depPrefabs.Count > 0)
            {
                foreach (string prefab in depPrefabs)
                {
                    EditorGUILayout.LabelField("- " + prefab);
                }
            }
            else
            {
                EditorGUILayout.LabelField("None.");
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("🟡 Scripts Referenced:", EditorStyles.boldLabel);
            if (scriptDependencies.Count > 0)
            {
                foreach (var scriptDep in scriptDependencies)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("- " + scriptDep.displayName);
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = scriptDep.gameObjectRef;
                        EditorGUIUtility.PingObject(scriptDep.gameObjectRef);
                    }
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        if (EditorUtility.DisplayDialog("Confirm Removal", "Remove script '" + scriptDep.displayName + "' from " + scriptDep.gameObjectRef.name + "?", "Yes", "No"))
                        {
                            Undo.RecordObject(scriptDep.gameObjectRef, "Remove Script");
                            DestroyImmediate(scriptDep.componentRef);
                            ScanSceneDependencies();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.LabelField("None.");
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("🔵 Textures, Materials, Audio (via AssetDatabase):", EditorStyles.boldLabel);
            if (depOthers.Count > 0)
            {
                foreach (string asset in depOthers)
                {
                    EditorGUILayout.LabelField("- " + asset);
                }
            }
            else
            {
                EditorGUILayout.LabelField("None.");
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("🟣 Prefab Instances in Scene:", EditorStyles.boldLabel);
            if (scenePrefabDependencies.Count > 0)
            {
                bool refreshDependencies = false;
                foreach (var pd in scenePrefabDependencies.ToList())
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("- " + pd.displayName);
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = pd.gameObjectRef;
                        EditorGUIUtility.PingObject(pd.gameObjectRef);
                    }
                    if (GUILayout.Button("Break Prefab", GUILayout.Width(90)))
                    {
                        if (EditorUtility.DisplayDialog("Confirm Break Prefab", "Break prefab instance for " + pd.gameObjectRef.name + "?", "Yes", "No"))
                        {
                            PrefabUtility.UnpackPrefabInstance(pd.gameObjectRef, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                            refreshDependencies = true;
                        }
                    }

                    int prefabId = pd.gameObjectRef.GetInstanceID();
                    if (prefabRenameDict.ContainsKey(prefabId))
                    {
                        string newName = EditorGUILayout.TextField(prefabRenameDict[prefabId], GUILayout.Width(100));
                        prefabRenameDict[prefabId] = newName;
                        if (GUILayout.Button("Save", GUILayout.Width(50)))
                        {
                            Undo.RecordObject(pd.gameObjectRef, "Rename Prefab Instance");
                            pd.gameObjectRef.name = newName;
                            prefabRenameDict.Remove(prefabId);
                            refreshDependencies = true;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Rename", GUILayout.Width(70)))
                        {
                            prefabRenameDict[prefabId] = pd.gameObjectRef.name;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (refreshDependencies)
                {
                    ScanSceneDependencies();
                }
            }
            else
            {
                EditorGUILayout.LabelField("None.");
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("🟤 Materials in Scene:", EditorStyles.boldLabel);
            if (sceneMaterialDependencies.Count > 0)
            {
                foreach (var md in sceneMaterialDependencies)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("- " + md.displayName);
                    if (md.materialRef.HasProperty(Color1))
                    {
                        Color col = md.materialRef.color;
                        EditorGUI.BeginChangeCheck();
                        Color newCol = EditorGUILayout.ColorField(col, GUILayout.Width(100));
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(md.materialRef, "Change Material Color");
                            md.materialRef.color = newCol;
                            EditorUtility.SetDirty(md.materialRef);
                        }
                    }

                    int matId = md.materialRef.GetInstanceID();
                    if (materialRenameDict.ContainsKey(matId))
                    {
                        string newName = EditorGUILayout.TextField(materialRenameDict[matId], GUILayout.Width(100));
                        materialRenameDict[matId] = newName;
                        if (GUILayout.Button("Save", GUILayout.Width(50)))
                        {
                            string assetPath = AssetDatabase.GetAssetPath(md.materialRef);
                            string error = AssetDatabase.RenameAsset(assetPath, newName);
                            if (!string.IsNullOrEmpty(error))
                            {
                                EditorUtility.DisplayDialog("Rename Error", error, "OK");
                            }
                            else
                            {
                                md.displayName = newName;
                            }
                            materialRenameDict.Remove(matId);
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Rename", GUILayout.Width(70)))
                        {
                            materialRenameDict[matId] = md.materialRef.name;
                        }
                    }
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeObject = md.materialRef;
                        EditorGUIUtility.PingObject(md.materialRef);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.LabelField("None.");
            }

            EditorGUILayout.EndScrollView();
        }
        #endregion

        /// <summary>
        /// Draws the Scene Selector tab (the original functionality).
        /// </summary>
        private void DrawSceneSelector()
        {
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = Color.cyan }
            };
            EditorGUILayout.LabelField("Scene Selector", titleStyle);
            EditorGUILayout.Space();

            if (GUILayout.Button("Refresh Scenes", GUILayout.Height(25)))
            {
                RefreshScenes();
            }
            EditorGUILayout.Space();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            #region Scene History Section
            showHistorySection = EditorGUILayout.Foldout(showHistorySection, "Scene History:");
            if (showHistorySection)
            {
                historySearch = EditorGUILayout.TextField("Search History:", historySearch);
                if (sceneHistory.Count > 0)
                {
                    foreach (string scenePath in new List<string>(sceneHistory))
                    {
                        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                        if (!string.IsNullOrEmpty(historySearch) &&
                            !sceneName.ToLower().Contains(historySearch.ToLower()))
                            continue;
                        if (GUILayout.Button(sceneName))
                        {
                            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            {
                                EditorSceneManager.OpenScene(scenePath);
                                AddToHistory(scenePath);
                            }
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No history available.");
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
            #endregion

            #region Scene Favorites Section
            showFavoritesSection = EditorGUILayout.Foldout(showFavoritesSection, "Scene Favorites:");
            if (showFavoritesSection)
            {
                favoritesSearch = EditorGUILayout.TextField("Search Favorites:", favoritesSearch);
                if (GUILayout.Button("Configure Hotkeys", GUILayout.Width(150)))
                {
                    editingHotkeys = !editingHotkeys;
                }
                if (favorites.Count > 0)
                {
                    foreach (string scenePath in new List<string>(favorites))
                    {
                        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                        if (!string.IsNullOrEmpty(favoritesSearch) &&
                            !sceneName.ToLower().Contains(favoritesSearch.ToLower()))
                            continue;
                        EditorGUILayout.BeginHorizontal("box");
                        if (GUILayout.Button(sceneName, GUILayout.ExpandWidth(true)))
                        {
                            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            {
                                EditorSceneManager.OpenScene(scenePath);
                                AddToHistory(scenePath);
                            }
                        }

                        if (!favoriteHotkeys.TryGetValue(scenePath, out var currentHotkey))
                        {
                            int idx = favorites.IndexOf(scenePath);
                            currentHotkey = (idx < 9) ? (idx + 1).ToString() : "";
                            favoriteHotkeys[scenePath] = currentHotkey;
                            SaveFavoriteHotkey(scenePath, currentHotkey);
                        }
                        if (editingHotkeys)
                        {
                            string newHotkey = EditorGUILayout.TextField(currentHotkey, GUILayout.Width(50));
                            if (newHotkey != currentHotkey)
                            {
                                favoriteHotkeys[scenePath] = newHotkey;
                                SaveFavoriteHotkey(scenePath, newHotkey);
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Ctrl+" + currentHotkey, GUILayout.Width(50));
                        }
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        {
                            RemoveFromFavorites(scenePath);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No favorites available.");
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
            #endregion

            #region Most Used Scenes Section
            DrawMostUsedSection();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
            #endregion

            #region Advanced Build Settings Options
            showBuildSettingsSection = EditorGUILayout.Foldout(showBuildSettingsSection, "Scenes in Build Settings:");
            if (showBuildSettingsSection)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clean Build Settings", GUILayout.Width(200)))
                {
                    CleanBuildSettings();
                }
                if (GUILayout.Button("Sort Build Settings (Alphabetical)", GUILayout.Width(250)))
                {
                    SortBuildSettingsAlphabetically();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                if (buildScenesList != null && buildScenesList.Count > 0)
                {
                    reorderableList.DoLayoutList();
                }
                else
                {
                    EditorGUILayout.LabelField("No scenes found in Build Settings.");
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
            #endregion

            #region Scenes Grouped by Folders
            showGroupedScenesSection = EditorGUILayout.Foldout(showGroupedScenesSection, "Scenes Grouped by Folders:");
            if (showGroupedScenesSection)
            {
                tagSearch = EditorGUILayout.TextField("Filter by Tags:", tagSearch);
                List<string> scenesNotInBuild = new List<string>(allScenePaths);
                foreach (EditorBuildSettingsScene scene in buildScenesList)
                {
                    scenesNotInBuild.Remove(scene.path);
                }
                Dictionary<string, List<string>> groupedScenes = new Dictionary<string, List<string>>();
                foreach (string scenePath in scenesNotInBuild)
                {
                    string folder = Path.GetDirectoryName(scenePath);
                    if (!groupedScenes.ContainsKey(folder))
                        groupedScenes[folder] = new List<string>();
                    groupedScenes[folder].Add(scenePath);
                }
                foreach (var kvp in groupedScenes.OrderBy(k => k.Key))
                {
                    string folder = kvp.Key;
                    List<string> scenesInFolder = kvp.Value;
                    folderFoldouts.TryAdd(folder, true);
                    folderFoldouts[folder] = EditorGUILayout.Foldout(folderFoldouts[folder], folder);
                    if (folderFoldouts[folder])
                    {
                        foreach (string scenePath in scenesInFolder)
                        {
                            string sceneTags = GetSceneTags(scenePath);
                            if (!string.IsNullOrEmpty(tagSearch) && !sceneTags.ToLower().Contains(tagSearch.ToLower()))
                                continue;
                            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                            EditorGUILayout.BeginHorizontal("box");
                            Texture2D preview = GetScenePreview(scenePath);
                            if (preview != null)
                                GUILayout.Label(preview, GUILayout.Width(50), GUILayout.Height(50));
                            else
                                GUILayout.Label("No Preview", GUILayout.Width(50), GUILayout.Height(50));
                            EditorGUILayout.LabelField(sceneName, GUILayout.Width(150));
                            if (GUILayout.Button("Open", GUILayout.Width(50)))
                            {
                                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                                {
                                    EditorSceneManager.OpenScene(scenePath);
                                    AddToHistory(scenePath);
                                }
                            }
                            Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
                            if (loadedScene.isLoaded && !loadedScene.Equals(SceneManager.GetActiveScene()))
                            {
                                if (GUILayout.Button("Close Additive", GUILayout.Width(110)))
                                {
                                    EditorSceneManager.CloseScene(loadedScene, true);
                                }
                            }
                            else
                            {
                                if (GUILayout.Button("Additive", GUILayout.Width(90)))
                                {
                                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                                    AddToHistory(scenePath);
                                }
                            }
                            if (GUILayout.Button("Add", GUILayout.Width(70)))
                            {
                                buildScenesList.Add(new EditorBuildSettingsScene(scenePath, true));
                                EditorBuildSettings.scenes = buildScenesList.ToArray();
                                RefreshScenes();
                            }
                            if (favorites.Contains(scenePath))
                            {
                                if (GUILayout.Button("Remove Fav", GUILayout.Width(80)))
                                {
                                    RemoveFromFavorites(scenePath);
                                }
                            }
                            else
                            {
                                if (GUILayout.Button("Favorite", GUILayout.Width(80)))
                                {
                                    AddToFavorites(scenePath);
                                    LoadFavoriteHotkeys();
                                }
                            }
                            if (GUILayout.Button("Capture Preview", GUILayout.Width(120)))
                            {
                                CaptureAndSavePreview(scenePath);
                            }
                            if (GetCustomPreview(scenePath) != null)
                            {
                                if (GUILayout.Button("Remove Preview", GUILayout.Width(120)))
                                {
                                    string key = GetKey("Preview", scenePath);
                                    EditorPrefs.DeleteKey(key);
                                    Repaint();
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                            EditorGUI.indentLevel++;
                            string currentNote = GetSceneNote(scenePath);
                            string newNote = EditorGUILayout.TextField("Note:", currentNote);
                            if (newNote != currentNote)
                            {
                                SetSceneNote(scenePath, newNote);
                            }
                            Rect tagsRect = EditorGUILayout.GetControlRect();
                            string currentTags = GetSceneTags(scenePath);
                            EditorGUI.BeginChangeCheck();
                            string newTags = EditorGUI.TextField(tagsRect, "Tags:", currentTags);
                            if (EditorGUI.EndChangeCheck())
                            {
                                SetSceneTags(scenePath, newTags);
                            }
                            if (Event.current.type == EventType.ContextClick && tagsRect.Contains(Event.current.mousePosition))
                            {
                                GenericMenu menu = new GenericMenu();
                                string[] commonTags = { "MainMenu", "Gameplay", "TestScene" };
                                foreach (var tag in commonTags)
                                {
                                    menu.AddItem(new GUIContent("Add " + tag), false, () =>
                                    {
                                        string tags = GetSceneTags(scenePath);
                                        if (!tags.ToLower().Contains(tag.ToLower()))
                                        {
                                            tags = string.IsNullOrEmpty(tags) ? tag : tags + ", " + tag;
                                            SetSceneTags(scenePath, tags);
                                            Repaint();
                                        }
                                    });
                                }
                                menu.ShowAsContext();
                                Event.current.Use();
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
            }
            #endregion

            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.KeyDown && Event.current.control)
            {
                if (Event.current.keyCode >= KeyCode.Alpha0 && Event.current.keyCode <= KeyCode.Alpha9)
                {
                    string pressed = ((int)Event.current.keyCode - (int)KeyCode.Alpha0).ToString();
                    foreach (var fav in favorites)
                    {
                        if (favoriteHotkeys.ContainsKey(fav) && favoriteHotkeys[fav] == pressed)
                        {
                            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            {
                                EditorSceneManager.OpenScene(fav);
                                AddToHistory(fav);
                                Event.current.Use();
                                break;
                            }
                        }
                    }
                }
            }
        }

        #region Documentation Tab
        private void DrawDocumentation()
        {
            EditorGUILayout.LabelField("Documentation", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            string docText = "Documentation:\n\n" +
                "This tool provides a convenient interface to manage scenes in your Unity project. It includes the following tabs:\n\n" +
                "1. Scene Selector: \n" +
                "   - View a history of opened scenes, add scenes to favorites, and manage the scenes included in Build Settings.\n" +
                "   - You can refresh the list of scenes, open a scene directly, capture a preview, add notes and tags, and more.\n\n" +
                "2. Scene Dependencies: \n" +
                "   - Displays the dependencies of the currently active scene including prefabs, scripts, textures, materials, and audio assets.\n" +
                "   - You can select, break prefab instances, or rename prefab instances and materials directly from this tab.\n\n" +
                "3. Documentation: \n" +
                "   - Provides a complete guide on how to use this tool. You can select and copy this text for reference.\n\n" +
                "Usage Instructions:\n" +
                "- Use the toolbar at the top to switch between tabs.\n" +
                "- In the Scene Selector tab, use the search boxes to filter scene history or favorites, and use the buttons to perform actions such as opening, adding, or capturing previews of scenes.\n" +
                "- In the Scene Dependencies tab, click \"View Dependencies\" to refresh the dependency list for the active scene, and use the provided buttons to interact with the listed dependencies.\n\n" +
                "**Developer Info:**\n" +
                "Author: Roman Vitolo\n" +
                "Website: https://romanvitolo.com\n" +
                "GitHub Repository: https://github.com/RomanVitolo\n" +
                "For Issues & Feedback: You can contact me via email (available on my website).\n";

            scrollDocumentationPos = EditorGUILayout.BeginScrollView(scrollDocumentationPos);
            EditorGUILayout.SelectableLabel(docText, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
        #endregion

        private void OnGUI()
        {
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
            EditorGUILayout.Space();
            if (selectedTab == 0)
            {
                DrawSceneSelector();
            }
            else if (selectedTab == 1)
            {
                DrawSceneDependencies();
            }
            else if (selectedTab == 2)
            {
                DrawDocumentation();
            }
        }
    }
}
#endif

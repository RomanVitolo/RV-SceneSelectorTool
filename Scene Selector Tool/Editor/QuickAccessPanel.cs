#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RV_SceneSelectorTool.Editor
{
    public class QuickAccessPanel : EditorWindow
    {
        private const int MAX_HISTORY = 5;

        private List<string> favorites;
        private List<string> history;

        private Vector2 scrollPos;
        private Vector2 docScrollPos;

        private List<string> allScenePaths;
        private Dictionary<string, string> renameDict;

        private int selectedTab;
        private readonly string[] toolbarTabs = { "Tool", "Documentation" };

        private bool showFavoritesSection = true;
        private bool showRecentScenesSection = true;
        private bool showBuildSettingsSection = true;
        private bool showExportImportSection = true;
        private bool showRenameSection = true;

        [MenuItem("RV - Template Tool/Scenes Tools/Quick Access Panel")]
        public static void ShowWindow()
        {
            QuickAccessPanel window = GetWindow<QuickAccessPanel>("Quick Access");
            window.minSize = new Vector2(250, 300);
            window.Show();
        }

        private void OnEnable()
        {
            favorites = PreferencesManager.LoadFavorites();
            history = PreferencesManager.LoadHistory();
            allScenePaths = SceneHelper.LoadAllScenes();
            renameDict = new Dictionary<string, string>();
        }

        private void AddToHistory(string scenePath)
        {
            history.Remove(scenePath);
            history.Insert(0, scenePath);
            if (history.Count > MAX_HISTORY)
                history.RemoveAt(history.Count - 1);
            PreferencesManager.SaveHistory(history);
        }

        private void UpdateEditorPrefsForScenePathChange(string oldPath, string newPath)
        {
            if (favorites.Contains(oldPath))
            {
                int idx = favorites.IndexOf(oldPath);
                favorites[idx] = newPath;
                PreferencesManager.SaveFavorites(favorites);
            }
            if (history.Contains(oldPath))
            {
                int idx = history.IndexOf(oldPath);
                history[idx] = newPath;
                PreferencesManager.SaveHistory(history);
            }
            PreferencesManager.UpdateKey("UsageCounter", oldPath, newPath, true);
            PreferencesManager.UpdateKey("SceneNote", oldPath, newPath);
            PreferencesManager.UpdateKey("SceneTags", oldPath, newPath);
            PreferencesManager.UpdateKey("Preview", oldPath, newPath);
            PreferencesManager.UpdateKey("FavHotkey", oldPath, newPath);
        }

        private void RefreshData()
        {
            favorites = PreferencesManager.LoadFavorites();
            history = PreferencesManager.LoadHistory();
            allScenePaths = SceneHelper.LoadAllScenes();
            renameDict.Clear();
        }

        private void DrawDivider(Color color, float height = 2f)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(rect, color);
        }

        private void DrawSectionHeader(string labelTitle, GUIStyle headerStyle)
        {
            EditorGUILayout.LabelField(labelTitle + ":", headerStyle);
            EditorGUILayout.Space();
        }

        private void DrawSectionVisibilitySettings()
        {
            EditorGUILayout.LabelField("Customize Visible Sections", EditorStyles.boldLabel);
            showFavoritesSection = EditorGUILayout.ToggleLeft("Favorites", showFavoritesSection);
            showRecentScenesSection = EditorGUILayout.ToggleLeft("Recent Scenes", showRecentScenesSection);
            showBuildSettingsSection = EditorGUILayout.ToggleLeft("Build Settings Scenes", showBuildSettingsSection);
            showExportImportSection = EditorGUILayout.ToggleLeft("Export / Import Scene List", showExportImportSection);
            showRenameSection = EditorGUILayout.ToggleLeft("Rename Scenes (All)", showRenameSection);
            EditorGUILayout.Space();
            DrawDivider(Color.gray);
            EditorGUILayout.Space();
        }

        private void DrawToolTab()
        {
            SceneHelper.ValidateSceneLists(favorites, history);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.cyan },
                fontSize = 16
            };

            EditorGUILayout.LabelField("Quick Access Panel", headerStyle);
            EditorGUILayout.Space();
            DrawDivider(Color.gray);
            EditorGUILayout.Space();

            if (GUILayout.Button("Refresh"))
                RefreshData();

            DrawSectionVisibilitySettings();

            if (showFavoritesSection)
            {
                DrawSectionHeader("Favorites", headerStyle);
                if (favorites.Count > 0)
                {
                    foreach (string scenePath in favorites.ToList())
                    {
                        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
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
                    EditorGUILayout.LabelField("No favorites found.");
                }
                EditorGUILayout.Space();
            }

            if (showRecentScenesSection)
            {
                DrawDivider(Color.gray);
                EditorGUILayout.Space();
                DrawSectionHeader("Recent Scenes", headerStyle);
                if (history.Count > 0)
                {
                    foreach (string scenePath in history.ToList())
                    {
                        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
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
                    EditorGUILayout.LabelField("No recent scenes found.");
                }
                EditorGUILayout.Space();
            }

            if (showBuildSettingsSection)
            {
                DrawDivider(Color.gray);
                EditorGUILayout.Space();
                DrawSectionHeader("Build Settings Scenes", headerStyle);
                EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
                if (buildScenes.Length > 0)
                {
                    foreach (var scene in buildScenes)
                    {
                        EditorGUILayout.BeginHorizontal();
                        string sceneName = Path.GetFileNameWithoutExtension(scene.path);
                        EditorGUILayout.LabelField("Scene:", GUILayout.Width(50));
                        EditorGUILayout.LabelField(sceneName);
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No scenes in Build Settings.");
                }
                EditorGUILayout.Space();
            }

            if (showExportImportSection)
            {
                DrawDivider(Color.gray);
                EditorGUILayout.Space();
                DrawSectionHeader("Export / Import Scene List", headerStyle);
                if (GUILayout.Button("Export Favorites"))
                {
                    string path = EditorUtility.SaveFilePanel("Export Favorites", "", "Favorites.txt", "txt");
                    if (!string.IsNullOrEmpty(path))
                    {
                        File.WriteAllText(path, string.Join("\n", favorites));
                        EditorUtility.DisplayDialog("Export Favorites", "Favorites exported successfully.", "OK");
                    }
                }
                if (GUILayout.Button("Import Favorites"))
                {
                    string path = EditorUtility.OpenFilePanel("Import Favorites", "", "txt");
                    if (!string.IsNullOrEmpty(path))
                    {
                        string fileContents = File.ReadAllText(path);
                        string[] lines = fileContents.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                        favorites = lines.ToList();
                        PreferencesManager.SaveFavorites(favorites);
                        EditorUtility.DisplayDialog("Import Favorites", "Favorites imported successfully.", "OK");
                    }
                }
                EditorGUILayout.Space();
                if (GUILayout.Button("Export Build Settings"))
                {
                    List<string> scenePaths = EditorBuildSettings.scenes.Select(scene => scene.path).ToList();
                    string path = EditorUtility.SaveFilePanel("Export Build Settings", "", "BuildSettings.txt", "txt");
                    if (!string.IsNullOrEmpty(path))
                    {
                        File.WriteAllText(path, string.Join("\n", scenePaths));
                        EditorUtility.DisplayDialog("Export Build Settings", "Build settings exported successfully.", "OK");
                    }
                }
                if (GUILayout.Button("Import Build Settings"))
                {
                    string path = EditorUtility.OpenFilePanel("Import Build Settings", "", "txt");
                    if (!string.IsNullOrEmpty(path))
                    {
                        string fileContents = File.ReadAllText(path);
                        string[] lines = fileContents.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                        List<EditorBuildSettingsScene> newBuildScenes = lines.Select(line => new EditorBuildSettingsScene(line, true)).ToList();
                        EditorBuildSettings.scenes = newBuildScenes.ToArray();
                        EditorUtility.DisplayDialog("Import Build Settings", "Build settings imported successfully.", "OK");
                    }
                }
                EditorGUILayout.Space();
            }

            if (showRenameSection)
            {
                DrawDivider(Color.gray);
                EditorGUILayout.Space();
                DrawSectionHeader("Rename Scenes (All)", headerStyle);
                if (GUILayout.Button("Refresh All Scenes List"))
                {
                    allScenePaths = SceneHelper.LoadAllScenes();
                    renameDict.Clear();
                }
                if (allScenePaths.Count > 0)
                {
                    foreach (string scenePath in allScenePaths.ToList())
                    {
                        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                        renameDict.TryAdd(scenePath, sceneName);

                        EditorGUILayout.BeginHorizontal();
                        renameDict[scenePath] = EditorGUILayout.TextField(renameDict[scenePath]);
                        if (GUILayout.Button("Rename", GUILayout.Width(70)))
                        {
                            if (!string.IsNullOrEmpty(renameDict[scenePath]) && renameDict[scenePath] != sceneName)
                            {
                                string error = AssetDatabase.RenameAsset(scenePath, renameDict[scenePath]);
                                if (!string.IsNullOrEmpty(error))
                                {
                                    EditorUtility.DisplayDialog("Rename Error", error, "OK");
                                }
                                else
                                {
                                    AssetDatabase.Refresh();
                                    string newPath = Path.Combine(Path.GetDirectoryName(scenePath) ?? string.Empty, renameDict[scenePath] + ".unity");
                                    List<EditorBuildSettingsScene> buildScenesList = EditorBuildSettings.scenes.ToList();
                                    for (int i = 0; i < buildScenesList.Count; i++)
                                    {
                                        if (buildScenesList[i].path == scenePath)
                                            buildScenesList[i] = new EditorBuildSettingsScene(newPath, buildScenesList[i].enabled);
                                    }
                                    EditorBuildSettings.scenes = buildScenesList.ToArray();
                                    UpdateEditorPrefsForScenePathChange(scenePath, newPath);
                                    EditorUtility.DisplayDialog("Rename Success", "Scene renamed successfully.", "OK");
                                    int index = allScenePaths.IndexOf(scenePath);
                                    if (index >= 0)
                                        allScenePaths[index] = newPath;
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No scenes found in project.");
                }
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDocumentationTab()
        {
            docScrollPos = EditorGUILayout.BeginScrollView(docScrollPos);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                normal = { textColor = Color.cyan }
            };
            EditorGUILayout.LabelField("Documentation", headerStyle);
            EditorGUILayout.Space();

            GUIStyle docTextStyle = new GUIStyle(EditorStyles.textArea)
            {
                fontSize = 14,
                wordWrap = true
            };
            string documentationText =
                "Overview:\n\n" +
                "This tool allows you to quickly access project scenes, manage favorites and history, " +
                "rename scenes, as well as export and import the scene list.\n\n" +
                "Usage:\n\n" +
                "1. In the 'Tool' tab you will find the quick access panel with several sections. " +
                "Use the 'Customize Visible Sections' options at the top to enable or disable each section.\n" +
                "   - Favorites: List of scenes marked as favorites.\n" +
                "   - Recent Scenes: Recently opened scenes.\n" +
                "   - Build Settings Scenes: Scenes included in the Build Settings (display only).\n" +
                "   - Export / Import Scene List: Options to export or import scene lists.\n" +
                "   - Rename Scenes (All): Rename scenes outside of Build Settings.\n\n" +
                "2. To open a scene, click its button in the respective section.\n" +
                "3. To rename a scene, use the 'Rename Scenes (All)' section.\n\n";
            EditorGUILayout.TextArea(documentationText, docTextStyle, GUILayout.Height(350));
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Developer Info", headerStyle);
            EditorGUILayout.Space();
            GUIStyle devInfoStyle = new GUIStyle(EditorStyles.textArea)
            {
                fontSize = 14,
                wordWrap = true
            };
            string developerInfoText =
                "Author: Roman Vitolo\n" +
                "Website: https://romanvitolo.com\n" +
                "GitHub Repository: https://github.com/RomanVitolo\n" +
                "For Issues & Feedback: You can contact me via email (details available on my website).\n";
            EditorGUILayout.TextArea(developerInfoText, devInfoStyle, GUILayout.Height(100));
            EditorGUILayout.EndScrollView();
        }

        private void OnGUI()
        {
            selectedTab = GUILayout.Toolbar(selectedTab, toolbarTabs);
            if (selectedTab == 0)
                DrawToolTab();
            else if (selectedTab == 1)
                DrawDocumentationTab();
        }
    }
}
#endif

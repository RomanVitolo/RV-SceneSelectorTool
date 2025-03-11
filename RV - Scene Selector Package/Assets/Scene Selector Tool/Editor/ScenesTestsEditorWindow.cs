#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using UnityEditor.TestTools.TestRunner.Api;
using System.Text;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.Serialization;

namespace RV_SceneSelectorTool.Editor
{
    public class ScenesTestsEditorWindow : EditorWindow, ICallbacks
    {
        private int selectedTab;
        private readonly string[] toolbarTabs = { "Tool", "Documentation" };
    
        private string sceneName;
        private string scenePath;
        private int testCoverage;
    
        private TestRunnerApi testRunnerApi;
    
        private bool showCustomGenerator;
        private string customTestScriptName = "";
        private string customAsmdefName = "";
        private GameObject selectedGameObject;
        private string customTestDescription = "";
    
        private bool showSettings;
        private string testFolderPath = "Assets/Tests/Generated";
        private bool createAssemblyDefinition = true;
    
        private bool showTestListRunner;
        private List<string> loadedTestFilters = new List<string>();
        private readonly List<TestResultInfo> testResults = new List<TestResultInfo>();
        private Vector2 resultsScrollPos;
    
        private string testSearchFilter = "";
        private readonly string[] testStateOptions = { "All", "Passed", "Failed", "Inconclusive", "Skipped" };
        private int selectedTestStateFilterIndex;
    
        private Vector2 mainScrollPos;
        private Vector2 docScrollPos;
    
        private readonly List<string> discoveredTests = new List<string>();
    
        [MenuItem("RV - Template Tool/Scenes Tools/Tests Scene Tool")]
        public static void ShowWindow()
        {
            GetWindow<ScenesTestsEditorWindow>("Scenes Tests");
        }
        
        private void LoadTestList()
        {
            string path = EditorUtility.OpenFilePanel("Load Test List", Application.dataPath, "txt,json");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    loadedTestFilters = new List<string>(lines);
                    Debug.Log("Loaded " + loadedTestFilters.Count + " test filter(s) from " + path);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Error loading test list: " + ex.Message);
                }
            }
        }
    
        private void DiscoverTestsInScene()
        {
            discoveredTests.Clear();
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                foreach (var type in assembly.GetTypes())
                {
                    if (!type.IsClass || !type.IsPublic) continue;
                    object[] attrs = type.GetCustomAttributes(typeof(CategoryAttribute), true);
                    foreach (object attr in attrs)
                    {
                        if (attr is CategoryAttribute cat && cat.Name == sceneName)
                        {
                            discoveredTests.Add(type.Name);
                            break;
                        }
                    }
                }
            }
        }
    
        private void RunDiscoveredTests()
        {
            if (discoveredTests.Count == 0)
            {
                Debug.LogWarning("No discovered tests to run.");
                return;
            }
            TestRunnerApi api = CreateInstance<TestRunnerApi>();
            Filter filter = new Filter
            {
                categoryNames = new[] { sceneName }
            };
            ExecutionSettings settings = new ExecutionSettings(filter);
            api.Execute(settings);
            Debug.Log("Running discovered tests: " + string.Join(", ", discoveredTests));
        }
    
        private void OnEnable()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            RefreshSceneInfo();
            testRunnerApi = CreateInstance<TestRunnerApi>();
            testRunnerApi.RegisterCallbacks(this);
            SceneView.duringSceneGui += OnSceneGUIOverlay;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }
    
        private void OnDisable()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            if (testRunnerApi != null)
                testRunnerApi.UnregisterCallbacks(this);
            SceneView.duringSceneGui -= OnSceneGUIOverlay;
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
        }
    
        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RefreshSceneInfo();
            Repaint();
        }
    
        /// <summary>
        /// Updates the active scene information.
        /// </summary>
        private void RefreshSceneInfo()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            sceneName = currentScene.name;
            scenePath = currentScene.path;
            testCoverage = DoesTestTemplateExist(sceneName) ? 100 : 0;
        }
    
        private void OnGUI()
        {
            selectedTab = GUILayout.Toolbar(selectedTab, toolbarTabs);
    
            if (selectedTab == 0)
            {
                mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);
    
                GUILayout.Label("Scenes Tests Tool", EditorStyles.boldLabel);
                GUILayout.Label("Current Scene: " + sceneName, new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold });
                GUILayout.Label("Scene Path: " + scenePath);
                GUILayout.Space(10);
                GUILayout.Label("Test Coverage: " + testCoverage + "%", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.cyan } });
                if (GUILayout.Button(new GUIContent("Refresh", "Reload scene info and test coverage.")))
                {
                    RefreshSceneInfo();
                }
    
                GUILayout.Space(20);
                showSettings = EditorGUILayout.Foldout(showSettings, new GUIContent("Settings", "Configure test folder and asmdef options."));
                if (GUILayout.Button("?", GUILayout.Width(25)))
                {
                    EditorUtility.DisplayDialog("Settings Help",
                        "Configure the folder (within Assets) where tests will be saved and toggle the automatic creation of an Assembly Definition.", "OK");
                }
                if (showSettings)
                {
                    testFolderPath = EditorGUILayout.TextField(new GUIContent("Test Folder Path", "Path to save tests."), testFolderPath);
                    if (GUILayout.Button(new GUIContent("Browse Folder", "Select folder to save tests.")))
                    {
                        string absolutePath = EditorUtility.OpenFolderPanel("Select Tests Folder", Application.dataPath, "");
                        if (!string.IsNullOrEmpty(absolutePath))
                        {
                            if (absolutePath.StartsWith(Application.dataPath))
                                testFolderPath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
                            else
                                Debug.LogWarning("Please select a folder inside your Assets folder.");
                        }
                    }
                    createAssemblyDefinition = EditorGUILayout.Toggle(new GUIContent("Create Assembly Definition", "Automatically generate an Assembly Definition for tests."), createAssemblyDefinition);
                }
    
                GUILayout.Space(20);
                showCustomGenerator = EditorGUILayout.Foldout(showCustomGenerator, new GUIContent("Custom Test Template Generator", "Generate custom or intelligent test templates."));
                if (GUILayout.Button("?", GUILayout.Width(25)))
                {
                    EditorUtility.DisplayDialog("Custom Test Template Generator Help",
                        "In this section, you can generate a custom test template by entering the test script name, optionally associating a GameObject, and describing what to test. You can also generate an intelligent template that automatically detects components and public methods.", "OK");
                }
                if (showCustomGenerator)
                {
                    EditorGUILayout.LabelField("Custom Test Template Settings", EditorStyles.boldLabel);
                    customTestScriptName = EditorGUILayout.TextField(new GUIContent("Test Script Name", "Name of the test script (class and file name)."), customTestScriptName);
                    customAsmdefName = EditorGUILayout.TextField(new GUIContent("Assembly Definition Name", "Name of the asmdef to be generated for tests."), customAsmdefName);
                    selectedGameObject = (GameObject)EditorGUILayout.ObjectField(new GUIContent("GameObject to Test", "Select the GameObject to test."), selectedGameObject, typeof(GameObject), true);
                    EditorGUILayout.LabelField(new GUIContent("Test Description", "Describe what you want to test."), EditorStyles.label);
                    customTestDescription = EditorGUILayout.TextArea(customTestDescription, GUILayout.Height(50));
    
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent("Generate Custom Test Template", "Generate a custom test template.")))
                    {
                        GenerateCustomTestTemplate();
                    }
                    if (GUILayout.Button(new GUIContent("Generate Intelligent Test Template", "Generate an intelligent test template (auto-detect components).")))
                    {
                        GenerateIntelligentTestTemplate();
                    }
                    GUILayout.EndHorizontal();
                }
    
                GUILayout.Space(20);
                showTestListRunner = EditorGUILayout.Foldout(showTestListRunner, new GUIContent("Test List Runner & Results", "Load, view, and execute tests from an external list."));
                if (GUILayout.Button("?", GUILayout.Width(25)))
                {
                    EditorUtility.DisplayDialog("Test List Runner Help",
                        "Load a file (.txt or .json) with a list of tests (names or categories) to run. You can then filter, view, and export the results.", "OK");
                }
                if (showTestListRunner)
                {
                    if (GUILayout.Button(new GUIContent("Load Test List", "Load test list from a file.")))
                    {
                        LoadTestList();
                    }
    
                    if (loadedTestFilters.Count > 0)
                    {
                        EditorGUILayout.LabelField("Loaded Test Filters:");
                        foreach (var filter in loadedTestFilters)
                        {
                            EditorGUILayout.LabelField("- " + filter);
                        }
                        if (GUILayout.Button(new GUIContent("Clear Loaded Tests", "Clear loaded test list.")))
                        {
                            loadedTestFilters.Clear();
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No tests loaded.");
                    }
    
                    if (GUILayout.Button(new GUIContent("Run Loaded Tests", "Run only tests with the loaded categories.")))
                    {
                        RunLoadedTests();
                    }
    
                    if (GUILayout.Button(new GUIContent("Clear Test Results", "Clear displayed test results.")))
                    {
                        testResults.Clear();
                    }
    
                    GUILayout.Space(10);
                    GUILayout.Label("Advanced Filters", EditorStyles.boldLabel);
                    testSearchFilter = EditorGUILayout.TextField(new GUIContent("Search Test Name", "Filter results by test name."), testSearchFilter);
                    selectedTestStateFilterIndex = EditorGUILayout.Popup(new GUIContent("Filter by State", "Filter results by test state."), selectedTestStateFilterIndex, testStateOptions);
    
                    List<TestResultInfo> filteredResults = new List<TestResultInfo>();
                    foreach (var result in testResults)
                    {
                        bool matchSearch = string.IsNullOrEmpty(testSearchFilter) || result.testName.ToLower().Contains(testSearchFilter.ToLower());
                        bool matchState = (testStateOptions[selectedTestStateFilterIndex] == "All") || result.resultState.ToLower().Contains(testStateOptions[selectedTestStateFilterIndex].ToLower());
                        if (matchSearch && matchState)
                            filteredResults.Add(result);
                    }
    
                    EditorGUILayout.LabelField("Test Results:");
                    resultsScrollPos = EditorGUILayout.BeginScrollView(resultsScrollPos, GUILayout.Height(200));
                    foreach (var result in filteredResults)
                    {
                        GUIStyle style = new GUIStyle(EditorStyles.label);
                        if (result.resultState.ToLower().Contains("passed"))
                            style.normal.textColor = Color.green;
                        else if (result.resultState.ToLower().Contains("failed"))
                            style.normal.textColor = Color.red;
                        else if (result.resultState.ToLower().Contains("skipped"))
                            style.normal.textColor = Color.gray;
                        else
                            style.normal.textColor = Color.yellow;
    
                        Rect rect = GUILayoutUtility.GetRect(new GUIContent(result.testName + " : " + result.resultState), style);
                        if (GUI.Button(rect, result.testName + " : " + result.resultState, style))
                        {
                        }
                        if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 && rect.Contains(Event.current.mousePosition))
                        {
                            if (result.resultState.ToLower().Contains("failed") && result.testName.StartsWith("CustomTest_"))
                            {
                                string goName = result.testName.Substring("CustomTest_".Length);
                                GameObject go = GameObject.Find(goName);
                                if (go != null)
                                {
                                    Selection.activeGameObject = go;
                                    SceneView.lastActiveSceneView.FrameSelected();
                                    Debug.Log("Focused on GameObject: " + go.name);
                                }
                            }
                        }
                    }
                    EditorGUILayout.EndScrollView();
    
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent("Export Results to JSON", "Preview and export results in JSON format.")))
                    {
                        ExportResultsToJSON();
                    }
                    if (GUILayout.Button(new GUIContent("Export Results to TXT", "Preview and export results in TXT format.")))
                    {
                        ExportResultsToTXT();
                    }
                    GUILayout.EndHorizontal();
    
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Discovered Tests", EditorStyles.boldLabel);
                    if (GUILayout.Button(new GUIContent("Discover Tests in Scene", "Automatically discover tests in the current scene based on their Category attribute.")))
                    {
                        DiscoverTestsInScene();
                    }
                    if (discoveredTests.Count > 0)
                    {
                        EditorGUILayout.LabelField("Detected Tests:");
                        foreach (var test in discoveredTests)
                        {
                            EditorGUILayout.LabelField("- " + test);
                        }
                        if (GUILayout.Button(new GUIContent("Run Discovered Tests", "Run only the tests discovered in the scene.")))
                        {
                            RunDiscoveredTests();
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No tests discovered in the current scene.");
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            else if (selectedTab == 1)
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
                    "This tool allows you to run tests for the current scene, generate custom or intelligent test templates, " +
                    "load and run tests from an external list, and view test results. It also features interactive overlays in the Scene View " +
                    "and displays test status icons in the Hierarchy.\n\n" +
                    "Usage:\n\n" +
                    "1. In the 'Tool' tab, you can:\n" +
                    "   - View current scene information and test coverage.\n" +
                    "   - Configure test settings, such as the folder for saving tests and the creation of an Assembly Definition.\n" +
                    "   - Generate custom test templates by specifying a test script name, selecting a GameObject, and providing a test description.\n" +
                    "   - Generate intelligent test templates that automatically detect components and public methods in the scene.\n" +
                    "   - Load a list of tests from a file and run tests filtered by loaded categories.\n" +
                    "   - Discover tests in the current scene based on the [Category] attribute and run them.\n" +
                    "   - View test results and double-click on a failed test result to auto-focus the corresponding GameObject.\n\n";
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
        }
    
        #region Helper Methods
    
        /// <summary>
        /// Checks if a test file exists for the current scene.
        /// </summary>
        private bool DoesTestTemplateExist(string sceneNameTemplate)
        {
            string fileName = sceneNameTemplate + "Tests.cs";
            string fullPath = Path.Combine(testFolderPath, fileName);
            return File.Exists(fullPath);
        }
    
        /// <summary>
        /// Creates an Assembly Definition file in the specified folder if it does not exist (unless disabled).
        /// </summary>
        private void EnsureAssemblyDefinitionExists(string folderPath, string asmdefFileName, string asmdefName)
        {
            if (!createAssemblyDefinition)
                return;
    
            string asmdefPath = Path.Combine(folderPath, asmdefFileName);
            if (!File.Exists(asmdefPath))
            {
                string asmdefContent =
    @"{
        ""name"": """ + asmdefName + @""",
        ""references"": [
            ""UnityEngine.TestRunner"",
            ""UnityEditor.TestRunner"",
            ""NUnit""
        ],
        ""includePlatforms"": [],
        ""excludePlatforms"": [],
        ""allowUnsafeCode"": false,
        ""overrideReferences"": false,
        ""precompiledReferences"": []
    }";
                File.WriteAllText(asmdefPath, asmdefContent);
                Debug.Log("Assembly Definition generated at " + asmdefPath);
            }
        }

        /// <summary>
        /// Runs tests using only the loaded categories.
        /// Only tests whose Category matches an entry in the loaded list will be executed.
        /// </summary>
        private void RunLoadedTests()
        {
            if (loadedTestFilters == null || loadedTestFilters.Count == 0)
            {
                Debug.LogWarning("No test filters loaded.");
                return;
            }
            TestRunnerApi api = CreateInstance<TestRunnerApi>();
            Filter filter = new Filter
            {
                categoryNames = loadedTestFilters.ToArray()
            };
            ExecutionSettings settings = new ExecutionSettings(filter);
            api.Execute(settings);
            Debug.Log("Running tests with categories: " + string.Join(", ", loadedTestFilters));
        }
    
        /// <summary>
        /// Generates a custom test template based on user input.
        /// </summary>
        private void GenerateCustomTestTemplate()
        {
            if (string.IsNullOrEmpty(customTestScriptName))
            {
                Debug.LogError("Test Script Name is required.");
                return;
            }
    
            if (!Directory.Exists(testFolderPath))
                Directory.CreateDirectory(testFolderPath);
    
            string asmdefFileName = string.IsNullOrEmpty(customAsmdefName) ? "GeneratedTests.asmdef" : customAsmdefName + ".asmdef";
            string asmdefName = string.IsNullOrEmpty(customAsmdefName) ? "GeneratedTests" : customAsmdefName;
            EnsureAssemblyDefinitionExists(testFolderPath, asmdefFileName, asmdefName);
    
            string fileName = customTestScriptName + ".cs";
            string fullPath = Path.Combine(testFolderPath, fileName);
            if (File.Exists(fullPath))
            {
                Debug.LogWarning("A test template with the name " + customTestScriptName + " already exists.");
                return;
            }
    
            string gameObjectName = selectedGameObject != null ? selectedGameObject.name : "TestObjectNotSpecified";
            string template = GetCustomTestTemplate(customTestScriptName, gameObjectName, customTestDescription);
            File.WriteAllText(fullPath, template);
            AssetDatabase.Refresh();
            Debug.Log("Custom test template generated: " + fullPath);
        }
    
        /// <summary>
        /// Returns a custom test template based on user input.
        /// The script name (customTestScriptName) is used as the Category.
        /// Modified to include both the scene name and the script name as categories.
        /// </summary>
        private string GetCustomTestTemplate(string scriptName, string gameObjectName, string testDescription)
        {
            string testMethodName = selectedGameObject != null ? "CustomTest_" + gameObjectName : "CustomTest";
            string playModeTestMethodName = selectedGameObject != null ? "CustomPlayModeTest_" + gameObjectName : "CustomPlayModeTest";
            return
    $@"using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using System.Collections;
    
    [Category(""{sceneName}"")]
    [Category(""{scriptName}"")]
    public class {scriptName}
    {{
        private GameObject testObject;
    
        [SetUp]
        public void Setup()
        {{
            testObject = GameObject.Find(""{gameObjectName}"");
            if(testObject == null)
            {{
                Debug.LogWarning(""GameObject not found: {gameObjectName}"");
            }}
        }}
    
        [Test]
        public void {testMethodName}()
        {{
            // Test Description:
            // {testDescription}
            Assert.Pass(""Custom test not implemented yet."");
        }}
    
        [UnityTest]
        public IEnumerator {playModeTestMethodName}()
        {{
            // Test Description:
            // {testDescription}
            yield return null;
            Assert.Pass();
        }}
    }}
    ";
        }
    
        /// <summary>
        /// Generates an intelligent test template that automatically detects components and public methods in the scene.
        /// </summary>
        private void GenerateIntelligentTestTemplate()
        {
            if (!Directory.Exists(testFolderPath))
                Directory.CreateDirectory(testFolderPath);
    
            string fileName = "IntelligentTests.cs";
            string fullPath = Path.Combine(testFolderPath, fileName);
            if (File.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog("Overwrite Intelligent Test Template?",
                    "A file named IntelligentTests.cs already exists. Do you want to overwrite it?", "Yes", "No"))
                    return;
            }
    
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using NUnit.Framework;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.TestTools;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine();
            sb.AppendLine("public class IntelligentTests");
            sb.AppendLine("{");
            sb.AppendLine("    [SetUp]");
            sb.AppendLine("    public void Setup()");
            sb.AppendLine("    {");
            sb.AppendLine("        // Global setup for intelligent tests");
            sb.AppendLine("    }");
            sb.AppendLine();
    
            MonoBehaviour[] monoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID);
            HashSet<string> processedTypes = new HashSet<string>();
    
            foreach (var mb in monoBehaviours)
            {
                if (mb == null) continue;
                System.Type type = mb.GetType();
                string typeName = type.Name;
                if (!processedTypes.Add(typeName))
                    continue;
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (methods.Length == 0)
                    continue;
                sb.AppendLine("    // Tests for component: " + typeName);
                foreach (var method in methods)
                {
                    if (method.IsSpecialName) continue;
                    string methodName = method.Name;
                    sb.AppendLine("    [Test]");
                    sb.AppendLine("    public void Test_" + typeName + "_" + methodName + "()");
                    sb.AppendLine("    {");
                    sb.AppendLine("        // TODO: Implement test for " + typeName + "." + methodName);
                    sb.AppendLine("        Assert.Pass(\"Not implemented yet\");");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                }
            }
            sb.AppendLine("}");
            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log("Intelligent test template generated at " + fullPath);
        }
    
        #endregion
    
        private void OnSceneGUIOverlay(SceneView sceneView)
        {
            Handles.BeginGUI();
            foreach (var result in testResults)
            {
                if (result.testName.StartsWith("CustomTest_"))
                {
                    string goName = result.testName.Substring("CustomTest_".Length);
                    GameObject go = GameObject.Find(goName);
                    if (go != null)
                    {
                        Vector3 worldPos = go.transform.position;
                        Vector2 guiPoint = HandleUtility.WorldToGUIPoint(worldPos);
                        Color labelColor = Color.yellow;
                        if (result.resultState.ToLower().Contains("passed"))
                            labelColor = Color.green;
                        else if (result.resultState.ToLower().Contains("failed"))
                            labelColor = Color.red;
                        
                        string tooltip = $"Test: {result.testName}\nStatus: {result.resultState}";
                        if (!string.IsNullOrEmpty(result.message))
                            tooltip += "\nError: " + result.message;
                        
                        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal =
                            {
                                textColor = labelColor
                            }
                        };

                        Rect buttonRect = new Rect(guiPoint.x, guiPoint.y, 150, 20);
                        EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
                        GUI.Button(buttonRect, new GUIContent(goName, tooltip), style);
                        
                        if (buttonRect.Contains(Event.current.mousePosition))
                        {
                            if (Event.current.type == EventType.MouseDown)
                            {
                                if (Event.current.button == 0)
                                {
                                    Selection.activeGameObject = go;
                                    EditorGUIUtility.PingObject(go);
                                    Event.current.Use();
                                }
                                else if (Event.current.button == 1)
                                {
                                    GenericMenu menu = new GenericMenu();
                                    menu.AddItem(new GUIContent("Run this test"), false, () => RunSingleTest(result));
                                    menu.AddItem(new GUIContent("View test details"), false, () => ViewTestDetails(result));
                                    menu.AddItem(new GUIContent("Open test script"), false, () => OpenTestScript(result));
                                    menu.ShowAsContext();
                                    Event.current.Use();
                                }
                            }
                        }
                    }
                }
            }
            Handles.EndGUI();
        }
    
        private void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (go == null) return;
            bool hasTest = false;
            bool hasFailed = false;
            bool hasPassed = false;
            foreach (var result in testResults)
            {
                if(result.testName.StartsWith("CustomTest_"))
                {
                    string targetName = result.testName.Substring("CustomTest_".Length);
                    if(targetName == go.name)
                    {
                        hasTest = true;
                        if(result.resultState.ToLower().Contains("failed"))
                            hasFailed = true;
                        else if(result.resultState.ToLower().Contains("passed"))
                            hasPassed = true;
                    }
                }
            }
            if (!hasTest) return;
            Texture icon = null;
            if (hasFailed)
            {
                icon = EditorGUIUtility.IconContent("sv_icon_dot5_pix16_gizmo").image;
            }
            else if (hasPassed)
            {
                icon = EditorGUIUtility.IconContent("sv_icon_dot0_pix16_gizmo").image;
            }
            if(icon != null)
            {
                Rect r = new Rect(selectionRect);
                r.x = r.xMax - 18;
                r.width = 16;
                GUI.Label(r, icon);
            }
        }
    
        private void RunSingleTest(TestResultInfo result)
        {
            Debug.Log("Running test: " + result.testName);
            TestRunnerApi api = CreateInstance<TestRunnerApi>();
            Filter filter = new Filter
            {
                testNames = new[] { result.testName }
            };
            ExecutionSettings settings = new ExecutionSettings(filter);
            api.Execute(settings);
        }
    
        private void ViewTestDetails(TestResultInfo result)
        {
            EditorUtility.DisplayDialog("Test Details", "Test: " + result.testName + "\nStatus: " + result.resultState + "\nMessage: " + result.message, "OK");
        }
    
        private void OpenTestScript(TestResultInfo result)
        {
            string[] guids = AssetDatabase.FindAssets(result.testName + " t:Script");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null)
                {
                    AssetDatabase.OpenAsset(script);
                }
                else
                {
                    Debug.LogWarning("Could not load test script for " + result.testName);
                }
            }
            else
            {
                Debug.LogWarning("Test script not found for " + result.testName);
            }
        }
    
        [System.Serializable]
        private class TestResultInfo
        {
            public string testName;
            public string resultState;
            public string message;
        }
    
        [System.Serializable]
        private class TestResultListWrapper
        {
            [field: FormerlySerializedAs("results")]
            public List<TestResultInfo> ResultsInfo { get; set; }
        }
    
        /// <summary>
        /// Exports test results as JSON.
        /// Opens a preview window to edit the content before saving.
        /// </summary>
        private void ExportResultsToJSON()
        {
            TestResultListWrapper wrapper = new TestResultListWrapper
            {
                ResultsInfo = testResults
            };
            string json = JsonUtility.ToJson(wrapper, true);
            ExportPreviewWindow.ShowWindow(json, "json");
        }
    
        /// <summary>
        /// Exports test results as TXT.
        /// Opens a preview window to edit the content before saving.
        /// </summary>
        private void ExportResultsToTXT()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var result in testResults)
            {
                sb.AppendLine(result.testName + " : " + result.resultState);
            }
            ExportPreviewWindow.ShowWindow(sb.ToString(), "txt");
        }
    
        void ICallbacks.RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log("Test Run Started");
            testResults.Clear();
        }
    
        void ICallbacks.RunFinished(ITestResultAdaptor result)
        {
            Debug.Log("Test Run Finished. Result: " + result.ResultState);
        }
    
        void ICallbacks.TestStarted(ITestAdaptor test)
        {
            Debug.Log("Test Started: " + test.Name);
        }
    
        void ICallbacks.TestFinished(ITestResultAdaptor result)
        {
            Debug.Log("Test Finished: " + result.Name + " - " + result.ResultState);
            TestResultInfo info = new TestResultInfo
            {
                testName = result.Name,
                resultState = result.ResultState,
                message = result.Message
            };
            testResults.Add(info);
        }
    }
}
#endif

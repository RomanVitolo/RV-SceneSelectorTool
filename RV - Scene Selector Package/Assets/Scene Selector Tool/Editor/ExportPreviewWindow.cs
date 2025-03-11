#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RV_SceneSelectorTool.Editor
{
    public class ExportPreviewWindow : EditorWindow
    {
        private string previewContent = "";
        private string fileExtension = "json";
        private Vector2 scrollPos;

        public static void ShowWindow(string initialContent, string fileExtension)
        {
            ExportPreviewWindow window = GetWindow<ExportPreviewWindow>("Export Preview");
            window.previewContent = initialContent;
            window.fileExtension = fileExtension;
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Preview Export", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            previewContent = EditorGUILayout.TextArea(previewContent, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                string defaultName = "TestResults." + fileExtension;
                string path = EditorUtility.SaveFilePanel("Save Exported Results", "", defaultName, fileExtension);
                if (!string.IsNullOrEmpty(path))
                {
                    File.WriteAllText(path, previewContent);
                    Debug.Log("Test results exported to " + path);
                    Close();
                }
            }
            if (GUILayout.Button("Cancel"))
                Close();
            
            GUILayout.EndHorizontal();
        }
    }
}
#endif
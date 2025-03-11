#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace RV_SceneSelectorTool.Editor
{
    public static class PreferencesManager
    {
        private const string favoritesKey = "RV_SceneSelectorTool.Favorites";
        private const string historyKey = "RV_SceneSelectorTool.SceneHistory";

        public static List<string> LoadFavorites()
        {
            string favStr = EditorPrefs.GetString(favoritesKey, "");
            return string.IsNullOrEmpty(favStr) ? new List<string>() : favStr.Split('|').ToList();
        }

        public static void SaveFavorites(List<string> favorites)
        {
            EditorPrefs.SetString(favoritesKey, string.Join("|", favorites));
        }

        public static List<string> LoadHistory()
        {
            string histStr = EditorPrefs.GetString(historyKey, "");
            return string.IsNullOrEmpty(histStr) ? new List<string>() : histStr.Split('|').ToList();
        }

        public static void SaveHistory(List<string> history)
        {
            EditorPrefs.SetString(historyKey, string.Join("|", history));
        }

        private static string GetKey(string category, string scenePath)
        {
            return "RV_SceneSelectorTool." + category + "_" + scenePath.Replace("/", "_").Replace(".", "_");
        }

        public static void UpdateKey(string category, string oldPath, string newPath, bool isInt = false)
        {
            string oldKey = GetKey(category, oldPath);
            if (EditorPrefs.HasKey(oldKey))
            {
                if (isInt)
                {
                    int value = EditorPrefs.GetInt(oldKey, 0);
                    EditorPrefs.DeleteKey(oldKey);
                    string newKey = GetKey(category, newPath);
                    EditorPrefs.SetInt(newKey, value);
                }
                else
                {
                    string value = EditorPrefs.GetString(oldKey);
                    EditorPrefs.DeleteKey(oldKey);
                    string newKey = GetKey(category, newPath);
                    EditorPrefs.SetString(newKey, value);
                }
            }
        }
    }
}
#endif
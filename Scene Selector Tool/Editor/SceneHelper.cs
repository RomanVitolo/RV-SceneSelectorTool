#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace RV_SceneSelectorTool.Editor
{
    public static class SceneHelper
    {
        public static List<string> LoadAllScenes()
        {
            var allScenePaths = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:Scene");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Packages/"))
                    allScenePaths.Add(path);
            }
            return allScenePaths;
        }

        public static void ValidateSceneLists(List<string> favorites, List<string> history)
        {
            bool updated = false;
            for (int i = favorites.Count - 1; i >= 0; i--)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(favorites[i]) == null)
                {
                    favorites.RemoveAt(i);
                    updated = true;
                }
            }
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(history[i]) == null)
                {
                    history.RemoveAt(i);
                    updated = true;
                }
            }
            if (updated)
            {
                PreferencesManager.SaveFavorites(favorites);
                PreferencesManager.SaveHistory(history);
            }
        }
    }
}
#endif
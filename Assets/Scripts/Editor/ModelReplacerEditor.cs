using UnityEngine;
using UnityEditor;

namespace BreathCasino.Gameplay
{
    [CustomEditor(typeof(ModelReplacer))]
    public class ModelReplacerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ModelReplacer replacer = (ModelReplacer)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Replace with Model"))
            {
                replacer.ReplaceWithModel();
                EditorUtility.SetDirty(replacer);
            }

            if (GUILayout.Button("Restore Blockout"))
            {
                replacer.RestoreBlockout();
                EditorUtility.SetDirty(replacer);
            }
        }
    }

    public class ModelReplacerTools : EditorWindow
    {
        [MenuItem("Breath Casino/Model Replacer/Replace All in Scene")]
        public static void ReplaceAllInScene()
        {
            ModelReplacer[] replacers = FindObjectsByType<ModelReplacer>(FindObjectsSortMode.None);
            int count = 0;

            foreach (var replacer in replacers)
            {
                replacer.ReplaceWithModel();
                count++;
            }

            Debug.Log($"[ModelReplacer] Replaced {count} objects in scene");
        }

        [MenuItem("Breath Casino/Model Replacer/Restore All in Scene")]
        public static void RestoreAllInScene()
        {
            ModelReplacer[] replacers = FindObjectsByType<ModelReplacer>(FindObjectsSortMode.None);
            int count = 0;

            foreach (var replacer in replacers)
            {
                replacer.RestoreBlockout();
                count++;
            }

            Debug.Log($"[ModelReplacer] Restored {count} blockout objects in scene");
        }
    }
}
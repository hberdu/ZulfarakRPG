using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;

// Tools > ZulfarakRPG > Fix Input Module
// Ensures StandaloneInputModule is on every EventSystem in the project scenes.
public static class FixInputModule
{
    [MenuItem("Tools/ZulfarakRPG/Fix Input Module")]
    public static void Fix()
    {
        string[] scenePaths = {
            "Assets/Scenes/Bootstrap.unity",
            "Assets/Scenes/CharacterCreation.unity",
            "Assets/Scenes/Zulfarak.unity"
        };

        foreach (var path in scenePaths)
        {
            if (!System.IO.File.Exists(Application.dataPath + "/../" + path))
            {
                Debug.LogWarning($"[ZulfarakRPG] Cena nao encontrada: {path}");
                continue;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var es    = Object.FindAnyObjectByType<EventSystem>();
            if (es == null) { Debug.LogWarning($"[ZulfarakRPG] Nao ha EventSystem em {path}"); continue; }

            // Remove any non-standard input modules via component type name (avoids package dependency)
            foreach (var comp in es.gameObject.GetComponents<BaseInputModule>())
            {
                string typeName = comp.GetType().Name;
                if (typeName != nameof(StandaloneInputModule))
                    Object.DestroyImmediate(comp);
            }

            if (es.GetComponent<StandaloneInputModule>() == null)
                es.gameObject.AddComponent<StandaloneInputModule>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ZulfarakRPG] Input module corrigido em {path}");
        }

        Debug.Log("[ZulfarakRPG] Fix concluido!");
    }
}

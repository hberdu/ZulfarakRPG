using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Keeps every map's / dungeon's portal at the SAME size and height as the first map (Zulfarak),
    // so per-scene authoring can't drift. Runs on gameplay scene load (same pattern as MapBounds /
    // BackgroundLayers). X is left as authored — entries sit on one side, exits on the other — only
    // scale and vertical height are unified.
    public static class PortalNormalizer
    {
        // Zulfarak's authored portal transform — the canonical "first map" look.
        const float PortalScale = 0.8f;
        const float PortalY     = -0.025f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!MapBounds.IsGameplayScene(scene.name)) return;
            foreach (var p in Object.FindObjectsByType<Portal2D>(FindObjectsSortMode.None))
            {
                if (p == null) continue;
                p.transform.localScale = new Vector3(PortalScale, PortalScale, 1f);
                var pos = p.transform.position;
                p.transform.position = new Vector3(pos.x, PortalY, pos.z);
            }
        }
    }
}

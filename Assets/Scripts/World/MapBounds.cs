using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Single source of truth for the playable map WIDTH. Every hub (city + settlements) and every
    // dungeon shares the SAME horizontal bounds, enforced at runtime so per-scene authoring can't
    // drift. Also the one place that decides which scenes are "gameplay" scenes — used by all the
    // world-dressing hooks (GroundDressing, BackgroundLayers, GroundFloorEnsurer, PortalSmoke) so
    // they apply to every map uniformly instead of only Zulfarak/Dungeon.
    public static class MapBounds
    {
        // The playable X range, identical for all maps (matches the value every scene authored).
        public const float MinX = 0.45f;
        public const float MaxX = 4.55f;

        public static float Width   => MaxX - MinX;
        public static float CenterX => (MinX + MaxX) * 0.5f;

        public static bool IsGameplayScene(string sceneName)
            => sceneName == "Zulfarak" || sceneName == "Dungeon"
               || sceneName.StartsWith("Camp_") || sceneName.StartsWith("Dungeon_");

        // Dungeons scroll their backdrop as the hero marches; city/camp hubs keep it static.
        public static bool IsDungeonScene(string sceneName)
            => sceneName == "Dungeon" || sceneName.StartsWith("Dungeon_");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsGameplayScene(scene.name)) return;

            // Pin the player's movement clamp to the shared width so every map plays identically,
            // regardless of what its Player was authored with.
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            if (player != null)
            {
                player.sceneBoundsMinX = MinX;
                player.sceneBoundsMaxX = MaxX;
            }
        }
    }
}

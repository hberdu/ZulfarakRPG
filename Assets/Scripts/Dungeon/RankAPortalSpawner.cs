using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Spawns the RED (rank-A) portal in the city map Camp_4_1 — NOT in a dungeon. It's a normal
    // Portal2D with rankA = true, so it reuses the exact same art / glow rings / tooltip as the
    // purple portals, just tinted red (see Portal2D.RankARingColors). Runs on scene load like
    // PortalNormalizer, so no fragile hand-editing of the .unity scene is needed.
    public static class RankAPortalSpawner
    {
        // ── Config — the red portal transitions into this dungeon scene as a MINOTAUR RUN
        // (MinotaurArena suppresses the normal waves and spawns the boss there instead). ──
        const string CityScene        = "Camp_4_1";
        const string DestinationScene = "Dungeon_4_1";  // reused as the Minotaur arena.
        const string TooltipText      = "MINOTAURO";
        const float  OffsetXFromPurple = -3.5f;          // sits a few units from the normal portal.
        const float  FallbackX         = -3.5f;          // used if the city has no portal to anchor to.

        // Match PortalNormalizer's canonical portal transform so the red portal reads identical.
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
            if (scene.name != CityScene) return;

            // Skip if a red portal is already present (authored or spawned), and remember a normal
            // portal to anchor the X position to.
            Portal2D anchor = null;
            foreach (var p in Object.FindObjectsByType<Portal2D>(FindObjectsSortMode.None))
            {
                if (p == null) continue;
                if (p.rankA || p.minotaurRun) return;
                anchor = p;
            }

            float x = anchor != null ? anchor.transform.position.x + OffsetXFromPurple : FallbackX;

            var go = new GameObject("RankAPortal");
            go.transform.position   = new Vector3(x, PortalY, 0f);
            go.transform.localScale = new Vector3(PortalScale, PortalScale, 1f);
            go.AddComponent<CircleCollider2D>();   // Portal2D.Start turns this into its trigger.

            var portal = go.AddComponent<Portal2D>();
            portal.minotaurRun      = true;         // red rings + TRANSITIONS to the Minotaur arena
            portal.destinationScene = DestinationScene;
            portal.tooltipText      = TooltipText;
            portal.openOnStart      = true;
        }
    }
}

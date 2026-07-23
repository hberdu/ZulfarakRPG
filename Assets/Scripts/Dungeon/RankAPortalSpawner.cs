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
        const string TooltipText      = "SS";
        // Stands just IN FRONT of the dungeon portal (to its left, the side the hero walks in
        // from) rather than off across the map.
        const float  OffsetXFromPurple = -1.1f;
        const float  FallbackX         = -3.5f;          // used if the city has no portal to anchor to.

        // Match PortalNormalizer's canonical portal transform so the red portal reads identical.
        // Only used when there's no dungeon portal to copy from — normally the anchor's own
        // transform wins, which is what actually guarantees "same art, different colour".
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
            // Copy the dungeon portal's OWN transform so the two are the same object visually —
            // only the ring colours differ. Constants are the fallback for a city with no portal.
            float y     = anchor != null ? anchor.transform.position.y      : PortalY;
            float scale = anchor != null ? anchor.transform.localScale.x    : PortalScale;

            var go = new GameObject("RankAPortal");
            go.transform.position   = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.AddComponent<CircleCollider2D>();   // Portal2D.Start turns this into its trigger.

            var portal = go.AddComponent<Portal2D>();
            // The glow CORE, not just the rings. Portal2D only builds the outer rings itself, so
            // without a glowSprite the red portal drew as a hollow outline while the purple ones
            // (which carry an authored core) looked solid — the two read as different art.
            portal.glowSprite = BuildCore(go, anchor);
            portal.minotaurRun      = true;         // red rings + TRANSITIONS to the Minotaur arena
            portal.destinationScene = DestinationScene;
            portal.tooltipText      = TooltipText;
            portal.openOnStart      = true;
        }

        // Red twin of the dungeon portal's glow core: same sprite the anchor uses (so it is
        // literally the same art), tinted red. Portal2D.Start fills in a procedural ring if the
        // anchor had none. Shared with MinotaurArena's exit portal.
        public static SpriteRenderer BuildCore(GameObject go, Portal2D anchor)
        {
            var core = go.AddComponent<SpriteRenderer>();
            core.sortingOrder = 2;
            core.color        = new Color(1f, 0.45f, 0.40f, 1f);
            if (anchor != null && anchor.glowSprite != null) core.sprite = anchor.glowSprite.sprite;
            return core;
        }
    }
}

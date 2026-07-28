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

        // ── Visible extent ────────────────────────────────────────────────────────────────
        // The play area above is only the hero's CLAMP. On this very wide overlay strip the
        // camera sees a good deal more than that, and it pans with the hero, so anything that
        // dresses or populates the world has to cover the visible window instead of the clamp:
        //   • scenery scattered only over [MinX, MaxX] stopped short of the right edge;
        //   • scenery recycled at MaxX+1 wrapped around while still on screen, which is what made
        //     the map look like it was shrinking as the party advanced;
        //   • enemies spawned at MaxX-0.3 appeared on top of a party that had walked right.
        public static float ViewHalfWidth
        {
            get
            {
                var cam = Camera.main;
                if (cam == null || !cam.orthographic) return Width * 0.5f;
                // CLAMPED. World dressing is built on sceneLoaded, which can run BEFORE the overlay
                // has resized the camera to the game strip — at Unity's default orthographicSize of
                // 5 on this very wide aspect that reads as a ~27 unit half-width, and the scenery
                // band blew up to ~60 units: the same prop count spread over 6× the ground, so the
                // map looked bare and the scroller's wrap-around was far off-screen, which read as
                // "the hero walks but nothing moves". One play-width of margin is plenty.
                return Mathf.Clamp(cam.orthographicSize * cam.aspect, Width * 0.5f, Width);
            }
        }

        // Band that world dressing must fill: the play area plus a full screen of margin each
        // side, so the camera can never pan onto bare ground or catch a piece recycling.
        public static float DressMinX => MinX - ViewHalfWidth - 1f;
        public static float DressMaxX => MaxX + ViewHalfWidth + 1f;

        // Just past the RIGHT edge of what the camera currently shows — where enemies should
        // enter from, so they always stalk in from off-screen instead of popping onto the party.
        // BOUNDED. Far enough right to be out of sight, but never so far that the walk-in takes
        // forever — anything spawned out here still has to reach the party for the fight to end.
        public static float OffscreenRightX
        {
            get
            {
                var cam = Camera.main;
                float camX = cam != null ? cam.transform.position.x : CenterX;
                return Mathf.Clamp(camX + ViewHalfWidth + 0.8f, MaxX + 0.5f, MaxX + 2.5f);
            }
        }

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

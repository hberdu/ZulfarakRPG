using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // "Rank-A" run: the red portal in the city transitions into a normal dungeon scene but flags
    // it as a MINOTAUR RUN. On load we suppress the normal wave spawner and instead run a single
    // boss fight against the Minotaur, then drop a purple portal back to the city when it dies.
    // Reuses an existing dungeon scene as the arena (no new .unity file needed).
    public static class MinotaurArena
    {
        // Set by Portal2D right before it loads the arena scene (minotaurRun portal).
        public static bool Pending;

        const string ReturnCity = "Camp_4_1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Pending) return;
            if (!MapBounds.IsGameplayScene(scene.name)) return;
            Pending = false;

            // Kill the normal wave spawner BEFORE its Start runs (sceneLoaded fires after Awake,
            // before Start — a disabled component's Start is deferred, so no waves ever spawn).
            var wm = WaveManager.Instance ?? Object.FindAnyObjectByType<WaveManager>();
            if (wm != null) wm.enabled = false;

            var go = new GameObject("MinotaurArenaRunner");
            go.AddComponent<MinotaurArenaRunner>();
        }

        internal static string City => ReturnCity;
    }

    // Drives the single-boss fight on the Unity main thread.
    internal class MinotaurArenaRunner : MonoBehaviour
    {
        IEnumerator Start()
        {
            // Clear any stray enemies the (now-disabled) spawner or scene authored.
            foreach (var e in Object.FindObjectsByType<SkeletonEnemy>(FindObjectsSortMode.None))
                if (e != null) Destroy(e.gameObject);

            yield return new WaitForSeconds(0.8f);
            PixelBanner.Show("BOSS", new Color(0.95f, 0.20f, 0.16f));
            yield return new WaitForSeconds(1.2f);

            // Spawn OFF-SCREEN to the right of the camera so the boss stalks in. MaxX - 0.3 is
            // inside the visible window whenever the party has walked right, which is why the
            // Minotaur kept appearing right on top of them.
            float groundY = GroundAlignUtil.FindGroundTopY();
            // ON the ground line — SkeletonEnemy.Start re-seats by the alpha-trimmed feet, and the
            // old +0.5 was a head start the seating clamp could not fully undo (it floated).
            var spawn = new Vector3(MapBounds.OffscreenRightX, groundY, 0f);
            var boss = MinotaurBoss.Spawn(spawn);

            // Wait until the boss is dead (its GameObject is destroyed on death → Unity null).
            while (boss != null) yield return null;

            // The Minotaur IS the whole phase: no follow-up wave, no second boss. The moment it
            // drops, the run is CLEARed exactly like a normal dungeon and the party walks home
            // through the usual purple portal.
            yield return new WaitForSeconds(0.6f);
            PixelBanner.Show("CLEAR", new Color(1f, 0.85f, 0.30f));
            yield return new WaitForSeconds(1.2f);

            var portal = SpawnExitPortal();
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            // WalkToPortal (not Celebrate) — Celebrate routes through WaveManager.OnCelebrationDone,
            // and this arena deliberately disabled the wave manager.
            player?.WalkToPortal(portal.transform.position, MinotaurArena.City);
            Destroy(gameObject);
        }

        // The ONE way out: the dungeon's CHARACTERISTIC portal — same art, same canonical transform
        // and the normal purple rings (not the red rank-A ones), sat at the far-LEFT edge so the
        // party visibly walks back past the arena to it. Any portal the arena scene authored is
        // removed first so no red/stale portal lingers after the fight.
        static Portal2D SpawnExitPortal()
        {
            // Capture the authored art BEFORE destroying, so the new portal is literally the same
            // sprite the dungeon portals use.
            Sprite coreSprite = null;
            float  y = PortalY, scale = PortalScale;
            foreach (var p in Object.FindObjectsByType<Portal2D>(FindObjectsSortMode.None))
            {
                if (p == null) continue;
                if (coreSprite == null && p.glowSprite != null) coreSprite = p.glowSprite.sprite;
                y     = p.transform.position.y;
                scale = p.transform.localScale.x;
                Destroy(p.gameObject);
            }

            var go = new GameObject("MinotaurExitPortal");
            go.transform.position   = new Vector3(MapBounds.MinX, y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.AddComponent<CircleCollider2D>();

            // Glow core (Portal2D only builds the outer rings itself — without a core the portal
            // reads as a hollow outline instead of the solid dungeon portal).
            var core = go.AddComponent<SpriteRenderer>();
            core.sortingOrder = 2;
            core.color        = new Color(0.95f, 0.88f, 1f, 1f);   // white-violet, the purple family
            core.sprite       = coreSprite;

            var portal = go.AddComponent<Portal2D>();
            portal.glowSprite       = core;
            portal.rankA            = false;   // plain scene transition, purple rings
            portal.minotaurRun      = false;
            portal.forceRedRings    = false;
            portal.destinationScene = MinotaurArena.City;
            portal.tooltipText      = "SAIR";
            portal.openOnStart      = true;
            return portal;
        }

        // PortalNormalizer's canonical dungeon-portal transform (fallback when the arena scene
        // authored no portal to copy from).
        const float PortalScale = 0.8f;
        const float PortalY     = -0.025f;
    }
}

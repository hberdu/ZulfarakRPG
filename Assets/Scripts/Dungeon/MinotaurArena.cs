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
            PixelBanner.Show("MINOTAURO", new Color(0.95f, 0.20f, 0.16f));
            yield return new WaitForSeconds(1.2f);

            // Spawn on the far side so the boss stalks in (same spot the in-place summon used).
            float groundY = GroundAlignUtil.FindGroundTopY();
            // ON the ground line — SkeletonEnemy.Start re-seats by the alpha-trimmed feet, and the
            // old +0.5 was a head start the seating clamp could not fully undo (it floated).
            var spawn = new Vector3(MapBounds.MaxX - 0.3f, groundY, 0f);
            var boss = MinotaurBoss.Spawn(spawn);

            // Wait until the boss is dead (its GameObject is destroyed on death → Unity null).
            while (boss != null) yield return null;

            yield return new WaitForSeconds(0.6f);
            SpawnExitPortal(groundY);
            Destroy(gameObject);
        }

        // The ONE way out: a single normal (purple) return portal. Any other portal the arena
        // authored is removed first, and it is NOT red — no red portal lingers after the fight.
        static void SpawnExitPortal(float groundY)
        {
            Portal2D anchor = null;
            foreach (var p in Object.FindObjectsByType<Portal2D>(FindObjectsSortMode.None))
            {
                if (p == null) continue;
                anchor = p;                       // keep one to copy the art/transform from
                Destroy(p.gameObject);
            }

            float x     = Mathf.Clamp(0f, MapBounds.MinX + 1f, MapBounds.MaxX - 1f);
            float y     = anchor != null ? anchor.transform.position.y   : -0.025f;
            float scale = anchor != null ? anchor.transform.localScale.x : 0.8f;

            var go = new GameObject("MinotaurExitPortal");
            go.transform.position   = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.AddComponent<CircleCollider2D>();
            var portal = go.AddComponent<Portal2D>();
            portal.rankA            = false;               // plain scene transition
            portal.minotaurRun      = false;
            portal.destinationScene = MinotaurArena.City;
            portal.tooltipText      = "SAIR";
            portal.openOnStart      = true;
        }
    }
}

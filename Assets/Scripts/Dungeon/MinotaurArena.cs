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
            var spawn = new Vector3(MapBounds.MaxX - 0.3f, groundY + 0.5f, 0f);
            var boss = MinotaurBoss.Spawn(spawn);

            // Wait until the boss is dead (its GameObject is destroyed on death → Unity null).
            while (boss != null) yield return null;

            yield return new WaitForSeconds(0.6f);
            SpawnExitPortal(groundY);
            Destroy(gameObject);
        }

        // Purple portal back to the city, dropped once the Minotaur falls.
        static void SpawnExitPortal(float groundY)
        {
            float x = Mathf.Clamp(0f, MapBounds.MinX + 1f, MapBounds.MaxX - 1f);
            var go = new GameObject("MinotaurExitPortal");
            go.transform.position   = new Vector3(x, -0.025f, 0f);
            go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            go.AddComponent<CircleCollider2D>();
            var portal = go.AddComponent<Portal2D>();
            portal.rankA            = false;               // purple = leave.
            portal.destinationScene = MinotaurArena.City;
            portal.openOnStart      = true;
        }
    }
}

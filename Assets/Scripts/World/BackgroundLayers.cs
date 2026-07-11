using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // OVERLAY AESTHETIC: the game runs as a transparent, click-through window over the live
    // desktop (see OverlayWindow — alpha-0 camera clear + DwmExtendFrameIntoClientArea). There
    // is NO scenic backdrop plane — the desktop shows through. But the three parallax layers
    // (ParallaxFar/Mid/Near) ARE kept: they carry the props/objects (buildings, walls, columns,
    // trees, rocks, vases) that dress the scene over the see-through desktop.
    //
    // Runs at runtime, so it takes effect regardless of what the scene assets contain.
    public class BackgroundLayers : MonoBehaviour
    {
        // WaveManager advances this during the inter-wave run; ParallaxLayer.Scroll uses it to
        // drift the prop layers so the world scrolls as the hero marches.
        public static float DungeonScroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool gameplay = scene.name == "Zulfarak" || scene.name == "Dungeon";
            if (!gameplay) return;

            DungeonScroll = 0f;

            // Remove any scenic backdrop a previous version parented to the camera, so the
            // camera's transparent clear shows the desktop behind the props/characters. The
            // parallax prop layers are left alone (they render over the transparent desktop).
            var cam = Camera.main;
            if (cam != null)
            {
                var prev = cam.transform.Find("__Background");
                if (prev != null) Destroy(prev.gameObject);
            }
        }
    }
}

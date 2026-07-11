using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // OVERLAY AESTHETIC: the game runs as a transparent, click-through window over the live
    // desktop (see OverlayWindow — alpha-0 camera clear + DwmExtendFrameIntoClientArea). So
    // there is NO scenic background at all: on every gameplay scene load we tear down any
    // backdrop that was ever built and switch OFF the dungeon's scrolling parallax tree
    // layers, leaving ONLY the ground, object/prop sprites and characters over the see-through
    // desktop.
    //
    // Runs at runtime, so it takes effect regardless of what the scene assets contain.
    public class BackgroundLayers : MonoBehaviour
    {
        // The scrolling parallax "background tree" layers authored in the Dungeon scene.
        static readonly string[] ParallaxLayerNames = { "ParallaxFar", "ParallaxMid", "ParallaxNear" };

        // Vestigial: WaveManager still advances this during the inter-wave run. Nothing reads
        // it anymore (there's no backdrop to drift), but keeping the field compiles that call.
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

            // 1) Remove any scenic backdrop a previous version parented to the camera, so the
            //    camera's transparent clear shows the desktop behind everything.
            var cam = Camera.main;
            if (cam != null)
            {
                var prev = cam.transform.Find("__Background");
                if (prev != null) Destroy(prev.gameObject);
            }

            // 2) Silence the dungeon's scrolling parallax tree layers. sceneLoaded fires BEFORE
            //    their Start(), so they never spawn a tree. Clearing the sprite pool also makes
            //    WaveManager.Scroll() early-return (so it never touches the un-Started layer's
            //    RNG), then we switch the layer off entirely. (No-op in the city — no parallax.)
            foreach (var n in ParallaxLayerNames)
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                var pl = go.GetComponent<ParallaxLayer>();
                if (pl != null) pl.sprites = new Sprite[0];
                go.SetActive(false);
            }
        }
    }
}

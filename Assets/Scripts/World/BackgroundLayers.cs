using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // 1. Builds a complete scenic BACKDROP from the GandalfHardcore parallax background
    //    layers (Resources/CityBg), stacked back→front and parented to the camera so it
    //    stays fixed in view and fills the black area behind the trees (city + dungeon).
    // 2. In the DUNGEON, re-dresses the existing scrolling ParallaxLayer objects with
    //    Gandalf trees/statues (occasional + widely spaced) instead of the old fragment
    //    art — WITHOUT disabling them, so WaveManager's wave-to-wave scroll still works.
    //
    // Runs at runtime, so it survives scene re-saves.
    public class BackgroundLayers : MonoBehaviour
    {
        static readonly string[] LayerRes = { "layer5", "castle", "layer4", "layer3", "layer2", "layer1" };
        const int   BaseSort = -22;
        const float Margin   = 1.50f;   // overscan so no black edges show while it drifts

        // Extra sideways scroll driven by WaveManager during the dungeon's inter-wave run,
        // so the backdrop drifts there too (the player stays put in the dungeon).
        public static float DungeonScroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool city    = scene.name == "Zulfarak";
            bool dungeon = scene.name == "Dungeon";
            if (!city && !dungeon) return;
            var cam = Camera.main;
            if (cam == null) return;

            DungeonScroll = 0f;
            BuildBackdrop(cam, parallax: true);   // moving backdrop in both scenes
            if (dungeon) DressParallax();
        }

        // Fixed-to-camera scenic backdrop (rebuilt each load). When parallax is on (city),
        // each layer drifts at its own speed as the player walks, so it reads as depth.
        static void BuildBackdrop(Camera cam, bool parallax)
        {
            var prev = cam.transform.Find("__Background");
            if (prev != null) Destroy(prev.gameObject);
            var root = new GameObject("__Background");
            root.transform.SetParent(cam.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, 10f);   // world z ≈ 0, in front of the camera

            float viewH = cam.orthographicSize * 2f;
            float viewW = viewH * cam.aspect;

            for (int i = 0; i < LayerRes.Length; i++)
            {
                var sprite = Resources.Load<Sprite>("CityBg/" + LayerRes[i]);
                if (sprite == null) continue;
                float sw = sprite.bounds.size.x, sh = sprite.bounds.size.y;
                if (sw < 0.0001f || sh < 0.0001f) continue;

                var go = new GameObject("Bg_" + LayerRes[i]);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = sprite;
                sr.sortingOrder = BaseSort + i;                        // -22 (back) → -17 (front)
                float scale     = Mathf.Max(viewW / sw, viewH / sh) * Margin;
                go.transform.localScale = new Vector3(scale, scale, 1f);

                if (parallax)
                    go.AddComponent<ParallaxBg>().factor = 0.04f + i * 0.055f;  // back slow → front fast
            }
        }

        // Point the dungeon's scrolling parallax layers at complete Gandalf props, spawned
        // sparsely (large spacing) so only the occasional tree/statue drifts past.
        static void DressParallax()
        {
            var props = new List<Sprite>();
            foreach (var n in new[] { "Tree3", "Birch2", "AngelStatue" })
            {
                var s = Resources.Load<Sprite>("CityDecor/" + n);
                if (s != null) props.Add(s);
            }
            if (props.Count == 0) return;
            var arr = props.ToArray();

            foreach (var n in new[] { "ParallaxFar", "ParallaxMid", "ParallaxNear" })
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                var pl = go.GetComponent<ParallaxLayer>();
                if (pl == null) continue;
                pl.sprites    = arr;
                pl.tint       = Color.white;
                pl.minSpacing = 5f;      // sparse → "one or another, spaced"
                pl.maxSpacing = 11f;
                pl.minScale   = 0.40f;
                pl.maxScale   = 0.80f;
            }
        }
    }

    // Drifts a camera-parented backdrop layer sideways as the player walks, so each layer
    // reads at its own depth. factor 0 = fixed, larger = moves more (nearer layer).
    class ParallaxBg : MonoBehaviour
    {
        public float factor;
        const float CenterX = 2.5f;   // city view centre
        Transform _player;

        void LateUpdate()
        {
            if (_player == null)
            {
                var p = Object.FindAnyObjectByType<PlayerController2D>();
                if (p == null) return;
                _player = p.transform;
            }
            var cam = Camera.main;
            float cx = cam != null ? cam.transform.position.x : CenterX;
            // City: driven by the player walking. Dungeon: player stays put, so the shared
            // DungeonScroll (from WaveManager) drives it during wave-runs.
            float driver = (_player.position.x - cx) + BackgroundLayers.DungeonScroll;
            var lp = transform.localPosition;
            lp.x = -driver * factor;
            transform.localPosition = lp;
        }
    }
}

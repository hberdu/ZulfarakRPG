using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Builds the layered scenic backdrop (sky gradient + hazy mountain ranges + horizon haze)
    // behind every gameplay scene. One wide image per mood (forest / dark), parented to the
    // camera and kept fitted to the view by CameraBackdrop.
    public class BackgroundLayers : MonoBehaviour
    {
        // WaveManager advances this during the inter-wave run; CameraBackdrop uses it to drift the
        // mountain backdrop so the world scrolls as the hero marches.
        public static float DungeonScroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!MapBounds.IsGameplayScene(scene.name)) return;
            DungeonScroll = 0f;

            // Strip any authored/leftover scenery-prop layers so the backdrop shows only the
            // mountains (no scattered trees / torches / statues).
            foreach (var pl in Object.FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None))
                if (pl != null) Destroy(pl.gameObject);

            // Dungeons: strip the fixed foreground decorations (graves, mausoleum, rocks). They
            // DON'T scroll with the inter-wave march, so a static prop under a "running" hero
            // killed the sense of movement — only the scrolling mountain backdrop should read as
            // the world going by. Pure-decoration = a root SpriteRenderer with no collider (Ground /
            // walls / portals keep theirs) and no rigidbody (hero / enemies keep theirs).
            if (MapBounds.IsDungeonScene(scene.name))
                foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    var go = sr.gameObject;
                    if (go.transform.parent != null) continue;
                    if (go.GetComponent<Collider2D>() != null || go.GetComponent<Rigidbody2D>() != null) continue;
                    // Don't strip the party: the bot is DontDestroyOnLoad (persistent scene) and
                    // remote teammates are avatars, not scenery — they'd vanish on dungeon entry.
                    if (go.scene.buildIndex < 0) continue;
                    if (go.GetComponent<BotPlayer>() != null || go.GetComponent<RemotePlayer>() != null) continue;
                    Destroy(go);
                }

            // Overlay aesthetic: NO opaque scenic backdrop. The window is TRANSPARENT (magenta
            // clear → AlphaMaskFeature → live desktop shows through), so a full-screen bg image
            // would defeat the point. The biome PROPS (MapScenery) ARE the layered background now.
            // Only clear any leftover backdrop a previous (opaque) build may have parented.
            var cam = Camera.main;
            if (cam == null) return;
            var prev = cam.transform.Find("__Background");
            if (prev != null) Destroy(prev.gameObject);
        }

        // Themed backdrop per map: forest/dark are EXCLUSIVE to the first city/dungeon; each
        // phase gets a mood matching its terrain + inhabitants (orc canyons, slime swamp,
        // werewolf cemetery). Phase = the digit right after "Camp_"/"Dungeon_".
        static string BgForScene(string name)
        {
            if (name == "Zulfarak") return "bg_forest";
            if (name == "Dungeon")  return "bg_dark";
            if (name.StartsWith("Camp_") || name.StartsWith("Dungeon_"))
            {
                int us = name.IndexOf('_');
                char phase = (us >= 0 && us + 1 < name.Length) ? name[us + 1] : '2';
                switch (phase)
                {
                    case '2': return "bg_orc";       // orc canyons
                    case '3': return "bg_slime";     // sickly swamp
                    case '4': return "bg_cemetery";  // werewolf night cemetery
                }
            }
            return "bg_forest";
        }
    }

    // Camera-parented backdrop that FITS the seamless image to the view height (full sky→
    // mountains, no crop) and PARALLAX-SCROLLS it opposite the hero's horizontal movement, so the
    // world drifts by continuously as the hero walks. Three tiled copies give an infinite scroll.
    public class CameraBackdrop : MonoBehaviour
    {
        public Sprite sprite;
        public int    sortingOrder = -100;
        public bool   scrolls = true;     // dungeons scroll; city/camp hubs stay static
        const float   Parallax = 0.35f;   // backdrop drifts at 35% of the hero's walk (distant feel)

        SpriteRenderer[] _copies;
        float _scroll, _lastX;
        PlayerController2D _player;

        void Start()
        {
            _copies = new SpriteRenderer[3];
            for (int i = 0; i < _copies.Length; i++)
            {
                var go = new GameObject("copy" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite; sr.sortingOrder = sortingOrder;
                _copies[i] = sr;
            }
            TrackPlayer();
        }

        void TrackPlayer()
        {
            _player = FindAnyObjectByType<PlayerController2D>();
            if (_player != null) _lastX = _player.transform.position.x;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic || sprite == null || _copies == null) return;
            float camH = 2f * cam.orthographicSize;
            float s    = camH / sprite.bounds.size.y;      // fit height (full image, no zoom)
            float bgW  = sprite.bounds.size.x * s;

            if (_player == null) TrackPlayer();
            if (scrolls && _player != null)
            {
                float x = _player.transform.position.x;
                _scroll -= (x - _lastX) * Parallax;         // hero right → world drifts left
                _lastX = x;
            }

            // Fold in the inter-wave march: WaveManager advances DungeonScroll while the hero
            // runs in place, so the backdrop drifts wave-to-wave (not only when the hero walks).
            float march = scrolls ? BackgroundLayers.DungeonScroll : 0f;
            float startX = -Mathf.Repeat(_scroll + march, bgW);   // wrap for a seamless infinite scroll
            var scale = new Vector3(s, s, 1f);
            for (int i = 0; i < _copies.Length; i++)
            {
                var t = _copies[i].transform;
                t.localScale    = scale;
                t.localPosition = new Vector3(startX + i * bgW, 0f, 5f);
            }
        }
    }
}

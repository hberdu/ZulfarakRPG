using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Animated planet shown in the Zulfarak city's far background ("third layer").
    // Loads the 5000×100 sprite sheet from Resources at runtime, slices it into
    // N square frames (no Unity Editor sprite-slicing needed), and cycles them
    // for a continuous rotation effect. Half-screen width, centered on the
    // camera's X — the planet's lower portion is hidden behind the dunes /
    // pyramids (sortingOrder −10) so only the top "rises" over the horizon.
    public class BackgroundPlanet : MonoBehaviour
    {
        [Header("Animation")]
        public float fps = 14f;   // smooth rotation — high enough that frame steps aren't visible

        // The procedural planet is AI-generated background art. The city was rethemed
        // to a GandalfHardcore forest/medieval look (see CityForestRetheme), so the
        // planet no longer fits — disabled by default. Flip to true to bring it back.
        public static bool AutoSpawnEnabled = false;

        // ── Auto-spawn ────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!AutoSpawnEnabled) return;
            if (scene.name != "Zulfarak") return;
            if (Object.FindAnyObjectByType<BackgroundPlanet>() != null) return;

            var tex = Resources.Load<Texture2D>("PlanetSheet");
            if (tex == null)
            {
                Debug.LogWarning("[BackgroundPlanet] Resources/PlanetSheet not found.");
                return;
            }

            // Sheet is a single horizontal strip of square frames (5000×100 → 50 of 100×100).
            int frameH = tex.height;
            int frameCount = Mathf.Max(1, tex.width / frameH);
            var frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = Sprite.Create(
                    tex,
                    new Rect(i * frameH, 0, frameH, frameH),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit: 100f);
            }

            // Camera ortho 0.75 + aspect ~3.33 → visible width ≈ 5 world units.
            // Half-width = 2.5; native frame = 1 world unit → scale 2.5×.
            // Position: centered on the camera's X (2.5); Y set low so only the
            // upper "tip" of the planet pokes above the horizon line.
            SpawnAt(new Vector3(2.5f, -0.95f, 0f), frames, sizeWorld: 2.5f);
        }

        public static BackgroundPlanet SpawnAt(Vector3 worldPos, Sprite[] frames, float sizeWorld)
        {
            var go = new GameObject("BackgroundPlanet");
            go.transform.position   = worldPos;
            go.transform.localScale = Vector3.one * sizeWorld;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = frames[0];
            // sortingOrder −11 → just behind the far decor layer (Dune_FarL /
            // Pyramid_C at −10), so the horizon objects always overlap the planet.
            sr.sortingOrder = -11;
            // Heavily shadowed/dim tint so it reads as a distant body in the dusk sky
            // rather than a vivid foreground sprite.
            sr.color        = new Color(0.40f, 0.36f, 0.50f, 0.55f);

            var planet     = go.AddComponent<BackgroundPlanet>();
            planet._sr     = sr;
            planet._frames = frames;
            return planet;
        }

        // ── Runtime ────────────────────────────────────────────────────────
        SpriteRenderer _sr;
        Sprite[]       _frames;
        int            _lastIndex = -1;

        void Update()
        {
            if (_frames == null || _frames.Length == 0 || _sr == null) return;
            int i = (int)(Time.time * fps) % _frames.Length;
            if (i == _lastIndex) return;
            _lastIndex = i;
            _sr.sprite = _frames[i];
        }
    }
}

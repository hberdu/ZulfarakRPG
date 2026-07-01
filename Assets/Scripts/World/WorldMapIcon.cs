using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Floating "world map" icon shown only in the Zulfarak city scene. Bobs
    // up/down with a gentle sine, hover shows a tooltip, click toggles the
    // WorldMapPanel overlay. Auto-spawned on scene load — no scene editing
    // required.
    public class WorldMapIcon : MonoBehaviour
    {
        public float bobAmplitude = 0.06f;
        public float bobSpeed     = 1.8f;
        public string tooltipText = "Mapa do Mundo";

        // ── Auto-spawn ────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Zulfarak") return;
            if (Object.FindAnyObjectByType<WorldMapIcon>() != null) return;
            // Upper-left corner of the camera viewport, well clear of the
            // DungeonPortal (x ≈ 4.5) and any NPCs.
            SpawnAt(new Vector3(0.65f, 0.55f, 0f));
        }

        public static WorldMapIcon SpawnAt(Vector3 worldPos)
        {
            var go = new GameObject("WorldMapIcon");
            go.transform.position   = worldPos;
            go.transform.localScale = Vector3.one * 0.55f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeMapIconSprite();
            sr.sortingOrder = 11;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = 0.30f;

            // White semi-transparent glow ring (à la Portal2D's outer rings)
            // sits behind the icon — invisible by default, only lights up on hover.
            var ringGO = new GameObject("Glow");
            ringGO.transform.SetParent(go.transform, false);
            ringGO.transform.localScale = Vector3.one * 0.85f;
            var ringSr = ringGO.AddComponent<SpriteRenderer>();
            ringSr.sprite       = MakeGlowRingSprite();
            ringSr.color        = new Color(1f, 1f, 1f, 0f);   // hidden until hover
            ringSr.sortingOrder = 10;

            var icon = go.AddComponent<WorldMapIcon>();
            icon._glowSr = ringSr;
            return icon;
        }

        // ── Runtime ──────────────────────────────────────────────────
        private SpriteRenderer  _sr;
        private CircleCollider2D _col;
        private Camera           _cam;
        private Vector3          _basePos;
        private GameObject       _tooltipRoot;
        private bool             _hovering;
        internal SpriteRenderer  _glowSr;       // assigned by SpawnAt

        // Stable world position (ignores the bobbing offset) so other systems
        // (e.g. WorldMapPanel) can anchor to the icon's resting location.
        public Vector3 BasePos => _basePos;

        void Awake()
        {
            _sr      = GetComponent<SpriteRenderer>();
            _col     = GetComponent<CircleCollider2D>();
            _basePos = transform.position;
            BuildTooltip();
        }

        void Update()
        {
            // Bob + sway — keep animating even when the panel is open so the
            // icon stays alive visually if the panel is partially transparent.
            float t = Time.time;
            transform.position = _basePos + new Vector3(0f,
                Mathf.Sin(t * bobSpeed) * bobAmplitude, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * 3.5f);

            // Pulsing white halo — only visible while hovered, off otherwise.
            if (_glowSr != null)
            {
                var c = _glowSr.color;
                if (_hovering)
                {
                    float pulse = (Mathf.Sin(t * 2.2f) + 1f) * 0.5f;     // 0..1
                    c.a = 0.55f + pulse * 0.30f;
                    _glowSr.transform.localScale = Vector3.one * (0.80f + pulse * 0.08f);
                }
                else
                {
                    c.a = 0f;
                    _glowSr.transform.localScale = Vector3.one * 0.80f;
                }
                _glowSr.color = c;
            }

            // While the map panel is open it consumes the click — skip hover/click here.
            if (WorldMapPopup.IsOpen)
            {
                if (_hovering)
                {
                    _hovering = false;
                    if (_tooltipRoot != null) _tooltipRoot.SetActive(false);
                }
                return;
            }

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            Vector3 mw = _cam.ScreenToWorldPoint(Input.mousePosition);
            mw.z = transform.position.z;
            bool inside = _col != null && _col.OverlapPoint(mw);
            if (inside != _hovering)
            {
                _hovering = inside;
                if (_tooltipRoot != null) _tooltipRoot.SetActive(_hovering);
            }
            if (_hovering && Input.GetMouseButtonDown(0))
                WorldMapPopup.Show();
        }

        void BuildTooltip()
        {
            if (string.IsNullOrEmpty(tooltipText)) return;
            _tooltipRoot = new GameObject("Tooltip");
            _tooltipRoot.transform.SetParent(transform, false);
            _tooltipRoot.transform.localPosition = new Vector3(0f, 0.32f, -0.3f);

            float w = tooltipText.Length * 0.030f + 0.10f;
            var bgGO = new GameObject("Bg");
            bgGO.transform.SetParent(_tooltipRoot.transform, false);
            bgGO.transform.localScale = new Vector3(w, 0.07f, 1f);
            var bgSr = bgGO.AddComponent<SpriteRenderer>();
            bgSr.sprite       = WhitePixel();
            bgSr.color        = new Color(0f, 0f, 0f, 0.92f);
            bgSr.sortingOrder = 12;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_tooltipRoot.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            var tmp = labelGO.AddComponent<TextMeshPro>();
            tmp.text      = tooltipText;
            tmp.fontSize  = 0.45f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            var mr = labelGO.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 14;
            _tooltipRoot.SetActive(false);
        }

        // ── Procedural sprites ─────────────────────────────────────────────
        static Sprite _whitePixel;
        static Sprite WhitePixel()
        {
            if (_whitePixel != null) return _whitePixel;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            _whitePixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _whitePixel;
        }

        // Pixel-art parchment scroll with an inked dashed route and a red "you are
        // here" marker — instantly reads as a "map" icon.
        static Sprite _iconSprite;
        static Sprite MakeMapIconSprite()
        {
            if (_iconSprite != null) return _iconSprite;
            const int W = 32, H = 28;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    t.SetPixel(x, y, Color.clear);

            var roll    = new Color(0.55f, 0.40f, 0.18f, 1f);
            var rollDk  = new Color(0.32f, 0.20f, 0.08f, 1f);
            var parch   = new Color(0.94f, 0.85f, 0.58f, 1f);
            var parchDk = new Color(0.78f, 0.66f, 0.36f, 1f);
            var ink     = new Color(0.30f, 0.18f, 0.05f, 1f);
            var red     = new Color(0.85f, 0.18f, 0.18f, 1f);

            // Parchment body
            for (int y = 6; y < H - 6; y++)
                for (int x = 2; x < W - 2; x++)
                    t.SetPixel(x, y, parch);
            // Left/right shading on parchment
            for (int y = 6; y < H - 6; y++)
            {
                t.SetPixel(2,     y, parchDk);
                t.SetPixel(W - 3, y, parchDk);
            }
            // Top + bottom rolled scroll bands
            for (int x = 1; x < W - 1; x++)
            {
                t.SetPixel(x, H - 5, rollDk); t.SetPixel(x, H - 4, roll);
                t.SetPixel(x, H - 3, roll);   t.SetPixel(x, H - 2, rollDk);
                t.SetPixel(x, 5,     rollDk); t.SetPixel(x, 4,     roll);
                t.SetPixel(x, 3,     roll);   t.SetPixel(x, 2,     rollDk);
            }
            // Scroll caps (rounded ends)
            t.SetPixel(0, 3, roll); t.SetPixel(0, 4, roll);
            t.SetPixel(W - 1, 3, roll); t.SetPixel(W - 1, 4, roll);
            t.SetPixel(0, H - 4, roll); t.SetPixel(0, H - 5, roll);
            t.SetPixel(W - 1, H - 4, roll); t.SetPixel(W - 1, H - 5, roll);

            // Ink dashed route across the parchment
            for (int x = 5; x < W - 5; x++)
            {
                int y = 14 + (int)(Mathf.Sin(x * 0.55f) * 2.3f);
                if (x % 2 == 0) t.SetPixel(x, y, ink);
            }
            // City dots along the route
            void Dot(int cx, int cy, Color c)
            {
                t.SetPixel(cx,     cy,     c);
                t.SetPixel(cx + 1, cy,     c);
                t.SetPixel(cx,     cy + 1, c);
                t.SetPixel(cx + 1, cy + 1, c);
            }
            Dot(7,  13, ink);
            Dot(13, 16, ink);
            Dot(19, 12, ink);
            Dot(24, 15, red);

            t.Apply();
            _iconSprite = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return _iconSprite;
        }

        // Soft white halo — a thick ring peaking near the edge plus a gentle
        // inner glow. Used behind the icon as a pulsing/hover-lit highlight.
        static Sprite _glowRing;
        static Sprite MakeGlowRingSprite()
        {
            if (_glowRing != null) return _glowRing;
            const int N = 64;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            float cx = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - cx) / cx;
                    float dy = (y - cx) / cx;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 3.2f);
                    float fill = Mathf.Clamp01(1f - d) * 0.22f;
                    float a    = Mathf.Clamp01(ring + fill);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            _glowRing = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _glowRing;
        }
    }
}

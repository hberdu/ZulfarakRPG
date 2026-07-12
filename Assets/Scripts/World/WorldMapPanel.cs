using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Modal world-map overlay opened by WorldMapIcon. Lazy-built on first Show.
    // Draws a parchment "tooltip" panel with 5 city circles connected by an
    // irregular dashed route; only Zulfarak (#1) is active, the other 4 are
    // darkened placeholders. ESC or click outside the panel closes it.
    public class WorldMapPanel : MonoBehaviour
    {
        private static WorldMapPanel _instance;
        public  static bool IsOpen => _instance != null && _instance.gameObject.activeSelf;

        public static void Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }
        public static void Show()
        {
            if (_instance == null) Build();
            // No more floating WorldMapIcon in the corner — anchor the panel to the
            // camera centre so the parchment sits in front of the player, matching
            // where the native WorldMapPopup would appear above the game strip in a
            // build. If a manual WorldMapIcon has been placed we still hang from it.
            var icon = Object.FindAnyObjectByType<WorldMapIcon>();
            if (icon != null)
            {
                Vector3 a = icon.BasePos;
                _instance.transform.position = new Vector3(
                    a.x + 1.55f,   // half panel-width — icon ends up just left of the paper
                    a.y - 0.68f,   // panel top sits below the icon's bottom edge
                    -1f);
            }
            else
            {
                var cam = Camera.main;
                var pos = cam != null ? cam.transform.position : new Vector3(2.5f, 0f, 0f);
                _instance.transform.position = new Vector3(pos.x, pos.y, -1f);
            }
            _instance.gameObject.SetActive(true);
        }
        public static void Hide()
        {
            if (_instance != null) _instance.gameObject.SetActive(false);
        }

        // ── City definitions ──────────────────────────────────────────────
        struct CityDef
        {
            public string  Name;
            public Vector2 LocalPos;
            public bool    Locked;
            public string  Scene;   // scene to load on click (null = no teleport)
        }

        // Local (panel-space) positions, slightly wavy so the route reads as
        // an irregular path. Panel root is centered on the camera at (2.5, 0).
        static readonly CityDef[] Cities =
        {
            new CityDef { Name = "Zulfarak",   LocalPos = new Vector2(-1.50f, -0.06f), Locked = false, Scene = "Zulfarak" },
            new CityDef { Name = "Acamp. Orc", LocalPos = new Vector2(-0.75f,  0.10f), Locked = false, Scene = "Camp_2_1" },
            new CityDef { Name = "Vila Slime", LocalPos = new Vector2( 0.00f, -0.10f), Locked = false, Scene = "Camp_3_1" },
            new CityDef { Name = "Cemiterio",  LocalPos = new Vector2( 0.75f,  0.08f), Locked = false, Scene = "Camp_4_1" },
            new CityDef { Name = "???",        LocalPos = new Vector2( 1.50f, -0.04f), Locked = true  },
        };

        // ── Build (one-shot) ──────────────────────────────────────────────
        static void Build()
        {
            var root = new GameObject("WorldMapPanel");
            root.transform.position = new Vector3(2.5f, 0f, -1f);
            _instance = root.AddComponent<WorldMapPanel>();
            // No dim backdrop — the panel renders as a tooltip-style popup,
            // matching the in-world popups used by the class masters.
            _instance.BuildPaper();
            _instance.BuildTitleAndHint();
            _instance.BuildConnectors();
            _instance.BuildCities();
        }

        Camera _cam;

        void BuildPaper()
        {
            // Dark border (slightly larger, behind paper)
            var border = MakeSprite("PaperBorder", new Color(0.28f, 0.18f, 0.06f, 1f), 41);
            border.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            border.transform.localScale    = new Vector3(3.78f, 1.06f, 1f);

            // Parchment background
            var paper = MakeSprite("Paper", new Color(0.94f, 0.85f, 0.58f, 1f), 42);
            paper.transform.localPosition = new Vector3(0f, 0f, 0f);
            paper.transform.localScale    = new Vector3(3.66f, 0.96f, 1f);
        }

        void BuildTitleAndHint()
        {
            var title = MakeText("Title",
                "Mapa do Mundo", 1.35f,
                new Color(0.30f, 0.18f, 0.05f, 1f), FontStyles.Bold, 46);
            title.transform.localPosition = new Vector3(0f, 0.36f, -0.1f);

            var hint = MakeText("Hint",
                "Clique fora para fechar", 0.50f,
                new Color(0.45f, 0.30f, 0.10f, 0.85f), FontStyles.Italic, 46);
            hint.transform.localPosition = new Vector3(0f, -0.40f, -0.1f);
        }

        void BuildConnectors()
        {
            // Dashed ink route between consecutive cities with a sine wobble
            // for an irregular hand-drawn feel.
            var ink = new Color(0.30f, 0.18f, 0.05f, 0.95f);
            for (int i = 0; i < Cities.Length - 1; i++)
            {
                Vector2 a = Cities[i].LocalPos;
                Vector2 b = Cities[i + 1].LocalPos;
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 1e-3f) continue;
                Vector2 unit = dir / len;
                Vector2 perp = new Vector2(-unit.y, unit.x);
                float   ang  = Mathf.Atan2(unit.y, unit.x) * Mathf.Rad2Deg;

                int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.055f));
                for (int k = 0; k < n; k++)
                {
                    float u = (k + 0.5f) / n;
                    if (u < 0.13f || u > 0.87f) continue; // leave a gap near city circles

                    Vector2 pos = a + dir * u + perp * (Mathf.Sin(u * 16f + i * 1.7f) * 0.022f);
                    var dash = MakeSprite($"Dash_{i}_{k}", ink, 43);
                    dash.transform.localPosition = new Vector3(pos.x, pos.y, -0.02f);
                    dash.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                    dash.transform.localScale    = new Vector3(0.045f, 0.013f, 1f);
                }
            }
        }

        void BuildCities()
        {
            for (int i = 0; i < Cities.Length; i++)
            {
                var c    = Cities[i];
                var node = new GameObject($"City_{i}_{c.Name}");
                node.transform.SetParent(transform, false);
                node.transform.localPosition = new Vector3(c.LocalPos.x, c.LocalPos.y, -0.05f);

                // Outer dark ring
                var outer = new GameObject("Outer");
                outer.transform.SetParent(node.transform, false);
                outer.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
                var outerSr = outer.AddComponent<SpriteRenderer>();
                outerSr.sprite       = CircleSprite();
                outerSr.color        = new Color(0.28f, 0.18f, 0.06f, 1f);
                outerSr.sortingOrder = 44;

                // Inner fill — gold for unlocked, dark slate for locked
                var inner = new GameObject("Inner");
                inner.transform.SetParent(node.transform, false);
                inner.transform.localScale = new Vector3(0.13f, 0.13f, 1f);
                var innerSr = inner.AddComponent<SpriteRenderer>();
                innerSr.sprite       = CircleSprite();
                innerSr.color        = c.Locked
                    ? new Color(0.22f, 0.20f, 0.22f, 1f)
                    : new Color(0.95f, 0.62f, 0.20f, 1f);
                innerSr.sortingOrder = 45;

                // Pulsing glow for the active city
                if (!c.Locked)
                {
                    var glow = new GameObject("Glow");
                    glow.transform.SetParent(node.transform, false);
                    glow.transform.localScale = new Vector3(0.28f, 0.28f, 1f);
                    var glowSr = glow.AddComponent<SpriteRenderer>();
                    glowSr.sprite       = CircleSprite();
                    glowSr.color        = new Color(0.95f, 0.65f, 0.20f, 0.35f);
                    glowSr.sortingOrder = 43;
                    glow.AddComponent<MapCityGlow>();
                }

                // City label below the circle
                var lblGO = new GameObject("Label");
                lblGO.transform.SetParent(node.transform, false);
                lblGO.transform.localPosition = new Vector3(0f, -0.18f, -0.1f);
                var lbl = lblGO.AddComponent<TextMeshPro>();
                lbl.text      = c.Name;
                lbl.fontSize  = 0.65f;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.color     = c.Locked
                    ? new Color(0.50f, 0.45f, 0.38f, 1f)
                    : new Color(0.30f, 0.18f, 0.05f, 1f);
                lbl.fontStyle = FontStyles.Bold;
                var mr = lblGO.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 46;

                // Tag locked cities with a small "?" hint above the circle
                if (c.Locked)
                {
                    var qGO = new GameObject("Q");
                    qGO.transform.SetParent(node.transform, false);
                    qGO.transform.localPosition = new Vector3(0f, 0.005f, -0.1f);
                    var q = qGO.AddComponent<TextMeshPro>();
                    q.text      = "?";
                    q.fontSize  = 0.55f;
                    q.alignment = TextAlignmentOptions.Center;
                    q.color     = new Color(0.70f, 0.65f, 0.55f, 0.85f);
                    q.fontStyle = FontStyles.Bold;
                    var qMr = qGO.GetComponent<MeshRenderer>();
                    if (qMr != null) qMr.sortingOrder = 46;
                }
            }
        }

        // ── Input handling ────────────────────────────────────────────────
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); return; }

            if (Input.GetMouseButtonDown(0))
            {
                if (_cam == null) _cam = Camera.main;
                if (_cam == null) return;
                Vector3 mw = _cam.ScreenToWorldPoint(Input.mousePosition);

                // City hit-test: click an unlocked city dot to travel there.
                for (int i = 0; i < Cities.Length; i++)
                {
                    if (Cities[i].Locked || string.IsNullOrEmpty(Cities[i].Scene)) continue;
                    Vector2 world = (Vector2)transform.position + Cities[i].LocalPos;
                    if (((Vector2)mw - world).sqrMagnitude <= 0.14f * 0.14f)
                    {
                        Hide();
                        HorseCutscene.Play(Cities[i].Scene);   // gallop → fade → load
                        return;
                    }
                }

                // Paper world bounds: center = panel root, scale (3.66 × 0.96).
                float dx = Mathf.Abs(mw.x - transform.position.x);
                float dy = Mathf.Abs(mw.y - transform.position.y);
                bool insidePaper = dx <= 1.83f && dy <= 0.48f;
                if (!insidePaper) Hide();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────
        GameObject MakeSprite(string name, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = WhitePixel();
            sr.color        = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        GameObject MakeText(string name, string text, float fontSize, Color color,
                            FontStyles style, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = color;
            tmp.fontStyle = style;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = sortingOrder;
            return go;
        }

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

        // Soft anti-aliased circle for the city dots.
        static Sprite _circleSprite;
        static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int N = 64;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            float cx = (N - 1) * 0.5f;
            float r  = N * 0.46f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - cx, dy = y - cx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - (d - r + 1.5f));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            _circleSprite = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _circleSprite;
        }
    }

    // Tiny per-city glow pulser — attached to the unlocked-city glow halo.
    public class MapCityGlow : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Vector3        _baseScale;
        void Awake()
        {
            _sr        = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }
        void Update()
        {
            float p = (Mathf.Sin(Time.time * 3.5f) + 1f) * 0.5f; // 0..1
            transform.localScale = _baseScale * (0.92f + p * 0.18f);
            if (_sr != null)
            {
                var c = _sr.color;
                c.a = 0.20f + p * 0.30f;
                _sr.color = c;
            }
        }
    }
}

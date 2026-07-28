using UnityEngine;
using UnityEngine.UI;

namespace ZulfarakRPG
{
    // The loot chest that sits on the HUD button rail, right after the last button and the same
    // size as one. Drops fly into it: it throws its lid open, glows yellow and shudders as each
    // item lands, then snaps shut once the last one is in.
    //
    // It lives on the HUD CANVAS, not in the world. Loot used to fly at the hero's transform,
    // which on this short strip read as items diving down into the HUD bar — now they have a real
    // destination, and being a UI element guarantees it layers with the buttons and measures in
    // the same pixels they do.
    //
    // Art: Resources/Chest/chest_open — a horizontal strip of SQUARE frames going closed → fully
    // open (same layout as Resources/Campfire/campfire_lit). Played forward to open, backwards to
    // close. Falls back to the single closed frame, then to a procedural crate.
    public class LootChest : MonoBehaviour
    {
        const float OpenDuration  = 0.18f;
        const float CloseDuration = 0.16f;
        const float MinOpenTime   = 0.45f;   // stay open at least this long, so it reads
        const float GlowScale     = 2.2f;    // glow sprite size relative to the chest

        enum Phase { Opening, Receiving, Closing, Done }

        static LootChest _active;

        RectTransform _rt;
        Image         _img, _glow;
        Sprite[]      _frames;
        Phase         _phase;
        float         _t, _openAge, _shake, _flash;
        int           _pending;
        Vector2       _basePos;

        static readonly Color GlowColor = new Color(1f, 0.86f, 0.25f, 1f);

        // World point drops should fly into — the chest's slot on the rail, projected into the
        // world so the world-space drop sprites can home in on it.
        public Vector3 MouthWorldPos
        {
            get
            {
                var cam = Camera.main;
                if (cam == null || _rt == null) return Vector3.zero;
                Vector3 screen = new Vector3(_basePos.x + PlayerHud.ButtonSize * 0.5f,
                                             _basePos.y + PlayerHud.ButtonSize * 0.6f,
                                             Mathf.Abs(cam.transform.position.z));
                var w = cam.ScreenToWorldPoint(screen);
                w.z = -0.35f;
                return w;
            }
        }

        // The chest currently collecting loot, creating one if needed. Returns null when the HUD
        // isn't up yet (the caller then just lets the drop fade on the spot).
        public static LootChest Ensure()
        {
            if (_active != null && _active._phase != Phase.Done) return _active;

            var canvas = PlayerHud.HudCanvas;
            if (canvas == null) return null;

            var go = new GameObject("LootChest", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var chest = go.AddComponent<LootChest>();

            chest._rt = (RectTransform)go.transform;
            chest._rt.anchorMin = chest._rt.anchorMax = chest._rt.pivot = new Vector2(0f, 0f);
            chest._basePos = PlayerHud.ChestSlotPos();
            chest._rt.anchoredPosition = chest._basePos;
            chest._rt.sizeDelta = new Vector2(PlayerHud.ButtonSize, PlayerHud.ButtonSize);

            // Yellow glow BEHIND the chest (added first = drawn first), scaled past the chest so
            // it reads as light spilling out rather than a tinted box.
            var glowGO = new GameObject("Glow", typeof(RectTransform));
            glowGO.transform.SetParent(go.transform, false);
            var grt = (RectTransform)glowGO.transform;
            grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
            grt.anchoredPosition = Vector2.zero;
            grt.sizeDelta = Vector2.one * (PlayerHud.ButtonSize * GlowScale);
            chest._glow = glowGO.AddComponent<Image>();
            chest._glow.sprite        = GlowSprite();
            chest._glow.raycastTarget = false;
            chest._glow.color         = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0f);

            var imgGO = new GameObject("Chest", typeof(RectTransform));
            imgGO.transform.SetParent(go.transform, false);
            var irt = (RectTransform)imgGO.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            chest._frames = LoadFrames();
            chest._img = imgGO.AddComponent<Image>();
            chest._img.sprite         = chest._frames != null && chest._frames.Length > 0
                                      ? chest._frames[0] : CrateSprite();
            chest._img.preserveAspect = true;
            chest._img.raycastTarget  = false;

            _active = chest;
            return chest;
        }

        // A drop has started flying here. Called when the pickup sweep begins.
        public void NotifyIncoming() => _pending++;

        // A drop has landed inside: flash the glow and shake, and when the last one is in the
        // lid can shut.
        public void NotifyReceived()
        {
            _pending = Mathf.Max(0, _pending - 1);
            _shake = 1f;
            _flash = 1f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;   // HUD keeps animating through any pause
            _openAge += dt;
            _shake = Mathf.Max(0f, _shake - dt * 2.4f);
            _flash = Mathf.Max(0f, _flash - dt * 1.8f);

            switch (_phase)
            {
                case Phase.Opening:
                    _t += dt / OpenDuration;
                    SetFrame(Mathf.Clamp01(_t));
                    if (_t >= 1f) { _t = 1f; _phase = Phase.Receiving; }
                    break;

                case Phase.Receiving:
                    if (_pending <= 0 && _openAge >= MinOpenTime) { _t = 1f; _phase = Phase.Closing; }
                    break;

                case Phase.Closing:
                    _t -= dt / CloseDuration;
                    SetFrame(Mathf.Clamp01(_t));
                    if (_t <= 0f)
                    {
                        _phase = Phase.Done;
                        if (_active == this) _active = null;
                        Destroy(gameObject);
                    }
                    break;
            }

            // Glow: a soft base while the lid is open, spiking on every item that lands.
            if (_glow != null)
            {
                float open  = _phase == Phase.Receiving ? 0.30f : 0.12f * _t;
                float alpha = Mathf.Clamp01(open + _flash * 0.70f);
                _glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, alpha);
                float s = PlayerHud.ButtonSize * GlowScale * (1f + _flash * 0.18f);
                ((RectTransform)_glow.transform).sizeDelta = Vector2.one * s;
            }

            // Shudder: a quick shimmy that decays, so each arrival is felt.
            if (_rt != null)
            {
                float k = _shake * 2.0f;
                _rt.anchoredPosition = _basePos + new Vector2(Mathf.Sin(Time.unscaledTime * 46f) * k, 0f);
                _rt.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 38f) * _shake * 7f);
            }
        }

        // Maps 0..1 (closed..open) onto the animation strip.
        void SetFrame(float open01)
        {
            if (_img == null || _frames == null || _frames.Length == 0) return;
            int i = Mathf.Clamp(Mathf.RoundToInt(open01 * (_frames.Length - 1)), 0, _frames.Length - 1);
            _img.sprite = _frames[i];
        }

        // ── Art ───────────────────────────────────────────────────────────────
        static Sprite[] _cached;
        static bool     _tried;

        static Sprite[] LoadFrames()
        {
            if (_tried) return _cached;
            _tried = true;
            // The open/close strip if it's there; otherwise the single closed frame, so the real
            // chest art still shows (just without a moving lid) instead of the procedural crate.
            var tex = Resources.Load<Texture2D>("Chest/chest_open")
                   ?? Resources.Load<Texture2D>("Chest/chest_closed");
            if (tex == null) return null;
            int fh = tex.height;
            int n  = Mathf.Max(1, tex.width / fh);          // horizontal strip of square frames
            var arr = new Sprite[n];
            for (int i = 0; i < n; i++)
                arr[i] = Sprite.Create(tex, new Rect(i * fh, 0, fh, fh), new Vector2(0.5f, 0.5f), 100f);
            _cached = arr;
            return arr;
        }

        // Soft radial falloff, tinted yellow by the Image — the light spilling out of the chest.
        static Sprite _glowSprite;
        static Sprite GlowSprite()
        {
            if (_glowSprite != null) return _glowSprite;
            const int N = 64;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));   // squared = soft edge
                }
            t.Apply();
            _glowSprite = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _glowSprite;
        }

        // Plain wooden crate — only if the generated chest art is missing, so loot still has
        // somewhere to go.
        static Sprite _crate;
        static Sprite CrateSprite()
        {
            if (_crate != null) return _crate;
            const int W = 24, H = 18;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var wood = new Color(0.45f, 0.28f, 0.13f, 1f);
            var dark = new Color(0.26f, 0.15f, 0.07f, 1f);
            var iron = new Color(0.42f, 0.42f, 0.47f, 1f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                    bool band = x == 4 || x == W - 5;
                    bool lid  = y >= H - 6;
                    t.SetPixel(x, y, edge ? dark : band ? iron : lid ? wood * 1.15f : wood);
                }
            t.Apply();
            _crate = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return _crate;
        }
    }
}

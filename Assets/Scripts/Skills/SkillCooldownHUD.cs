using UnityEngine;

namespace ZulfarakRPG
{
    // Vertical cooldown bars sitting just above the world HP bar — one per equipped skill.
    // Each bar is one third of the HP bar's width and taller than wide, RISING bottom→top
    // as the cooldown ticks down (empty right after a cast, full when ready). While charging
    // the fill is light-gray; the instant it hits full it flips to warm yellow so the
    // player instantly sees a skill is ready.
    public class SkillCooldownHUD : MonoBehaviour
    {
        // Each bar is 1/3 of the HP bar width; the two bars are laid out side-by-side
        // (centered under the character) with a small gap between them.
        const float BarFrac = 1f / 3f;
        const float GapFrac = 0.06f;

        static readonly Color ChargingColor = new Color(0.82f, 0.82f, 0.86f, 1f);   // light gray
        static readonly Color ReadyColor    = new Color(1.00f, 0.86f, 0.28f, 1f);   // warm yellow

        SkillAutoCaster _caster;
        WorldHealthBar  _hpBar;
        SpriteRenderer  _playerSr;
        Bar[] _bars;

        class Bar
        {
            public GameObject root;
            public Transform outline, bg, fill;
            public SpriteRenderer fillSr;
        }

        public static void Attach(SkillAutoCaster caster)
        {
            if (caster == null) return;
            var go = new GameObject("SkillCooldownHUD");
            var hud = go.AddComponent<SkillCooldownHUD>();
            hud._caster   = caster;
            hud._playerSr = caster.GetComponent<SpriteRenderer>();
            hud._hpBar    = caster.GetComponentInChildren<WorldHealthBar>(true);
            hud.Build();
        }

        void Build()
        {
            _bars = new Bar[SkillManager.MaxEquipped];
            for (int i = 0; i < _bars.Length; i++)
            {
                var root = new GameObject($"CdBar{i}");
                root.transform.SetParent(transform, false);
                var outline = MakeQuad(root.transform, "Outline", new Color(0f, 0f, 0f, 0.9f), 40);
                var bg      = MakeQuad(root.transform, "Bg",      new Color(0.12f, 0.10f, 0.12f, 0.95f), 41);
                var fillGo  = MakeQuad(root.transform, "Fill",    ChargingColor, 42);
                _bars[i] = new Bar { root = root, outline = outline.transform, bg = bg.transform, fill = fillGo.transform, fillSr = fillGo };
            }
        }

        void LateUpdate()
        {
            if (_caster == null || _bars == null) { HideAll(); return; }
            var active = _caster.Active;
            int n = Mathf.Min(active.Count, _bars.Length);
            if (n == 0 || _hpBar == null) { HideAll(); return; }

            float W = _hpBar.BarWorldWidth;
            float hpH = _hpBar.BarWorldHeight;
            if (W <= 0.001f) { W = _playerSr != null ? _playerSr.bounds.size.x * 0.5f : 0.5f; hpH = 0.04f; }
            Vector3 c = _hpBar.BarWorldPos;

            // Two vertical bars, each 1/3 of the HP bar width, centered under the character,
            // and TALLER than wide so the cooldown reads as it rises.
            float barW   = W * BarFrac;
            float gap    = W * GapFrac;
            float barH   = barW * 1.5f;                     // taller than wide → vertical fill
            float y      = c.y + hpH * 0.5f + 0.03f + barH * 0.5f;   // just above the HP bar
            float totalW = 2f * barW + gap;
            float left   = c.x - totalW * 0.5f;

            for (int i = 0; i < _bars.Length; i++)
            {
                var bar = _bars[i];
                if (i >= n) { bar.root.SetActive(false); continue; }
                bar.root.SetActive(true);

                var a = active[i];
                float bx = left + i * (barW + gap) + barW * 0.5f;
                bar.root.transform.position = new Vector3(bx, y, -0.15f);

                bar.outline.localScale = new Vector3(barW + barW * 0.22f, barH + barW * 0.22f, 1f);
                bar.bg.localScale      = new Vector3(barW, barH, 1f);

                // Fill fraction: 0 right after a cast, 1 when ready. Grows BOTTOM → TOP.
                float ready = a.total > 0f ? 1f - Mathf.Clamp01(a.remaining / a.total) : 1f;
                float fh = barH * ready;
                bar.fill.localScale    = new Vector3(barW * 0.78f, fh, 1f);
                bar.fill.localPosition = new Vector3(0f, (fh - barH) * 0.5f, -0.01f);   // anchored at bottom
                bar.fillSr.color = ready >= 0.999f ? ReadyColor : ChargingColor;
            }
        }

        void HideAll()
        {
            if (_bars == null) return;
            foreach (var b in _bars) if (b != null && b.root != null) b.root.SetActive(false);
        }

        SpriteRenderer MakeQuad(Transform parent, string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = Pixel();
            sr.color        = color;
            sr.sortingOrder = order;
            if (_playerSr != null) sr.sortingLayerID = _playerSr.sortingLayerID;
            return sr;
        }

        static Sprite _pixel;
        static Sprite Pixel()
        {
            if (_pixel != null) return _pixel;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, Color.white); t.Apply();
            _pixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _pixel;
        }
    }
}

using System.Collections.Generic;
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
        // Each bar is HALF the HP bar's width, so the two of them side by side span exactly the
        // same width as the HP bar and read as one more line of the same widget.
        const float BarFrac = 0.5f;
        const float GapFrac = 0.04f;

        static readonly Color ChargingColor = new Color(0.82f, 0.82f, 0.86f, 1f);   // light gray
        static readonly Color ReadyColor    = new Color(1.00f, 0.86f, 0.28f, 1f);   // warm yellow

        // Fill fraction per equipped skill (0 = just cast, 1 = ready). Local reads it from the
        // SkillAutoCaster; a remote avatar reads the values synced from its owner.
        System.Func<List<float>> _fills;
        WorldHealthBar  _hpBar;
        SpriteRenderer  _playerSr;
        Bar[] _bars;

        class Bar
        {
            public GameObject root;
            public Transform outline, bg, fill;
            public SpriteRenderer fillSr;
        }

        // Local hero: cooldowns come straight from its caster.
        public static void Attach(SkillAutoCaster caster)
        {
            if (caster == null) return;
            var go = new GameObject("SkillCooldownHUD");
            var hud = go.AddComponent<SkillCooldownHUD>();
            hud._fills    = caster.CooldownFills;
            hud._playerSr = caster.GetComponent<SpriteRenderer>();
            hud._hpBar    = caster.GetComponentInChildren<WorldHealthBar>(true);
            hud.Build();
        }

        // Partner avatar: cooldowns come from the values synced onto the RemotePlayer.
        public static void AttachRemote(RemotePlayer rp)
        {
            if (rp == null) return;
            AttachTo(rp, "SkillCooldownHUD_Remote", () => rp.CooldownFractions);
        }

        // Any other party member that tracks its own cooldowns (the local test bot). Same bars,
        // same geometry as the hero's — every party member reads identically.
        public static void AttachTo(Component owner, string name, System.Func<List<float>> fills)
        {
            if (owner == null || fills == null) return;
            var go = new GameObject(name);
            var hud = go.AddComponent<SkillCooldownHUD>();
            hud._fills    = fills;
            hud._playerSr = owner.GetComponent<SpriteRenderer>();
            hud._hpBar    = owner.GetComponentInChildren<WorldHealthBar>(true);
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
            if (_fills == null || _bars == null) { HideAll(); return; }
            var fills = _fills();
            int n = Mathf.Min(fills != null ? fills.Count : 0, _bars.Length);
            if (n == 0 || _hpBar == null) { HideAll(); return; }

            float W = _hpBar.BarWorldWidth;
            if (W <= 0.001f) W = _playerSr != null ? _playerSr.bounds.size.x * 0.5f : 0.5f;
            Vector3 c = _hpBar.BarWorldPos;

            // One thin LINE directly above the HP bar: two bars, each half the HP bar's width, as
            // thick as the HP bar itself. Together they span the HP bar exactly, so bar / cooldowns
            // / name stack as three tight rows of the same widget.
            float gap    = W * GapFrac;
            float barW   = (W - gap) * BarFrac;
            float barH   = Mathf.Max(0.012f, _hpBar.BarWorldHeight);
            float y      = _hpBar.BarTopWorldY + WorldHealthBar.LineGap + barH * 0.5f;
            float left   = c.x - W * 0.5f;

            for (int i = 0; i < _bars.Length; i++)
            {
                var bar = _bars[i];
                if (i >= n) { bar.root.SetActive(false); continue; }
                bar.root.SetActive(true);

                float bx = left + i * (barW + gap) + barW * 0.5f;
                bar.root.transform.position = new Vector3(bx, y, -0.15f);

                bar.outline.localScale = new Vector3(barW + 0.012f, barH + 0.010f, 1f);
                bar.bg.localScale      = new Vector3(barW, barH, 1f);

                // Fill fraction: 0 right after a cast, 1 when ready. Now that the bar is a thin
                // row it fills LEFT → RIGHT like the HP bar, instead of rising bottom → top.
                float ready = Mathf.Clamp01(fills[i]);
                float fw = barW * ready;
                bar.fill.localScale    = new Vector3(fw, barH * 0.62f, 1f);
                bar.fill.localPosition = new Vector3((fw - barW) * 0.5f, 0f, -0.01f);   // anchored left
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

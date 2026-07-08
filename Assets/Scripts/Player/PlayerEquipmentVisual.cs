using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Draws the equipped WEAPON into the hero's own sprite: it erases the weapon baked
    // into the base art and blits the equipped weapon's icon at the hand, so the weapon
    // becomes part of the sprite (no separate child) and replaces the original. Body armour
    // is NOT drawn (the pack icons aren't paperdoll art). A legendary piece adds a pulsing
    // golden aura.
    //
    // The animator swaps _sr.sprite each frame, so this intercepts in LateUpdate and
    // substitutes a composited copy (cached per base-frame + weapon).
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerEquipmentVisual : MonoBehaviour
    {
        // Weapon icon placement: a square (aspect preserved) ~0.62× the body height,
        // centred at the front hand (x 0=left..1=right, y 0=feet..1=head). It sticks out past
        // the body edge like a real held weapon. Tune these if it sits wrong.
        const float WpnCx = 0.72f, WpnCy = 0.50f, WpnSizeFrac = 0.62f;

        static readonly ItemType[] AllSlots =
        { ItemType.Weapon, ItemType.Helmet, ItemType.Chest, ItemType.Gloves, ItemType.Boots, ItemType.Cape };

        SpriteRenderer _sr;
        SpriteRenderer _glow;
        string _sig = "~";
        bool   _anyLegendary;
        string _weaponIcon;
        ItemRarity _weaponRarity;

        readonly Dictionary<Sprite, Sprite> _cache = new Dictionary<Sprite, Sprite>();
        readonly HashSet<Sprite> _outputs = new HashSet<Sprite>();

        class IconPx { public Color32[] px; public int w, h; }
        static readonly Dictionary<string, IconPx> _iconCache = new Dictionary<string, IconPx>();

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            BuildGlow();
            RefreshLoadout();
        }

        void OnEnable()  { if (Inventory.Instance != null) Inventory.Instance.OnInventoryChanged += RefreshLoadout; }
        void OnDisable() { if (Inventory.Instance != null) Inventory.Instance.OnInventoryChanged -= RefreshLoadout; }

        void BuildGlow()
        {
            var go = new GameObject("LegendaryGlow");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 1.10f;
            _glow = go.AddComponent<SpriteRenderer>();
            _glow.sortingLayerID = _sr.sortingLayerID;
            _glow.sortingOrder   = _sr.sortingOrder - 1;
            _glow.color          = new Color(1f, 0.82f, 0.20f, 0f);
            _glow.enabled        = false;
        }

        void RefreshLoadout()
        {
            var eq = Inventory.Instance != null ? Inventory.Instance.Equipment : null;
            _weaponIcon = null;
            _anyLegendary = false;

            foreach (var slot in AllSlots)
            {
                string id = eq != null ? eq.GetSlot(slot) : null;
                if (string.IsNullOrEmpty(id)) continue;
                var d = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(id) : null;
                if (d == null) continue;
                if (d.rarity == ItemRarity.Legendary) _anyLegendary = true;
                if (slot == ItemType.Weapon) { _weaponIcon = d.iconPath; _weaponRarity = d.rarity; }
            }

            string newSig = $"W={_weaponIcon ?? "-"}|L={_anyLegendary}";
            if (newSig != _sig)
            {
                _sig = newSig;
                if (_sr != null && _sr.sprite != null) _sr.sprite = OriginalOf(_sr.sprite);
                foreach (var s in _outputs)
                {
                    if (s == null) continue;
                    if (s.texture != null) Destroy(s.texture);
                    Destroy(s);
                }
                _cache.Clear();
                _outputs.Clear();
            }
        }

        void LateUpdate()
        {
            if (_sr == null) return;

            if (_anyLegendary && _sr.sprite != null)
            {
                _glow.enabled = true;
                _glow.sprite  = OriginalOf(_sr.sprite);
                _glow.flipX   = _sr.flipX;
                float pulse   = 0.30f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * 4.5f));
                _glow.color   = new Color(1f, 0.82f, 0.22f, pulse);
            }
            else if (_glow.enabled) _glow.enabled = false;

            // Only composite when a weapon is equipped.
            if (string.IsNullOrEmpty(_weaponIcon)) return;

            var cur = _sr.sprite;
            if (cur == null || _outputs.Contains(cur)) return;

            if (!_cache.TryGetValue(cur, out var comp))
            {
                comp = Composite(cur) ?? cur;
                _cache[cur] = comp;
                if (comp != cur) _outputs.Add(comp);
            }
            if (comp != cur) _sr.sprite = comp;
        }

        Sprite OriginalOf(Sprite s)
        {
            if (!_outputs.Contains(s)) return s;
            foreach (var kv in _cache) if (kv.Value == s) return kv.Key;
            return s;
        }

        Sprite Composite(Sprite src)
        {
            if (src == null || src.texture == null) return null;
            int w = Mathf.RoundToInt(src.rect.width);
            int h = Mathf.RoundToInt(src.rect.height);
            Color[] px;
            try { px = src.texture.GetPixels(Mathf.RoundToInt(src.rect.x), Mathf.RoundToInt(src.rect.y), w, h); }
            catch { return null; }

            int minY = h, maxY = -1, minX = w, maxX = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 0.35f)
                    {
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                    }
            if (maxY < minY) return null;
            int bw = Mathf.Max(1, maxX - minX);
            int bh = Mathf.Max(1, maxY - minY);
            float cxAll = minX + bw * 0.5f;

            // Erase the weapon baked into the base art (outer columns, mid-height).
            for (int y = 0; y < h; y++)
            {
                float ny = (y - minY) / (float)bh;
                if (ny < 0.26f || ny > 0.86f) continue;
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    if (px[idx].a <= 0.35f) continue;
                    float nx = Mathf.Abs(x - cxAll) / (bw * 0.5f);
                    if (nx > 0.60f) px[idx] = new Color(0, 0, 0, 0);
                }
            }

            // Blit the equipped weapon icon at the hand, lightly quality-tinted.
            var icon = IconPixels(_weaponIcon);
            if (icon != null)
            {
                Color tint = ItemData.QualityColor(_weaponRarity);
                int cxp = minX + Mathf.RoundToInt(WpnCx * bw);
                int cyp = minY + Mathf.RoundToInt(WpnCy * bh);
                int rw = Mathf.Max(4, Mathf.RoundToInt(WpnSizeFrac * bh));   // square: aspect preserved
                int rh = rw;
                int x0 = cxp - rw / 2, y0 = cyp - rh / 2;
                for (int j = 0; j < rh; j++)
                {
                    int dy = y0 + j;
                    if (dy < 0 || dy >= h) continue;
                    int v = Mathf.Clamp(j * icon.h / rh, 0, icon.h - 1);
                    for (int i = 0; i < rw; i++)
                    {
                        int dx = x0 + i;
                        if (dx < 0 || dx >= w) continue;
                        int u = Mathf.Clamp(i * icon.w / rw, 0, icon.w - 1);
                        Color32 ic = icon.px[v * icon.w + u];
                        if (ic.a < 100) continue;
                        float a = ic.a / 255f;
                        float cr = Mathf.Lerp(ic.r / 255f, tint.r, 0.30f);
                        float cg = Mathf.Lerp(ic.g / 255f, tint.g, 0.30f);
                        float cb = Mathf.Lerp(ic.b / 255f, tint.b, 0.30f);
                        int di = dy * w + dx;
                        var bg = px[di];
                        px[di] = new Color(cr * a + bg.r * (1f - a), cg * a + bg.g * (1f - a),
                                           cb * a + bg.b * (1f - a), Mathf.Max(bg.a, a));
                    }
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels(px);
            tex.Apply();
            var pivot = new Vector2(src.pivot.x / Mathf.Max(1f, src.rect.width),
                                    src.pivot.y / Mathf.Max(1f, src.rect.height));
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, src.pixelsPerUnit);
        }

        static IconPx IconPixels(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_iconCache.TryGetValue(path, out var e)) return e;
            IconPx r = null;
            var tex = IconLibrary.Tex(path);
            if (tex != null)
            {
                try { r = new IconPx { px = tex.GetPixels32(), w = tex.width, h = tex.height }; }
                catch { r = null; }
            }
            _iconCache[path] = r;
            return r;
        }
    }
}

using UnityEngine;

namespace ZulfarakRPG
{
    // Floating damage number that rises above a character and fades out.
    //
    // Rendered as a PIXEL-ART bitmap sprite (a hand-drawn 3x5 digit font) instead of a
    // vector TMP label, so the numbers match the game's pixel style. Each glyph gets a
    // 1 px black outline baked into the texture (a subtle edge — no separate drop shadow),
    // keeping it readable over any background without a heavy shadow. Crits render bright
    // yellow with a "*" prefix and a small pop.
    public class DamagePopup : MonoBehaviour
    {
        // Fades out a touch quicker so numbers don't linger.
        public float duration     = 0.6f;
        public float riseDistance = 0.30f;

        private Vector3        _startPos;
        private SpriteRenderer _sr;
        private Color          _baseColor;
        private float          _t;
        private float          _popScale;

        // Back-compat overload (no crit) — defaults to a normal hit.
        public static void Spawn(Transform target, float amount, Color color)
            => Spawn(target, amount, color, false);

        public static void Spawn(Transform target, float amount, Color color, bool crit)
        {
            if (target == null || amount <= 0f) return;

            Vector3 headPos;
            var col = target.GetComponent<Collider2D>();
            if (col != null)
                headPos = new Vector3(col.bounds.center.x, col.bounds.center.y, target.position.z);
            else
                headPos = target.position + Vector3.up * 0.4f;

            var go = new GameObject("DamagePopup");
            go.transform.position = headPos + new Vector3(Random.Range(-0.10f, 0.10f), 0f, -0.4f);

            string text  = Mathf.RoundToInt(amount).ToString();
            Color  shown = crit ? new Color(1f, 0.92f, 0.20f, 1f) : color;   // crits = yellow
            if (crit) text = "*" + text;                                     // crit marker

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = PixelFont.BuildNumber(text, shown);
            sr.sortingOrder = 20;   // above HP bars and tooltips

            // HALF the previous on-screen size: the old TMP label was ~2.2 units of cap
            // height; this bitmap sits around ~0.14 units, which reads as a small pixel
            // number. Crits are only slightly larger + pop.
            go.transform.localScale = Vector3.one * (crit ? 1.15f : 1f);

            var pop = go.AddComponent<DamagePopup>();
            pop._popScale = crit ? 1.6f : 1f;
        }

        void Start()
        {
            _startPos  = transform.position;
            _sr        = GetComponent<SpriteRenderer>();
            _baseColor = _sr != null ? _sr.color : Color.white;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float p = _t / duration;
            if (p >= 1f) { Destroy(gameObject); return; }

            transform.position = _startPos + Vector3.up * (riseDistance * p);

            // Crit "pop": bursts in large, then eases quickly back to the resting size.
            float baseScale = _popScale > 1.01f ? 1.15f : 1f;   // crit rests slightly bigger
            float settle = Mathf.Clamp01(p / 0.30f);
            float eased  = 1f - (1f - settle) * (1f - settle);
            float scale  = Mathf.Lerp(_popScale, baseScale, eased);
            transform.localScale = Vector3.one * scale;

            // Fade out over the second half so it stays fully legible longer.
            float alpha = 1f - Mathf.Clamp01((p - 0.45f) / 0.55f);
            if (_sr != null) { var c = _baseColor; c.a = alpha; _sr.color = c; }
        }
    }

    // Procedural 3x5 pixel-art digit font. Builds a single sprite for a whole number
    // string with a baked 1 px black outline. Cached per (text,color) so repeated hits
    // don't rebuild the texture. Sprites are Point-filtered at 48 PPU (~half the old size).
    public static class PixelFont
    {
        const int GW = 3, GH = 5, GAP = 1, PPU = 56;

        static readonly System.Collections.Generic.Dictionary<string, Sprite> _cache = new();

        // 3-wide x 5-tall glyphs (top row first). '1' = lit pixel.
        static readonly System.Collections.Generic.Dictionary<char, string[]> Glyphs = new()
        {
            ['0'] = new[] { "111", "101", "101", "101", "111" },
            ['1'] = new[] { "010", "110", "010", "010", "111" },
            ['2'] = new[] { "111", "001", "111", "100", "111" },
            ['3'] = new[] { "111", "001", "111", "001", "111" },
            ['4'] = new[] { "101", "101", "111", "001", "001" },
            ['5'] = new[] { "111", "100", "111", "001", "111" },
            ['6'] = new[] { "111", "100", "111", "101", "111" },
            ['7'] = new[] { "111", "001", "010", "010", "010" },
            ['8'] = new[] { "111", "101", "111", "101", "111" },
            ['9'] = new[] { "111", "101", "111", "001", "111" },
            ['*'] = new[] { "101", "010", "111", "010", "101" },
            ['-'] = new[] { "000", "000", "111", "000", "000" },
            ['!'] = new[] { "010", "010", "010", "000", "010" },
            [' '] = new[] { "000", "000", "000", "000", "000" },
            // Uppercase alphabet (3x5). Tricky letters (M/N/W/…) are best-effort.
            ['A'] = new[] { "111", "101", "111", "101", "101" },
            ['B'] = new[] { "110", "101", "110", "101", "110" },
            ['C'] = new[] { "111", "100", "100", "100", "111" },
            ['D'] = new[] { "110", "101", "101", "101", "110" },
            ['E'] = new[] { "111", "100", "110", "100", "111" },
            ['F'] = new[] { "111", "100", "110", "100", "100" },
            ['G'] = new[] { "111", "100", "101", "101", "111" },
            ['H'] = new[] { "101", "101", "111", "101", "101" },
            ['I'] = new[] { "111", "010", "010", "010", "111" },
            ['J'] = new[] { "001", "001", "001", "101", "111" },
            ['K'] = new[] { "101", "110", "100", "110", "101" },
            ['L'] = new[] { "100", "100", "100", "100", "111" },
            ['M'] = new[] { "101", "111", "111", "101", "101" },
            ['N'] = new[] { "101", "111", "111", "111", "101" },
            ['O'] = new[] { "111", "101", "101", "101", "111" },
            ['P'] = new[] { "111", "101", "111", "100", "100" },
            ['Q'] = new[] { "111", "101", "101", "111", "011" },
            ['R'] = new[] { "110", "101", "110", "101", "101" },
            ['S'] = new[] { "111", "100", "111", "001", "111" },
            ['T'] = new[] { "111", "010", "010", "010", "010" },
            ['U'] = new[] { "101", "101", "101", "101", "111" },
            ['V'] = new[] { "101", "101", "101", "101", "010" },
            ['W'] = new[] { "101", "101", "111", "111", "101" },
            ['X'] = new[] { "101", "101", "010", "101", "101" },
            ['Y'] = new[] { "101", "101", "010", "010", "010" },
            ['Z'] = new[] { "111", "001", "010", "100", "111" },
        };

        // Alias for building word banners (CLEAR / BOSS / DEFEAT) in the same pixel font.
        public static Sprite BuildText(string text, Color color) => BuildNumber(text, color);

        public static Sprite BuildNumber(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) text = "0";
            string key = text + "|" + ColorUtility.ToHtmlStringRGB(color);
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            // Bound the cache — damage values vary a lot, so drop everything once in a while
            // rather than growing forever (each entry is a tiny 13x7-ish texture).
            if (_cache.Count > 384) _cache.Clear();

            int inW = text.Length * GW + (text.Length - 1) * GAP;
            int inH = GH;
            int W = inW + 2, H = inH + 2;   // +1 px border all round for the outline

            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[W * H];    // starts transparent (all zero)

            // 1) Paint the lit glyph pixels in `color`.
            int penX = 1;
            Color32 fill = color;
            foreach (char ch in text)
            {
                if (Glyphs.TryGetValue(ch, out var g))
                {
                    for (int row = 0; row < GH; row++)
                        for (int cxi = 0; cxi < GW; cxi++)
                            if (g[row][cxi] == '1')
                            {
                                int x = penX + cxi;
                                int y = H - 2 - row;   // row 0 = top; texture y grows up
                                px[y * W + x] = fill;
                            }
                }
                penX += GW + GAP;
            }

            // 2) Bake a 1 px black outline: any transparent pixel touching a lit pixel
            // (8-neighbour) becomes a SOFT black edge (semi-transparent, less shadow-heavy).
            var outline = new Color32(0, 0, 0, 150);
            var outlined = (Color32[])px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (px[y * W + x].a != 0) continue;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                            if (px[ny * W + nx].a != 0) { near = true; break; }
                        }
                    if (near) outlined[y * W + x] = outline;
                }

            t.SetPixels32(outlined);
            t.Apply();
            var sp = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), PPU);
            _cache[key] = sp;
            return sp;
        }
    }
}

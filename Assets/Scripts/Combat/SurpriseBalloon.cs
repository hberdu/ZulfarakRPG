using UnityEngine;

namespace ZulfarakRPG
{
    // A little pixel-art speech bubble with a red "!" that pops over a character's head when a boss
    // appears — a surprised reaction. Self-contained (procedural sprite), follows the target's head,
    // pops in, holds, then fades out over ~1 s.
    public class SurpriseBalloon : MonoBehaviour
    {
        Transform _target;
        float _t;
        SpriteRenderer _sr;
        const float Life = 1.0f;

        public static void Spawn(Transform target)
        {
            if (target == null) return;
            var go = new GameObject("SurpriseBalloon");
            var b = go.AddComponent<SurpriseBalloon>();
            b._target = target;
            b._sr = go.AddComponent<SpriteRenderer>();
            b._sr.sprite       = Art();
            b._sr.sortingOrder = 500;   // floats above the hero + FX
            go.transform.localScale = Vector3.one * BaseScale;
        }

        // ~0.26 world-units tall (sprite is 18px @100PPU = 0.18u) — small, sits just above the head.
        const float BaseScale = 0.26f / 0.18f;

        void LateUpdate()
        {
            _t += Time.deltaTime;
            if (_t >= Life || _target == null) { Destroy(gameObject); return; }

            // Sit to the RIGHT of the character's VISIBLE body, near head height. Use the
            // COLLIDER (visible-body bounds) — the SpriteRenderer bounds span the whole 100px
            // frame, whose empty top padding otherwise floated the emote far above the hero
            // ("no alto do jogo"). Fall back to a fixed offset if there's no collider.
            var col = _target.GetComponent<Collider2D>();
            Vector3 pos;
            if (col != null)
                pos = new Vector3(col.bounds.max.x + 0.10f, col.bounds.max.y - 0.18f, 0f);
            else
                pos = new Vector3(_target.position.x + 0.38f, _target.position.y + 0.60f, 0f);
            transform.position = pos;

            // Pop-in (overshoot) → hold → fade out.
            float baseScale = BaseScale;
            float pop = _t < 0.12f ? Mathf.SmoothStep(0f, 1.15f, _t / 0.12f)
                      : _t < 0.20f ? Mathf.Lerp(1.15f, 1f, (_t - 0.12f) / 0.08f) : 1f;
            transform.localScale = Vector3.one * baseScale * pop;

            if (_sr != null)
            {
                float a = _t > Life - 0.25f ? Mathf.Clamp01((Life - _t) / 0.25f) : 1f;
                var c = _sr.color; c.a = a; _sr.color = c;
            }
        }

        // ── Procedural bubble sprite (cream body + black outline + red "!") ──
        static Sprite _art;
        static Sprite Art()
        {
            if (_art != null) return _art;
            const int W = 16, H = 18;
            var fill  = new bool[W, H];

            // Rounded body (rows 5..16), corners trimmed.
            for (int y = 5; y <= 16; y++)
                for (int x = 1; x <= 14; x++)
                {
                    bool corner = (x <= 1 || x >= 14) && (y <= 5 || y >= 16);
                    if (!corner) fill[x, y] = true;
                }
            // Little tail pointing down-left toward the head.
            fill[6, 4] = true; fill[7, 4] = true; fill[5, 3] = true; fill[6, 3] = true; fill[5, 2] = true;

            var cream = new Color(1f, 0.96f, 0.82f, 1f);
            var ink   = new Color(0.08f, 0.06f, 0.05f, 1f);
            var red   = new Color(0.90f, 0.16f, 0.14f, 1f);

            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (fill[x, y]) { t.SetPixel(x, y, cream); continue; }
                    // Outline: empty pixel touching the body → ink.
                    bool edge = false;
                    for (int oy = -1; oy <= 1 && !edge; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = x + ox, ny = y + oy;
                            if (nx >= 0 && nx < W && ny >= 0 && ny < H && fill[nx, ny]) { edge = true; break; }
                        }
                    t.SetPixel(x, y, edge ? ink : Color.clear);
                }

            // Red "!" — bar on top (high y), dot at the bottom.
            for (int y = 9; y <= 14; y++) { t.SetPixel(7, y, red); t.SetPixel(8, y, red); }
            t.SetPixel(7, 6, red); t.SetPixel(8, 6, red); t.SetPixel(7, 7, red); t.SetPixel(8, 7, red);

            t.Apply();
            _art = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 100f);
            return _art;
        }
    }
}

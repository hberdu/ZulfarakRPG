using UnityEngine;

namespace ZulfarakRPG
{
    // Floating "+N" gold reward that rises above a monster the moment it dies and fades out.
    // Drawn in the SAME pixel-art font as the damage numbers (PixelFont) so it matches the game's
    // style, but at a SMALLER scale than the damage number, with a little gold coin that SPINS
    // (rotates around its vertical axis) beside it so it reads clearly as loot.
    public class GoldPopup : MonoBehaviour
    {
        public float duration     = 1.10f;
        public float riseDistance = 0.65f;

        // Smaller than the damage numbers (which render at scale 1) — the reward reads as a
        // secondary, quieter number under the hit.
        const float PopupScale = 0.7f;
        const float SpinDegPerSec = 360f;

        static readonly Color Gold = new Color(1f, 0.84f, 0.20f, 1f);

        private Vector3        _startPos;
        private SpriteRenderer _txt, _coin;
        private float          _t, _spin;

        public static void Spawn(Vector3 worldPos, long amount)
        {
            if (amount <= 0) return;

            var go = new GameObject("GoldPopup");
            go.transform.position   = worldPos + new Vector3(Random.Range(-0.08f, 0.08f), 0.15f, -0.45f);
            go.transform.localScale = Vector3.one * PopupScale;

            // Spinning coin disc, just left of the number.
            var coinGO = new GameObject("Coin");
            coinGO.transform.SetParent(go.transform, false);
            coinGO.transform.localPosition = new Vector3(-0.17f, 0.0f, 0.02f);
            coinGO.transform.localScale    = Vector3.one * 0.40f;
            var coin = coinGO.AddComponent<SpriteRenderer>();
            coin.sprite       = CoinSprite();
            coin.color        = Gold;
            coin.sortingOrder = 22;

            // "+N" pixel-font sprite (baked black outline — no separate shadow needed).
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = PixelFont.BuildText("+" + amount, Gold);
            sr.sortingOrder = 23;   // above the coin and the white damage numbers

            var pop = go.AddComponent<GoldPopup>();
            pop._txt  = sr;
            pop._coin = coin;
        }

        void Start() => _startPos = transform.position;

        void Update()
        {
            _t += Time.deltaTime;
            float p = _t / duration;
            if (p >= 1f) { Destroy(gameObject); return; }

            // Rise with an ease-out so the coin springs up then drifts to a stop.
            float rise = 1f - (1f - p) * (1f - p);
            transform.position = _startPos + Vector3.up * (riseDistance * rise);

            // Spin the coin around its vertical axis — in the orthographic 2D view a Y rotation
            // narrows the disc to an edge and back, reading as a spinning coin.
            _spin += Time.deltaTime * SpinDegPerSec;
            if (_coin != null) _coin.transform.localRotation = Quaternion.Euler(0f, _spin, 0f);

            // Hold full opacity, then fade over the last 45% of life.
            float alpha = p < 0.55f ? 1f : 1f - (p - 0.55f) / 0.45f;
            if (_txt  != null) { var c = _txt.color;  c.a = alpha; _txt.color  = c; }
            if (_coin != null) { var c = _coin.color; c.a = alpha; _coin.color = c; }
        }

        // Small filled disc with a slightly darker rim — a minimal "coin".
        static Sprite _coinSprite;
        static Sprite CoinSprite()
        {
            if (_coinSprite != null) return _coinSprite;
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    float a  = d <= 1f ? 1f : 0f;
                    // Darker ring near the edge for a coin-like bevel.
                    float shade = d > 0.72f ? 0.72f : 1f;
                    t.SetPixel(x, y, new Color(shade, shade, shade, a));
                }
            t.Apply();
            _coinSprite = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 64f);
            return _coinSprite;
        }
    }
}

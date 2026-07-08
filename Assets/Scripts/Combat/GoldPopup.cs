using TMPro;
using UnityEngine;

namespace ZulfarakRPG
{
    // Floating "+N" gold reward that rises above a monster the moment it dies and
    // fades out. Rendered in warm gold with a black outline (like DamagePopup) plus a
    // little coin disc so it reads clearly as loot. Rises higher and lingers a touch
    // longer than a damage number so the reward stands out.
    public class GoldPopup : MonoBehaviour
    {
        public float duration     = 1.10f;
        public float riseDistance = 0.75f;

        static readonly Color Gold = new Color(1f, 0.84f, 0.20f, 1f);

        private Vector3     _startPos;
        private TextMeshPro _tmp;
        private Transform   _shadow;
        private SpriteRenderer _coin;
        private float       _t;
        private float       _pop = 1.55f;   // small burst-in, then settle

        public static void Spawn(Vector3 worldPos, long amount)
        {
            if (amount <= 0) return;

            var go = new GameObject("GoldPopup");
            go.transform.position = worldPos + new Vector3(Random.Range(-0.08f, 0.08f), 0.15f, -0.45f);

            string text = "+" + amount;

            // Coin disc just left of the number.
            var coinGO = new GameObject("Coin");
            coinGO.transform.SetParent(go.transform, false);
            coinGO.transform.localPosition = new Vector3(-0.34f, 0.02f, 0.02f);
            coinGO.transform.localScale    = Vector3.one * 0.30f;
            var coin = coinGO.AddComponent<SpriteRenderer>();
            coin.sprite       = CoinSprite();
            coin.color        = Gold;
            coin.sortingOrder = 21;

            // Black shadow copy (offset) for readability over any background.
            var shadowGO = new GameObject("Shadow");
            shadowGO.transform.SetParent(go.transform, false);
            shadowGO.transform.localPosition = new Vector3(0.035f, -0.035f, 0.05f);
            StyleText(shadowGO.AddComponent<TextMeshPro>(), text, 2.4f, new Color(0f, 0f, 0f, 0.85f), outline: false);
            var shMr = shadowGO.GetComponent<MeshRenderer>();
            if (shMr != null) shMr.sortingOrder = 20;

            // Main gold label with a black outline.
            StyleText(go.AddComponent<TextMeshPro>(), text, 2.4f, Gold, outline: true);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 22;   // above damage numbers

            var pop = go.AddComponent<GoldPopup>();
            pop._shadow = shadowGO.transform;
            pop._coin   = coin;
        }

        static TextMeshPro StyleText(TextMeshPro tmp, string text, float size, Color color, bool outline)
        {
            if (GameFont.Tmp != null) tmp.font = GameFont.Tmp;   // IBM Plex Sans
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = color;
            tmp.fontStyle = FontStyles.Bold;
            if (outline)
            {
                var mat = tmp.fontMaterial;
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                tmp.UpdateMeshPadding();
            }
            return tmp;
        }

        void Start()
        {
            _startPos = transform.position;
            _tmp      = GetComponent<TextMeshPro>();
        }

        void Update()
        {
            _t += Time.deltaTime;
            float p = _t / duration;
            if (p >= 1f) { Destroy(gameObject); return; }

            // Rise with an ease-out so the coin springs up then drifts to a stop.
            float rise = 1f - (1f - p) * (1f - p);
            transform.position = _startPos + Vector3.up * (riseDistance * rise);

            // Burst-in scale that settles to 1 over the first 25% of life.
            float settle = Mathf.Clamp01(p / 0.25f);
            float eased  = 1f - (1f - settle) * (1f - settle);
            transform.localScale = Vector3.one * Mathf.Lerp(_pop, 1f, eased);

            // Hold full opacity, then fade over the last 45% of life.
            float alpha = p < 0.55f ? 1f : 1f - (p - 0.55f) / 0.45f;
            if (_tmp != null)   { var c = Gold;              c.a = alpha; _tmp.color = c; }
            if (_coin != null)  { var c = _coin.color;       c.a = alpha; _coin.color = c; }
            if (_shadow != null)
            {
                var sTmp = _shadow.GetComponent<TextMeshPro>();
                if (sTmp != null) { var sc = sTmp.color; sc.a = 0.85f * alpha; sTmp.color = sc; }
            }
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

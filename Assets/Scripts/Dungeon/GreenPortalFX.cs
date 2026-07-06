using System.Collections;
using UnityEngine;

namespace ZulfarakRPG
{
    // Purely visual green portal (same pulsing three-ring look as Portal2D, green
    // palette, no trigger/scene transition). Used for the Necromancer's entrance:
    // Create() at the spawn spot, boss steps out, then Dismiss() shrinks it away.
    public class GreenPortalFX : MonoBehaviour
    {
        static readonly float[] RingSizes = { 1.40f, 1.00f, 0.55f };
        static readonly Color[] RingColors = {
            new Color(0.18f, 0.85f, 0.35f, 0.55f), // outer: dim green
            new Color(0.45f, 1.00f, 0.55f, 0.80f), // mid: bright green
            new Color(0.85f, 1.00f, 0.88f, 0.95f), // inner: white-green core
        };

        private SpriteRenderer[] _rings;
        private float _scaleMul = 0f;   // grows in on spawn, shrinks out on dismiss
        private bool _dismissing;

        public static GreenPortalFX Create(Vector3 pos)
        {
            var go = new GameObject("GreenPortalFX");
            go.transform.position = pos;
            return go.AddComponent<GreenPortalFX>();
        }

        void Start()
        {
            var spr = MakeRing();
            _rings = new SpriteRenderer[RingSizes.Length];
            for (int i = 0; i < RingSizes.Length; i++)
            {
                var go = new GameObject($"GlowRing_{i}");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * RingSizes[i];
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = spr;
                sr.color        = RingColors[i];
                sr.sortingOrder = i;   // boss SpriteRenderer (order 1+) draws in front of outer ring
                _rings[i] = sr;
            }
            StartCoroutine(GrowIn());
        }

        IEnumerator GrowIn()
        {
            float t = 0f;
            const float dur = 0.35f;
            while (t < dur && !_dismissing)
            {
                t += Time.deltaTime;
                _scaleMul = Mathf.Clamp01(t / dur);
                yield return null;
            }
            if (!_dismissing) _scaleMul = 1f;
        }

        void Update()
        {
            if (_rings == null) return;
            float time = Time.time;
            float[] speeds  = { 2.2f,  3.5f,  5.2f  };
            float[] phases  = { 0f,    2.09f, 4.19f };
            float[] baseAlp = { 0.55f, 0.78f, 0.92f };
            float[] ampAlp  = { 0.10f, 0.12f, 0.08f };
            float[] ampSc   = { 0.08f, 0.05f, 0.04f };

            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i] == null) continue;
                float s = Mathf.Sin(time * speeds[i] + phases[i]);
                _rings[i].transform.localScale = Vector3.one * (RingSizes[i] + s * ampSc[i]) * _scaleMul;
                var c = _rings[i].color;
                c.a = (baseAlp[i] + s * ampAlp[i]) * _scaleMul;
                _rings[i].color = c;
            }
        }

        public void Dismiss()
        {
            if (_dismissing) return;
            _dismissing = true;
            StartCoroutine(ShrinkOut());
        }

        IEnumerator ShrinkOut()
        {
            float start = _scaleMul;
            float t = 0f;
            const float dur = 0.45f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _scaleMul = Mathf.Lerp(start, 0f, t / dur);
                yield return null;
            }
            Destroy(gameObject);
        }

        static Sprite _ring;
        static Sprite MakeRing()
        {
            if (_ring != null) return _ring;
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
                    float a  = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) * 4f);
                    a       += Mathf.Clamp01(1f - d) * 0.30f;
                    t.SetPixel(x, y, new Color(0.55f, 1.0f, 0.65f, Mathf.Clamp01(a)));
                }
            t.Apply();
            _ring = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _ring;
        }
    }
}

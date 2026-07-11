using UnityEngine;

namespace ZulfarakRPG
{
    // Big centre-screen announcement (CLEAR / BOSS / DEFEAT) drawn in the SAME pixel-art
    // font as the damage numbers (PixelFont), on a dark background banner with a coloured
    // transition strip that wipes in beneath the word. World-space, parented to the camera
    // so it stays centred. Slides/wipes in, holds, then wipes out and self-destructs.
    public class PixelBanner : MonoBehaviour
    {
        public static void Show(string word, Color color)
        {
            var cam = Camera.main;
            if (cam == null || string.IsNullOrEmpty(word)) return;
            var root = new GameObject("PixelBanner");
            root.transform.SetParent(cam.transform, false);
            root.transform.localPosition = new Vector3(0f, 0.05f, 1f);   // centred, in front
            root.transform.localRotation = Quaternion.identity;
            root.AddComponent<PixelBanner>().Build(word, color);
        }

        SpriteRenderer _bg, _under, _txt;
        Color _txtColor, _underColor, _bgColor;
        float _bgFullW, _underFullW, _bgH, _underH, _txtBaseY, _slideFrom;
        float _t;
        const float InDur = 0.30f, Hold = 1.05f, OutDur = 0.30f, TargetH = 0.22f;

        void Build(string word, Color color)
        {
            _txtColor = color;
            var cam = Camera.main;

            // Word sprite (pixel font).
            var txtGO = new GameObject("word");
            txtGO.transform.SetParent(transform, false);
            _txt = txtGO.AddComponent<SpriteRenderer>();
            _txt.sprite = PixelFont.BuildText(word, color);
            _txt.sortingOrder = 601;

            float sprW = _txt.sprite.bounds.size.x;
            float sprH = _txt.sprite.bounds.size.y;

            // Size the word to FILL the screen: up to ~92% of the camera's visible width, but
            // no taller than ~62% of its height (so short words like BOSS don't overflow). For
            // an orthographic 2D camera the visible extent is 2×orthographicSize (× aspect).
            float scale, visW = 1f;
            if (cam != null && cam.orthographic && sprW > 1e-4f && sprH > 1e-4f)
            {
                float visH = cam.orthographicSize * 2f;
                visW = visH * cam.aspect;
                scale = Mathf.Min((visW * 0.92f) / sprW, (visH * 0.62f) / sprH);
            }
            else
            {
                scale = sprH > 1e-4f ? (TargetH / sprH) * 4f : 1.5f;   // big fallback
            }
            txtGO.transform.localScale = Vector3.one * scale;
            txtGO.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            float wordW = sprW * scale;
            float wordH = sprH * scale;

            // Dark background band behind the word — spans the FULL screen width so the huge
            // announcement reads as a full-screen transition splash.
            _bgFullW = Mathf.Max(visW, wordW + 0.4f);
            _bgH     = wordH + 0.18f;
            _bgColor = new Color(0.04f, 0.02f, 0.05f, 0.82f);
            _bg = MakeStrip("bg", _bgColor, 600);

            // Coloured transition strip that wipes in just BELOW the word (thickness scales
            // with the word so it stays proportional at any size).
            _underFullW = wordW + 0.20f;
            _underH     = Mathf.Max(0.02f, wordH * 0.10f);
            _underColor = color;
            _under = MakeStrip("under", color, 602);
            _under.transform.localPosition = new Vector3(0f, -wordH * 0.5f - 0.05f, -0.03f);

            _txtBaseY  = 0f;
            _slideFrom = wordH * 0.35f;   // drop-in distance, proportional to the word size
            // Start collapsed for the wipe-in.
            SetBgWidth(0f); SetUnderWidth(0f); SetTxtAlpha(0f);
        }

        SpriteRenderer MakeStrip(string name, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhitePixel();
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;   // unscaled → plays during any pause/celebration
            if (_t < InDur)
            {
                float p = 1f - Mathf.Pow(1f - _t / InDur, 3f);       // ease-out
                SetBgWidth(_bgFullW * p);
                SetUnderWidth(_underFullW * p);
                SetTxtAlpha(p);
                SetTxtSlide(Mathf.Lerp(_slideFrom, 0f, p));          // drop into place
            }
            else if (_t < InDur + Hold)
            {
                SetBgWidth(_bgFullW); SetUnderWidth(_underFullW); SetTxtAlpha(1f); SetTxtSlide(0f);
            }
            else if (_t < InDur + Hold + OutDur)
            {
                float p = (_t - InDur - Hold) / OutDur;
                float e = Mathf.Pow(p, 3f);                          // ease-in
                SetBgWidth(_bgFullW * (1f - e));
                SetUnderWidth(_underFullW * (1f - e));
                SetTxtAlpha(1f - p);
            }
            else Destroy(gameObject);
        }

        void SetBgWidth(float w)
        {
            if (_bg != null) _bg.transform.localScale = new Vector3(Mathf.Max(0f, w), _bgH, 1f);
        }
        void SetUnderWidth(float w)
        {
            if (_under != null) _under.transform.localScale = new Vector3(Mathf.Max(0f, w), _underH, 1f);
        }
        void SetTxtAlpha(float a)
        {
            if (_txt != null)   { var c = _txtColor;  c.a = a;         _txt.color = c; }
            if (_bg != null)    { var c = _bgColor;   c.a = 0.82f * a; _bg.color = c; }
            if (_under != null) { var c = _underColor; c.a = a;        _under.color = c; }
        }
        void SetTxtSlide(float dy)
        {
            if (_txt != null)
            {
                var p = _txt.transform.localPosition;
                _txt.transform.localPosition = new Vector3(p.x, _txtBaseY + dy, p.z);
            }
        }

        static Sprite _white;
        static Sprite WhitePixel()
        {
            if (_white != null) return _white;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, Color.white); t.Apply();
            _white = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _white;
        }
    }
}

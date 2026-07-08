using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZulfarakRPG
{
    // Full-screen stylised scene transition, modelled on the classic "transition
    // overlay" pattern (Brackeys-style): a Canvas + CanvasGroup that COVERS the screen
    // before the scene unloads and REVEALS it after the next one loads, so the swap
    // itself is hidden. Instead of a flat black fade this uses a portal-themed look:
    //   • a deep purple-black sheet that wipes in as a RADIAL (clock) wipe, and
    //   • a swirling violet portal glow that blooms at the centre while covered.
    // The reveal runs the wipe in reverse. Auto-bootstrapped and DontDestroyOnLoad so
    // the covered state persists across SceneManager.LoadScene.
    //
    // API is unchanged: FadeToBlack(duration[, onDone]) covers the screen; the reveal
    // runs automatically on the next sceneLoaded.
    public class SceneFader : MonoBehaviour
    {
        static SceneFader _instance;

        CanvasGroup _group;
        Image       _cover;   // radial-wipe sheet (deep purple-black)
        Image       _glow;    // centre portal glow
        RectTransform _glowRt;
        Coroutine   _active;
        bool        _covered;

        static readonly Color Sheet = new Color(0.05f, 0.02f, 0.09f, 1f);   // deep portal-black
        static readonly Color Glow  = new Color(0.62f, 0.34f, 0.95f, 1f);   // mystic violet

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("SceneFader");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneFader>();
            _instance.Build();
            SceneManager.sceneLoaded += _instance.OnSceneLoaded;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;                 // above every world sprite / TMP label
            gameObject.AddComponent<CanvasScaler>();

            // CanvasGroup fades the whole transition (sheet + glow) as one — and keeps
            // clicks flowing through once it's clear (blocksRaycasts off).
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha          = 0f;
            _group.blocksRaycasts = false;
            _group.interactable   = false;

            // Radial-wipe cover sheet.
            var coverGO = new GameObject("Cover");
            coverGO.transform.SetParent(transform, false);
            _cover = coverGO.AddComponent<Image>();
            _cover.sprite        = SolidSprite();
            _cover.color         = Sheet;
            _cover.raycastTarget = false;
            _cover.type          = Image.Type.Filled;
            _cover.fillMethod    = Image.FillMethod.Radial360;
            _cover.fillOrigin    = (int)Image.Origin360.Top;
            _cover.fillClockwise = true;
            _cover.fillAmount    = 1f;                 // starts fully covering; reveal wipes it away
            Stretch(_cover.rectTransform);

            // Centre portal glow that blooms while the screen is covered.
            var glowGO = new GameObject("PortalGlow");
            glowGO.transform.SetParent(transform, false);
            _glow = glowGO.AddComponent<Image>();
            _glow.sprite        = GlowSprite();
            _glow.color         = new Color(Glow.r, Glow.g, Glow.b, 0f);
            _glow.raycastTarget = false;
            _glowRt = _glow.rectTransform;
            _glowRt.anchorMin = new Vector2(0.5f, 0.5f);
            _glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            _glowRt.sizeDelta = new Vector2(700f, 700f);
            _glowRt.anchoredPosition = Vector2.zero;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void Update()
        {
            // Slowly rotate the glow so the covered moment reads as a live portal vortex.
            if (_glowRt != null && _group != null && _group.alpha > 0.01f)
                _glowRt.Rotate(0f, 0f, -60f * Time.unscaledDeltaTime);
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            // Covered on load (we transitioned via FadeToBlack) → wipe the new scene in.
            if (_covered)
            {
                if (_active != null) StopCoroutine(_active);
                _active = StartCoroutine(Reveal(0.5f));
            }
        }

        // Covers the screen with the portal wipe. Name/signature kept for back-compat.
        public static void FadeToBlack(float duration, Action onDone = null)
        {
            if (_instance == null) Bootstrap();
            if (_instance._active != null) _instance.StopCoroutine(_instance._active);
            _instance._active = _instance.StartCoroutine(_instance.Cover(duration, onDone));
        }

        IEnumerator Cover(float duration, Action cb)
        {
            duration = Mathf.Max(0.01f, duration);
            _covered = true;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                if (_group != null) _group.alpha = p;
                if (_cover != null) _cover.fillAmount = p;              // radial wipe in
                if (_glow  != null)
                {
                    var c = _glow.color; c.a = Mathf.SmoothStep(0f, 0.9f, p); _glow.color = c;
                    _glowRt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.15f, p);
                }
                yield return null;
            }
            if (_group != null) _group.alpha = 1f;
            if (_cover != null) _cover.fillAmount = 1f;
            _active = null;
            cb?.Invoke();
        }

        IEnumerator Reveal(float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                if (_cover != null) _cover.fillAmount = 1f - p;         // radial wipe out
                if (_glow  != null) { var c = _glow.color; c.a = 0.9f * (1f - p); _glow.color = c; }
                if (_group != null) _group.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.5f) / 0.5f));
                yield return null;
            }
            if (_group != null) _group.alpha = 0f;
            if (_cover != null) _cover.fillAmount = 1f;   // reset for next cover
            _covered = false;
            _active  = null;
        }

        // ── Procedural sprites ───────────────────────────────────────────────
        static Sprite _solid;
        static Sprite SolidSprite()
        {
            if (_solid != null) return _solid;
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            t.SetPixels(px); t.Apply();
            _solid = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _solid;
        }

        // Soft radial violet glow with a hint of ring structure so it reads as a vortex.
        static Sprite _glowSprite;
        static Sprite GlowSprite()
        {
            if (_glowSprite != null) return _glowSprite;
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Clamp01(1f - d);
                    core = core * core;                                   // bright dense centre
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.55f) * 3.5f) * 0.35f;
                    float a = Mathf.Clamp01(core + ring);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            _glowSprite = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _glowSprite;
        }
    }
}

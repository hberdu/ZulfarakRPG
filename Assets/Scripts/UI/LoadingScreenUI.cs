using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ZulfarakRPG
{
    // Runtime-built boot loading screen: opaque dark backdrop (the overlay window
    // is alpha-composited, so this is the only thing keeping the desktop hidden
    // while Unity boots), a stylized ZULFARAK logo title, a progress bar and a
    // status line. Created by GameBootstrap; destroyed via FinishAndFadeOut once
    // the first scene has finished loading.
    public class LoadingScreenUI : MonoBehaviour
    {
        private const float BarW = 300f;
        private const float BarH = 8f;

        private RectTransform   _fill;
        private TextMeshProUGUI _status;
        private CanvasGroup     _group;
        private float           _shownProgress;
        private float           _targetProgress;

        public static LoadingScreenUI Create()
        {
            var go = new GameObject("LoadingScreenUI");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;   // above SceneFader (999)

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480, 180);
            scaler.matchWidthOrHeight  = 0.5f;

            var ui = go.AddComponent<LoadingScreenUI>();
            ui._group = go.AddComponent<CanvasGroup>();
            ui.Build();
            return ui;
        }

        void Build()
        {
            // Opaque dark backdrop with a subtle vertical gradient feel (two layers).
            var bg = MakeImage("BG", transform, new Color(0.055f, 0.045f, 0.09f, 1f));
            Stretch(bg.rectTransform);

            // Pixel-art night panorama (Resources/UI/LoadingBg, 480x180) over the flat base
            // when present — the flat colour stays underneath as the opacity guarantee.
            var bgSprite = Resources.Load<Sprite>("UI/LoadingBg");
            if (bgSprite != null)
            {
                var art = MakeImage("BgArt", transform, Color.white);
                art.sprite = bgSprite;
                Stretch(art.rectTransform);
            }

            var vignette = MakeImage("Vignette", transform, new Color(0f, 0f, 0f, 0.35f));
            var vrt = vignette.rectTransform;
            vrt.anchorMin = new Vector2(0f, 0f);
            vrt.anchorMax = new Vector2(1f, 0.35f);
            vrt.offsetMin = vrt.offsetMax = Vector2.zero;

            // "ZULFARAK" logo title — gold, wide letter spacing, dark shadow layer.
            MakeTitle("LogoShadow", new Vector2(2f, 26f), new Color(0f, 0f, 0f, 0.8f));
            MakeTitle("Logo", new Vector2(0f, 28f), new Color(0.93f, 0.76f, 0.28f, 1f));

            // Progress trough
            var trough = MakeImage("Trough", transform, new Color(0f, 0f, 0f, 0.65f));
            var trt = trough.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, -22f);
            trt.sizeDelta = new Vector2(BarW + 4f, BarH + 4f);

            var border = MakeImage("Border", trough.transform, new Color(0.93f, 0.76f, 0.28f, 0.35f));
            var brt = border.rectTransform;
            Stretch(brt);
            brt.offsetMin = new Vector2(-1f, -1f);
            brt.offsetMax = new Vector2(1f, 1f);
            border.transform.SetAsFirstSibling();

            // Fill (left-anchored, width = progress)
            var fill = MakeImage("Fill", trough.transform, new Color(0.93f, 0.76f, 0.28f, 0.95f));
            _fill = fill.rectTransform;
            _fill.anchorMin = new Vector2(0f, 0.5f);
            _fill.anchorMax = new Vector2(0f, 0.5f);
            _fill.pivot     = new Vector2(0f, 0.5f);
            _fill.anchoredPosition = new Vector2(2f, 0f);
            _fill.sizeDelta = new Vector2(0f, BarH);

            // Status line under the bar
            var sgo = new GameObject("Status", typeof(RectTransform));
            var srt = (RectTransform)sgo.transform;
            srt.SetParent(transform, false);
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0f, -42f);
            srt.sizeDelta = new Vector2(BarW + 100f, 20f);
            _status = sgo.AddComponent<TextMeshProUGUI>();
            if (GameFont.Tmp != null) _status.font = GameFont.Tmp;
            _status.fontSize  = 11f;
            _status.alignment = TextAlignmentOptions.Center;
            _status.color     = new Color(1f, 1f, 1f, 0.75f);
            _status.raycastTarget = false;
            _status.text = "Iniciando...";
        }

        void MakeTitle(string name, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(500f, 50f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (GameFont.Tmp != null) tmp.font = GameFont.Tmp;
            tmp.text = "ZULFARAK";
            tmp.fontSize  = 34f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
        }

        void Update()
        {
            // Smooth the bar toward the target so steps don't snap.
            if (_shownProgress < _targetProgress)
            {
                _shownProgress = Mathf.MoveTowards(_shownProgress, _targetProgress, Time.unscaledDeltaTime * 0.9f);
                if (_fill) _fill.sizeDelta = new Vector2(BarW * _shownProgress, BarH);
            }
        }

        public void SetProgress(float progress, string status = null)
        {
            _targetProgress = Mathf.Clamp01(progress);
            if (!string.IsNullOrEmpty(status) && _status) _status.text = status;
        }

        // Snaps the bar to 100% and fades the whole screen out, then destroys it.
        public void FinishAndFadeOut(float delay = 0.15f, float duration = 0.4f)
        {
            SetProgress(1f);
            _shownProgress = 1f;
            if (_fill) _fill.sizeDelta = new Vector2(BarW, BarH);
            StartCoroutine(FadeOutRoutine(delay, duration));
        }

        IEnumerator FadeOutRoutine(float delay, float duration)
        {
            yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (_group) _group.alpha = 1f - Mathf.Clamp01(t / duration);
                yield return null;
            }
            Destroy(gameObject);
        }

        static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ZulfarakRPG
{
    // Lightweight panel animations without DOTween dependency.
    public static class UIAnimator
    {
        // Fade + slide-up panel open
        public static IEnumerator ShowPanel(CanvasGroup group, float duration = 0.18f)
        {
            group.alpha          = 0f;
            group.blocksRaycasts = true;
            var rect = group.GetComponent<RectTransform>();
            Vector2 start = rect.anchoredPosition - new Vector2(0, 20);
            Vector2 end   = rect.anchoredPosition;
            rect.anchoredPosition = start;

            float t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / duration;
                group.alpha                  = p;
                rect.anchoredPosition = Vector2.Lerp(start, end, EaseOut(p));
                yield return null;
            }
            group.alpha = 1f;
            rect.anchoredPosition = end;
        }

        // Fade out + slide down
        public static IEnumerator HidePanel(CanvasGroup group, float duration = 0.14f)
        {
            group.blocksRaycasts = false;
            var rect  = group.GetComponent<RectTransform>();
            Vector2 start = rect.anchoredPosition;
            Vector2 end   = rect.anchoredPosition - new Vector2(0, 20);

            float t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / duration;
                group.alpha           = 1f - p;
                rect.anchoredPosition = Vector2.Lerp(start, end, EaseIn(p));
                yield return null;
            }
            group.alpha = 0f;
            rect.anchoredPosition = start;
        }

        // Counter animation (numbers ticking up)
        public static IEnumerator AnimateCounter(TMPro.TextMeshProUGUI label, long from, long to, float duration = 0.6f)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                long value = (long)Mathf.Lerp(from, to, EaseOut(t / duration));
                label.text = value.ToString("N0");
                yield return null;
            }
            label.text = to.ToString("N0");
        }

        // Pulse scale on a reward pop
        public static IEnumerator Pulse(RectTransform rect, float scale = 1.2f, float duration = 0.25f)
        {
            Vector3 original = rect.localScale;
            float half = duration * 0.5f;
            float t = 0;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                rect.localScale = Vector3.Lerp(original, original * scale, t / half);
                yield return null;
            }
            t = 0;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                rect.localScale = Vector3.Lerp(original * scale, original, t / half);
                yield return null;
            }
            rect.localScale = original;
        }

        // Color flash on damage
        public static IEnumerator FlashColor(Image img, Color flashColor, float duration = 0.2f)
        {
            Color original = img.color;
            img.color = flashColor;
            yield return new WaitForSecondsRealtime(duration);
            img.color = original;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseIn(float t)  => t * t;
    }
}

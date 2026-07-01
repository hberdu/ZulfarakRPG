using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZulfarakRPG
{
    // Full-screen black overlay that fades in/out across scene transitions.
    //
    // Auto-bootstrapped on first runtime initialisation and DontDestroyOnLoad
    // so its alpha persists between SceneManager.LoadScene calls (e.g. the
    // dungeon portal entry: fade-to-black BEFORE the scene unloads, then
    // fade-back-from-black after the new scene loads).
    public class SceneFader : MonoBehaviour
    {
        static SceneFader _instance;
        Image     _img;
        Coroutine _active;

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
            // Screen-space overlay canvas with high sortingOrder so the black
            // sheet sits above every world-space sprite and TMP label. The
            // image's raycastTarget is OFF so clicks never get eaten by it.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder  = 999;
            gameObject.AddComponent<CanvasScaler>();

            var imgGO = new GameObject("FadeImage");
            imgGO.transform.SetParent(transform, false);
            _img = imgGO.AddComponent<Image>();
            _img.color         = new Color(0f, 0f, 0f, 0f);
            _img.raycastTarget = false;
            var rt = _img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            // If we transitioned via FadeToBlack the overlay is opaque on load;
            // fade it back to clear so the new scene is revealed cleanly. This
            // also masks Unity's brief one-frame "blink" during scene swap.
            if (_img != null && _img.color.a > 0.01f)
            {
                if (_active != null) StopCoroutine(_active);
                _active = StartCoroutine(FadeTo(0f, 0.35f));
            }
        }

        public static void FadeToBlack(float duration, Action onDone = null)
        {
            if (_instance == null) Bootstrap();
            if (_instance._active != null) _instance.StopCoroutine(_instance._active);
            _instance._active = _instance.StartCoroutine(_instance.FadeAndCall(1f, duration, onDone));
        }

        IEnumerator FadeAndCall(float target, float duration, Action cb)
        {
            yield return FadeTo(target, duration);
            cb?.Invoke();
        }

        IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (_img == null) yield break;
            float from = _img.color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var c = _img.color;
                c.a = Mathf.Lerp(from, targetAlpha, t / duration);
                _img.color = c;
                yield return null;
            }
            var fc = _img.color;
            fc.a = targetAlpha;
            _img.color = fc;
            _active    = null;
        }
    }
}

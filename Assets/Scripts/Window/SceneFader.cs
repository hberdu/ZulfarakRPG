using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZulfarakRPG
{
    // Minimal full-screen black fade across scene transitions — no stylised wipe/vortex.
    // Covers before the scene unloads and fades back in after the next scene loads, so the
    // swap itself is hidden. Auto-bootstrapped and DontDestroyOnLoad.
    public class SceneFader : MonoBehaviour
    {
        static SceneFader _instance;
        Image     _img;
        Coroutine _active;
        bool      _covered;

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
            canvas.sortingOrder = 999;
            gameObject.AddComponent<CanvasScaler>();

            var imgGO = new GameObject("FadeImage");
            imgGO.transform.SetParent(transform, false);
            _img = imgGO.AddComponent<Image>();
            _img.color         = new Color(0f, 0f, 0f, 0f);
            _img.raycastTarget = false;
            var rt = _img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (!_covered) return;
            if (_active != null) StopCoroutine(_active);
            _active = StartCoroutine(FadeTo(0f, 0.3f));
            _covered = false;
        }

        public static void FadeToBlack(float duration, Action onDone = null)
        {
            if (_instance == null) Bootstrap();
            _instance._covered = true;
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
            duration = Mathf.Max(0.01f, duration);
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var c = _img.color; c.a = Mathf.Lerp(from, targetAlpha, t / duration); _img.color = c;
                yield return null;
            }
            var fc = _img.color; fc.a = targetAlpha; _img.color = fc;
            _active = null;
        }
    }
}

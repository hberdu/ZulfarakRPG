using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZulfarakRPG
{
    // Forces every piece of Unity text in the game to render in IBM Plex Sans.
    //
    // No call site assigns a font explicitly, so all TextMeshPro / TextMeshProUGUI text
    // falls back to TMP's DEFAULT font asset. We therefore:
    //   1. build a dynamic IBM Plex TMP font asset at runtime (from the TTF in Resources),
    //   2. swap it in as TMP's default → every NEW text object uses it automatically,
    //   3. re-skin any already-created / scene-authored text on each scene load as a
    //      belt-and-suspenders pass (covers objects created before the default was set).
    public static class GameFont
    {
        public const string ResourcePath = "Fonts/IBMPlexSans-Regular";

        static Font          _legacy;
        static TMP_FontAsset _tmp;
        static bool          _loaded;

        public static Font          Legacy { get { EnsureLoaded(); return _legacy; } }
        public static TMP_FontAsset Tmp    { get { EnsureLoaded(); return _tmp;    } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureLoaded();
            if (_tmp != null) TrySetTmpDefault(_tmp);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            FontApplier.Ensure();
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            _legacy = Resources.Load<Font>(ResourcePath);
            if (_legacy == null)
            {
                Debug.LogWarning($"[GameFont] Font '{ResourcePath}' not found in Resources — text stays on the default font.");
                return;
            }

            // Dynamic TMP font asset: rasterizes IBM Plex glyphs on demand at runtime,
            // so no pre-baked SDF atlas (Editor-only) is required.
            _tmp = TMP_FontAsset.CreateFontAsset(_legacy);
            if (_tmp != null) _tmp.name = "IBMPlexSans SDF (runtime)";
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureLoaded();
            if (_tmp != null) TrySetTmpDefault(_tmp);   // re-assert (TMP_Settings may not exist at BeforeSceneLoad)
            FontApplier.Instance?.ScheduleReskin();
        }

        // Assign IBM Plex to every text component currently loaded (including inactive).
        public static void ApplyToAll()
        {
            EnsureLoaded();
            if (_tmp != null)
            {
                foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.font != _tmp) t.font = _tmp;
            }
            if (_legacy != null)
            {
                foreach (var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.font != _legacy) t.font = _legacy;
            }
        }

        // TMP_Settings.defaultFontAsset has no public setter — set the backing field so
        // every TMP object created afterwards adopts IBM Plex with no per-site change.
        static void TrySetTmpDefault(TMP_FontAsset asset)
        {
            try
            {
                var t = typeof(TMP_Settings);
                var instProp = t.GetProperty("instance",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var settings = instProp?.GetValue(null);
                if (settings == null) return;
                var field = t.GetField("m_defaultFontAsset", BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(settings, asset);
            }
            catch { /* best-effort; the per-scene re-skin still covers loaded text */ }
        }
    }

    // Re-skins text a few frames after each scene load (text is frequently built in
    // Start()/coroutines, i.e. after sceneLoaded fires). Cheap for this game's text count.
    class FontApplier : MonoBehaviour
    {
        public static FontApplier Instance { get; private set; }

        int _reskinFrames;

        internal static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("FontApplier");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<FontApplier>();
        }

        internal void ScheduleReskin() => _reskinFrames = 5;

        void Start() => _reskinFrames = 5;

        void LateUpdate()
        {
            if (_reskinFrames <= 0) return;
            _reskinFrames--;
            GameFont.ApplyToAll();
        }
    }
}

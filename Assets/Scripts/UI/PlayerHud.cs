using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Bottom-left HUD built from the GandalfHardcore HP-bar art:
    //   • A single round vessel (the "pote") holding red liquid that drains with health.
    // The source frame sprite is 116×64 (round orb ring on the left + a meter housing on
    // the right). We crop it to the left 64×64 square so ONLY the round pot is shown — the
    // meter beside it is dropped, and there is no mana/MP bar.
    public class PlayerHud : MonoBehaviour
    {
        static PlayerHud _instance;

        Canvas _canvas;
        Image  _hpFill;     // red liquid, fillAmount tracks health
        PlayerController2D _player;

        // Where the liquid sits inside the 64×64 ring crop (matches the frame art).
        static readonly Vector2 OrbMin = new Vector2( 3f / 64f,  5f / 64f);
        static readonly Vector2 OrbMax = new Vector2(59f / 64f, 59f / 64f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool gameplay = scene.name == "Zulfarak" || scene.name == "Dungeon";
            if (_instance == null && gameplay) Build();
            if (_instance != null) _instance._canvas.enabled = gameplay;
        }

        static void Build()
        {
            var root = new GameObject("PlayerHud");
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<PlayerHud>();

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;                         // below native popups (800)
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _instance._canvas = canvas;

            _instance._hpFill = BuildOrb(canvas.transform);
        }

        // Builds the round HP vessel pinned to the bottom-left corner: red liquid
        // (drains with health) over a dark "empty glass" backing, with the frame's
        // ring on top. Returns the liquid image so Update() can drive its fillAmount.
        static Image BuildOrb(Transform parent)
        {
            const float SIZE = 40f, margin = 8f;   // display size in px — shrink here to taste

            var group = new GameObject("HP", typeof(RectTransform));
            group.transform.SetParent(parent, false);
            var grt = (RectTransform)group.transform;
            grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0f, 0f); // bottom-left
            grt.sizeDelta = new Vector2(SIZE, SIZE);
            grt.anchoredPosition = new Vector2(margin, margin);

            var orbSprite = Resources.Load<Sprite>("HpBarOrb");   // red liquid globe
            var ring      = RingSprite();                          // frame cropped to the pot

            // Dark backing so the drained (empty) part reads as dark glass, not desktop.
            var back = MakeImage("Empty", grt, orbSprite, new Color(0.12f, 0.02f, 0.02f, 1f));
            SetRect(back, OrbMin, OrbMax);

            // Red liquid — vertical Filled so it lowers from the top as health drops.
            var fill = MakeImage("Liquid", grt, orbSprite, Color.white);
            SetRect(fill, OrbMin, OrbMax);
            fill.type       = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 1f;

            // Ring / vessel frame on top.
            if (ring != null)
            {
                var fr = MakeImage("Ring", grt, ring, Color.white);
                SetRect(fr, Vector2.zero, Vector2.one);
            }

            return fill;
        }

        void Update()
        {
            if (_hpFill == null) return;
            if (_player == null) _player = FindAnyObjectByType<PlayerController2D>();
            if (_player != null) _hpFill.fillAmount = _player.HealthFraction;
        }

        // ── UI helpers ────────────────────────────────────────────────────
        static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite        = sprite;
            img.color         = color;
            img.raycastTarget = false;
            return img;
        }

        static void SetRect(Image img, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = (RectTransform)img.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // Frame is 116×64: the round orb ring occupies the left 64px, the meter housing
        // the rest. Cropping to the left square yields just the round pot.
        static Sprite _ring;
        static Sprite RingSprite()
        {
            if (_ring != null) return _ring;
            var tex = Resources.Load<Texture2D>("HpBarFrame");
            if (tex == null) return null;
            _ring = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            return _ring;
        }
    }
}

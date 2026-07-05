using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Bottom-left HUD, collapsed to a SINGLE small "menu" button with an up-arrow
    // glyph — roughly the on-screen size of the floating world-map icon. Tapping it
    // opens the consolidated menu popup above the game strip.
    //
    // The old round HP-bar / statue portrait frame ("face") and the four square
    // action buttons were removed — health is shown by the world-space bar over the
    // character's head, and the four systems now live behind this one button.
    public class PlayerHud : MonoBehaviour
    {
        static PlayerHud _instance;
        Canvas _canvas;

        const float SIZE   = 24f;   // small square — about the on-screen size of the map icon
        const float MARGIN = 8f;    // gap from screen edge

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
            root.AddComponent<GraphicRaycaster>();
            _instance._canvas = canvas;

            BuildMenuButton(canvas.transform);
            BuildWindowButtons(canvas.transform);
        }

        static void BuildMenuButton(Transform parent)
        {
            var go = new GameObject("MenuButton", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(4f, 2f);   // low in the bottom-left corner
            rt.sizeDelta        = new Vector2(SIZE, SIZE);

            // Gold border + dark inner panel — matches the tooltip / popup theme.
            var border = go.AddComponent<Image>();
            border.color         = new Color(0.85f, 0.65f, 0.20f, 1f);
            border.raycastTarget = true;

            var innerGO = new GameObject("Inner", typeof(RectTransform));
            innerGO.transform.SetParent(go.transform, false);
            var irt = (RectTransform)innerGO.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2( 2f,  2f);
            irt.offsetMax = new Vector2(-2f, -2f);
            var inner = innerGO.AddComponent<Image>();
            inner.color         = new Color(0.10f, 0.08f, 0.06f, 0.95f);
            inner.raycastTarget = false;

            // Up-arrow glyph (procedural) centred in the button.
            var arrowGO = new GameObject("Arrow", typeof(RectTransform));
            arrowGO.transform.SetParent(innerGO.transform, false);
            var art = (RectTransform)arrowGO.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2( 3f,  3f);
            art.offsetMax = new Vector2(-3f, -3f);
            var arrow = arrowGO.AddComponent<Image>();
            arrow.sprite        = UpArrowSprite();
            arrow.color         = new Color(1f, 0.93f, 0.72f, 1f);
            arrow.raycastTarget = false;

            // Click handler: opens inventory/equipment native popup.
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = border;
            btn.onClick.AddListener(() => InventoryPopupWindow.Toggle());
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
                InventoryPopupWindow.Toggle();
        }

        // Two tiny buttons in the TOP-RIGHT corner: close (×) and minimize (_).
        const float WinBtn = 14f;
        static void BuildWindowButtons(Transform parent)
        {
            BuildWinBtn(parent, 0, "Close", CrossSprite(), new Color(0.80f, 0.28f, 0.24f, 1f), () => OverlayWindow.QuitGame());
            BuildWinBtn(parent, 1, "Min",   MinusSprite(), new Color(0.72f, 0.58f, 0.24f, 1f), () => OverlayWindow.Minimize());
        }

        static void BuildWinBtn(Transform parent, int idx, string name, Sprite glyph,
                                Color border, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("WinBtn_" + name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);   // top-right
            rt.anchoredPosition = new Vector2(-(3f + idx * (WinBtn + 3f)), -3f);
            rt.sizeDelta        = new Vector2(WinBtn, WinBtn);

            var bg = go.AddComponent<Image>();
            bg.color = border;

            var innerGO = new GameObject("Inner", typeof(RectTransform));
            innerGO.transform.SetParent(go.transform, false);
            var irt = (RectTransform)innerGO.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2( 1.5f,  1.5f);
            irt.offsetMax = new Vector2(-1.5f, -1.5f);
            var inner = innerGO.AddComponent<Image>();
            inner.color = new Color(0.10f, 0.08f, 0.06f, 0.95f);
            inner.raycastTarget = false;

            var gGO = new GameObject("Glyph", typeof(RectTransform));
            gGO.transform.SetParent(innerGO.transform, false);
            var grt = (RectTransform)gGO.transform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2( 2f,  2f);
            grt.offsetMax = new Vector2(-2f, -2f);
            var gImg = gGO.AddComponent<Image>();
            gImg.sprite = glyph;
            gImg.color = new Color(1f, 0.95f, 0.85f, 1f);
            gImg.raycastTarget = false;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);
        }

        static void Plot(Texture2D t, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int px = x + dx, py = y + dy;
                    if (px >= 0 && px < t.width && py >= 0 && py < t.height) t.SetPixel(px, py, Color.white);
                }
        }

        static Sprite _cross;
        static Sprite CrossSprite()
        {
            if (_cross != null) return _cross;
            const int N = 16;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) t.SetPixel(x, y, Color.clear);
            for (int i = 3; i < N - 3; i++) { Plot(t, i, i); Plot(t, i, N - 1 - i); }
            t.Apply();
            _cross = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _cross;
        }

        static Sprite _minus;
        static Sprite MinusSprite()
        {
            if (_minus != null) return _minus;
            const int N = 16;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) t.SetPixel(x, y, Color.clear);
            for (int x = 3; x < N - 3; x++) { t.SetPixel(x, 4, Color.white); t.SetPixel(x, 5, Color.white); }
            t.Apply();
            _minus = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _minus;
        }

        // Procedural white up-arrow (stem + arrowhead) on a transparent field,
        // tinted at runtime. y = 0 is the texture bottom, so the apex sits on top.
        static Sprite _upArrow;
        static Sprite UpArrowSprite()
        {
            if (_upArrow != null) return _upArrow;
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    t.SetPixel(x, y, Color.clear);

            float cx = (N - 1) * 0.5f;

            // Arrowhead: filled upward triangle — wide base, apex near the top.
            const int headBaseY = 10;
            const int headTopY  = N - 3;
            for (int y = headBaseY; y <= headTopY; y++)
            {
                float tprog = (float)(y - headBaseY) / (headTopY - headBaseY); // base 0 → apex 1
                float halfW = Mathf.Lerp(13f, 0f, tprog);
                for (int x = 0; x < N; x++)
                    if (Mathf.Abs(x - cx) <= halfW) t.SetPixel(x, y, Color.white);
            }

            // Stem: short vertical bar under the head.
            const int stemHalf = 3;
            for (int y = 2; y < headBaseY; y++)
                for (int x = 0; x < N; x++)
                    if (Mathf.Abs(x - cx) <= stemHalf) t.SetPixel(x, y, Color.white);

            t.Apply();
            _upArrow = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _upArrow;
        }
    }
}

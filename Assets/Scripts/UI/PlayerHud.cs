using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Bottom-left HUD: a compact row of pixel-art beveled buttons anchored to the
    // corner of the game strip. From left to right: the up-arrow menu button (opens
    // the inventory popup), a Map button (opens the World Map popup), and a Friends
    // button (opens the Steam invite popup).
    //
    // The old floating world icons (WorldMapIcon, InventoryWorldIcon, InviteFriendIcon)
    // are gone — the systems they exposed now live behind these HUD buttons instead.
    public class PlayerHud : MonoBehaviour
    {
        static PlayerHud _instance;
        Canvas _canvas;

        // Bottom-left button-row metrics (public so the dungeon progress bar can center
        // itself directly above the row).
        public const float ButtonSize     = 24f;   // small square — about the on-screen size of the map icon
        public const float ButtonGap      = 3f;    // spacing between HUD buttons
        public const float ButtonRowLeftX = 4f;    // x offset of the first button from the left edge
        public const float ButtonRowY     = 2f;    // y offset of the row from the bottom edge
        public const int   ButtonCount    = 3;     // Menu / Map / Friends

        const float SIZE   = ButtonSize;
        const float GAP    = ButtonGap;

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

            // Three pixel-art beveled buttons in a row along the bottom-left of the
            // game strip. All share the same 24×24 size + gold-bevel styling so they
            // read as a single tarnished-metal panel.
            BuildHudButton(canvas.transform, slot: 0, name: "MenuButton",   glyph: UpArrowSprite(),
                onClick: () => InventoryPopupWindow.Toggle());
            BuildHudButton(canvas.transform, slot: 1, name: "MapButton",    glyph: MapGlyphSprite(),
                onClick: () => WorldMapPopup.Show());
            BuildHudButton(canvas.transform, slot: 2, name: "FriendsButton", glyph: FriendsGlyphSprite(),
                onClick: () =>
                {
                    SteamLobbyManager.Instance?.EnsureLobby();
                    FriendsListPopup.Show();
                });

            BuildWindowButtons(canvas.transform);
        }

        // Pixel-art beveled HUD button: pitch-black outline, gold bevel (bright top/left
        // + dark bottom/right shoulder), near-black panel, ruby corner studs, then the
        // glyph centered inside. `slot` (0..n) offsets the button along the bottom-left row.
        static void BuildHudButton(Transform parent, int slot, string name, Sprite glyph,
                                   UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(ButtonRowLeftX + slot * (SIZE + GAP), ButtonRowY);
            rt.sizeDelta        = new Vector2(SIZE, SIZE);

            // Outer 1px pitch-black outline (target of the button click).
            var outline = go.AddComponent<Image>();
            outline.sprite        = SolidPixel();
            outline.color         = Color.black;
            outline.raycastTarget = true;

            // Bright gold shoulder — sits inside the outline and defines the button's
            // full-bright silhouette; the darker lower-right shoulder is painted over it.
            var hiGO = new GameObject("Hi", typeof(RectTransform));
            hiGO.transform.SetParent(go.transform, false);
            var hrt = (RectTransform)hiGO.transform;
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = new Vector2( 1f,  1f);
            hrt.offsetMax = new Vector2(-1f, -1f);
            var hi = hiGO.AddComponent<Image>();
            hi.sprite        = SolidPixel();
            hi.color         = new Color(0.95f, 0.75f, 0.30f, 1f);
            hi.raycastTarget = false;

            // Dark gold shoulder: shifted 2px down+right so the bright edges peek out
            // above and to the left — that's what gives the button its pixel-art bevel.
            var loGO = new GameObject("Lo", typeof(RectTransform));
            loGO.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)loGO.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2( 3f,  1f);
            lrt.offsetMax = new Vector2(-1f, -3f);
            var lo = loGO.AddComponent<Image>();
            lo.sprite        = SolidPixel();
            lo.color         = new Color(0.35f, 0.24f, 0.08f, 1f);
            lo.raycastTarget = false;

            // Inner near-black panel that the glyph sits on.
            var innerGO = new GameObject("Inner", typeof(RectTransform));
            innerGO.transform.SetParent(go.transform, false);
            var irt = (RectTransform)innerGO.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2( 3f,  3f);
            irt.offsetMax = new Vector2(-3f, -3f);
            var inner = innerGO.AddComponent<Image>();
            inner.sprite        = SolidPixel();
            inner.color         = new Color(0.08f, 0.06f, 0.05f, 1f);
            inner.raycastTarget = false;

            // Ruby corner stud (single dot in the upper-left corner) — classic
            // pixel-art bolt so the buttons feel like enameled metal instead of flat UI.
            var studGO = new GameObject("Stud", typeof(RectTransform));
            studGO.transform.SetParent(go.transform, false);
            var srt = (RectTransform)studGO.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(2f, -2f);
            srt.sizeDelta = new Vector2(2f, 2f);
            var stud = studGO.AddComponent<Image>();
            stud.sprite = SolidPixel();
            stud.color  = new Color(0.85f, 0.15f, 0.15f, 1f);
            stud.raycastTarget = false;

            // Centered glyph on top of the inner panel.
            var glyphGO = new GameObject("Glyph", typeof(RectTransform));
            glyphGO.transform.SetParent(innerGO.transform, false);
            var grt = (RectTransform)glyphGO.transform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2( 1f,  1f);
            grt.offsetMax = new Vector2(-1f, -1f);
            var gi = glyphGO.AddComponent<Image>();
            gi.sprite        = glyph;
            gi.color         = new Color(1f, 0.93f, 0.72f, 1f);
            gi.raycastTarget = false;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = outline;
            btn.onClick.AddListener(onClick);
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

        // Full-white 1×1 sprite so Unity's Image can tint solid-colour rectangles for the
        // pixel bevel/shoulder layers (Point filter keeps corners crisp at any scale).
        static Sprite _solid;
        static Sprite SolidPixel()
        {
            if (_solid != null) return _solid;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            _solid = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _solid;
        }

        // Chunky pixel-art map glyph: rolled parchment with a dashed route and a red
        // "you are here" marker. Fits the 24×24 HUD button (rendered at higher rez here
        // for tint clarity + Point filter → pixel-perfect scale-down).
        static Sprite _mapGlyph;
        static Sprite MapGlyphSprite()
        {
            if (_mapGlyph != null) return _mapGlyph;
            const int N = 16;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    t.SetPixel(x, y, Color.clear);

            // Parchment body (tinted white → any tint recolors it).
            for (int y = 3; y <= 12; y++)
                for (int x = 2; x <= 13; x++)
                    t.SetPixel(x, y, Color.white);
            // Rolled top/bottom bands — leave 1-pixel gaps to read as rolls.
            for (int x = 2; x <= 13; x++)
            {
                t.SetPixel(x, 2, Color.white);
                t.SetPixel(x, 13, Color.white);
            }
            // Ink dashed route across the middle.
            var ink = new Color(0.15f, 0.10f, 0.02f, 1f);
            for (int x = 3; x <= 12; x++)
            {
                int y = 7 + (int)(Mathf.Sin(x * 0.75f) * 1.2f);
                if (x % 2 == 0) t.SetPixel(x, y, ink);
            }
            // City dots
            t.SetPixel(4, 7, ink); t.SetPixel(5, 7, ink);
            t.SetPixel(9, 8, ink); t.SetPixel(10, 8, ink);
            // Red "you are here" marker
            var red = new Color(0.85f, 0.18f, 0.18f, 1f);
            t.SetPixel(12, 6, red); t.SetPixel(12, 7, red);

            t.Apply();
            _mapGlyph = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _mapGlyph;
        }

        // Chunky pixel-art envelope glyph with a small "+" badge for the friends invite
        // button. Reads instantly as "convidar" at 24×24 with Point filter.
        static Sprite _friendsGlyph;
        static Sprite FriendsGlyphSprite()
        {
            if (_friendsGlyph != null) return _friendsGlyph;
            const int N = 16;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    t.SetPixel(x, y, Color.clear);

            // Envelope body (tinted white so Image colour recolors it).
            for (int y = 3; y <= 11; y++)
                for (int x = 1; x <= 11; x++)
                    t.SetPixel(x, y, Color.white);
            // Flap V (drawn as a slightly darker inset).
            var fold = new Color(0.55f, 0.42f, 0.20f, 1f);
            for (int i = 0; i <= 5; i++)
            {
                t.SetPixel(1 + i, 10 - i, fold);
                t.SetPixel(11 - i, 10 - i, fold);
            }
            // "+" badge (green disk with a white plus) in the bottom-right corner.
            var plus   = new Color(0.20f, 0.82f, 0.40f, 1f);
            var plusDk = new Color(0.06f, 0.32f, 0.14f, 1f);
            // Disk radius 3 around (12, 3).
            for (int y = -3; y <= 3; y++)
                for (int x = -3; x <= 3; x++)
                {
                    if (x * x + y * y > 9) continue;
                    int px = 12 + x, py = 3 + y;
                    if (px < 0 || px >= N || py < 0 || py >= N) continue;
                    t.SetPixel(px, py, (Mathf.Abs(x) == 3 || Mathf.Abs(y) == 3) ? plusDk : plus);
                }
            // Plus glyph (white arms).
            for (int d = -1; d <= 1; d++)
            {
                t.SetPixel(12, 3 + d, Color.white);
                t.SetPixel(12 + d, 3, Color.white);
            }

            t.Apply();
            _friendsGlyph = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _friendsGlyph;
        }
    }
}

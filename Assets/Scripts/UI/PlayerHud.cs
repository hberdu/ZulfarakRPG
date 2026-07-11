using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace ZulfarakRPG
{
    // Bottom-left HUD: a compact COLUMN of pixel-art beveled buttons stacked along the
    // left edge of the game strip (so the shorter TaskbarHero-style gameplay window
    // still has room for the character). From bottom to top: the up-arrow menu button
    // (opens the inventory popup), a Map button (opens the World Map popup), and a
    // Friends button (opens the Steam invite popup).
    //
    // The Dungeon scene also gets two extra bottom-right buttons: a small pulsing red
    // portal (returns the party to the city, tooltip "Voltar para a cidade") and a
    // round yin-yang button whose arrows keep spinning (restarts the current dungeon).
    // The old floating world icons (WorldMapIcon, InventoryWorldIcon, InviteFriendIcon)
    // are gone — the systems they exposed now live behind these HUD buttons instead.
    public class PlayerHud : MonoBehaviour
    {
        static PlayerHud _instance;
        Canvas _canvas;
        GameObject _dungeonOnlyRoot;   // holds the two Dungeon-only right-side buttons
        GameObject _cityOnlyMap;       // hidden inside the dungeon per HUD spec
        GameObject _cityOnlyFriends;   // hidden inside the dungeon per HUD spec

        // When ON, returning to the city auto-walks the hero straight back into the
        // dungeon portal, looping the challenge. Persisted so it survives restarts.
        static bool _repeatOn;

        // Bottom-left button-column metrics (public so the dungeon progress bar can
        // align itself relative to the button rail).
        public const float ButtonSize          = 24f;   // small square — about the on-screen size of the map icon
        // Buttons touch (1 px overlap on their shared 9-slice border) so the whole
        // column of five HUD buttons fits inside the 120 px taskbar window without
        // pushing the topmost button off the top edge.
        public const float ButtonGap           = -1f;
        public const float ButtonColumnX       = 4f;    // x offset of the column from the left edge
        public const float ButtonColumnBottomY = 2f;    // y offset of the bottom-most button in the column
        public const int   ButtonCount         = 3;     // Menu / Map / Friends

        // Legacy aliases (row-layout) kept so any external caller compiled against the
        // old horizontal layout keeps building; both resolve to the column origin.
        public const float ButtonRowLeftX = ButtonColumnX;
        public const float ButtonRowY     = ButtonColumnBottomY;

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
            // Native top popups are Win32 windows, not GameObjects, so they survive a scene
            // change and get stranded at a position computed for the OLD strip (e.g. leaving
            // the dungeon with the skill tree open left it bugged over the city). Close them
            // all on every transition — they're transient overlays and reopen from the HUD.
            TopPopups.CloseAllExcept(TopPopups.Kind.None);

            bool gameplay = scene.name == "Zulfarak" || scene.name == "Dungeon";
            if (_instance == null && gameplay) Build();
            if (_instance != null)
            {
                _instance._canvas.enabled = gameplay;
                bool inDungeon = scene.name == "Dungeon";
                // The return-to-city portal + yin-yang restart only make sense inside
                // the dungeon; hide them in the city so they don't sit on top of the NPCs.
                if (_instance._dungeonOnlyRoot != null)
                    _instance._dungeonOnlyRoot.SetActive(inDungeon);
                // Symmetrically, the map + friends buttons only make sense in the city;
                // the dungeon HUD keeps the inventory, skills, repeat and progress rail.
                // (Repeat is always visible — the player may toggle it mid-run.)
                if (_instance._cityOnlyMap     != null) _instance._cityOnlyMap.SetActive(!inDungeon);
                if (_instance._cityOnlyFriends != null) _instance._cityOnlyFriends.SetActive(!inDungeon);

                // Repeat-challenge loop: back in the city with the toggle ON, auto-walk
                // the hero straight into the dungeon portal again.
                if (!inDungeon && _repeatOn)
                    _instance.StartCoroutine(_instance.AutoRepeatToDungeon());
            }
        }

        // Waits for the city to settle, then walks the hero into the open Dungeon portal.
        IEnumerator AutoRepeatToDungeon()
        {
            yield return new WaitForSecondsRealtime(0.9f);
            if (!_repeatOn || SceneManager.GetActiveScene().name != "Zulfarak") yield break;

            Portal2D dungeonPortal = null;
            foreach (var p in FindObjectsByType<Portal2D>(FindObjectsSortMode.None))
                if (p != null && p.IsOpen &&
                    string.Equals(p.destinationScene, "Dungeon", System.StringComparison.OrdinalIgnoreCase))
                { dungeonPortal = p; break; }
            if (dungeonPortal == null) yield break;

            var player = FindAnyObjectByType<PlayerController2D>();
            if (player != null) player.AutoWalkToX(dungeonPortal.transform.position.x);
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

            // Three pixel-art beveled buttons stacked vertically along the bottom-left
            // of the game strip. All share the same 24×24 size + gold-bevel styling so
            // they read as a single tarnished-metal panel. Bottom → top: Menu, Map,
            // Friends — the frequently used inventory sits closest to the thumb. Map
            // and Friends only appear inside the city; the dungeon collapses down to
            // just the inventory button (see OnSceneLoaded).
            // Bottom-left button column. Slots stacked bottom → top:
            //   0: Menu (inventory) — always
            //   1: Magias (skill tree) — always (available in the city AND the
            //      dungeon so the mage can retune spells mid-run).
            //   2: Map        — city only
            //   3: Friends    — city only
            //   4: Repeat run — city only
            BuildHudButton(canvas.transform, slot: 0, name: "MenuButton",   glyph: UpArrowSprite(),
                onClick: () => InventoryPopupWindow.Toggle());
            BuildHudButton(canvas.transform, slot: 1, name: "SkillsButton", glyph: WizardHatSprite(),
                onClick: () => SkillTreePopup.Show());
            _instance._cityOnlyMap = BuildHudButton(canvas.transform, slot: 2, name: "MapButton",    glyph: MapGlyphSprite(),
                onClick: () => WorldMapPopup.Show());
            _instance._cityOnlyFriends = BuildHudButton(canvas.transform, slot: 3, name: "FriendsButton", glyph: FriendsGlyphSprite(),
                onClick: () =>
                {
                    SteamLobbyManager.Instance?.EnsureLobby();
                    FriendsListPopup.Show();
                });

            // "Repeat challenge" toggle (slot 4 = above Friends). Available in both
            // scenes so the player can flip it on/off mid-run without stopping at the
            // city. When ON in the city it auto-walks back into the dungeon portal.
            _repeatOn = PlayerPrefs.GetInt("dungeon_repeat", 0) == 1;
            BuildRepeatButton(canvas.transform, slot: 4);

            BuildWindowButtons(canvas.transform);
            _instance.BuildDungeonButtons(canvas.transform);
        }

        // Repeat-challenge toggle button (left column). Toggles _repeatOn; when ON it turns
        // purple. While ON, returning to the city loops the hero straight back into the
        // dungeon portal (see OnSceneLoaded / AutoRepeatToDungeon).
        static GameObject BuildRepeatButton(Transform parent, int slot)
        {
            var go = new GameObject("RepeatButton", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(ButtonColumnX, ButtonColumnBottomY + slot * (SIZE + GAP));
            rt.sizeDelta        = new Vector2(SIZE, SIZE);

            var chassis = go.AddComponent<Image>();
            chassis.sprite        = RpgUiSprites.ButtonBlankLight();
            chassis.type          = Image.Type.Sliced;
            chassis.raycastTarget = true;

            var coverGO = new GameObject("Cover", typeof(RectTransform));
            coverGO.transform.SetParent(go.transform, false);
            var crt = (RectTransform)coverGO.transform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2( 4f,  4f); crt.offsetMax = new Vector2(-4f, -4f);
            var cover = coverGO.AddComponent<Image>();
            cover.sprite        = SolidPixel();
            cover.raycastTarget = false;

            var glyphGO = new GameObject("Glyph", typeof(RectTransform));
            glyphGO.transform.SetParent(coverGO.transform, false);
            var grt = (RectTransform)glyphGO.transform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2( 1f,  1f); grt.offsetMax = new Vector2(-1f, -1f);
            var gi = glyphGO.AddComponent<Image>();
            gi.sprite         = RepeatSprite();
            gi.preserveAspect = true;
            gi.raycastTarget  = false;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = chassis;
            btn.onClick.AddListener(() =>
            {
                _repeatOn = !_repeatOn;
                PlayerPrefs.SetInt("dungeon_repeat", _repeatOn ? 1 : 0);
                ApplyRepeatVisual(cover, gi);
                // Turning it on from the city starts the loop right away.
                if (_repeatOn && _instance != null) _instance.StartCoroutine(_instance.AutoRepeatToDungeon());
            });
            ApplyRepeatVisual(cover, gi);
            AttachTooltip(go, "Repetir desafio (loop)");
            return go;
        }

        static void ApplyRepeatVisual(Image cover, Image glyph)
        {
            if (_repeatOn)
            {
                cover.color = new Color(0.44f, 0.16f, 0.66f, 1f);   // purple = ON
                glyph.color = new Color(1f, 0.92f, 1f, 1f);
            }
            else
            {
                cover.color = new Color(0.17f, 0.13f, 0.17f, 1f);   // dark = OFF
                glyph.color = new Color(1f, 0.94f, 0.78f, 1f);
            }
        }

        // HUD button chassis is now the shield sprite from the RPG UI Pack (see
        // RpgUiSprites.ButtonBlankLight). We render the shield at atlas resolution
        // (26×32) and drop the game glyph on top of it, so every button matches the
        // pack's pixel style out of the box. `slot` (0..n) stacks the button in the
        // bottom-left column (slot 0 = closest to the bottom edge). Returns the
        // button root so the caller can retain a reference (used to hide the
        // city-only buttons in the dungeon).
        static GameObject BuildHudButton(Transform parent, int slot, string name, Sprite glyph,
                                         UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(ButtonColumnX, ButtonColumnBottomY + slot * (SIZE + GAP));
            rt.sizeDelta        = new Vector2(SIZE, SIZE);

            // Button chassis from the atlas (9-sliced so it fills the square cleanly). It's
            // the click target + pixel-art frame in one image.
            var chassis = go.AddComponent<Image>();
            chassis.sprite        = RpgUiSprites.ButtonBlankLight();
            chassis.type          = Image.Type.Sliced;
            chassis.raycastTarget = true;

            // Dark cover over the pack button's BAKED glyph, so only our glyph shows.
            var coverGO = new GameObject("Cover", typeof(RectTransform));
            coverGO.transform.SetParent(go.transform, false);
            var crt = (RectTransform)coverGO.transform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2( 4f,  4f);
            crt.offsetMax = new Vector2(-4f, -4f);
            var cover = coverGO.AddComponent<Image>();
            cover.sprite        = SolidPixel();
            cover.color         = new Color(0.17f, 0.13f, 0.17f, 1f);
            cover.raycastTarget = false;

            // Our glyph centred on the cover.
            var glyphGO = new GameObject("Glyph", typeof(RectTransform));
            glyphGO.transform.SetParent(coverGO.transform, false);
            var grt = (RectTransform)glyphGO.transform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2( 1f,  1f);
            grt.offsetMax = new Vector2(-1f, -1f);
            var gi = glyphGO.AddComponent<Image>();
            gi.sprite         = glyph;
            gi.color          = new Color(1f, 0.94f, 0.78f, 1f);
            gi.preserveAspect = true;
            gi.raycastTarget  = false;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = chassis;
            btn.onClick.AddListener(onClick);
            return go;
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

        // Repeat/loop glyph: a circular ring (two arcs) with a down-arrow on the right and
        // an up-arrow on the left, so the two arrows chase each other clockwise.
        static Sprite _repeat;
        static Sprite RepeatSprite()
        {
            if (_repeat != null) return _repeat;
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) t.SetPixel(x, y, Color.clear);

            float cx = 15.5f, cy = 15.5f, R = 10f, thick = 2.4f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Mathf.Abs(d - R) > thick) continue;
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; if (ang < 0f) ang += 360f;
                    // Two arcs with gaps on the right (~340..20) and left (~160..200).
                    if ((ang >= 22f && ang <= 158f) || (ang >= 202f && ang <= 338f))
                        t.SetPixel(x, y, Color.white);
                }

            // Down-arrow on the right (apex at bottom, widening up).
            ArrowTriV(t, 24, 11, +1, 4);
            // Up-arrow on the left (apex at top, widening down).
            ArrowTriV(t, 7, 20, -1, 4);

            t.Apply();
            _repeat = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _repeat;
        }

        // Vertical filled arrowhead. up=+1 → points DOWN (apex low, widen upward);
        // up=-1 → points UP (apex high, widen downward).
        static void ArrowTriV(Texture2D t, int ax, int apexY, int up, int size)
        {
            for (int s = 0; s <= size; s++)
                for (int w = -s; w <= s; w++)
                {
                    int px = ax + w, py = apexY + up * s;
                    if (px >= 0 && px < t.width && py >= 0 && py < t.height) t.SetPixel(px, py, Color.white);
                }
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

        // Chunky pixel-art wizard hat glyph for the Skills button (Magias). A pointed
        // triangle hat with a yellow star on the band and a wide brim below — reads
        // instantly as "spells / magic" at the 24×24 HUD size (Point filter keeps the
        // pixel corners crisp). The glyph is white so the button code can tint it.
        static Sprite _wizardHat;
        static Sprite WizardHatSprite()
        {
            if (_wizardHat != null) return _wizardHat;
            const int N = 16;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    t.SetPixel(x, y, Color.clear);

            var hat  = Color.white;
            var star = new Color(1f, 0.86f, 0.28f, 1f);

            // Legend: H = hat cloth, S = yellow star, . = transparent.
            // Art rows are top → bottom; texture Y is flipped when writing.
            string[] rows = {
                ".......HH.......",   //  0 — tip of the hat
                "......HHHH......",   //  1
                "......HHHH......",   //  2
                ".....HHHHHH.....",   //  3
                ".....HHHHHH.....",   //  4
                "....HHHHHHHH....",   //  5
                "....HHHHHHHH....",   //  6
                "...HHHHSSHHHH...",   //  7 — star on the band
                "...HHHSSSSHHH...",   //  8
                "..HHHSSSSSSHHH..",   //  9
                "..HHHHSSSSHHHH..",   // 10
                ".HHHHHHSSHHHHHH.",   // 11
                "HHHHHHHHHHHHHHHH",   // 12 — brim
                "HHHHHHHHHHHHHHHH",   // 13 — brim (double-tall)
                "................",  // 14
                "................",  // 15
            };

            for (int row = 0; row < rows.Length && row < N; row++)
            {
                string s = rows[row];
                int texY = N - 1 - row;
                for (int col = 0; col < s.Length && col < N; col++)
                {
                    switch (s[col])
                    {
                        case 'H': t.SetPixel(col, texY, hat);  break;
                        case 'S': t.SetPixel(col, texY, star); break;
                    }
                }
            }

            t.Apply();
            _wizardHat = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _wizardHat;
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

        // ── Dungeon-only right-side buttons ─────────────────────────────────────
        // A pulsing red mini-portal (returns the party to Zulfarak) and a round
        // yin-yang restart glyph whose arrows keep spinning. Both live under a
        // single _dungeonOnlyRoot so we can hide them wholesale outside the dungeon.
        void BuildDungeonButtons(Transform parent)
        {
            _dungeonOnlyRoot = new GameObject("DungeonButtons", typeof(RectTransform));
            var rrt = (RectTransform)_dungeonOnlyRoot.transform;
            rrt.SetParent(parent, false);
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0f, 0f);
            rrt.anchoredPosition = Vector2.zero;
            rrt.sizeDelta        = Vector2.zero;

            // Bottom-right corner: yin-yang closest to the edge (slot 0), red portal
            // just to its left (slot 1) so the two icons mirror the left-column rail.
            BuildYinYangButton(_dungeonOnlyRoot.transform, slot: 0);
            BuildReturnPortalButton(_dungeonOnlyRoot.transform, slot: 1);

            _dungeonOnlyRoot.SetActive(false);   // OnSceneLoaded flips this per scene
        }

        // Small pulsing red portal — clicking it triggers the exit Portal2D so the
        // return trip reuses the same absorb/fade/lobby-broadcast pipeline as walking
        // into the portal in world space.
        static void BuildReturnPortalButton(Transform parent, int slot)
        {
            var go = MakeBevelRoot(parent, "ReturnPortalButton", slot,
                                   hi:   new Color(0.95f, 0.32f, 0.28f, 1f),
                                   lo:   new Color(0.42f, 0.10f, 0.06f, 1f),
                                   stud: new Color(1.00f, 0.86f, 0.30f, 1f));

            // Three concentric red rings inside the inner panel — mini-me of Portal2D.
            var inner = go.transform.Find("Inner");
            var ring0 = MakeRingChild(inner, "Ring0", new Color(0.95f, 0.18f, 0.18f, 0.55f));
            var ring1 = MakeRingChild(inner, "Ring1", new Color(1.00f, 0.42f, 0.32f, 0.80f));
            var ring2 = MakeRingChild(inner, "Ring2", new Color(1.00f, 0.86f, 0.72f, 0.95f));
            var pulse = go.AddComponent<HudPortalPulse>();
            pulse.rings     = new [] { ring0, ring1, ring2 };
            pulse.baseScale = new [] { 1.00f, 0.72f, 0.42f };
            pulse.speeds    = new [] { 2.2f,  3.4f,  5.0f  };

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (Portal2D.TriggerReturnToCity()) return;
                // Fallback: no return portal in the scene (shouldn't happen during
                // normal play) — just fade to black and swap the scene ourselves.
                SceneFader.FadeToBlack(0.3f, () => SceneManager.LoadScene("Zulfarak"));
            });

            AttachTooltip(go, "Voltar para a cidade");
        }

        // Round yin-yang restart button: the glyph itself rotates continuously so the
        // two arrows keep chasing each other. Clicking reloads the current Dungeon.
        static void BuildYinYangButton(Transform parent, int slot)
        {
            var go = MakeBevelRoot(parent, "RestartButton", slot,
                                   hi:   new Color(0.30f, 0.85f, 0.95f, 1f),
                                   lo:   new Color(0.06f, 0.24f, 0.35f, 1f),
                                   stud: new Color(0.95f, 0.95f, 0.98f, 1f));

            var inner = go.transform.Find("Inner");
            var glyphGO = new GameObject("YinYang", typeof(RectTransform));
            glyphGO.transform.SetParent(inner, false);
            var grt = (RectTransform)glyphGO.transform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2( 1f,  1f);
            grt.offsetMax = new Vector2(-1f, -1f);
            var gi = glyphGO.AddComponent<Image>();
            gi.sprite        = YinYangSprite();
            gi.color         = Color.white;
            gi.raycastTarget = false;
            var spin = glyphGO.AddComponent<HudSpin>();
            spin.degreesPerSecond = 90f;

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                SceneFader.FadeToBlack(0.3f, () =>
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            });

            AttachTooltip(go, "Reiniciar dungeon");
        }

        // Shared button chassis for the right-side dungeon buttons. Uses the same
        // shield sprite from the RPG UI Pack as the left column (via BuildHudButton),
        // but anchored to bottom-right so the two buttons mirror the left rail.
        // `tint` is applied on top of the shield so we can differentiate the portal
        // (warm red) from the restart (cool teal). The `Inner` child is a full-rect
        // stretched RectTransform where callers drop their glyph/animation content.
        static GameObject MakeBevelRoot(Transform parent, string name, int slot,
                                        Color hi, Color lo, Color stud)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-(ButtonColumnX + slot * (SIZE + GAP)), ButtonColumnBottomY);
            rt.sizeDelta        = new Vector2(SIZE, SIZE);

            // Shield chassis from the atlas. `hi` doubles as the shield tint so each
            // right-column button reads with its element colour without a second layer.
            var chassis = go.AddComponent<Image>();
            chassis.sprite            = RpgUiSprites.ButtonBlankLight();
            chassis.preserveAspect    = true;
            chassis.color             = new Color(hi.r * 0.85f + 0.15f,
                                                  hi.g * 0.85f + 0.15f,
                                                  hi.b * 0.85f + 0.15f, 1f);
            chassis.raycastTarget     = true;

            // Kept for backward compat: callers do go.transform.Find("Inner") to
            // parent their glyph/animation content. It stretches to the same rect the
            // shield occupies, minus a small margin that matches the shield's inner recess.
            var innerGO = new GameObject("Inner", typeof(RectTransform));
            innerGO.transform.SetParent(go.transform, false);
            var irt = (RectTransform)innerGO.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2( 4f,  6f);
            irt.offsetMax = new Vector2(-4f, -4f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = chassis;
            _ = lo; _ = stud;  // colours from the old bevel API — now unused

            return go;
        }

        // Adds an alpha-ringed Image inside the inner panel; used to stack three of
        // these into the red portal button's pulsing mini-portal.
        static Image MakeRingChild(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite        = AlphaRingSprite();
            img.color         = color;
            img.raycastTarget = false;
            return img;
        }

        // Wraps a button with a small tooltip that appears above it on pointer-enter.
        static void AttachTooltip(GameObject host, string message)
        {
            var tipGO = new GameObject("Tooltip", typeof(RectTransform));
            tipGO.transform.SetParent(host.transform, false);
            var trt = (RectTransform)tipGO.transform;

            // Auto-align the tooltip to the same side of the button as the button is
            // anchored on, so right-column buttons don't push their labels off-screen.
            var hostRT = (RectTransform)host.transform;
            bool rightAligned = hostRT.anchorMin.x >= 0.5f;
            if (rightAligned)
            {
                trt.anchorMin = trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot     = new Vector2(1f, 0f);
            }
            else
            {
                trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
                trt.pivot     = new Vector2(0f, 0f);
            }
            trt.anchoredPosition = new Vector2(0f, 4f);
            trt.sizeDelta = new Vector2(Mathf.Max(48f, message.Length * 5f + 10f), 12f);

            var bg = tipGO.AddComponent<Image>();
            bg.color         = new Color(0f, 0f, 0f, 0.92f);
            bg.raycastTarget = false;

            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.transform.SetParent(tipGO.transform, false);
            var lrt = (RectTransform)lgo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2( 2f,  1f);
            lrt.offsetMax = new Vector2(-2f, -1f);
            var lbl = lgo.AddComponent<TextMeshProUGUI>();
            lbl.text      = message;
            lbl.fontSize  = 8f;
            lbl.color     = Color.white;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.raycastTarget = false;

            tipGO.SetActive(false);

            var toggler = host.AddComponent<HudTooltip>();
            toggler.tip = tipGO;
        }

        // Procedural yin-yang glyph: red half + ivory half divided by the classic
        // S-curve made of two half-circles, with opposite-colour eyes. When spun by
        // HudSpin the two halves read as arrows chasing each other around the disc.
        static Sprite _yinYang;
        static Sprite YinYangSprite()
        {
            if (_yinYang != null) return _yinYang;
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    t.SetPixel(x, y, Color.clear);

            float cx = (N - 1) * 0.5f;
            float cy = (N - 1) * 0.5f;
            const float R  = 14.5f;
            const float R2 = 7.25f;

            var red   = new Color(0.90f, 0.22f, 0.22f, 1f);
            var pale  = new Color(1.00f, 0.97f, 0.90f, 1f);
            var edge  = new Color(0.06f, 0.03f, 0.03f, 1f);

            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > R) continue;
                    if (d > R - 1f) { t.SetPixel(x, y, edge); continue; }

                    // Divide the disc with two half-circles (radius R/2) stacked
                    // vertically — the classic yin-yang S-curve.
                    float dTop = Mathf.Sqrt(dx * dx + (dy - R2) * (dy - R2));
                    float dBot = Mathf.Sqrt(dx * dx + (dy + R2) * (dy + R2));
                    Color c;
                    if      (dTop < R2) c = red;         // inside top small circle → red
                    else if (dBot < R2) c = pale;        // inside bottom small circle → pale
                    else if (dy > 0f)   c = pale;        // upper half is pale (yang)
                    else                c = red;         // lower half is red (yin)

                    // Eye dots (small opposite-colour discs in each half).
                    if (dTop < 2.2f) c = pale;
                    if (dBot < 2.2f) c = red;

                    t.SetPixel(x, y, c);
                }
            }

            t.Apply();
            _yinYang = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _yinYang;
        }

        // Soft alpha ring sprite: an anti-aliased circle with a bright rim and a
        // fading centre. Tinted at runtime; used by the red mini-portal button to
        // stack three concentric rings for the pulse effect.
        static Sprite _alphaRing;
        static Sprite AlphaRingSprite()
        {
            if (_alphaRing != null) return _alphaRing;
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float cx = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - cx) / cx;
                    float dy = (y - cx) / cx;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    float a  = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 3.5f);
                    a       += Mathf.Clamp01(1f - d) * 0.30f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            t.Apply();
            _alphaRing = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _alphaRing;
        }

        // ── Nested helper components ──────────────────────────────────────────────
        // Continuous rotation for the yin-yang glyph — negative in Unity's screen
        // space so the sprite spins visually clockwise.
        class HudSpin : MonoBehaviour
        {
            public float degreesPerSecond = 90f;
            void Update() => transform.Rotate(0f, 0f, -degreesPerSecond * Time.deltaTime);
        }

        // Pulses concentric ring Images the way Portal2D pulses its world-space
        // rings, scaled/coloured in-place so several rings stack into a mini-portal.
        class HudPortalPulse : MonoBehaviour
        {
            public Image[] rings;
            public float[] baseScale;
            public float[] speeds;
            void Update()
            {
                if (rings == null) return;
                float t = Time.time;
                for (int i = 0; i < rings.Length; i++)
                {
                    if (rings[i] == null) continue;
                    float sp    = speeds    != null && i < speeds.Length    ? speeds[i]    : 3f;
                    float baseS = baseScale != null && i < baseScale.Length ? baseScale[i] : 1f;
                    float s = Mathf.Sin(t * sp + i * 2.09f);
                    rings[i].rectTransform.localScale = Vector3.one * (baseS + s * 0.08f);
                    var c = rings[i].color;
                    c.a = Mathf.Clamp01(baseS * 0.55f + 0.35f + s * 0.20f);
                    rings[i].color = c;
                }
            }
        }

        // Bare-bones tooltip toggler: shows a pre-built child on pointer-enter and
        // hides it on exit. Uses UGUI event interfaces so it plays nicely with the
        // Button + GraphicRaycaster already on the HUD canvas.
        class HudTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public GameObject tip;
            public void OnPointerEnter(PointerEventData eventData) { if (tip != null) tip.SetActive(true); }
            public void OnPointerExit (PointerEventData eventData) { if (tip != null) tip.SetActive(false); }
        }
    }
}

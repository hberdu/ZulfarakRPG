using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Runtime slicer for the "Pixel RPG UI Pack" atlas (Assets/Resources/rpg_ui.png).
    //
    // The pack ships as one big sprite sheet with buttons, banners, scrolls, papyrus
    // and mission boards. We load it once from Resources at runtime and expose named
    // Sprites cut out with hard-coded pixel rects — that way every HUD/menu piece
    // shares the same atlas and building a new UI element is just picking a name.
    //
    // Coordinates were sampled directly off Ui.png (984×640, top-left origin) and can
    // be tuned in one place if a rect looks off. To adjust visually use the Editor
    // window at Zulfarak → RPG UI Atlas Inspector (RpgUiAtlasWindow.cs).
    public static class RpgUiSprites
    {
        // ── Atlas ─────────────────────────────────────────────────────────────
        static Texture2D _atlas;
        static Texture2D Atlas
        {
            get
            {
                if (_atlas != null) return _atlas;
                _atlas = Resources.Load<Texture2D>("rpg_ui");
                if (_atlas == null)
                    Debug.LogError("[RpgUiSprites] Resources/rpg_ui.png não encontrado. Copie o Ui.png do pack para Assets/Resources/rpg_ui.png.");
                else
                    _atlas.filterMode = FilterMode.Point;
                return _atlas;
            }
        }

        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        // Cut a rect from the atlas. Coordinates are TOP-LEFT (Y grows down) to match
        // how the pack was authored; we convert to Unity's bottom-up here.
        // `border` optionally enables 9-slice stretching (left, bottom, right, top).
        public static Sprite Slice(string name, int x, int y, int w, int h, Vector4 border = default, float pivotY = 0.5f)
        {
            if (_cache.TryGetValue(name, out var s) && s != null) return s;
            var tex = Atlas;
            if (tex == null || w <= 0 || h <= 0) return null;
            int py = tex.height - (y + h);
            var rect = new Rect(x, py, w, h);
            var sp = Sprite.Create(tex, rect, new Vector2(0.5f, pivotY), 100f,
                                   0, SpriteMeshType.FullRect, border);
            sp.name = name;
            _cache[name] = sp;
            return sp;
        }

        public static void ClearCache() { _cache.Clear(); _atlas = null; }

        // ── Square buttons (top-left grid) ────────────────────────────────────
        // Measured off the atlas: each button is 20×24 px. Group A starts at x=24, y=57
        // (3 columns at x 24/56/88); group B (pressed/alt shade) starts at x=135. Row
        // pitch is 32 px. The pack bakes a glyph into every cell, so the HUD covers the
        // centre with a dark panel and draws its own glyph on top.
        public const int  BtnCellW   = 20;
        public const int  BtnCellH   = 24;
        public const int  BtnGridX   = 24;      // atlas x of the first button
        public const int  BtnGridY   = 57;      // atlas y of the first button
        public const int  BtnStepX   = 32;      // horizontal pitch inside a group
        public const int  BtnStepY   = 32;      // vertical pitch between rows
        public const int  BtnGroupBX = 135;     // atlas x of group B's first column

        // Returns a button-cell sprite (9-sliced 4px so it scales to any HUD size).
        // group 0 = normal shade, 1 = alt/pressed; col 0..2; row 0..5.
        public static Sprite ButtonCell(int group, int col, int row)
        {
            col = Mathf.Clamp(col, 0, 2);
            row = Mathf.Clamp(row, 0, 5);
            int baseX = group >= 1 ? BtnGroupBX : BtnGridX;
            int x = baseX + col * BtnStepX;
            int y = BtnGridY + row * BtnStepY;
            return Slice($"btn_{group}_{col}_{row}", x, y, BtnCellW, BtnCellH, new Vector4(4, 4, 4, 4));
        }

        // Neutral button chassis (the HUD hides the baked glyph and adds its own).
        public static Sprite ButtonBlankLight()  => ButtonCell(0, 0, 0);
        public static Sprite ButtonBlankDark()   => ButtonCell(1, 0, 0);

        // ── Horizontal papyrus banner (right-side of the sheet) ───────────────
        // Perfect frame for a progress bar: papyrus middle stretches, curly ends fixed.
        // 9-slice border keeps the pointed ends crisp when the bar changes width.
        public static Sprite BannerHorizontal()
            => Slice("banner_h", 828, 380, 96, 30, new Vector4(14, 6, 14, 6));

        // Alternative slim ribbon (brown/painted) — used as a title tag on top of panels.
        public static Sprite RibbonBrown()
            => Slice("ribbon_brown", 828, 320, 60, 26, new Vector4(10, 4, 10, 4));

        // ── Panel backgrounds ─────────────────────────────────────────────────
        // Dark bulletin board (top-right of the sheet) — used as popup background.
        public static Sprite BulletinBoard()
            => Slice("bulletin", 780, 24, 200, 148, new Vector4(20, 24, 20, 12));

        // Papyrus page (open scroll body) — used as a subtle content panel.
        public static Sprite PapyrusPage()
            => Slice("papyrus_page", 380, 24, 84, 108, new Vector4(10, 10, 10, 12));

        // Big open book background (center of the sheet) — great for menus with pages.
        public static Sprite BookBackground()
            => Slice("book_bg", 208, 344, 272, 168, new Vector4(24, 24, 24, 24));

        // ── Speech bubbles (center of the sheet) ──────────────────────────────
        public static Sprite SpeechBubbleDark()
            => Slice("bubble_dark", 400, 220, 168, 32, new Vector4(10, 8, 18, 8));
        public static Sprite SpeechBubblePapyrus()
            => Slice("bubble_papyrus", 584, 220, 152, 32, new Vector4(12, 8, 20, 8));

        // ── Arrow direction buttons (bottom-right corner) ─────────────────────
        public static Sprite ArrowButtonLeft()  => Slice("arrow_left",  760, 452, 24, 20);
        public static Sprite ArrowButtonRight() => Slice("arrow_right", 792, 452, 24, 20);
        public static Sprite ArrowButtonUp()    => Slice("arrow_up",    824, 480, 24, 20);
        public static Sprite ArrowButtonDown()  => Slice("arrow_down",  824, 508, 24, 20);
    }
}

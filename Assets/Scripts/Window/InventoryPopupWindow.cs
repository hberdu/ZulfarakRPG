using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZulfarakRPG
{
    // Native Win32 inventory/equipment popup rendered outside the game window.
    // Actions (equip/unequip/use) call Inventory methods, which forward to server APIs.
    //
    // Fixed to the game strip width (400 px). All content — dragon emblem, character
    // summary, equipment column, scrollable bag, compact stats — fits inside a single
    // pixel-art beveled frame that sits directly above the game window.
    public static class InventoryPopupWindow
    {
        // Width is pinned to the live game-strip width (like MenuPopupWindow) so the
        // inventory sits flush above the game and shares its exact width. Height is fixed.
        public static int PopupWidth => _popupWidth;
        public const int PopupHeight = 480;

        static int _popupWidth = 400;   // actual created window width; refreshed from the game strip
        static int CurrentWidth() => OverlayWindow.Instance != null ? OverlayWindow.Instance.windowWidth : 400;

        // Red-Eyes-Black-Dragon emblem (Resources/UI/DragonFrame); absent → plain dark card.
        const string DragonRes = "UI/DragonFrame";

        // Section layout (popup-local pixels, no outer margins — the pixel bevel IS the frame).
        const int HeaderH   = 28;
        const int SummaryH  = 46;              // name/class/level line
        const int StatsH    = 44;              // 2 rows × 3 stats compact strip
        const int SectionHeaderH = 18;         // "Equipamento" / "Sacola" bar
        const int FooterH   = 16;
        const int DollSlot  = 44;              // equipment slot cell (paper-doll)
        const int BagCell   = 46;              // bag icon cell
        const int BagGap    = 4;

        static int BodyTop     => HeaderH + SummaryH + StatsH;
        static int BodyBottom  => PopupHeight - FooterH;
        // Two panes: left = Diablo-style paper doll, right = bag icon grid.
        static int LeftPaneW   => PopupWidth * 52 / 100;
        static int RightPaneX  => LeftPaneW + 2;
        static int RightPaneW  => PopupWidth - RightPaneX - 2;

        // Bag grid metrics.
        static int GridLeft  => RightPaneX + 6;
        static int GridRight => PopupWidth - 8;
        static int GridTop   => BodyTop + SectionHeaderH + 4;
        static int GridBot   => BodyBottom - 4;
        static int BagCols     => Mathf.Max(1, (GridRight - GridLeft + BagGap) / (BagCell + BagGap));
        static int BagRowsCount => Mathf.CeilToInt(_bagRows.Count / (float)BagCols);
        static int MaxBagScroll => Mathf.Max(0, BagRowsCount * (BagCell + BagGap) - (GridBot - GridTop));

        struct EquipRow
        {
            public string label;
            public ItemType slotType;
            public string itemId;
            public string itemName;
            public ItemRarity rarity;
            public bool has;
            public string iconPath;
        }

        struct BagRow
        {
            public string itemId;
            public string itemName;
            public int quantity;
            public bool consumable;
            public ItemRarity rarity;
            public string iconPath;
        }

        static readonly List<EquipRow> _equipRows = new List<EquipRow>(6);
        static readonly List<BagRow> _bagRows = new List<BagRow>(64);
        static int _bagScrollY;
        static float _nextRefreshAt;

        public static bool IsOpen => _hwnd != IntPtr.Zero;

        public static void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        public static void Show()
        {
            TopPopups.CloseAllExcept(TopPopups.Kind.Inventory);
            // Seed the bag with one item of every quality per slot so the equipment
            // visuals can be tested (idempotent — skips items already held).
            TestItems.AddAllToBag(Inventory.Instance);
#if UNITY_EDITOR
            Debug.Log("[InventoryPopup] aberto (editor).");
#else
            if (_hwnd != IntPtr.Zero)
            {
                Reposition();
                InvalidateRect(_hwnd, IntPtr.Zero, true);
                SetForegroundWindow(_hwnd);
                return;
            }

            EnsureClassRegistered();
            EnsureGdiObjects();
            _bagScrollY = 0;
            _popupWidth = CurrentWidth();
            (int x, int y) = ComputePosition();

            _hwnd = CreateWindowExW(
                WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
                ClassName,
                "Inventario",
                WS_POPUP | WS_VISIBLE,
                x, y, PopupWidth, PopupHeight,
                IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);

            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SW_SHOW);
                SetForegroundWindow(_hwnd);
                _nextRefreshAt = Time.unscaledTime;
                InventoryPopupPump.Ensure();
            }
#endif
        }

        public static void Hide()
        {
#if UNITY_EDITOR
#else
            if (_hwnd == IntPtr.Zero) return;
            var h = _hwnd;
            _hwnd = IntPtr.Zero;
            DestroyWindow(h);
#endif
        }

        public static void Reposition()
        {
            if (_hwnd == IntPtr.Zero) return;
            int newW = CurrentWidth();
            bool sizeChanged = newW != _popupWidth;
            _popupWidth = newW;
            (int x, int y) = ComputePosition();
            if (sizeChanged)
            {
                SetWindowPos(_hwnd, HWND_TOPMOST, x, y, _popupWidth, PopupHeight,
                    SWP_SHOWWINDOW | SWP_NOACTIVATE);
                InvalidateRect(_hwnd, IntPtr.Zero, true);
            }
            else
            {
                SetWindowPos(_hwnd, HWND_TOPMOST, x, y, 0, 0,
                    SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
            }
        }

        static (int, int) ComputePosition()
        {
            int gx = OverlayWindow.WinX;
            int gy = OverlayWindow.WinY;
            int gw = CurrentWidth();
            int x = gx + (gw - PopupWidth) / 2;
            int y = gy - PopupHeight;
            int sw = Screen.currentResolution.width;
            int sh = Screen.currentResolution.height;
            x = Mathf.Clamp(x, 0, Mathf.Max(0, sw - PopupWidth));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, sh - PopupHeight));
            return (x, y);
        }

        static void RebuildRows()
        {
            _equipRows.Clear();
            _bagRows.Clear();

            var inv = Inventory.Instance;
            if (inv == null) return;
            var eq = inv.Equipment ?? new Equipment();

            // Six equipment slots, in the requested order.
            AddEquipRow("Capacete", ItemType.Helmet, eq.helmetId);
            AddEquipRow("Peito",    ItemType.Chest,  eq.chestId);
            AddEquipRow("Maos",     ItemType.Gloves, eq.glovesId);
            AddEquipRow("Pes",      ItemType.Boots,  eq.bootsId);
            AddEquipRow("Arma",     ItemType.Weapon, eq.weaponId);
            AddEquipRow("Capa",     ItemType.Cape,   eq.capeId);

            var items = inv.Items;
            items.Sort((a, b) => string.Compare(a.itemId, b.itemId, StringComparison.Ordinal));
            var db = ItemDatabase.Instance;
            foreach (var it in items)
            {
                var data = db != null ? db.Get(it.itemId) : null;
                _bagRows.Add(new BagRow
                {
                    itemId = it.itemId,
                    itemName = data != null && !string.IsNullOrWhiteSpace(data.itemName) ? data.itemName : it.itemId,
                    quantity = it.quantity,
                    consumable = data != null && data.itemType == ItemType.Consumable,
                    rarity = data != null ? data.rarity : ItemRarity.Common,
                    iconPath = data != null ? data.iconPath : null
                });
            }

            _bagScrollY = Mathf.Clamp(_bagScrollY, 0, MaxBagScroll);
        }

        static void AddEquipRow(string label, ItemType slotType, string itemId)
        {
            var data = !string.IsNullOrWhiteSpace(itemId) && ItemDatabase.Instance != null
                       ? ItemDatabase.Instance.Get(itemId) : null;
            _equipRows.Add(new EquipRow
            {
                label = label,
                slotType = slotType,
                itemName = data != null && !string.IsNullOrWhiteSpace(data.itemName) ? data.itemName : itemId,
                rarity = data != null ? data.rarity : ItemRarity.Common,
                has = !string.IsNullOrWhiteSpace(itemId),
                itemId = itemId,
                iconPath = data != null ? data.iconPath : null
            });
        }

        // ── Paper-doll + bag-grid layout (shared by Paint and click hit-tests) ──
        struct DollSlotRect { public int equipIndex; public int x, y, w, h; }
        struct BagCellRect { public int bagIndex; public int x, y, w, h; }
        static readonly List<DollSlotRect> _dollSlots = new List<DollSlotRect>(6);
        static readonly List<BagCellRect> _bagCells = new List<BagCellRect>(64);

        // Diablo-style doll: hero in the centre, 3 slots down each side.
        // Left column (top→bottom): Capacete, Peito, Maos. Right: Capa, Pes, Arma.
        static readonly int[] LeftCol  = { 0, 1, 2 };   // indices into _equipRows
        static readonly int[] RightCol = { 5, 3, 4 };

        static void BuildDoll()
        {
            _dollSlots.Clear();
            if (_equipRows.Count < 6) return;
            int top   = BodyTop + SectionHeaderH + 6;
            int bot   = BodyBottom - 6;
            int rowH  = (bot - top) / 3;
            int leftX = 10;
            int rightX = LeftPaneW - 8 - DollSlot;
            for (int r = 0; r < 3; r++)
            {
                int cy = top + r * rowH + (rowH - DollSlot) / 2;
                _dollSlots.Add(new DollSlotRect { equipIndex = LeftCol[r],  x = leftX,  y = cy, w = DollSlot, h = DollSlot });
                _dollSlots.Add(new DollSlotRect { equipIndex = RightCol[r], x = rightX, y = cy, w = DollSlot, h = DollSlot });
            }
        }

        static void BuildBag()
        {
            _bagCells.Clear();
            int cols = BagCols;
            for (int i = 0; i < _bagRows.Count; i++)
            {
                int col = i % cols, row = i / cols;
                int x = GridLeft + col * (BagCell + BagGap);
                int y = GridTop + row * (BagCell + BagGap) - _bagScrollY;
                _bagCells.Add(new BagCellRect { bagIndex = i, x = x, y = y, w = BagCell, h = BagCell });
            }
        }

        static IntPtr _hwnd = IntPtr.Zero;
        static WndProcDelegate _wndProcDelegate;
        static bool _classRegistered;
        static IntPtr _brushBorder, _brushVoid, _brushPanel;
        static IntPtr _brushOutline, _brushBevHi, _brushBevLo, _brushRuby, _brushDivider;
        static IntPtr _brushRowA, _brushRowB, _brushTag, _brushTagUse, _brushSlot;
        static IntPtr _fontTitle, _fontRow, _fontHint, _fontTag, _fontSection, _fontSummary;
        // Quality border brushes (index 0..3 = Comum/Raro/Mito/Lendario).
        static readonly IntPtr[] _qBrush = new IntPtr[4];
        static readonly int[] QRarity = { (int)ItemRarity.Common, (int)ItemRarity.Rare, (int)ItemRarity.Epic, (int)ItemRarity.Legendary };
        static int QIndex(ItemRarity r) => r switch
        {
            ItemRarity.Rare => 1, ItemRarity.Epic => 2, ItemRarity.Legendary => 3, _ => 0
        };
        static IntPtr QBrushOf(ItemRarity r) => _qBrush[QIndex(r)];
        const string ClassName = "ZulfarakInventoryPopup";

        static void EnsureClassRegistered()
        {
            if (_classRegistered) return;
            _wndProcDelegate = WndProc;
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = CS_OWNDC,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = GetModuleHandleW(null),
                hCursor = LoadCursorW(IntPtr.Zero, (IntPtr)IDC_ARROW),
                hbrBackground = IntPtr.Zero,
                lpszClassName = ClassName,
            };
            RegisterClassExW(ref wc);
            _classRegistered = true;
        }

        static void EnsureGdiObjects()
        {
            // Pixel-art palette: pitch-black outline, warm gold bevel highlight, dark-gold
            // shoulder, near-black panel base, ruby corner studs, alternating row bands.
            if (_brushVoid    == IntPtr.Zero) _brushVoid    = CreateSolidBrush(Bgr(0.02f, 0.02f, 0.03f));
            if (_brushPanel   == IntPtr.Zero) _brushPanel   = CreateSolidBrush(Bgr(0.06f, 0.05f, 0.05f));
            if (_brushBorder  == IntPtr.Zero) _brushBorder  = CreateSolidBrush(Bgr(0.52f, 0.40f, 0.15f));
            if (_brushOutline == IntPtr.Zero) _brushOutline = CreateSolidBrush(Bgr(0.00f, 0.00f, 0.00f));
            if (_brushBevHi   == IntPtr.Zero) _brushBevHi   = CreateSolidBrush(Bgr(0.95f, 0.75f, 0.30f));
            if (_brushBevLo   == IntPtr.Zero) _brushBevLo   = CreateSolidBrush(Bgr(0.35f, 0.24f, 0.08f));
            if (_brushRuby    == IntPtr.Zero) _brushRuby    = CreateSolidBrush(Bgr(0.85f, 0.15f, 0.15f));
            if (_brushDivider == IntPtr.Zero) _brushDivider = CreateSolidBrush(Bgr(0.20f, 0.15f, 0.06f));
            if (_brushRowA    == IntPtr.Zero) _brushRowA    = CreateSolidBrush(Bgr(0.10f, 0.08f, 0.08f));
            if (_brushRowB    == IntPtr.Zero) _brushRowB    = CreateSolidBrush(Bgr(0.14f, 0.11f, 0.10f));
            if (_brushTag     == IntPtr.Zero) _brushTag     = CreateSolidBrush(Bgr(0.32f, 0.11f, 0.10f));
            if (_brushTagUse  == IntPtr.Zero) _brushTagUse  = CreateSolidBrush(Bgr(0.16f, 0.32f, 0.12f));
            if (_brushSlot    == IntPtr.Zero) _brushSlot    = CreateSolidBrush(Bgr(0.09f, 0.08f, 0.10f));
            if (_qBrush[0]    == IntPtr.Zero)
                for (int i = 0; i < 4; i++)
                    _qBrush[i] = CreateSolidBrush(BgrOf(ItemData.QualityColor((ItemRarity)QRarity[i])));
            if (_fontTitle    == IntPtr.Zero) _fontTitle    = MakeFont(15, FW_BOLD);
            if (_fontSection  == IntPtr.Zero) _fontSection  = MakeFont(12, FW_BOLD);
            if (_fontSummary  == IntPtr.Zero) _fontSummary  = MakeFont(11, FW_NORMAL);
            if (_fontRow      == IntPtr.Zero) _fontRow      = MakeFont(11, FW_NORMAL);
            if (_fontHint     == IntPtr.Zero) _fontHint     = MakeFont(10, FW_NORMAL);
            if (_fontTag      == IntPtr.Zero) _fontTag      = MakeFont(10, FW_BOLD);
        }

        static IntPtr MakeFont(int sizePx, int weight)
        {
            var lf = new LOGFONT
            {
                lfHeight = -sizePx,
                lfWeight = weight,
                lfCharSet = DEFAULT_CHARSET,
                lfQuality = CLEARTYPE_QUALITY,
                lfFaceName = NativeFont.Face,
            };
            return CreateFontIndirectW(ref lf);
        }

        static uint Bgr(float r, float g, float b)
        {
            byte R = (byte)(Mathf.Clamp01(r) * 255);
            byte G = (byte)(Mathf.Clamp01(g) * 255);
            byte B = (byte)(Mathf.Clamp01(b) * 255);
            return (uint)(R | (G << 8) | (B << 16));
        }

        static uint BgrOf(Color c) => Bgr(c.r, c.g, c.b);

        // ── Doll / grid draw helpers ──────────────────────────────────────
        static PlayerController2D _heroRef;
        static NativeFrameImage GetHeroImage()
        {
            if (_heroRef == null) _heroRef = UnityEngine.Object.FindAnyObjectByType<PlayerController2D>();
            if (_heroRef == null) return null;
            var sr = _heroRef.GetComponent<SpriteRenderer>();
            var spr = sr != null ? sr.sprite : null;
            if (spr == null || spr.texture == null) return null;
            // Key by the frame's texture + sub-rect so each distinct frame (including the
            // recoloured, possibly-unnamed equipment frames) builds its DIB exactly once.
            string key = $"hero:{spr.texture.GetHashCode()}:{Mathf.RoundToInt(spr.rect.x)}:{Mathf.RoundToInt(spr.rect.y)}:{Mathf.RoundToInt(spr.rect.width)}x{Mathf.RoundToInt(spr.rect.height)}";
            return NativeFrameImage.GetTexture(key, () => ExtractSpriteTex(spr));
        }

        static Texture2D ExtractSpriteTex(Sprite s)
        {
            if (s == null || s.texture == null) return null;
            try
            {
                var r = s.rect;
                var px = s.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                var t = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                t.SetPixels(px); t.Apply();
                return t;
            }
            catch { return null; }
        }

        static void DrawPaneHeader(IntPtr hdc, int left, int right, int bodyTop, string text)
        {
            var bar = new RECT { Left = left, Top = bodyTop + 3, Right = right, Bottom = bodyTop + SectionHeaderH };
            FillRect(hdc, ref bar, _brushDivider);
            SelectObject(hdc, _fontSection);
            SetTextColor(hdc, Bgr(1f, 0.82f, 0.32f));
            var rc = new RECT { Left = left + 4, Top = bodyTop + 1, Right = right, Bottom = bodyTop + SectionHeaderH };
            DrawTextW(hdc, text, -1, ref rc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
        }

        // 2px frame in the item's quality colour around a slot / bag cell.
        static void DrawQualityBorder(IntPtr hdc, int x, int y, int w, int h, ItemRarity r)
        {
            var b = QBrushOf(r);
            if (b == IntPtr.Zero) return;
            var t  = new RECT { Left = x + 1, Top = y + 1,     Right = x + w - 1, Bottom = y + 3 };
            var bo = new RECT { Left = x + 1, Top = y + h - 3, Right = x + w - 1, Bottom = y + h - 1 };
            var l  = new RECT { Left = x + 1, Top = y + 1,     Right = x + 3,     Bottom = y + h - 1 };
            var rr = new RECT { Left = x + w - 3, Top = y + 1, Right = x + w - 1, Bottom = y + h - 1 };
            FillRect(hdc, ref t, b); FillRect(hdc, ref bo, b); FillRect(hdc, ref l, b); FillRect(hdc, ref rr, b);
        }

        static string SlotShort(ItemType t) => t switch
        {
            ItemType.Helmet => "Elmo", ItemType.Chest  => "Peito", ItemType.Gloves => "Maos",
            ItemType.Boots  => "Pes",  ItemType.Weapon => "Arma",  ItemType.Cape   => "Capa",
            _ => ""
        };

        static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_PAINT:
                    BeginPaint(hWnd, out var ps);
                    Paint(ps.hdc);
                    EndPaint(hWnd, ref ps);
                    return IntPtr.Zero;
                case WM_ERASEBKGND:
                    return new IntPtr(1);
                case WM_KEYDOWN:
                    if (wParam.ToInt32() == VK_ESCAPE) Hide();
                    return IntPtr.Zero;
                case WM_MOUSEWHEEL:
                {
                    RebuildRows();
                    int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    _bagScrollY = Mathf.Clamp(_bagScrollY - (delta / 120) * (BagCell + BagGap), 0, MaxBagScroll);
                    InvalidateRect(hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }
                case WM_LBUTTONDOWN:
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    // Close box (top-right of header)?
                    if (my < HeaderH && mx >= PopupWidth - 26) { Hide(); return IntPtr.Zero; }

                    RebuildRows();
                    BuildDoll();
                    BuildBag();

                    // Click an equipped doll slot → unequip.
                    foreach (var s in _dollSlots)
                    {
                        if (mx < s.x || mx > s.x + s.w || my < s.y || my > s.y + s.h) continue;
                        var row = _equipRows[s.equipIndex];
                        if (row.has) { Inventory.Instance?.Unequip(row.slotType); InvalidateRect(hWnd, IntPtr.Zero, true); }
                        return IntPtr.Zero;
                    }

                    // Click a bag icon (inside the grid viewport) → equip / use.
                    foreach (var c in _bagCells)
                    {
                        if (c.y + c.h <= GridTop || c.y >= GridBot) continue;
                        if (mx < c.x || mx > c.x + c.w || my < c.y || my > c.y + c.h) continue;
                        var row = _bagRows[c.bagIndex];
                        if (row.consumable) Inventory.Instance?.UseConsumable(row.itemId);
                        else                Inventory.Instance?.Equip(row.itemId);
                        InvalidateRect(hWnd, IntPtr.Zero, true);
                        return IntPtr.Zero;
                    }
                    return IntPtr.Zero;
                }
                case WM_CLOSE:
                case WM_DESTROY:
                    _hwnd = IntPtr.Zero;
                    return IntPtr.Zero;
            }
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        // Alternating opaque row band — the new layout is fully solid (no dragon behind
        // the text) so the rows use plain FillRect for maximum readability at 400×360.
        static void FillRow(IntPtr hdc, int left, int top, int right, int bottom, int i)
        {
            var rc = new RECT { Left = left, Top = top, Right = right, Bottom = bottom };
            FillRect(hdc, ref rc, (i & 1) == 0 ? _brushRowA : _brushRowB);
        }

        static void Paint(IntPtr hdc)
        {
            RebuildRows();

            int w = PopupWidth;
            int h = PopupHeight;

            // Solid near-black base, then chunky pixel-art bevel + corner studs.
            var full = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            FillRect(hdc, ref full, _brushPanel);
            NativeFrameImage.PixelBevel(hdc, 0, 0, w, h, _brushOutline, _brushBevHi, _brushBevLo, _brushPanel);
            NativeFrameImage.PixelCornerStuds(hdc, 0, 0, w, h, _brushRuby, inset: 5, size: 3);

            SetBkMode(hdc, TRANSPARENT);

            // ── Header bar ────────────────────────────────────────────────
            var headerBar = new RECT { Left = 3, Top = 3, Right = w - 3, Bottom = HeaderH };
            FillRect(hdc, ref headerBar, _brushDivider);
            var titleRc = new RECT { Left = 10, Top = 6, Right = w - 30, Bottom = HeaderH };
            SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
            var prev = SelectObject(hdc, _fontTitle);
            DrawTextW(hdc, "Inventario", -1, ref titleRc,
                DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            // Pixel-beveled close button (X)
            var closeX = w - 24;
            var closeY = 6;
            NativeFrameImage.PixelBevel(hdc, closeX, closeY, 18, HeaderH - 10,
                _brushOutline, _brushBevHi, _brushBevLo, _brushTag);
            SetTextColor(hdc, Bgr(1f, 1f, 1f));
            SelectObject(hdc, _fontTag);
            var xRc = new RECT { Left = closeX, Top = closeY, Right = closeX + 18, Bottom = closeY + HeaderH - 10 };
            DrawTextW(hdc, "X", -1, ref xRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            SelectObject(hdc, prev);

            // ── Character summary card (dragon emblem + name/class/level) ─
            int sumTop = HeaderH;
            var sumBar = new RECT { Left = 3, Top = sumTop, Right = w - 3, Bottom = sumTop + SummaryH };
            FillRect(hdc, ref sumBar, _brushPanel);
            // Dragon emblem on the left — square, framed with its own tiny pixel bevel.
            int emblemSize = SummaryH - 6;
            int emblemX = 8;
            int emblemY = sumTop + 3;
            NativeFrameImage.PixelBevel(hdc, emblemX, emblemY, emblemSize, emblemSize,
                _brushOutline, _brushBevHi, _brushBevLo, _brushVoid);
            var dragon = NativeFrameImage.Get(DragonRes);
            if (dragon.Ready)
                dragon.BlitAspect(hdc, emblemX + 4, emblemY + 4, emblemSize - 8, emblemSize - 8);

            DrawSummaryText(hdc, emblemX + emblemSize + 8, sumTop + 4,
                            w - (emblemX + emblemSize + 12), SummaryH - 8);

            // ── Compact stats strip (2 rows × 3 cols) ─────────────────────
            int statTop = sumTop + SummaryH;
            DrawStatsStrip(hdc, 6, statTop, w - 12, StatsH);

            // Divider between the summary strip and the body.
            var div = new RECT { Left = 3, Top = statTop + StatsH - 1, Right = w - 3, Bottom = statTop + StatsH };
            FillRect(hdc, ref div, _brushDivider);

            // ── Body: paper-doll (left) + bag icon grid (right) ──
            int bodyTop = BodyTop;
            int bodyBot = BodyBottom;
            BuildDoll();
            BuildBag();

            // Pane frames.
            NativeFrameImage.PixelBevel(hdc, 4, bodyTop, LeftPaneW - 6, bodyBot - bodyTop,
                _brushOutline, _brushBevHi, _brushBevLo, _brushVoid);
            NativeFrameImage.PixelBevel(hdc, RightPaneX, bodyTop, RightPaneW, bodyBot - bodyTop,
                _brushOutline, _brushBevHi, _brushBevLo, _brushVoid);
            DrawPaneHeader(hdc, 8, LeftPaneW - 8, bodyTop, "Equipamento");
            DrawPaneHeader(hdc, RightPaneX + 4, w - 8, bodyTop, "Sacola");

            // Hero in the centre of the doll (live current frame, aspect-fit) — as large
            // as the space between the slot columns allows.
            int heroL = 10 + DollSlot + 4;
            int heroR = (LeftPaneW - 8 - DollSlot) - 4;
            int heroTop = bodyTop + SectionHeaderH + 6;
            int heroBot = bodyBot - 8;
            if (heroR - heroL > 20)
            {
                var hero = GetHeroImage();
                if (hero != null && hero.Ready)
                    hero.BlitAspect(hdc, heroL, heroTop, heroR - heroL, heroBot - heroTop);
            }

            // Equipment slots around the hero.
            for (int i = 0; i < _dollSlots.Count; i++)
            {
                var s = _dollSlots[i];
                var row = _equipRows[s.equipIndex];
                NativeFrameImage.PixelBevel(hdc, s.x, s.y, s.w, s.h, _brushOutline, _brushBevLo, _brushBevLo, _brushSlot);
                if (row.has && !string.IsNullOrEmpty(row.iconPath))
                {
                    var img = IconLibrary.Gdi(row.iconPath);
                    if (img != null && img.Ready) img.BlitAspect(hdc, s.x + 4, s.y + 4, s.w - 8, s.h - 8);
                    DrawQualityBorder(hdc, s.x, s.y, s.w, s.h, row.rarity);
                }
                else
                {
                    // Empty slot marker = its short name.
                    SelectObject(hdc, _fontTag);
                    SetTextColor(hdc, Bgr(0.45f, 0.45f, 0.50f));
                    var rc = new RECT { Left = s.x, Top = s.y, Right = s.x + s.w, Bottom = s.y + s.h };
                    DrawTextW(hdc, SlotShort(row.slotType), -1, ref rc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                }
                // Slot name under the cell.
                SelectObject(hdc, _fontHint);
                SetTextColor(hdc, Bgr(0.70f, 0.62f, 0.42f));
                var lblRc = new RECT { Left = s.x - 6, Top = s.y + s.h, Right = s.x + s.w + 6, Bottom = s.y + s.h + 12 };
                DrawTextW(hdc, row.label, -1, ref lblRc, DT_CENTER | DT_TOP | DT_SINGLELINE | DT_NOPREFIX);
            }

            // ── Bag icon grid (clipped to the pane viewport) ──
            IntersectClipRect(hdc, RightPaneX + 2, GridTop, w - 4, GridBot);
            for (int i = 0; i < _bagCells.Count; i++)
            {
                var c = _bagCells[i];
                if (c.y + c.h <= GridTop || c.y >= GridBot) continue;
                var row = _bagRows[c.bagIndex];
                NativeFrameImage.PixelBevel(hdc, c.x, c.y, c.w, c.h, _brushOutline, _brushBevLo, _brushBevLo, _brushSlot);
                if (!string.IsNullOrEmpty(row.iconPath))
                {
                    var img = IconLibrary.Gdi(row.iconPath);
                    if (img != null && img.Ready) img.BlitAspect(hdc, c.x + 3, c.y + 3, c.w - 6, c.h - 6);
                }
                else
                {
                    SelectObject(hdc, _fontTag);
                    SetTextColor(hdc, row.consumable ? Bgr(0.75f, 1f, 0.75f) : BgrOf(ItemData.QualityColor(row.rarity)));
                    var rc = new RECT { Left = c.x, Top = c.y, Right = c.x + c.w, Bottom = c.y + c.h };
                    DrawTextW(hdc, row.itemName, -1, ref rc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
                }
                DrawQualityBorder(hdc, c.x, c.y, c.w, c.h, row.consumable ? ItemRarity.Common : row.rarity);
                if (row.quantity > 1)
                {
                    SelectObject(hdc, _fontHint);
                    SetTextColor(hdc, Bgr(1f, 1f, 1f));
                    var qRc = new RECT { Left = c.x, Top = c.y + c.h - 13, Right = c.x + c.w - 3, Bottom = c.y + c.h - 1 };
                    DrawTextW(hdc, "x" + row.quantity, -1, ref qRc, DT_RIGHT | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);
                }
            }
            SelectClipRgn(hdc, IntPtr.Zero);

            // ── Footer hint ───────────────────────────────────────────────
            SelectObject(hdc, _fontHint);
            SetTextColor(hdc, Bgr(0.62f, 0.62f, 0.68f));
            var hintRc = new RECT { Left = 8, Top = h - FooterH, Right = w - 8, Bottom = h - 4 };
            DrawTextW(hdc, "Clique na sacola p/ equipar · clique no slot p/ retirar · roda rola · ESC fecha", -1, ref hintRc,
                DT_CENTER | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);
            SelectObject(hdc, prev);
        }

        // Compact identity line to the right of the dragon emblem: name, class, level.
        static void DrawSummaryText(IntPtr hdc, int x, int y, int w, int h)
        {
            var player = PlayerManager.Instance != null ? PlayerManager.Instance.Data : null;
            SelectObject(hdc, _fontSummary);
            if (player == null)
            {
                SetTextColor(hdc, Bgr(0.70f, 0.70f, 0.70f));
                var rc = new RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
                DrawTextW(hdc, "Sem dados do personagem.", -1, ref rc,
                    DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                return;
            }

            // Three tight rows of 14 px each — fits inside the 44 px summary strip
            // without spilling into the stats cells below.
            SetTextColor(hdc, Bgr(1f, 0.90f, 0.55f));
            var nameRc = new RECT { Left = x, Top = y, Right = x + w, Bottom = y + 14 };
            DrawTextW(hdc, Safe(player.playerName), -1, ref nameRc,
                DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            SetTextColor(hdc, Bgr(0.86f, 0.86f, 0.92f));
            var classRc = new RECT { Left = x, Top = y + 14, Right = x + w, Bottom = y + 28 };
            DrawTextW(hdc, $"{player.classType} / {player.subclassType}", -1, ref classRc,
                DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            var xpNext = Mathf.Max(1L, player.expToNextLevel);
            var xpPct  = Mathf.Clamp01((float)player.currentExp / xpNext) * 100f;
            SetTextColor(hdc, Bgr(0.75f, 0.80f, 0.90f));
            var lvlRc = new RECT { Left = x, Top = y + 28, Right = x + w, Bottom = y + 42 };
            DrawTextW(hdc, $"Nivel {player.level}   XP {xpPct:0.0}%", -1, ref lvlRc,
                DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
        }

        // Six compact stat cells arranged in two rows of three — HP, Dano, Defesa /
        // Velocidade, Regen, Ouro. Each cell has a tiny pixel bevel so they read as
        // enameled tokens on the character card.
        static void DrawStatsStrip(IntPtr hdc, int x, int y, int w, int h)
        {
            var player = PlayerManager.Instance != null ? PlayerManager.Instance.Data : null;
            if (player == null)
            {
                var rc = new RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
                FillRect(hdc, ref rc, _brushPanel);
                return;
            }

            var shownMaxHp = Mathf.Max(1, Mathf.Max(player.maxHp, player.hp));
            var shownHp    = Mathf.Clamp(player.hp, 0, shownMaxHp);
            string[] labels = { "Vida", "Dano", "Defesa", "Vel", "Regen", "Ouro" };
            string[] values =
            {
                $"{shownHp:N0}/{shownMaxHp:N0}",
                $"{player.attack:N0}",
                $"{player.defense:N0}",
                $"{player.speed:0.0}",
                $"{player.healPower:0.0}",
                $"{player.gold:N0}",
            };

            int cellW = (w - 4) / 3;
            int cellH = (h - 4) / 2;
            SelectObject(hdc, _fontRow);
            for (int i = 0; i < 6; i++)
            {
                int col = i % 3;
                int row = i / 3;
                int cx  = x + col * cellW + 2;
                int cy  = y + row * cellH + 2;
                int cw  = cellW - 4;
                int ch  = cellH - 4;
                NativeFrameImage.PixelBevel(hdc, cx, cy, cw, ch,
                    _brushOutline, _brushBevHi, _brushBevLo, _brushPanel);
                // Single line per cell (the cell is only ~18 px tall — stacking label
                // over value made them overlap): label left, value right, both vcentered.
                int split = cx + Mathf.RoundToInt(cw * 0.42f);
                var lblRc = new RECT { Left = cx + 5, Top = cy, Right = split, Bottom = cy + ch };
                SetTextColor(hdc, Bgr(0.78f, 0.66f, 0.34f));
                DrawTextW(hdc, labels[i], -1, ref lblRc,
                    DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                var valRc = new RECT { Left = split - 2, Top = cy, Right = cx + cw - 5, Bottom = cy + ch };
                SetTextColor(hdc, Bgr(0.98f, 0.96f, 0.88f));
                DrawTextW(hdc, values[i], -1, ref valRc,
                    DT_RIGHT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            }
        }

        static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        internal static void Pump()
        {
            if (_hwnd == IntPtr.Zero) return;
            if (Time.unscaledTime >= _nextRefreshAt)
            {
                InvalidateRect(_hwnd, IntPtr.Zero, false);
                _nextRefreshAt = Time.unscaledTime + 0.25f;
            }
            while (PeekMessageW(out var msg, _hwnd, 0, 0, PM_REMOVE) != 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
        }

        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASSEX
        {
            public uint cbSize; public uint style;
            public IntPtr lpfnWndProc; public int cbClsExtra; public int cbWndExtra;
            public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int pt_x; public int pt_y; }
        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct PAINTSTRUCT { public IntPtr hdc; public bool fErase; public RECT rcPaint; public bool fRestore; public bool fIncUpdate; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct LOGFONT
        {
            public int lfHeight; public int lfWidth; public int lfEscapement; public int lfOrientation;
            public int lfWeight; public byte lfItalic; public byte lfUnderline; public byte lfStrikeOut;
            public byte lfCharSet; public byte lfOutPrecision; public byte lfClipPrecision; public byte lfQuality;
            public byte lfPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
        }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const int WM_PAINT = 0x000F;
        const int WM_KEYDOWN = 0x0100;
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_MOUSEWHEEL = 0x020A;
        const int WM_DESTROY = 0x0002;
        const int WM_CLOSE = 0x0010;
        const int WM_ERASEBKGND = 0x0014;
        const int WS_POPUP = unchecked((int)0x80000000);
        const int WS_VISIBLE = 0x10000000;
        const int WS_EX_TOPMOST = 0x00000008;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int SW_SHOW = 5;
        const uint PM_REMOVE = 0x0001;
        const int TRANSPARENT = 1;
        const int VK_ESCAPE = 0x1B;
        const uint DT_TOP = 0x00000000;
        const uint DT_LEFT = 0x00000000;
        const uint DT_CENTER = 0x00000001;
        const uint DT_RIGHT = 0x00000002;
        const uint DT_VCENTER = 0x00000004;
        const uint DT_BOTTOM = 0x00000008;
        const uint DT_SINGLELINE = 0x00000020;
        const uint DT_NOPREFIX = 0x00000800;
        const uint DT_END_ELLIPSIS = 0x00008000;
        const uint CS_OWNDC = 0x0020;
        const int IDC_ARROW = 32512;
        const byte DEFAULT_CHARSET = 1;
        const byte CLEARTYPE_QUALITY = 5;
        const int FW_NORMAL = 400;
        const int FW_BOLD = 700;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")] static extern ushort RegisterClassExW(ref WNDCLASSEX lpwcx);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
        static extern IntPtr CreateWindowExW(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
            int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr ins, int x, int y, int w, int hgt, uint flags);
        [DllImport("user32.dll", EntryPoint = "DefWindowProcW")] static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", EntryPoint = "PeekMessageW")] static extern int PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] static extern int TranslateMessage(ref MSG lpMsg);
        [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] static extern IntPtr DispatchMessageW(ref MSG lpMsg);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
        [DllImport("user32.dll")] static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);
        [DllImport("user32.dll")] static extern int FrameRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DrawTextW")] static extern int DrawTextW(IntPtr hdc, string s, int n, ref RECT rc, uint fmt);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll", EntryPoint = "LoadCursorW")] static extern IntPtr LoadCursorW(IntPtr hInst, IntPtr name);

        [DllImport("gdi32.dll")] static extern IntPtr CreateSolidBrush(uint color);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern int SetTextColor(IntPtr hdc, uint color);
        [DllImport("gdi32.dll")] static extern int SetBkMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateFontIndirectW")] static extern IntPtr CreateFontIndirectW(ref LOGFONT lf);
        [DllImport("gdi32.dll")] static extern int IntersectClipRect(IntPtr hdc, int l, int t, int r, int b);
        [DllImport("gdi32.dll")] static extern int SelectClipRgn(IntPtr hdc, IntPtr hrgn);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
        static extern IntPtr GetModuleHandleW(string name);
    }

    class InventoryPopupPump : MonoBehaviour
    {
        static InventoryPopupPump _instance;
        internal static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("InventoryPopupPump");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<InventoryPopupPump>();
        }

        void Update()
        {
            InventoryPopupWindow.Pump();
            if (InventoryPopupWindow.IsOpen && Input.GetKeyDown(KeyCode.Escape))
                InventoryPopupWindow.Hide();
        }
    }
}

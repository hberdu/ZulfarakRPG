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
        const int DollSlot  = 34;              // equipment slot cell (paper-doll) — smaller icons
        const int BagCell   = 38;              // bag icon cell — smaller icons
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
            public int upgradeLevel;
        }

        static readonly List<EquipRow> _equipRows = new List<EquipRow>(6);
        static readonly List<BagRow> _bagRows = new List<BagRow>(64);
        static int _bagScrollY;
        static float _nextRefreshAt;

        // Sort mode for the bag: false = by id (stable), true = by quality (best first).
        static bool _sortByQuality;

        // Hover state for the item tooltip. Kind: 0 = none, 1 = bag cell, 2 = doll slot.
        static int _hoverKind;
        static int _hoverIndex = -1;
        static int _hoverX, _hoverY;
        static bool _mouseTracking;

        // ── Slot-picker modal ────────────────────────────────────────────────
        // Clicking an equipment slot opens a modal listing every bag item that fits that slot
        // (plus "Retirar" when the slot is filled); clicking an entry equips it into the slot.
        struct PickerEntry { public bool unequip; public string itemId, itemName; public ItemRarity rarity; public string iconPath; }
        static bool _pickerOpen;
        static ItemType _pickerSlot;
        static string _pickerLabel = "";
        static readonly List<PickerEntry> _pickerEntries = new List<PickerEntry>();
        static int _pickerScroll;
        static int _pickerHover = -1;

        public static bool IsOpen => _hwnd != IntPtr.Zero;

        public static void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        public static void Show()
        {
            TopPopups.CloseAllExcept(TopPopups.Kind.Inventory);
            _hoverKind = 0; _hoverIndex = -1; _mouseTracking = false;
            // Items are earned from server-defined monster drops now — nothing is seeded here. Make
            // sure the server item catalog is loaded so drops render + equip (bootstrap may miss it).
            Inventory.Instance?.EnsureCatalog();
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
                    iconPath = data != null ? data.iconPath : null,
                    upgradeLevel = it.upgradeLevel
                });
            }

            // Sort by quality (best first) when the "Qualid." toggle is on; otherwise a
            // stable alphabetical order by id keeps cells from jumping around.
            if (_sortByQuality)
                _bagRows.Sort((a, b) =>
                {
                    int c = QIndex(b.rarity).CompareTo(QIndex(a.rarity));   // Lendario → Comum
                    return c != 0 ? c : string.Compare(a.itemName, b.itemName, StringComparison.Ordinal);
                });
            else
                _bagRows.Sort((a, b) => string.Compare(a.itemId, b.itemId, StringComparison.Ordinal));

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
            // Dark-gray trim (was tarnished gold) — outline stays pitch black.
            if (_brushBorder  == IntPtr.Zero) _brushBorder  = CreateSolidBrush(Bgr(0.30f, 0.30f, 0.34f));
            if (_brushOutline == IntPtr.Zero) _brushOutline = CreateSolidBrush(Bgr(0.00f, 0.00f, 0.00f));
            if (_brushBevHi   == IntPtr.Zero) _brushBevHi   = CreateSolidBrush(Bgr(0.42f, 0.42f, 0.46f));
            if (_brushBevLo   == IntPtr.Zero) _brushBevLo   = CreateSolidBrush(Bgr(0.15f, 0.15f, 0.17f));
            if (_brushRuby    == IntPtr.Zero) _brushRuby    = CreateSolidBrush(Bgr(0.85f, 0.15f, 0.15f));
            if (_brushDivider == IntPtr.Zero) _brushDivider = CreateSolidBrush(Bgr(0.16f, 0.16f, 0.18f));
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
            SelectObject(hdc, _fontSection);
            if (Rpg)
            {
                // Dark ink directly on the parchment (no divider bar).
                SetTextColor(hdc, RpgUiNative.InkDark);
                var rcp = new RECT { Left = left + 2, Top = bodyTop + 1, Right = right, Bottom = bodyTop + SectionHeaderH };
                DrawTextW(hdc, text, -1, ref rcp, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                return;
            }
            var bar = new RECT { Left = left, Top = bodyTop + 3, Right = right, Bottom = bodyTop + SectionHeaderH };
            FillRect(hdc, ref bar, _brushDivider);
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

        // Yellow "+N" in the icon's top-right corner = times the item was enhanced at the forge.
        static void DrawUpgradeBadge(IntPtr hdc, int x, int y, int cellW, int lvl)
        {
            if (lvl <= 0) return;
            SelectObject(hdc, _fontTag);
            string t = "+" + lvl;
            var sh = new RECT { Left = x + 2, Top = y + 2, Right = x + cellW - 1, Bottom = y + 15 };
            SetTextColor(hdc, Bgr(0f, 0f, 0f));
            DrawTextW(hdc, t, -1, ref sh, DT_RIGHT | DT_TOP | DT_SINGLELINE | DT_NOPREFIX);
            var rc = new RECT { Left = x + 1, Top = y + 1, Right = x + cellW - 2, Bottom = y + 14 };
            SetTextColor(hdc, Bgr(1f, 0.90f, 0.20f));
            DrawTextW(hdc, t, -1, ref rc, DT_RIGHT | DT_TOP | DT_SINGLELINE | DT_NOPREFIX);
        }

        static string SlotShort(ItemType t) => t switch
        {
            ItemType.Helmet => "Elmo", ItemType.Chest  => "Peito", ItemType.Gloves => "Maos",
            ItemType.Boots  => "Pes",  ItemType.Weapon => "Arma",  ItemType.Cape   => "Capa",
            _ => ""
        };

        // "Qualid." sort toggle, tucked into the right end of the Sacola pane header.
        static void SortButtonRect(out int x, out int y, out int w, out int h)
        {
            w = 54;
            h = SectionHeaderH - 6;
            x = PopupWidth - 8 - w;
            y = BodyTop + 4;
        }

        static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_PAINT:
                    BeginPaint(hWnd, out var ps);
                    // Double-buffer: compose the whole frame on an off-screen DC and blit it
                    // in one shot. The periodic refresh (live hero + hover tooltip) otherwise
                    // repaints straight to the window DC, which the eye catches as a flicker.
                    {
                        int bw = PopupWidth, bh = PopupHeight;
                        IntPtr mem = CreateCompatibleDC(ps.hdc);
                        IntPtr bmp = CreateCompatibleBitmap(ps.hdc, bw, bh);
                        if (mem != IntPtr.Zero && bmp != IntPtr.Zero)
                        {
                            IntPtr oldBmp = SelectObject(mem, bmp);
                            Paint(mem);
                            BitBlt(ps.hdc, 0, 0, bw, bh, mem, 0, 0, SRCCOPY);
                            SelectObject(mem, oldBmp);
                        }
                        else
                        {
                            Paint(ps.hdc);   // fallback if the buffer couldn't be created
                        }
                        if (bmp != IntPtr.Zero) DeleteObject(bmp);
                        if (mem != IntPtr.Zero) DeleteDC(mem);
                    }
                    EndPaint(hWnd, ref ps);
                    return IntPtr.Zero;
                case WM_ERASEBKGND:
                    return new IntPtr(1);
                case WM_KEYDOWN:
                    if (wParam.ToInt32() == VK_ESCAPE)
                    {
                        if (_pickerOpen) ClosePicker(hWnd);
                        else Hide();
                    }
                    return IntPtr.Zero;
                case WM_MOUSEWHEEL:
                {
                    int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    if (_pickerOpen)
                    {
                        PickerLayout(out _, out _, out _, out _, out _, out int rowH2, out int visRows2, out _, out _);
                        int maxS = Mathf.Max(0, (_pickerEntries.Count - visRows2) * rowH2);
                        _pickerScroll = Mathf.Clamp(_pickerScroll - (delta / 120) * rowH2, 0, maxS);
                        InvalidateRect(hWnd, IntPtr.Zero, true);
                        return IntPtr.Zero;
                    }
                    RebuildRows();
                    _bagScrollY = Mathf.Clamp(_bagScrollY - (delta / 120) * (BagCell + BagGap), 0, MaxBagScroll);
                    InvalidateRect(hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }
                case WM_MOUSEMOVE:
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    _hoverX = mx; _hoverY = my;

                    // While the slot picker is open it owns hover highlighting.
                    if (_pickerOpen)
                    {
                        int ph2 = PickerRowAt(mx, my);
                        if (ph2 != _pickerHover) { _pickerHover = ph2; InvalidateRect(hWnd, IntPtr.Zero, false); }
                        return IntPtr.Zero;
                    }

                    // Ask Windows to post WM_MOUSELEAVE once the cursor exits, so the tooltip
                    // clears when the mouse drops back down to the game window.
                    if (!_mouseTracking)
                    {
                        var tme = new TRACKMOUSEEVENT
                        {
                            cbSize = (uint)Marshal.SizeOf(typeof(TRACKMOUSEEVENT)),
                            dwFlags = TME_LEAVE, hwndTrack = hWnd, dwHoverTime = 0
                        };
                        TrackMouseEvent(ref tme);
                        _mouseTracking = true;
                    }

                    int prevKind = _hoverKind, prevIndex = _hoverIndex;
                    _hoverKind = 0; _hoverIndex = -1;

                    // Prefer an equipped doll slot, then a visible bag cell.
                    foreach (var s in _dollSlots)
                    {
                        if (mx < s.x || mx > s.x + s.w || my < s.y || my > s.y + s.h) continue;
                        if (s.equipIndex >= 0 && s.equipIndex < _equipRows.Count && _equipRows[s.equipIndex].has)
                        { _hoverKind = 2; _hoverIndex = s.equipIndex; }
                        break;
                    }
                    if (_hoverKind == 0)
                        foreach (var c in _bagCells)
                        {
                            if (c.y + c.h <= GridTop || c.y >= GridBot) continue;
                            if (mx < c.x || mx > c.x + c.w || my < c.y || my > c.y + c.h) continue;
                            _hoverKind = 1; _hoverIndex = c.bagIndex; break;
                        }

                    if (_hoverKind != prevKind || _hoverIndex != prevIndex)
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    return IntPtr.Zero;
                }
                case WM_MOUSELEAVE:
                {
                    _mouseTracking = false;
                    if (_hoverKind != 0) { _hoverKind = 0; _hoverIndex = -1; InvalidateRect(hWnd, IntPtr.Zero, false); }
                    return IntPtr.Zero;
                }
                case WM_LBUTTONDOWN:
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    // The slot picker is modal — it eats the click while open.
                    if (_pickerOpen) { HandlePickerClick(hWnd, mx, my); return IntPtr.Zero; }
                    // Close box (top-right of header)?
                    if (my < HeaderH && mx >= PopupWidth - 26) { Hide(); return IntPtr.Zero; }

                    // Sort-by-quality toggle?
                    SortButtonRect(out int sbx, out int sby, out int sbw, out int sbh);
                    if (mx >= sbx && mx <= sbx + sbw && my >= sby && my <= sby + sbh)
                    {
                        _sortByQuality = !_sortByQuality;
                        RebuildRows();
                        InvalidateRect(hWnd, IntPtr.Zero, true);
                        return IntPtr.Zero;
                    }

                    RebuildRows();
                    BuildDoll();
                    BuildBag();

                    // Click a doll slot → open the modal picker of bag items that fit that slot.
                    foreach (var s in _dollSlots)
                    {
                        if (mx < s.x || mx > s.x + s.w || my < s.y || my > s.y + s.h) continue;
                        var row = _equipRows[s.equipIndex];
                        OpenPicker(row.slotType, row.label);
                        InvalidateRect(hWnd, IntPtr.Zero, true);
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

        // True when the RPG UI Pack atlas is available — switches the whole window from the
        // old dark pixel-bevel theme to the wood-frame + parchment RPG UI skin.
        static bool Rpg => RpgUiNative.Ready;

        static void Paint(IntPtr hdc)
        {
            RebuildRows();

            int w = PopupWidth;
            int h = PopupHeight;

            // Outer frame: RPG UI wood-trim board (falls back to the pixel bevel + studs).
            var full = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            FillRect(hdc, ref full, _brushPanel);
            if (Rpg)
            {
                RpgUiNative.DarkBoard(hdc, 0, 0, w, h);
            }
            else if (!NativeFrameImage.DrawWindowTheme(hdc, 0, 0, w, h))
            {
                NativeFrameImage.PixelBevel(hdc, 0, 0, w, h, _brushOutline, _brushBevHi, _brushBevLo, _brushPanel);
                NativeFrameImage.PixelCornerStuds(hdc, 0, 0, w, h, _brushRuby, inset: 5, size: 3);
            }

            SetBkMode(hdc, TRANSPARENT);

            // ── Header bar (wood ribbon title) ────────────────────────────
            var prev = SelectObject(hdc, _fontTitle);
            if (Rpg)
            {
                int rbW = Mathf.Min(w - 40, 210);
                RpgUiNative.WoodRibbon(hdc, 6, 3, rbW, HeaderH - 2);
                var t = new RECT { Left = 16, Top = 3, Right = rbW - 6, Bottom = HeaderH };
                SetTextColor(hdc, RpgUiNative.InkTitle);
                DrawTextW(hdc, "Inventario", -1, ref t, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            }
            else
            {
                var headerBar = new RECT { Left = 3, Top = 3, Right = w - 3, Bottom = HeaderH };
                FillRect(hdc, ref headerBar, _brushDivider);
                var titleRc = new RECT { Left = 10, Top = 6, Right = w - 30, Bottom = HeaderH };
                SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
                DrawTextW(hdc, "Inventario", -1, ref titleRc,
                    DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            }
            // Close button (X) — dark atlas button when skinned.
            var closeX = w - 24;
            var closeY = 6;
            if (Rpg) RpgUiNative.DarkButton(hdc, closeX, closeY, 18, HeaderH - 10);
            else NativeFrameImage.PixelBevel(hdc, closeX, closeY, 18, HeaderH - 10,
                     _brushOutline, _brushBevHi, _brushBevLo, _brushTag);
            SetTextColor(hdc, Bgr(1f, 1f, 1f));
            SelectObject(hdc, _fontTag);
            var xRc = new RECT { Left = closeX, Top = closeY, Right = closeX + 18, Bottom = closeY + HeaderH - 10 };
            DrawTextW(hdc, "X", -1, ref xRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            SelectObject(hdc, prev);

            // ── Character summary card (name/class/level) ─
            int sumTop = HeaderH;
            var sumBar = new RECT { Left = 3, Top = sumTop, Right = w - 3, Bottom = sumTop + SummaryH };
            FillRect(hdc, ref sumBar, _brushPanel);
            if (Rpg)
            {
                // No dragon emblem in the RPG UI skin — the identity line uses the full width.
                DrawSummaryText(hdc, 12, sumTop + 4, w - 24, SummaryH - 8);
            }
            else
            {
                int emblemSize = SummaryH - 6;
                int emblemX = 8;
                int emblemY = sumTop + 3;
                NativeFrameImage.PixelBevel(hdc, emblemX, emblemY, emblemSize, emblemSize,
                    _brushOutline, _brushBevHi, _brushBevLo, _brushVoid);
                // Themed emblem (treasure chest; old dragon art as fallback).
                var dragon = NativeFrameImage.Get("UI/Emblem_Inventory");
                if (!dragon.Ready) dragon = NativeFrameImage.Get(DragonRes);
                if (dragon.Ready)
                    dragon.BlitAspect(hdc, emblemX + 4, emblemY + 4, emblemSize - 8, emblemSize - 8);

                DrawSummaryText(hdc, emblemX + emblemSize + 8, sumTop + 4,
                                w - (emblemX + emblemSize + 12), SummaryH - 8);
            }

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

            // Pane frames: parchment panels (RPG UI) or the old dark bevels.
            if (Rpg)
            {
                RpgUiNative.Parchment(hdc, 4, bodyTop, LeftPaneW - 6, bodyBot - bodyTop);
                RpgUiNative.Parchment(hdc, RightPaneX, bodyTop, RightPaneW, bodyBot - bodyTop);
            }
            else
            {
                NativeFrameImage.PixelBevel(hdc, 4, bodyTop, LeftPaneW - 6, bodyBot - bodyTop,
                    _brushOutline, _brushBevHi, _brushBevLo, _brushVoid);
                NativeFrameImage.PixelBevel(hdc, RightPaneX, bodyTop, RightPaneW, bodyBot - bodyTop,
                    _brushOutline, _brushBevHi, _brushBevLo, _brushVoid);
            }
            DrawPaneHeader(hdc, 8, LeftPaneW - 8, bodyTop, "Equipamento");
            DrawPaneHeader(hdc, RightPaneX + 4, w - 8, bodyTop, "Sacola");

            // Sort-by-quality toggle at the right of the Sacola header.
            SortButtonRect(out int sbx, out int sby, out int sbw, out int sbh);
            if (Rpg) RpgUiNative.DarkButton(hdc, sbx, sby, sbw, sbh, _sortByQuality);
            else NativeFrameImage.PixelBevel(hdc, sbx, sby, sbw, sbh,
                     _brushOutline, _brushBevHi, _brushBevLo, _sortByQuality ? _brushTagUse : _brushTag);
            SelectObject(hdc, _fontTag);
            SetTextColor(hdc, _sortByQuality ? Bgr(1f, 0.96f, 0.62f) : Bgr(0.90f, 0.88f, 0.80f));
            var sbRc = new RECT { Left = sbx, Top = sby, Right = sbx + sbw, Bottom = sby + sbh };
            DrawTextW(hdc, "Qualid.", -1, ref sbRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

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
                    DrawUpgradeBadge(hdc, s.x, s.y, s.w, Inventory.Instance?.Equipment?.GetSlotLevel(row.slotType) ?? 0);
                }
                else
                {
                    // Empty slot marker = its short name.
                    SelectObject(hdc, _fontTag);
                    SetTextColor(hdc, Bgr(0.45f, 0.45f, 0.50f));
                    var rc = new RECT { Left = s.x, Top = s.y, Right = s.x + s.w, Bottom = s.y + s.h };
                    DrawTextW(hdc, SlotShort(row.slotType), -1, ref rc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                }
                // Slot name under the cell (dark ink on parchment when skinned).
                SelectObject(hdc, _fontHint);
                SetTextColor(hdc, Rpg ? RpgUiNative.InkMuted : Bgr(0.70f, 0.62f, 0.42f));
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
                DrawUpgradeBadge(hdc, c.x, c.y, c.w, row.upgradeLevel);
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
            SetTextColor(hdc, Rpg ? RpgUiNative.InkOnDark : Bgr(0.62f, 0.62f, 0.68f));
            var hintRc = new RECT { Left = 8, Top = h - FooterH, Right = w - 8, Bottom = h - 4 };
            DrawTextW(hdc, "Clique na sacola p/ equipar · clique no slot p/ trocar · roda rola · ESC fecha", -1, ref hintRc,
                DT_CENTER | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);
            SelectObject(hdc, prev);

            // ornate frame LAST so it rings the window over the content
            NativeFrameImage.DrawWindowFrame(hdc, 0, 0, w, h);

            // Hover tooltip / slot-picker modal drawn last so they float above everything.
            if (_pickerOpen) DrawPicker(hdc, w, h);
            else DrawTooltip(hdc, w, h);
        }

        // Resolves the ItemData for whatever the cursor is hovering (bag cell or equipped
        // doll slot), or null when nothing hoverable is under the cursor.
        static ItemData HoveredItem()
        {
            string id = null;
            if (_hoverKind == 1 && _hoverIndex >= 0 && _hoverIndex < _bagRows.Count)
                id = _bagRows[_hoverIndex].itemId;
            else if (_hoverKind == 2 && _hoverIndex >= 0 && _hoverIndex < _equipRows.Count && _equipRows[_hoverIndex].has)
                id = _equipRows[_hoverIndex].itemId;
            if (string.IsNullOrEmpty(id)) return null;
            return ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(id) : TestItems.Get(id);
        }

        // TaskbarHero-style floating card: item icon + name (quality-tinted) + type/quality
        // line, then one row per non-zero attribute (label left, value right).
        static void DrawTooltip(IntPtr hdc, int w, int h)
        {
            var item = HoveredItem();
            if (item == null) return;

            var lines = item.StatLines();
            // Required-level line: shown for items that gate on level (>1). Red when the player is
            // too low to equip, white once they meet it.
            bool showReq = item.requiredLevel > 1;
            int playerLevel = PlayerManager.Instance != null && PlayerManager.Instance.Data != null ? PlayerManager.Instance.Data.level : 1;
            const int pad = 8, iconBox = 40, headH = 46, lineH = 15;
            int tw = 182;
            int th = pad + headH + (lines.Count > 0 ? lines.Count * lineH + 6 : 0) + (showReq ? lineH + 4 : 0) + pad;
            int tx = Mathf.Clamp(_hoverX + 16, 4, w - tw - 4);
            int ty = Mathf.Clamp(_hoverY + 16, HeaderH, h - th - 4);

            if (Rpg) RpgUiNative.DarkBoard(hdc, tx, ty, tw, th);
            else NativeFrameImage.PixelBevel(hdc, tx, ty, tw, th, _brushOutline, _brushBevHi, _brushBevLo, _brushVoid);

            // Icon.
            int ix = tx + pad, iy = ty + pad;
            NativeFrameImage.PixelBevel(hdc, ix, iy, iconBox, iconBox, _brushOutline, _brushBevLo, _brushBevLo, _brushSlot);
            if (!string.IsNullOrEmpty(item.iconPath))
            {
                var img = IconLibrary.Gdi(item.iconPath);
                if (img != null && img.Ready) img.BlitAspect(hdc, ix + 3, iy + 3, iconBox - 6, iconBox - 6);
            }
            DrawQualityBorder(hdc, ix, iy, iconBox, iconBox, item.rarity);

            // Name (quality colour) + type · quality line, to the right of the icon.
            int tex = ix + iconBox + 8;
            SelectObject(hdc, _fontSection);
            SetTextColor(hdc, BgrOf(ItemData.QualityColor(item.rarity)));
            var nameRc = new RECT { Left = tex, Top = ty + pad, Right = tx + tw - pad, Bottom = ty + pad + 28 };
            DrawTextW(hdc, item.itemName, -1, ref nameRc, DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);

            SelectObject(hdc, _fontHint);
            SetTextColor(hdc, Bgr(0.72f, 0.70f, 0.60f));
            var typeRc = new RECT { Left = tex, Top = ty + pad + 28, Right = tx + tw - pad, Bottom = ty + pad + iconBox + 2 };
            DrawTextW(hdc, $"{TypeLabel(item.itemType)} · {ItemData.QualityLabel(item.rarity)}", -1, ref typeRc,
                DT_LEFT | DT_BOTTOM | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            if (lines.Count == 0 && !showReq) return;

            // Divider under the header, then the attribute rows.
            var div = new RECT { Left = tx + pad, Top = ty + pad + headH - 4, Right = tx + tw - pad, Bottom = ty + pad + headH - 3 };
            FillRect(hdc, ref div, _brushDivider);

            SelectObject(hdc, _fontSummary);
            int ry = ty + pad + headH + 2;
            for (int i = 0; i < lines.Count; i++)
            {
                var lblRc = new RECT { Left = tx + pad, Top = ry, Right = tx + tw - pad, Bottom = ry + lineH };
                SetTextColor(hdc, Bgr(0.80f, 0.78f, 0.66f));
                DrawTextW(hdc, lines[i].label, -1, ref lblRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                var valRc = new RECT { Left = tx + pad, Top = ry, Right = tx + tw - pad, Bottom = ry + lineH };
                SetTextColor(hdc, Bgr(0.55f, 1f, 0.62f));
                DrawTextW(hdc, lines[i].value, -1, ref valRc, DT_RIGHT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                ry += lineH;
            }

            // Required-level line — red when the player can't equip it yet, white when they can.
            if (showReq)
            {
                if (lines.Count > 0) ry += 2;
                SelectObject(hdc, _fontSummary);
                SetTextColor(hdc, playerLevel < item.requiredLevel ? Bgr(1f, 0.28f, 0.24f) : Bgr(1f, 1f, 1f));
                var reqRc = new RECT { Left = tx + pad, Top = ry, Right = tx + tw - pad, Bottom = ry + lineH };
                DrawTextW(hdc, $"Requer Nível {item.requiredLevel}", -1, ref reqRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            }
        }

        static string TypeLabel(ItemType t) => t switch
        {
            ItemType.Weapon => "Arma", ItemType.Helmet => "Capacete", ItemType.Chest => "Peito",
            ItemType.Gloves => "Maos", ItemType.Boots  => "Pes",      ItemType.Cape  => "Capa",
            ItemType.Consumable => "Consumivel", _ => "Item"
        };

        // ── Slot picker modal ─────────────────────────────────────────────────
        static void OpenPicker(ItemType slot, string label)
        {
            _pickerSlot = slot; _pickerLabel = label; _pickerScroll = 0; _pickerHover = -1;
            _pickerEntries.Clear();

            var inv = Inventory.Instance;
            var db  = ItemDatabase.Instance;
            if (inv != null)
            {
                // "Retirar" first when the slot is currently filled.
                if (!string.IsNullOrWhiteSpace(inv.Equipment?.GetSlot(slot)))
                    _pickerEntries.Add(new PickerEntry { unequip = true, itemName = "Retirar equipamento" });

                // Every bag item that fits this slot (equipping already pulls it out of the bag,
                // so the equipped item is never in this list).
                foreach (var it in inv.Items)
                {
                    var data = db != null ? db.Get(it.itemId) : null;
                    if (data == null || data.itemType != slot) continue;
                    _pickerEntries.Add(new PickerEntry
                    {
                        itemId = it.itemId,
                        itemName = !string.IsNullOrWhiteSpace(data.itemName) ? data.itemName : it.itemId,
                        rarity = data.rarity,
                        iconPath = data.iconPath
                    });
                }
                _pickerEntries.Sort((a, b) =>
                {
                    if (a.unequip != b.unequip) return a.unequip ? -1 : 1;   // Retirar stays on top
                    return QIndex(b.rarity).CompareTo(QIndex(a.rarity));      // best quality first
                });
            }
            _pickerOpen = true;
        }

        static void ClosePicker(IntPtr hWnd)
        {
            _pickerOpen = false;
            RebuildRows(); BuildDoll(); BuildBag();
            InvalidateRect(hWnd, IntPtr.Zero, true);
        }

        static void HandlePickerClick(IntPtr hWnd, int mx, int my)
        {
            PickerLayout(out int px, out int py, out int pw, out int ph, out _, out _, out _, out int titleH, out _);
            // Close X on the panel title, or a click outside the panel → dismiss.
            if ((my >= py && my < py + titleH && mx >= px + pw - 24) ||
                mx < px || mx > px + pw || my < py || my > py + ph)
            { ClosePicker(hWnd); return; }

            int idx = PickerRowAt(mx, my);
            if (idx < 0) return;
            var e = _pickerEntries[idx];
            if (e.unequip) Inventory.Instance?.Unequip(_pickerSlot);
            else           Inventory.Instance?.Equip(e.itemId);
            ClosePicker(hWnd);
        }

        // Panel + list geometry (shared by paint, hit-test and wheel). `visRows` = rows actually
        // shown (panel is sized to fit up to `cap`; extra items scroll).
        static void PickerLayout(out int px, out int py, out int pw, out int ph,
                                 out int listTop, out int rowH, out int visRows, out int titleH, out int footerH)
        {
            int w = PopupWidth, h = PopupHeight;
            pw = Mathf.Clamp(w * 74 / 100, 220, w - 20);
            titleH = 26; footerH = 16; rowH = 30;
            int avail = h - HeaderH - 30 - titleH - footerH;
            int cap   = Mathf.Max(1, avail / rowH);
            visRows   = Mathf.Clamp(_pickerEntries.Count, 1, cap);
            ph = titleH + visRows * rowH + footerH + 8;
            px = (w - pw) / 2;
            py = (h - ph) / 2 + 6;
            listTop = py + titleH + 2;
        }

        static int PickerRowAt(int mx, int my)
        {
            if (!_pickerOpen) return -1;
            PickerLayout(out int px, out int py, out int pw, out int ph, out int listTop, out int rowH, out int visRows, out _, out _);
            if (mx < px + 3 || mx > px + pw - 3) return -1;
            int listBot = listTop + visRows * rowH;
            if (my < listTop || my >= listBot) return -1;
            int idx = (my - listTop + _pickerScroll) / rowH;
            return (idx >= 0 && idx < _pickerEntries.Count) ? idx : -1;
        }

        static void DrawPicker(IntPtr hdc, int w, int h)
        {
            PickerLayout(out int px, out int py, out int pw, out int ph, out int listTop, out int rowH, out int visRows, out int titleH, out int footerH);

            // Drop shadow, then the bevelled panel.
            var shadow = new RECT { Left = px + 4, Top = py + 4, Right = px + pw + 4, Bottom = py + ph + 4 };
            FillRect(hdc, ref shadow, _brushOutline);
            NativeFrameImage.PixelBevel(hdc, px, py, pw, ph, _brushOutline, _brushBevHi, _brushBevLo, _brushPanel);
            SetBkMode(hdc, TRANSPARENT);

            // Title bar + close X.
            var titleBar = new RECT { Left = px + 3, Top = py + 3, Right = px + pw - 3, Bottom = py + titleH };
            FillRect(hdc, ref titleBar, _brushDivider);
            SelectObject(hdc, _fontSection);
            SetTextColor(hdc, Bgr(1f, 0.82f, 0.32f));
            var titleRc = new RECT { Left = px + 8, Top = py + 3, Right = px + pw - 24, Bottom = py + titleH };
            DrawTextW(hdc, "Trocar: " + _pickerLabel, -1, ref titleRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            var xRc = new RECT { Left = px + pw - 22, Top = py + 3, Right = px + pw - 4, Bottom = py + titleH };
            SetTextColor(hdc, Bgr(1f, 0.9f, 0.85f));
            DrawTextW(hdc, "X", -1, ref xRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

            if (_pickerEntries.Count == 0)
            {
                SelectObject(hdc, _fontRow);
                SetTextColor(hdc, Bgr(0.70f, 0.68f, 0.60f));
                var emptyRc = new RECT { Left = px + 10, Top = listTop + 6, Right = px + pw - 10, Bottom = listTop + rowH * 2 };
                DrawTextW(hdc, "Nenhum equipamento na sacola para este slot.", -1, ref emptyRc, DT_CENTER | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);
            }

            int listBot = listTop + visRows * rowH;
            IntersectClipRect(hdc, px + 3, listTop, px + pw - 3, listBot);
            for (int i = 0; i < _pickerEntries.Count; i++)
            {
                int rt = listTop + i * rowH - _pickerScroll;
                if (rt + rowH <= listTop || rt >= listBot) continue;
                var e = _pickerEntries[i];
                var rc = new RECT { Left = px + 4, Top = rt, Right = px + pw - 4, Bottom = rt + rowH - 2 };
                FillRect(hdc, ref rc, i == _pickerHover ? _brushTagUse : ((i & 1) == 0 ? _brushRowA : _brushRowB));

                if (e.unequip)
                {
                    SelectObject(hdc, _fontRow);
                    SetTextColor(hdc, Bgr(0.95f, 0.55f, 0.45f));
                    var trc = new RECT { Left = px + 14, Top = rt, Right = px + pw - 8, Bottom = rt + rowH - 2 };
                    DrawTextW(hdc, e.itemName, -1, ref trc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                    continue;
                }

                int iconBox = rowH - 8, ix = px + 8, iy = rt + 4;
                NativeFrameImage.PixelBevel(hdc, ix, iy, iconBox, iconBox, _brushOutline, _brushBevLo, _brushBevLo, _brushSlot);
                if (!string.IsNullOrEmpty(e.iconPath))
                {
                    var img = IconLibrary.Gdi(e.iconPath);
                    if (img != null && img.Ready) img.BlitAspect(hdc, ix + 2, iy + 2, iconBox - 4, iconBox - 4);
                }
                DrawQualityBorder(hdc, ix, iy, iconBox, iconBox, e.rarity);

                SelectObject(hdc, _fontRow);
                SetTextColor(hdc, BgrOf(ItemData.QualityColor(e.rarity)));
                var nrc = new RECT { Left = ix + iconBox + 8, Top = rt, Right = px + pw - 8, Bottom = rt + rowH - 2 };
                DrawTextW(hdc, e.itemName, -1, ref nrc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            }
            SelectClipRgn(hdc, IntPtr.Zero);

            SelectObject(hdc, _fontHint);
            SetTextColor(hdc, Bgr(0.66f, 0.64f, 0.56f));
            var frc = new RECT { Left = px + 8, Top = py + ph - footerH, Right = px + pw - 8, Bottom = py + ph - 3 };
            DrawTextW(hdc, "Clique p/ equipar · roda rola · ESC volta", -1, ref frc, DT_CENTER | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);
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
        struct TRACKMOUSEEVENT { public uint cbSize; public uint dwFlags; public IntPtr hwndTrack; public uint dwHoverTime; }
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
        const int WM_MOUSEMOVE = 0x0200;
        const int WM_MOUSELEAVE = 0x02A3;
        const int WM_MOUSEWHEEL = 0x020A;
        const int WM_DESTROY = 0x0002;
        const int WM_CLOSE = 0x0010;
        const int WM_ERASEBKGND = 0x0014;
        const uint TME_LEAVE = 0x00000002;
        const uint SRCCOPY = 0x00CC0020;
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
        const uint DT_WORDBREAK = 0x00000010;
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
        [DllImport("user32.dll")] static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

        [DllImport("gdi32.dll")] static extern IntPtr CreateSolidBrush(uint color);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
        [DllImport("gdi32.dll")] static extern bool   BitBlt(IntPtr hDest, int x, int y, int w, int h, IntPtr hSrc, int sx, int sy, uint rop);
        [DllImport("gdi32.dll")] static extern bool   DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool   DeleteObject(IntPtr h);
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

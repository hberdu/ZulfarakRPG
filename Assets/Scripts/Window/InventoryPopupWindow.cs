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
        public const int PopupWidth  = 400;
        public const int PopupHeight = 360;

        // Red-Eyes-Black-Dragon emblem (Resources/UI/DragonFrame); absent → plain dark card.
        const string DragonRes = "UI/DragonFrame";

        // Section layout (popup-local pixels, no outer margins — the pixel bevel IS the frame).
        const int HeaderH   = 28;
        const int SummaryH  = 52;              // dragon emblem + name/class/level line
        const int StatsH    = 48;              // 2 rows × 3 stats compact strip
        const int SectionHeaderH = 18;         // "Equipamento" / "Sacola" bar
        const int EquipRowH = 22;
        const int EquipRows = 8;
        const int BagRowH   = 24;
        const int FooterH   = 16;

        static int BodyTop     => HeaderH + SummaryH + StatsH;
        static int BodyBottom  => PopupHeight - FooterH;
        // Two vertical columns split near the middle (equipment | bag).
        const int LeftPaneW  = 196;
        const int RightPaneX = LeftPaneW + 2;
        static int RightPaneW  => PopupWidth - RightPaneX - 2;

        static int EquipTop     => BodyTop + SectionHeaderH;
        static int EquipBottom  => EquipTop + EquipRows * EquipRowH;
        static int BagListTop   => BodyTop + SectionHeaderH;
        static int BagListBot   => BodyBottom - 2;
        static int BagViewH     => BagListBot - BagListTop;
        static int MaxBagScroll => Mathf.Max(0, _bagRows.Count * BagRowH - BagViewH);

        struct EquipRow
        {
            public string label;
            public ItemType slotType;
            public string itemId;
        }

        struct BagRow
        {
            public string itemId;
            public string itemName;
            public int quantity;
            public bool consumable;
        }

        static readonly List<EquipRow> _equipRows = new List<EquipRow>(EquipRows);
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
            (int x, int y) = ComputePosition();
            SetWindowPos(_hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }

        static (int, int) ComputePosition()
        {
            int gx = OverlayWindow.WinX;
            int gy = OverlayWindow.WinY;
            int gw = OverlayWindow.Instance != null ? OverlayWindow.Instance.windowWidth : 400;
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

            AddEquipRow("Arma", ItemType.Weapon, eq.weaponId);
            AddEquipRow("Capacete", ItemType.Helmet, eq.helmetId);
            AddEquipRow("Peitoral", ItemType.Chest, eq.chestId);
            AddEquipRow("Calca", ItemType.Legs, eq.legsId);
            AddEquipRow("Botas", ItemType.Boots, eq.bootsId);
            AddEquipRow("Luvas", ItemType.Gloves, eq.glovesId);
            AddEquipRow("Anel", ItemType.Ring, eq.ringId);
            AddEquipRow("Amuleto", ItemType.Amulet, eq.amuletId);

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
                    consumable = data != null && data.itemType == ItemType.Consumable
                });
            }

            _bagScrollY = Mathf.Clamp(_bagScrollY, 0, MaxBagScroll);
        }

        static void AddEquipRow(string label, ItemType slotType, string itemId)
        {
            _equipRows.Add(new EquipRow
            {
                label = label,
                slotType = slotType,
                itemId = itemId
            });
        }

        static int EquipRowAtY(int y)
        {
            if (y < EquipTop || y >= EquipBottom) return -1;
            int idx = (y - EquipTop) / EquipRowH;
            return idx >= 0 && idx < _equipRows.Count ? idx : -1;
        }

        static int BagRowAtY(int y)
        {
            if (y < BagListTop || y >= BagListBot) return -1;
            int idx = (y - BagListTop + _bagScrollY) / BagRowH;
            return idx >= 0 && idx < _bagRows.Count ? idx : -1;
        }

        static IntPtr _hwnd = IntPtr.Zero;
        static WndProcDelegate _wndProcDelegate;
        static bool _classRegistered;
        static IntPtr _brushBorder, _brushVoid, _brushPanel;
        static IntPtr _brushOutline, _brushBevHi, _brushBevLo, _brushRuby, _brushDivider;
        static IntPtr _brushRowA, _brushRowB, _brushTag, _brushTagUse;
        static IntPtr _fontTitle, _fontRow, _fontHint, _fontTag, _fontSection, _fontSummary;
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
                    _bagScrollY = Mathf.Clamp(_bagScrollY - (delta / 120) * BagRowH, 0, MaxBagScroll);
                    InvalidateRect(hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }
                case WM_LBUTTONDOWN:
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    // Close box (top-right of header)?
                    if (my < HeaderH && mx >= PopupWidth - 26)
                    {
                        Hide();
                        return IntPtr.Zero;
                    }

                    RebuildRows();
                    if (mx < LeftPaneW)
                    {
                        int equipRow = EquipRowAtY(my);
                        if (equipRow >= 0)
                        {
                            var row = _equipRows[equipRow];
                            if (!string.IsNullOrWhiteSpace(row.itemId))
                            {
                                Inventory.Instance?.Unequip(row.slotType);
                                InvalidateRect(hWnd, IntPtr.Zero, true);
                            }
                            return IntPtr.Zero;
                        }
                    }
                    else
                    {
                        int bagRow = BagRowAtY(my);
                        if (bagRow >= 0)
                        {
                            var row = _bagRows[bagRow];
                            if (row.consumable)
                                Inventory.Instance?.UseConsumable(row.itemId);
                            else
                                Inventory.Instance?.Equip(row.itemId);
                            InvalidateRect(hWnd, IntPtr.Zero, true);
                            return IntPtr.Zero;
                        }
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

            // ── Body: equipment (left) + bag (right), split by a 2px divider ─
            int bodyTop = BodyTop;
            int bodyBot = BodyBottom;
            // Column divider
            var colDiv = new RECT { Left = LeftPaneW, Top = bodyTop, Right = LeftPaneW + 2, Bottom = bodyBot };
            FillRect(hdc, ref colDiv, _brushDivider);

            // Left column header + equipment slots
            var equipHdr = new RECT { Left = 6, Top = bodyTop + 2, Right = LeftPaneW - 4, Bottom = bodyTop + SectionHeaderH };
            FillRect(hdc, ref equipHdr, _brushDivider);
            SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
            SelectObject(hdc, _fontSection);
            var equipHdrRc = new RECT { Left = 10, Top = bodyTop, Right = LeftPaneW - 4, Bottom = bodyTop + SectionHeaderH };
            DrawTextW(hdc, "Equipamento", -1, ref equipHdrRc,
                DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

            SelectObject(hdc, _fontRow);
            for (int i = 0; i < _equipRows.Count; i++)
            {
                int top = EquipTop + i * EquipRowH;
                int bot = top + EquipRowH;
                FillRow(hdc, 4, top, LeftPaneW - 2, bot, i);

                var row = _equipRows[i];
                bool has = !string.IsNullOrWhiteSpace(row.itemId);
                string itemLabel = has ? row.itemId : "-";

                SetTextColor(hdc, has ? Bgr(0.96f, 0.94f, 0.86f) : Bgr(0.62f, 0.62f, 0.62f));
                var txtRc = new RECT { Left = 10, Top = top, Right = LeftPaneW - 62, Bottom = bot };
                DrawTextW(hdc, $"{row.label}: {itemLabel}", -1, ref txtRc,
                    DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

                if (has)
                {
                    // Pixel-beveled "Desequipar" tag
                    int tagX = LeftPaneW - 58;
                    int tagY = top + 3;
                    int tagW = 52;
                    int tagH = EquipRowH - 6;
                    NativeFrameImage.PixelBevel(hdc, tagX, tagY, tagW, tagH,
                        _brushOutline, _brushBevHi, _brushBevLo, _brushTag);
                    SelectObject(hdc, _fontTag);
                    SetTextColor(hdc, Bgr(1f, 0.86f, 0.42f));
                    var tagRc = new RECT { Left = tagX, Top = tagY, Right = tagX + tagW, Bottom = tagY + tagH };
                    DrawTextW(hdc, "Retirar", -1, ref tagRc,
                        DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                    SelectObject(hdc, _fontRow);
                }
            }

            // Right column header + bag list (scrollable)
            var bagHdr = new RECT { Left = RightPaneX + 4, Top = bodyTop + 2, Right = w - 6, Bottom = bodyTop + SectionHeaderH };
            FillRect(hdc, ref bagHdr, _brushDivider);
            SelectObject(hdc, _fontSection);
            SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
            var bagHdrRc = new RECT { Left = RightPaneX + 8, Top = bodyTop, Right = w - 6, Bottom = bodyTop + SectionHeaderH };
            DrawTextW(hdc, "Sacola", -1, ref bagHdrRc,
                DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

            SelectObject(hdc, _fontRow);
            for (int i = 0; i < _bagRows.Count; i++)
            {
                int rowTop = BagListTop + i * BagRowH - _bagScrollY;
                int rowBot = rowTop + BagRowH;
                if (rowBot <= BagListTop || rowTop >= BagListBot) continue;
                int drawTop = Mathf.Max(rowTop, BagListTop);
                int drawBot = Mathf.Min(rowBot, BagListBot);
                FillRow(hdc, RightPaneX + 2, drawTop, w - 4, drawBot, i);

                var row = _bagRows[i];
                SetTextColor(hdc, Bgr(0.96f, 0.94f, 0.86f));
                var txtRc = new RECT { Left = RightPaneX + 8, Top = rowTop, Right = w - 60, Bottom = rowBot };
                DrawTextW(hdc, $"{row.itemName} x{row.quantity}", -1, ref txtRc,
                    DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

                int tagX = w - 56;
                int tagY = rowTop + 3;
                int tagW = 48;
                int tagH = BagRowH - 6;
                NativeFrameImage.PixelBevel(hdc, tagX, tagY, tagW, tagH,
                    _brushOutline, _brushBevHi, _brushBevLo,
                    row.consumable ? _brushTagUse : _brushTag);
                SelectObject(hdc, _fontTag);
                SetTextColor(hdc, row.consumable ? Bgr(0.75f, 1f, 0.75f) : Bgr(1f, 0.86f, 0.42f));
                var tagRc = new RECT { Left = tagX, Top = tagY, Right = tagX + tagW, Bottom = tagY + tagH };
                DrawTextW(hdc, row.consumable ? "Usar" : "Equipar", -1, ref tagRc,
                    DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                SelectObject(hdc, _fontRow);
            }

            // ── Footer hint ───────────────────────────────────────────────
            SelectObject(hdc, _fontHint);
            SetTextColor(hdc, Bgr(0.62f, 0.62f, 0.68f));
            var hintRc = new RECT { Left = 8, Top = h - FooterH, Right = w - 8, Bottom = h - 4 };
            DrawTextW(hdc, "Clique nos itens · roda do mouse rola a sacola · ESC fecha", -1, ref hintRc,
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
            SelectObject(hdc, _fontSection);
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
                var lblRc = new RECT { Left = cx + 4, Top = cy + 1, Right = cx + cw - 4, Bottom = cy + ch / 2 + 1 };
                SetTextColor(hdc, Bgr(0.75f, 0.63f, 0.32f));
                DrawTextW(hdc, labels[i], -1, ref lblRc,
                    DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                var valRc = new RECT { Left = cx + 4, Top = cy + ch / 2, Right = cx + cw - 4, Bottom = cy + ch };
                SetTextColor(hdc, Bgr(0.96f, 0.94f, 0.86f));
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

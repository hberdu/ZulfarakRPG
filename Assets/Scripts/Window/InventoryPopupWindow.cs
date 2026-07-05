using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZulfarakRPG
{
    // Native Win32 inventory/equipment popup rendered outside the game window.
    // Actions (equip/unequip/use) call Inventory methods, which forward to server APIs.
    public static class InventoryPopupWindow
    {
        public const int PopupWidth = 760;
        public const int PopupHeight = 460;
        const int LeftPaneW = 520;
        const int RightPaneX = LeftPaneW + 6;

        const int HeaderH = 34;
        const int FooterH = 22;
        const int EquipRowH = 24;
        const int BagRowH = 28;
        const int EquipTop = HeaderH + 6;
        const int EquipRows = 8;
        const int EquipBottom = EquipTop + EquipRows * EquipRowH;
        const int BagTop = EquipBottom + 8;
        static int BagBottom => PopupHeight - FooterH - 8;
        static int BagViewH => BagBottom - BagTop;
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
            if (y < BagTop || y >= BagBottom) return -1;
            int idx = (y - BagTop + _bagScrollY) / BagRowH;
            return idx >= 0 && idx < _bagRows.Count ? idx : -1;
        }

        static IntPtr _hwnd = IntPtr.Zero;
        static WndProcDelegate _wndProcDelegate;
        static bool _classRegistered;
        static IntPtr _brushBg, _brushBorder, _brushHeader, _brushRow, _brushRowAlt, _brushTag, _brushClose;
        static IntPtr _fontTitle, _fontRow, _fontHint, _fontTag;
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
            if (_brushBg == IntPtr.Zero) _brushBg = CreateSolidBrush(Bgr(0.05f, 0.03f, 0.02f));
            if (_brushBorder == IntPtr.Zero) _brushBorder = CreateSolidBrush(Bgr(0.85f, 0.65f, 0.20f));
            if (_brushHeader == IntPtr.Zero) _brushHeader = CreateSolidBrush(Bgr(0.18f, 0.10f, 0.05f));
            if (_brushRow == IntPtr.Zero) _brushRow = CreateSolidBrush(Bgr(0.10f, 0.07f, 0.04f));
            if (_brushRowAlt == IntPtr.Zero) _brushRowAlt = CreateSolidBrush(Bgr(0.13f, 0.09f, 0.05f));
            if (_brushTag == IntPtr.Zero) _brushTag = CreateSolidBrush(Bgr(0.28f, 0.16f, 0.06f));
            if (_brushClose == IntPtr.Zero) _brushClose = CreateSolidBrush(Bgr(0.40f, 0.05f, 0.05f));
            if (_fontTitle == IntPtr.Zero) _fontTitle = MakeFont(18, FW_BOLD);
            if (_fontRow == IntPtr.Zero) _fontRow = MakeFont(13, FW_NORMAL);
            if (_fontHint == IntPtr.Zero) _fontHint = MakeFont(11, FW_NORMAL);
            if (_fontTag == IntPtr.Zero) _fontTag = MakeFont(12, FW_BOLD);
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
                    if (my < HeaderH && mx >= PopupWidth - 30)
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

        static void Paint(IntPtr hdc)
        {
            RebuildRows();

            int w = PopupWidth;
            int h = PopupHeight;
            var full = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            FillRect(hdc, ref full, _brushBg);
            FrameRect(hdc, ref full, _brushBorder);
            var inset = new RECT { Left = 1, Top = 1, Right = w - 1, Bottom = h - 1 };
            FrameRect(hdc, ref inset, _brushBorder);

            SetBkMode(hdc, TRANSPARENT);

            var header = new RECT { Left = 2, Top = 2, Right = w - 2, Bottom = HeaderH };
            FillRect(hdc, ref header, _brushHeader);
            var titleRc = new RECT { Left = 12, Top = 8, Right = w - 36, Bottom = HeaderH };
            SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
            var prev = SelectObject(hdc, _fontTitle);
            DrawTextW(hdc, "Inventario e Equipamentos", -1, ref titleRc, DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            var closeRc = new RECT { Left = w - 30, Top = 7, Right = w - 8, Bottom = HeaderH - 5 };
            FillRect(hdc, ref closeRc, _brushClose);
            SetTextColor(hdc, Bgr(1f, 1f, 1f));
            DrawTextW(hdc, "X", -1, ref closeRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            SelectObject(hdc, prev);

            SelectObject(hdc, _fontRow);
            for (int i = 0; i < _equipRows.Count; i++)
            {
                int top = EquipTop + i * EquipRowH;
                int bottom = top + EquipRowH;
                var rowRc = new RECT { Left = 2, Top = top, Right = LeftPaneW - 2, Bottom = bottom };
                FillRect(hdc, ref rowRc, (i & 1) == 0 ? _brushRow : _brushRowAlt);

                var row = _equipRows[i];
                string itemLabel = string.IsNullOrWhiteSpace(row.itemId) ? "-" : row.itemId;
                SetTextColor(hdc, Bgr(0.95f, 0.95f, 0.95f));
                var txtRc = new RECT { Left = 12, Top = top, Right = LeftPaneW - 110, Bottom = bottom };
                DrawTextW(hdc, $"{row.label}: {itemLabel}", -1, ref txtRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

                if (!string.IsNullOrWhiteSpace(row.itemId))
                {
                    var tagRc = new RECT { Left = LeftPaneW - 102, Top = top + 4, Right = LeftPaneW - 12, Bottom = bottom - 4 };
                    FillRect(hdc, ref tagRc, _brushTag);
                    SelectObject(hdc, _fontTag);
                    SetTextColor(hdc, Bgr(1f, 0.82f, 0.32f));
                    DrawTextW(hdc, "Desequipar", -1, ref tagRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                    SelectObject(hdc, _fontRow);
                }
            }

            for (int i = 0; i < _bagRows.Count; i++)
            {
                int rowTop = BagTop + i * BagRowH - _bagScrollY;
                int rowBottom = rowTop + BagRowH;
                if (rowBottom <= BagTop || rowTop >= BagBottom) continue;

                int drawTop = Mathf.Max(rowTop, BagTop);
                int drawBottom = Mathf.Min(rowBottom, BagBottom);
                var rowRc = new RECT { Left = 2, Top = drawTop, Right = LeftPaneW - 2, Bottom = drawBottom };
                FillRect(hdc, ref rowRc, (i & 1) == 0 ? _brushRow : _brushRowAlt);

                var row = _bagRows[i];
                SetTextColor(hdc, Bgr(0.95f, 0.95f, 0.95f));
                var txtRc = new RECT { Left = 12, Top = rowTop, Right = LeftPaneW - 110, Bottom = rowBottom };
                DrawTextW(hdc, $"{row.itemName} x{row.quantity}", -1, ref txtRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

                var tagRc = new RECT { Left = LeftPaneW - 102, Top = rowTop + 4, Right = LeftPaneW - 12, Bottom = rowBottom - 4 };
                FillRect(hdc, ref tagRc, _brushTag);
                SelectObject(hdc, _fontTag);
                SetTextColor(hdc, Bgr(1f, 0.82f, 0.32f));
                DrawTextW(hdc, row.consumable ? "Usar" : "Equipar", -1, ref tagRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                SelectObject(hdc, _fontRow);
            }

            DrawCharacterPanel(hdc, w, h);

            SelectObject(hdc, _fontHint);
            SetTextColor(hdc, Bgr(0.60f, 0.60f, 0.66f));
            var hintRc = new RECT { Left = 12, Top = h - FooterH, Right = LeftPaneW - 12, Bottom = h - 4 };
            DrawTextW(hdc, "Clique para equipar/desequipar/usar · roda do mouse rola · ESC fecha", -1, ref hintRc, DT_LEFT | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);
        }

        static void DrawCharacterPanel(IntPtr hdc, int w, int h)
        {
            var panel = new RECT
            {
                Left = RightPaneX,
                Top = HeaderH + 6,
                Right = w - 2,
                Bottom = h - 6
            };
            FillRect(hdc, ref panel, _brushRowAlt);
            FrameRect(hdc, ref panel, _brushBorder);

            var titleRc = new RECT { Left = RightPaneX + 10, Top = HeaderH + 12, Right = w - 12, Bottom = HeaderH + 36 };
            SelectObject(hdc, _fontTitle);
            SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
            DrawTextW(hdc, "Status do Personagem", -1, ref titleRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            var player = PlayerManager.Instance != null ? PlayerManager.Instance.Data : null;
            SelectObject(hdc, _fontRow);
            SetTextColor(hdc, Bgr(0.95f, 0.95f, 0.95f));

            if (player == null)
            {
                var emptyRc = new RECT { Left = RightPaneX + 10, Top = HeaderH + 48, Right = w - 12, Bottom = HeaderH + 78 };
                DrawTextW(hdc, "Sem dados do personagem.", -1, ref emptyRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                return;
            }

            var xpNext = Mathf.Max(1L, player.expToNextLevel);
            var xpPct = Mathf.Clamp01((float)player.currentExp / xpNext) * 100f;
            var shownMaxHp = Mathf.Max(1, Mathf.Max(player.maxHp, player.hp));
            var shownHp = Mathf.Clamp(player.hp, 0, shownMaxHp);

            var lines = new[]
            {
                $"Nome: {Safe(player.playerName)}",
                $"Classe: {player.classType} / {player.subclassType}",
                $"Nivel: {player.level}",
                $"XP atual: {player.currentExp:N0}",
                $"XP proximo nivel: {xpNext:N0} ({xpPct:0.0}%)",
                $"Vida: {shownHp:N0} / {shownMaxHp:N0}",
                $"Dano: {player.attack:N0}",
                $"Defesa: {player.defense:N0}",
                $"Velocidade: {player.speed:0.00}",
                $"Regeneracao/s: {player.healPower:0.00}",
                $"Ouro: {player.gold:N0}",
                $"Cidade: {Safe(player.currentCity)}",
                $"Guilda: {Safe(player.guildId)}"
            };

            int y = HeaderH + 44;
            for (int i = 0; i < lines.Length; i++)
            {
                var lineRc = new RECT { Left = RightPaneX + 10, Top = y, Right = w - 12, Bottom = y + 22 };
                DrawTextW(hdc, lines[i], -1, ref lineRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
                y += 24;
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

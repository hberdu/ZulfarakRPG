using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace ZulfarakRPG
{
    // Native Win32 popup that lists the player's Steam friends and lets them be
    // invited to the current lobby. Mirrors MenuPopupWindow / WorldMapPopup so it
    // floats as a separate top-level window ABOVE the game strip — exactly like the
    // Kael / Ferreiro dialog — instead of an in-world canvas. Only one top popup is
    // open at a time (see TopPopups).
    //
    // Painted with GDI: title bar, scrollable friend rows (status dot + name +
    // "Convidar" tag), footer hint. Mouse wheel scrolls; clicking a row invites that
    // friend; ESC or the close box dismisses it.
    public static class FriendsListPopup
    {
        // ── Public API ────────────────────────────────────────────────────
        public const int PopupWidth  = 380;
        public const int PopupHeight = 260;

        public static bool IsOpen => _hwnd != IntPtr.Zero;

        public static void Show()
        {
            // Replace any other top popup (NPC dialog / map) — only one at a time.
            TopPopups.CloseAllExcept(TopPopups.Kind.Invite);

            // Lobby must exist before invites can be sent — create it on demand.
            SteamLobbyManager.Instance?.EnsureLobby();
            RefreshFriends();

#if UNITY_EDITOR
            // Native window can't render reliably while the editor owns the Game view.
            var sb = new System.Text.StringBuilder("[FriendsListPopup] amigos:\n");
            foreach (var f in _friends) sb.AppendLine($"  {(f.online ? "●" : "○")} {f.name}");
            Debug.Log(sb.ToString());
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
            _scrollY = 0;
            (int x, int y) = ComputePosition();

            _hwnd = CreateWindowExW(
                WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
                ClassName,
                "Convidar amigo",
                WS_POPUP | WS_VISIBLE,
                x, y, PopupWidth, PopupHeight,
                IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);

            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SW_SHOW);
                SetForegroundWindow(_hwnd);
                FriendsListPump.Ensure();
            }
#endif
        }

        public static void Hide()
        {
#if UNITY_EDITOR
            // nothing persistent to hide in the editor path
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
            SetWindowPos(_hwnd, HWND_TOPMOST, x, y, 0, 0,
                SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }

        // Centered above the game strip — mirrors MenuPopupWindow's anchor.
        static (int, int) ComputePosition()
        {
            int gx = OverlayWindow.WinX;
            int gy = OverlayWindow.WinY;
            int gw = OverlayWindow.Instance != null ? OverlayWindow.Instance.windowWidth : 400;
            int x  = gx + (gw - PopupWidth) / 2;
            int y  = gy - PopupHeight;
            int sw = Screen.currentResolution.width;
            int sh = Screen.currentResolution.height;
            x = Mathf.Clamp(x, 0, Mathf.Max(0, sw - PopupWidth));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, sh - PopupHeight));
            return (x, y);
        }

        // ── Friend data ───────────────────────────────────────────────────
        struct FriendEntry { public string name; public ulong id; public bool online; public bool sent; public bool isCopy; }
        static readonly List<FriendEntry> _friends = new List<FriendEntry>();

        static void RefreshFriends()
        {
            _friends.Clear();
            // Always-available "outside Steam" option: copy a download link + lobby code.
            _friends.Add(new FriendEntry { name = "Convidar de fora da Steam (link + código)", isCopy = true, online = true });
#if STEAMWORKS_NET
            if (SteamIntegration.Instance == null || !SteamIntegration.Instance.IsInitialized)
            {
                _friends.Add(new FriendEntry { name = "Steam offline — não foi possível listar amigos.", id = 0, online = false });
                return;
            }
            int n = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            if (n <= 0)
            {
                _friends.Add(new FriendEntry { name = "Nenhum amigo encontrado.", id = 0, online = false });
                return;
            }
            for (int i = 0; i < n; i++)
            {
                var fid   = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                var name  = SteamFriends.GetFriendPersonaName(fid);
                var state = SteamFriends.GetFriendPersonaState(fid);
                bool online = state != EPersonaState.k_EPersonaStateOffline;
                _friends.Add(new FriendEntry { name = name, id = fid.m_SteamID, online = online });
            }
            _friends.Sort((a, b) =>
            {
                if (a.online != b.online) return a.online ? -1 : 1;
                return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });
#else
            _friends.Add(new FriendEntry { name = "Build sem Steamworks — lista indisponível.", id = 0, online = false });
#endif
        }

        static void InviteRow(int index)
        {
            if (index < 0 || index >= _friends.Count) return;
            var f = _friends[index];

            // "Outside Steam" row: copy the shareable download link + lobby code.
            if (f.isCopy)
            {
                ExternalInvite.CopyToClipboard();
                f.sent = true;
                _friends[index] = f;
                if (_hwnd != IntPtr.Zero) InvalidateRect(_hwnd, IntPtr.Zero, true);
                return;
            }

            if (f.id == 0) return;   // placeholder row
#if STEAMWORKS_NET
            var lobby = SteamLobbyManager.Instance;
            if (lobby == null) return;
            if (!lobby.InLobby) lobby.EnsureLobby();
            if (lobby.LobbyIdString != null && ulong.TryParse(lobby.LobbyIdString, out var raw))
            {
                SteamMatchmaking.InviteUserToLobby(new CSteamID(raw), new CSteamID(f.id));
                f.sent = true;
                _friends[index] = f;
                if (_hwnd != IntPtr.Zero) InvalidateRect(_hwnd, IntPtr.Zero, true);
            }
#endif
        }

        // ── Layout constants (popup-local pixels) ─────────────────────────
        const int HeaderH = 34;
        const int FooterH = 22;
        const int RowH    = 30;
        static int ListTop    => HeaderH + 2;
        static int ListBottom => PopupHeight - FooterH - 2;
        static int ListViewH  => ListBottom - ListTop;
        static int ContentH   => _friends.Count * RowH;
        static int MaxScroll  => Mathf.Max(0, ContentH - ListViewH);

        static int _scrollY;

        // Maps a click Y (popup-local) to a friend row index, or -1.
        static int RowAtY(int y)
        {
            if (y < ListTop || y >= ListBottom) return -1;
            int idx = (y - ListTop + _scrollY) / RowH;
            return (idx >= 0 && idx < _friends.Count) ? idx : -1;
        }

        // ── Internal state ────────────────────────────────────────────────
        static IntPtr _hwnd = IntPtr.Zero;
        static WndProcDelegate _wndProcDelegate;
        static bool   _classRegistered;
        static IntPtr _brushBg, _brushBorder, _brushHeader, _brushRow, _brushRowAlt;
        static IntPtr _brushOnline, _brushOffline, _brushInvite, _brushSent, _brushClose;
        static IntPtr _fontTitle, _fontRow, _fontHint, _fontTag;
        const string ClassName = "ZulfarakFriendsPopup";

        static void EnsureClassRegistered()
        {
            if (_classRegistered) return;
            _wndProcDelegate = WndProc;
            var wc = new WNDCLASSEX
            {
                cbSize        = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style         = CS_OWNDC,
                lpfnWndProc   = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance     = GetModuleHandleW(null),
                hCursor       = LoadCursorW(IntPtr.Zero, (IntPtr)IDC_ARROW),
                hbrBackground = IntPtr.Zero,
                lpszClassName = ClassName,
            };
            RegisterClassExW(ref wc);
            _classRegistered = true;
        }

        static void EnsureGdiObjects()
        {
            if (_brushBg      == IntPtr.Zero) _brushBg      = CreateSolidBrush(Bgr(0.05f, 0.03f, 0.02f));
            if (_brushBorder  == IntPtr.Zero) _brushBorder  = CreateSolidBrush(Bgr(0.85f, 0.65f, 0.20f));
            if (_brushHeader  == IntPtr.Zero) _brushHeader  = CreateSolidBrush(Bgr(0.18f, 0.10f, 0.05f));
            if (_brushRow     == IntPtr.Zero) _brushRow     = CreateSolidBrush(Bgr(0.10f, 0.07f, 0.04f));
            if (_brushRowAlt  == IntPtr.Zero) _brushRowAlt  = CreateSolidBrush(Bgr(0.13f, 0.09f, 0.05f));
            if (_brushOnline  == IntPtr.Zero) _brushOnline  = CreateSolidBrush(Bgr(0.30f, 0.85f, 0.30f));
            if (_brushOffline == IntPtr.Zero) _brushOffline = CreateSolidBrush(Bgr(0.45f, 0.45f, 0.45f));
            if (_brushInvite  == IntPtr.Zero) _brushInvite  = CreateSolidBrush(Bgr(0.28f, 0.16f, 0.06f));
            if (_brushSent    == IntPtr.Zero) _brushSent    = CreateSolidBrush(Bgr(0.10f, 0.40f, 0.18f));
            if (_brushClose   == IntPtr.Zero) _brushClose   = CreateSolidBrush(Bgr(0.40f, 0.05f, 0.05f));
            if (_fontTitle    == IntPtr.Zero) _fontTitle    = MakeFont(18, FW_BOLD);
            if (_fontRow      == IntPtr.Zero) _fontRow      = MakeFont(13, FW_NORMAL);
            if (_fontHint     == IntPtr.Zero) _fontHint     = MakeFont(11, FW_NORMAL);
            if (_fontTag      == IntPtr.Zero) _fontTag      = MakeFont(12, FW_BOLD);
        }

        static IntPtr MakeFont(int sizePx, int weight)
        {
            var lf = new LOGFONT
            {
                lfHeight   = -sizePx,
                lfWeight   = weight,
                lfCharSet  = DEFAULT_CHARSET,
                lfQuality  = CLEARTYPE_QUALITY,
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

        // ── WindowProc + Paint ────────────────────────────────────────────
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
                    int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    _scrollY = Mathf.Clamp(_scrollY - (delta / 120) * RowH, 0, MaxScroll);
                    InvalidateRect(hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }
                case WM_LBUTTONDOWN:
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    // Close box (top-right of header)?
                    if (my < HeaderH && mx >= PopupWidth - 30) { Hide(); return IntPtr.Zero; }
                    int row = RowAtY(my);
                    if (row >= 0) InviteRow(row);
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
            int w = PopupWidth, h = PopupHeight;
            var full = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            FillRect(hdc, ref full, _brushBg);
            FrameRect(hdc, ref full, _brushBorder);
            var inset = new RECT { Left = 1, Top = 1, Right = w - 1, Bottom = h - 1 };
            FrameRect(hdc, ref inset, _brushBorder);

            SetBkMode(hdc, TRANSPARENT);

            // Header bar
            var header = new RECT { Left = 2, Top = 2, Right = w - 2, Bottom = HeaderH };
            FillRect(hdc, ref header, _brushHeader);
            var titleRc = new RECT { Left = 12, Top = 8, Right = w - 36, Bottom = HeaderH };
            SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
            var prev = SelectObject(hdc, _fontTitle);
            DrawTextW(hdc, "Convidar amigo da Steam", -1, ref titleRc,
                DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            // Close box
            var closeRc = new RECT { Left = w - 30, Top = 7, Right = w - 8, Bottom = HeaderH - 5 };
            FillRect(hdc, ref closeRc, _brushClose);
            SetTextColor(hdc, Bgr(1f, 1f, 1f));
            DrawTextW(hdc, "X", -1, ref closeRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            SelectObject(hdc, prev);

            // Rows (clipped to the list viewport)
            for (int i = 0; i < _friends.Count; i++)
            {
                int rowTop = ListTop + i * RowH - _scrollY;
                int rowBot = rowTop + RowH;
                if (rowBot <= ListTop || rowTop >= ListBottom) continue;   // off-screen
                int drawTop = Mathf.Max(rowTop, ListTop);
                int drawBot = Mathf.Min(rowBot, ListBottom);

                var f = _friends[i];
                var rowRc = new RECT { Left = 2, Top = drawTop, Right = w - 2, Bottom = drawBot };
                FillRect(hdc, ref rowRc, f.isCopy ? _brushHeader : ((i & 1) == 0 ? _brushRow : _brushRowAlt));

                bool placeholder = f.id == 0 && !f.isCopy;
                bool actionable  = !placeholder;
                if (actionable && !f.isCopy)
                {
                    // Status dot (Steam friends only)
                    var dot = new RECT { Left = 12, Top = rowTop + RowH / 2 - 4, Right = 20, Bottom = rowTop + RowH / 2 + 4 };
                    FillRect(hdc, ref dot, f.online ? _brushOnline : _brushOffline);
                }

                // Name
                SelectObject(hdc, _fontRow);
                SetTextColor(hdc, f.isCopy   ? Bgr(1f, 0.82f, 0.32f)
                                : placeholder ? Bgr(0.65f, 0.60f, 0.55f)
                                : (f.online ? Bgr(0.95f, 0.95f, 0.95f) : Bgr(0.62f, 0.62f, 0.62f)));
                var nameRc = new RECT { Left = (placeholder || f.isCopy) ? 12 : 28, Top = rowTop, Right = w - 90, Bottom = rowBot };
                DrawTextW(hdc, f.name, -1, ref nameRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

                // Action tag
                if (actionable)
                {
                    var tagRc = new RECT { Left = w - 84, Top = rowTop + 4, Right = w - 12, Bottom = rowBot - 4 };
                    FillRect(hdc, ref tagRc, f.sent ? _brushSent : _brushInvite);
                    SelectObject(hdc, _fontTag);
                    SetTextColor(hdc, f.sent ? Bgr(0.75f, 1f, 0.75f) : Bgr(1f, 0.82f, 0.32f));
                    string tag = f.isCopy ? (f.sent ? "Copiado!" : "Copiar")
                                          : (f.sent ? "Enviado!" : "Convidar");
                    DrawTextW(hdc, tag, -1, ref tagRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                }
            }

            // Footer
            SelectObject(hdc, _fontHint);
            SetTextColor(hdc, Bgr(0.60f, 0.60f, 0.66f));
            var hintRc = new RECT { Left = 12, Top = h - FooterH, Right = w - 12, Bottom = h - 4 };
            DrawTextW(hdc, "Clique num amigo para convidar · roda do mouse rola · ESC fecha", -1, ref hintRc,
                DT_LEFT | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);
        }

        internal static void Pump()
        {
            if (_hwnd == IntPtr.Zero) return;
            while (PeekMessageW(out var msg, _hwnd, 0, 0, PM_REMOVE) != 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
        }

        // ── WinAPI plumbing (mirrors WorldMapPopup) ───────────────────────
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
        const int  WM_PAINT       = 0x000F;
        const int  WM_KEYDOWN     = 0x0100;
        const int  WM_LBUTTONDOWN = 0x0201;
        const int  WM_MOUSEWHEEL  = 0x020A;
        const int  WM_DESTROY     = 0x0002;
        const int  WM_CLOSE       = 0x0010;
        const int  WM_ERASEBKGND  = 0x0014;
        const int  WS_POPUP       = unchecked((int)0x80000000);
        const int  WS_VISIBLE     = 0x10000000;
        const int  WS_EX_TOPMOST  = 0x00000008;
        const int  WS_EX_TOOLWINDOW = 0x00000080;
        const int  SW_SHOW        = 5;
        const uint PM_REMOVE      = 0x0001;
        const int  TRANSPARENT    = 1;
        const int  VK_ESCAPE      = 0x1B;
        const uint DT_TOP         = 0x00000000;
        const uint DT_LEFT        = 0x00000000;
        const uint DT_CENTER      = 0x00000001;
        const uint DT_RIGHT       = 0x00000002;
        const uint DT_VCENTER     = 0x00000004;
        const uint DT_BOTTOM      = 0x00000008;
        const uint DT_SINGLELINE  = 0x00000020;
        const uint DT_NOPREFIX    = 0x00000800;
        const uint DT_END_ELLIPSIS= 0x00008000;
        const uint CS_OWNDC       = 0x0020;
        const int  IDC_ARROW      = 32512;
        const byte DEFAULT_CHARSET    = 1;
        const byte CLEARTYPE_QUALITY  = 5;
        const int  FW_NORMAL = 400;
        const int  FW_BOLD   = 700;
        const uint SWP_NOSIZE     = 0x0001;
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
        [DllImport("user32.dll")] static extern int  TranslateMessage(ref MSG lpMsg);
        [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] static extern IntPtr DispatchMessageW(ref MSG lpMsg);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
        [DllImport("user32.dll")] static extern int  FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);
        [DllImport("user32.dll")] static extern int  FrameRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DrawTextW")] static extern int DrawTextW(IntPtr hdc, string s, int n, ref RECT rc, uint fmt);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll", EntryPoint = "LoadCursorW")] static extern IntPtr LoadCursorW(IntPtr hInst, IntPtr name);

        [DllImport("gdi32.dll")] static extern IntPtr CreateSolidBrush(uint color);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern int    SetTextColor(IntPtr hdc, uint color);
        [DllImport("gdi32.dll")] static extern int    SetBkMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateFontIndirectW")] static extern IntPtr CreateFontIndirectW(ref LOGFONT lf);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
        static extern IntPtr GetModuleHandleW(string name);
    }

    // Pumps the friends-popup messages on the Unity main thread.
    class FriendsListPump : MonoBehaviour
    {
        static FriendsListPump _instance;
        internal static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("FriendsListPump");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FriendsListPump>();
        }
        void Update()
        {
            FriendsListPopup.Pump();
            if (FriendsListPopup.IsOpen && Input.GetKeyDown(KeyCode.Escape))
                FriendsListPopup.Hide();
        }
    }
}

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
        // Width is pinned to the live game-strip width (like MenuPopupWindow) so the
        // invite panel sits flush above the game and shares its exact width.
        public static int PopupWidth => _popupWidth;
        public const int PopupHeight = 300;

        static int _popupWidth = 380;   // actual created window width; refreshed from the game strip
        static int CurrentWidth() => OverlayWindow.Instance != null ? OverlayWindow.Instance.windowWidth : 400;

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
            _search = "";
            RebuildView();
            _popupWidth = CurrentWidth();
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

        // Centered above the game strip — mirrors MenuPopupWindow's anchor.
        static (int, int) ComputePosition()
        {
            int gx = OverlayWindow.WinX;
            int gy = OverlayWindow.WinY;
            int gw = CurrentWidth();
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
        // Indices into _friends actually shown, after applying the name search filter.
        static readonly List<int> _view = new List<int>();
        // Current text typed into the search box (case-insensitive substring filter).
        static string _search = "";

        // Rebuilds _view from _friends applying the current search text. The "outside
        // Steam" action row is always kept; placeholder/info rows are hidden while
        // filtering so only matching friends remain.
        static void RebuildView()
        {
            _view.Clear();
            bool filtering = !string.IsNullOrEmpty(_search);
            for (int i = 0; i < _friends.Count; i++)
            {
                var f = _friends[i];
                if (f.isCopy) { _view.Add(i); continue; }
                bool placeholder = f.id == 0;
                if (!filtering)
                {
                    _view.Add(i);
                    continue;
                }
                if (placeholder) continue;
                if (!string.IsNullOrEmpty(f.name) &&
                    f.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                    _view.Add(i);
            }
        }

        // Reloads the friend list from Steam and rebuilds the filtered view.
        static void RefreshFriends()
        {
            PopulateFriends();
            RebuildView();
        }

        static void PopulateFriends()
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

            // Queue-backed invite: never a silent no-op even if the lobby is still
            // being created — SteamLobbyManager fires it once the lobby exists.
            var lobby = SteamLobbyManager.Instance;
            if (lobby == null) return;
            if (lobby.InviteFriend(f.id))
            {
                f.sent = true;
                _friends[index] = f;
                if (_hwnd != IntPtr.Zero) InvalidateRect(_hwnd, IntPtr.Zero, true);
            }
        }

        // ── Layout constants (popup-local pixels) ─────────────────────────
        const int HeaderH = 34;
        const int SearchH = 30;                     // search bar strip below the header
        const int FooterH = 22;
        const int RowH    = 30;
        static int SearchTop  => HeaderH + 4;
        static int ListTop    => HeaderH + SearchH + 4;
        static int ListBottom => PopupHeight - FooterH - 2;
        static int ListViewH  => ListBottom - ListTop;
        static int ContentH   => _view.Count * RowH;
        static int MaxScroll  => Mathf.Max(0, ContentH - ListViewH);

        static int _scrollY;

        // Maps a click Y (popup-local) to a view-row index, or -1.
        static int RowAtY(int y)
        {
            if (y < ListTop || y >= ListBottom) return -1;
            int idx = (y - ListTop + _scrollY) / RowH;
            return (idx >= 0 && idx < _view.Count) ? idx : -1;
        }

        // ── Internal state ────────────────────────────────────────────────
        static IntPtr _hwnd = IntPtr.Zero;
        static WndProcDelegate _wndProcDelegate;
        static bool   _classRegistered;
        // Red-Eyes-Black-Dragon palette (matches InventoryPopupWindow).
        static IntPtr _brushPanel, _brushOutline, _brushBevHi, _brushBevLo, _brushRuby, _brushDivider;
        static IntPtr _brushRowA,  _brushRowB,   _brushTag,   _brushTagUse, _brushOnline, _brushOffline;
        static IntPtr _fontTitle, _fontRow, _fontHint, _fontTag;
        const string  ClassName  = "ZulfarakFriendsPopup";
        const string  DragonRes  = "UI/DragonFrame";

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
            // Dark near-black panel + gold pixel bevel + ruby corner studs (mirrors inventory menu).
            if (_brushPanel   == IntPtr.Zero) _brushPanel   = CreateSolidBrush(Bgr(0.06f, 0.05f, 0.05f));
            if (_brushOutline == IntPtr.Zero) _brushOutline = CreateSolidBrush(Bgr(0.00f, 0.00f, 0.00f));
            if (_brushBevHi   == IntPtr.Zero) _brushBevHi   = CreateSolidBrush(Bgr(0.95f, 0.75f, 0.30f));
            if (_brushBevLo   == IntPtr.Zero) _brushBevLo   = CreateSolidBrush(Bgr(0.35f, 0.24f, 0.08f));
            if (_brushRuby    == IntPtr.Zero) _brushRuby    = CreateSolidBrush(Bgr(0.85f, 0.15f, 0.15f));
            if (_brushDivider == IntPtr.Zero) _brushDivider = CreateSolidBrush(Bgr(0.20f, 0.15f, 0.06f));
            if (_brushRowA    == IntPtr.Zero) _brushRowA    = CreateSolidBrush(Bgr(0.10f, 0.08f, 0.08f));
            if (_brushRowB    == IntPtr.Zero) _brushRowB    = CreateSolidBrush(Bgr(0.14f, 0.11f, 0.10f));
            if (_brushTag     == IntPtr.Zero) _brushTag     = CreateSolidBrush(Bgr(0.32f, 0.11f, 0.10f));
            if (_brushTagUse  == IntPtr.Zero) _brushTagUse  = CreateSolidBrush(Bgr(0.16f, 0.32f, 0.12f));
            if (_brushOnline  == IntPtr.Zero) _brushOnline  = CreateSolidBrush(Bgr(0.30f, 0.85f, 0.30f));
            if (_brushOffline == IntPtr.Zero) _brushOffline = CreateSolidBrush(Bgr(0.45f, 0.45f, 0.45f));
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
                    if (wParam.ToInt32() == VK_ESCAPE)
                    {
                        // First ESC clears an active search; a second ESC closes.
                        if (_search.Length > 0) { _search = ""; RebuildView(); _scrollY = 0; InvalidateRect(hWnd, IntPtr.Zero, true); }
                        else Hide();
                    }
                    return IntPtr.Zero;
                case WM_CHAR:
                {
                    int ch = wParam.ToInt32();
                    if (ch == 8) // Backspace
                    {
                        if (_search.Length > 0) _search = _search.Substring(0, _search.Length - 1);
                    }
                    else if (ch >= 32) // printable
                    {
                        _search += (char)ch;
                    }
                    else return IntPtr.Zero; // ignore Enter/Tab/ESC etc.
                    _scrollY = 0;
                    RebuildView();
                    InvalidateRect(hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }
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
                    int viewRow = RowAtY(my);
                    if (viewRow >= 0) InviteRow(_view[viewRow]);
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

            // ── Frame: dark panel + gold pixel bevel + ruby corner studs ──
            var full = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            FillRect(hdc, ref full, _brushPanel);
            NativeFrameImage.PixelBevel(hdc, 0, 0, w, h, _brushOutline, _brushBevHi, _brushBevLo, _brushPanel);
            NativeFrameImage.PixelCornerStuds(hdc, 0, 0, w, h, _brushRuby, inset: 5, size: 3);

            SetBkMode(hdc, TRANSPARENT);

            // ── Header bar with divider strip ──
            var headerBar = new RECT { Left = 3, Top = 3, Right = w - 3, Bottom = HeaderH };
            FillRect(hdc, ref headerBar, _brushDivider);

            // Small dragon emblem inside a bevelled square on the left of the header.
            const int EmblemSize = 24;
            int emblemY = 3 + (HeaderH - 3 - EmblemSize) / 2;
            int emblemX = 6;
            NativeFrameImage.PixelBevel(hdc, emblemX, emblemY, EmblemSize, EmblemSize,
                _brushOutline, _brushBevHi, _brushBevLo, _brushPanel);
            var dragon = NativeFrameImage.Get(DragonRes);
            if (dragon.Ready)
                dragon.BlitAspect(hdc, emblemX + 3, emblemY + 3, EmblemSize - 6, EmblemSize - 6);

            // Title
            var titleRc = new RECT { Left = emblemX + EmblemSize + 8, Top = 6, Right = w - 36, Bottom = HeaderH };
            SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
            var prev = SelectObject(hdc, _fontTitle);
            DrawTextW(hdc, "Convidar amigo da Steam", -1, ref titleRc,
                DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            // Bevelled close box (top-right).
            NativeFrameImage.PixelBevel(hdc, w - 26, 7, 20, HeaderH - 11,
                _brushOutline, _brushBevHi, _brushBevLo, _brushTag);
            var closeRc = new RECT { Left = w - 26, Top = 7, Right = w - 6, Bottom = HeaderH - 4 };
            SetTextColor(hdc, Bgr(1f, 0.92f, 0.85f));
            DrawTextW(hdc, "X", -1, ref closeRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            SelectObject(hdc, prev);

            // ── Search bar (type to filter the friend list by name) ──
            int searchX = 8, searchY = SearchTop, searchW = w - 16, searchBoxH = SearchH - 8;
            NativeFrameImage.PixelBevel(hdc, searchX, searchY, searchW, searchBoxH,
                _brushOutline, _brushBevHi, _brushBevLo, _brushPanel);
            SelectObject(hdc, _fontRow);
            var searchRc = new RECT { Left = searchX + 12, Top = searchY, Right = searchX + searchW - 10, Bottom = searchY + searchBoxH };
            if (string.IsNullOrEmpty(_search))
            {
                SetTextColor(hdc, Bgr(0.55f, 0.50f, 0.42f));
                DrawTextW(hdc, "Buscar amigo pelo nome...", -1, ref searchRc,
                    DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            }
            else
            {
                SetTextColor(hdc, Bgr(0.98f, 0.96f, 0.88f));
                DrawTextW(hdc, _search + "|", -1, ref searchRc,
                    DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            }

            // ── Rows (the filtered view, clipped to the list viewport) ──
            for (int vi = 0; vi < _view.Count; vi++)
            {
                int i = _view[vi];
                int rowTop = ListTop + vi * RowH - _scrollY;
                int rowBot = rowTop + RowH;
                if (rowBot <= ListTop || rowTop >= ListBottom) continue;
                int drawTop = Mathf.Max(rowTop, ListTop);
                int drawBot = Mathf.Min(rowBot, ListBottom);

                var f = _friends[i];
                var rowRc = new RECT { Left = 3, Top = drawTop, Right = w - 3, Bottom = drawBot };
                FillRect(hdc, ref rowRc, f.isCopy ? _brushDivider : ((vi & 1) == 0 ? _brushRowA : _brushRowB));

                bool placeholder = f.id == 0 && !f.isCopy;
                bool actionable  = !placeholder;
                if (actionable && !f.isCopy)
                {
                    var dot = new RECT { Left = 12, Top = rowTop + RowH / 2 - 4, Right = 20, Bottom = rowTop + RowH / 2 + 4 };
                    FillRect(hdc, ref dot, f.online ? _brushOnline : _brushOffline);
                }

                SelectObject(hdc, _fontRow);
                SetTextColor(hdc, f.isCopy    ? Bgr(1f, 0.82f, 0.32f)
                                : placeholder ? Bgr(0.65f, 0.60f, 0.55f)
                                : (f.online ? Bgr(0.95f, 0.95f, 0.95f) : Bgr(0.62f, 0.62f, 0.62f)));
                var nameRc = new RECT { Left = (placeholder || f.isCopy) ? 12 : 28, Top = rowTop, Right = w - 92, Bottom = rowBot };
                DrawTextW(hdc, f.name, -1, ref nameRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

                // Bevelled action tag (green when sent, ruby-dark when actionable).
                if (actionable)
                {
                    int tagX = w - 86, tagY = rowTop + 4, tagW = 74, tagH = RowH - 8;
                    NativeFrameImage.PixelBevel(hdc, tagX, tagY, tagW, tagH,
                        _brushOutline, _brushBevHi, _brushBevLo, f.sent ? _brushTagUse : _brushTag);
                    var tagRc = new RECT { Left = tagX, Top = tagY, Right = tagX + tagW, Bottom = tagY + tagH };
                    SelectObject(hdc, _fontTag);
                    SetTextColor(hdc, f.sent ? Bgr(0.85f, 1f, 0.85f) : Bgr(1f, 0.90f, 0.55f));
                    string tag = f.isCopy ? (f.sent ? "Copiado!" : "Copiar")
                                          : (f.sent ? "Enviado!" : "Convidar");
                    DrawTextW(hdc, tag, -1, ref tagRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                }
            }

            // ── Empty-search state ──
            if (!string.IsNullOrEmpty(_search))
            {
                bool anyFriend = false;
                for (int k = 0; k < _view.Count; k++)
                {
                    var fe = _friends[_view[k]];
                    if (!fe.isCopy && fe.id != 0) { anyFriend = true; break; }
                }
                if (!anyFriend)
                {
                    SelectObject(hdc, _fontRow);
                    SetTextColor(hdc, Bgr(0.65f, 0.60f, 0.55f));
                    var emptyRc = new RECT { Left = 12, Top = ListTop + RowH + 6, Right = w - 12, Bottom = ListTop + RowH * 3 };
                    DrawTextW(hdc, $"Nenhum amigo encontrado para \"{_search}\".", -1, ref emptyRc,
                        DT_CENTER | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
                }
            }

            // ── Footer hint ──
            SelectObject(hdc, _fontHint);
            SetTextColor(hdc, Bgr(0.72f, 0.62f, 0.42f));
            var hintRc = new RECT { Left = 12, Top = h - FooterH, Right = w - 12, Bottom = h - 6 };
            DrawTextW(hdc, "Digite para buscar · clique para convidar · roda rola · ESC limpa/fecha", -1, ref hintRc,
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
        const int  WM_CHAR        = 0x0102;
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

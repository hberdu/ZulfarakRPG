using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZulfarakRPG
{
    // A native Win32 popup window used to render NPC menus / dialog text. Opens
    // as a SEPARATE top-level window above the game strip — like a new tab —
    // so the main gameplay window never resizes or shifts when a menu opens.
    //
    // Closes on left-click anywhere inside it, ESC, or via MenuPopupWindow.Hide().
    public static class MenuPopupWindow
    {
        // ── Public API ────────────────────────────────────────────────────────
        public const int PopupHeight = 220;

        public static bool IsOpen => _hwnd != IntPtr.Zero;

        public static void Show(string title, string body)
        {
            // Replace any other top popup (map / invite) — only one modal at a time.
            TopPopups.CloseAllExcept(TopPopups.Kind.Npc);
#if UNITY_EDITOR
            Debug.Log($"[MenuPopup] {title}\n\n{body}");
#else
            _title = title ?? "";
            _body  = body  ?? "";

            if (_hwnd != IntPtr.Zero)
            {
                Reposition();
                InvalidateRect(_hwnd, IntPtr.Zero, true);
                SetForegroundWindow(_hwnd);
                return;
            }

            EnsureClassRegistered();
            EnsureGdiObjects();
            _popupWidth = CurrentWidth();
            (int x, int y) = ComputePosition();

            _hwnd = CreateWindowExW(
                WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
                ClassName,
                title ?? "",
                WS_POPUP | WS_VISIBLE,
                x, y, _popupWidth, PopupHeight,
                IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);

            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SW_SHOW);
                SetForegroundWindow(_hwnd);
                MenuPopupPump.Ensure();
            }
#endif
        }

        public static void Hide()
        {
            if (_hwnd == IntPtr.Zero) return;
            var h = _hwnd;
            _hwnd = IntPtr.Zero;
            DestroyWindow(h);
        }

        // Reposition (and resize, if the game width changed) so the popup stays
        // glued directly above the game strip. Safe to call when closed.
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

        // ── Internal ──────────────────────────────────────────────────────────
        static IntPtr _hwnd = IntPtr.Zero;
        static string _title = "";
        static string _body  = "";
        static WndProcDelegate _wndProcDelegate;   // kept alive for the class
        static bool _classRegistered;
        static IntPtr _fontTitle, _fontBody;
        static IntPtr _brushBg, _brushBorder;
        static IntPtr _brushOutline, _brushBevHi, _brushBevLo, _brushRuby;
        static int    _popupWidth = 400;

        const string ClassName = "ZulfarakMenuPopup";
        // Red-Eyes-Black-Dragon frame (Resources/UI/DragonFrame). Painted behind the
        // dark content card; absent → the card simply sits on the near-black bg.
        const string DragonRes = "UI/DragonFrame";

        static int CurrentWidth()
            => OverlayWindow.Instance != null ? OverlayWindow.Instance.windowWidth : 400;

        static (int, int) ComputePosition()
        {
            int gx = OverlayWindow.WinX;
            int gy = OverlayWindow.WinY;
            int gw = CurrentWidth();
            // Match the game strip horizontally and sit flush on top of it.
            int x = gx + (gw - _popupWidth) / 2;
            int y = gy - PopupHeight;
            int sw = Screen.currentResolution.width;
            int sh = Screen.currentResolution.height;
            x = Mathf.Clamp(x, 0, Mathf.Max(0, sw - _popupWidth));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, sh - PopupHeight));
            return (x, y);
        }

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
            // Dark dragon palette: near-black window base, tarnished-gold trim with a
            // classic pixel-art bevel (dark outline + light/dark gold shoulders + panel).
            if (_brushBg      == IntPtr.Zero) _brushBg      = CreateSolidBrush(Bgr(0.03f, 0.02f, 0.03f));
            if (_brushBorder  == IntPtr.Zero) _brushBorder  = CreateSolidBrush(Bgr(0.52f, 0.40f, 0.15f));
            if (_brushOutline == IntPtr.Zero) _brushOutline = CreateSolidBrush(Bgr(0.00f, 0.00f, 0.00f));
            if (_brushBevHi   == IntPtr.Zero) _brushBevHi   = CreateSolidBrush(Bgr(0.95f, 0.75f, 0.30f));
            if (_brushBevLo   == IntPtr.Zero) _brushBevLo   = CreateSolidBrush(Bgr(0.35f, 0.24f, 0.08f));
            if (_brushRuby    == IntPtr.Zero) _brushRuby    = CreateSolidBrush(Bgr(0.85f, 0.15f, 0.15f));
            if (_fontTitle    == IntPtr.Zero) _fontTitle    = MakeFont(20, FW_BOLD);
            if (_fontBody     == IntPtr.Zero) _fontBody     = MakeFont(14, FW_NORMAL);
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

        static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_PAINT:
                {
                    BeginPaint(hWnd, out var ps);
                    Paint(ps.hdc);
                    EndPaint(hWnd, ref ps);
                    return IntPtr.Zero;
                }

                case WM_ERASEBKGND:
                    return new IntPtr(1);

                case WM_KEYDOWN:
                    if (wParam.ToInt32() == VK_ESCAPE) Hide();
                    return IntPtr.Zero;

                case WM_LBUTTONDOWN:
                    Hide();
                    return IntPtr.Zero;

                case WM_CLOSE:
                case WM_DESTROY:
                    _hwnd = IntPtr.Zero;
                    return IntPtr.Zero;
            }
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        static void Paint(IntPtr hdc)
        {
            int w = _popupWidth;
            int h = PopupHeight;
            var full = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            FillRect(hdc, ref full, _brushBg);   // near-black base

            // Chunky pixel-art bevel across the whole window (outline + gold shoulders +
            // dark panel). This is the "menu frame" the dragon sits inside — no more
            // stretched dragon-across-everything, so the wings keep their true silhouette.
            NativeFrameImage.PixelBevel(hdc, 0, 0, w, h,
                _brushOutline, _brushBevHi, _brushBevLo, _brushBg);
            NativeFrameImage.PixelCornerStuds(hdc, 0, 0, w, h, _brushRuby, inset: 5, size: 3);

            // Compact dragon emblem — top-left badge, native aspect ratio (no squash),
            // with its own smaller pixel bevel so it reads as a framed portrait next to
            // the title. The dragon "integrates" the menu instead of drowning it.
            const int EmblemPad = 6;
            int emblemH = h - EmblemPad * 2;                // fills the vertical strip
            int emblemW = emblemH;                          // square badge (art is ~square)
            int emblemX = EmblemPad;
            int emblemY = EmblemPad;
            NativeFrameImage.PixelBevel(hdc, emblemX, emblemY, emblemW, emblemH,
                _brushOutline, _brushBevHi, _brushBevLo, _brushBg);
            var frame = NativeFrameImage.Get(DragonRes);
            if (frame.Ready)
                frame.BlitAspect(hdc, emblemX + 4, emblemY + 4, emblemW - 8, emblemH - 8);

            SetBkMode(hdc, TRANSPARENT);

            // Title + body pushed right of the emblem.
            int textLeft = emblemX + emblemW + 12;
            var titleRc = new RECT { Left = textLeft, Top = 12, Right = w - 12, Bottom = 40 };
            SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
            var prev = SelectObject(hdc, _fontTitle);
            DrawTextW(hdc, _title, -1, ref titleRc, DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            SelectObject(hdc, prev);

            // 1-px gold rule under the title.
            var rule = new RECT { Left = textLeft, Top = 42, Right = w - 12, Bottom = 43 };
            FillRect(hdc, ref rule, _brushBorder);

            // Body
            var bodyRc = new RECT { Left = textLeft, Top = 48, Right = w - 12, Bottom = h - 24 };
            SetTextColor(hdc, Bgr(0.94f, 0.94f, 0.98f));
            prev = SelectObject(hdc, _fontBody);
            DrawTextW(hdc, _body, -1, ref bodyRc, DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);

            // Footer hint
            var hintRc = new RECT { Left = textLeft, Top = h - 22, Right = w - 12, Bottom = h - 6 };
            SetTextColor(hdc, Bgr(0.62f, 0.62f, 0.68f));
            DrawTextW(hdc, "Clique ou ESC para fechar", -1, ref hintRc, DT_RIGHT | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);
            SelectObject(hdc, prev);
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

        // ── WinAPI ────────────────────────────────────────────────────────────
        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASSEX
        {
            public uint   cbSize;
            public uint   style;
            public IntPtr lpfnWndProc;
            public int    cbClsExtra;
            public int    cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint   message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint   time;
            public int    pt_x;
            public int    pt_y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool   fErase;
            public RECT   rcPaint;
            public bool   fRestore;
            public bool   fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct LOGFONT
        {
            public int  lfHeight;
            public int  lfWidth;
            public int  lfEscapement;
            public int  lfOrientation;
            public int  lfWeight;
            public byte lfItalic;
            public byte lfUnderline;
            public byte lfStrikeOut;
            public byte lfCharSet;
            public byte lfOutPrecision;
            public byte lfClipPrecision;
            public byte lfQuality;
            public byte lfPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
        }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        const int  WM_PAINT       = 0x000F;
        const int  WM_KEYDOWN     = 0x0100;
        const int  WM_LBUTTONDOWN = 0x0201;
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
        const uint DT_RIGHT       = 0x00000002;
        const uint DT_BOTTOM      = 0x00000008;
        const uint DT_WORDBREAK   = 0x00000010;
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

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")]
        static extern ushort RegisterClassExW(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
        static extern IntPtr CreateWindowExW(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int X, int Y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")] static extern bool   DestroyWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool   ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] static extern bool   InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
        [DllImport("user32.dll")] static extern bool   SetWindowPos(IntPtr h, IntPtr ins, int x, int y, int w, int hgt, uint flags);
        [DllImport("user32.dll", EntryPoint = "DefWindowProcW")] static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", EntryPoint = "PeekMessageW")] static extern int PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] static extern int    TranslateMessage(ref MSG lpMsg);
        [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] static extern IntPtr DispatchMessageW(ref MSG lpMsg);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);
        [DllImport("user32.dll")] static extern bool   EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
        [DllImport("user32.dll")] static extern int    FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);
        [DllImport("user32.dll")] static extern int    FrameRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DrawTextW")] static extern int DrawTextW(IntPtr hdc, string s, int n, ref RECT rc, uint fmt);
        [DllImport("user32.dll")] static extern bool   SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll", EntryPoint = "LoadCursorW")] static extern IntPtr LoadCursorW(IntPtr hInst, IntPtr name);

        [DllImport("gdi32.dll")] static extern IntPtr CreateSolidBrush(uint color);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern int    SetTextColor(IntPtr hdc, uint color);
        [DllImport("gdi32.dll")] static extern int    SetBkMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateFontIndirectW")] static extern IntPtr CreateFontIndirectW(ref LOGFONT lf);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
        static extern IntPtr GetModuleHandleW(string name);
    }

    // Pumps native popup messages on the Unity main thread.
    class MenuPopupPump : MonoBehaviour
    {
        static MenuPopupPump _instance;
        internal static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("MenuPopupPump");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MenuPopupPump>();
        }

        void Update()
        {
            MenuPopupWindow.Pump();
            // Allow ESC to close the popup even when the game window has focus.
            if (MenuPopupWindow.IsOpen && Input.GetKeyDown(KeyCode.Escape))
                MenuPopupWindow.Hide();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZulfarakRPG
{
    // Native Win32 popup that renders the WORLD MAP using GDI. Mirrors the
    // approach used by MenuPopupWindow (separate top-level window above the
    // game strip) so the map matches the visual / behavioural style of the
    // other NPC popups (Mestre Arqueiro, Ferreiro, etc.) instead of appearing
    // in-world over the gameplay.
    //
    // Drawn entirely via GDI primitives: FillRect for the parchment, Ellipse
    // for city circles, short LineTo segments for the dashed route, DrawText
    // for the title + city labels. Closes on left-click or ESC.
    public static class WorldMapPopup
    {
        // ── Public API ────────────────────────────────────────────────────
        public const int PopupWidth  = 460;
        public const int PopupHeight = 220;

        public static bool IsOpen => _hwnd != IntPtr.Zero;

        public static void Show()
        {
            // Replace any other top popup (NPC dialog / invite) — only one at a time.
            TopPopups.CloseAllExcept(TopPopups.Kind.Map);
#if UNITY_EDITOR
            // The native window can't render reliably while the editor owns
            // the Game view — fall back to the existing in-world WorldMapPanel
            // so the feature still works during play-mode testing.
            WorldMapPanel.Show();
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
            (int x, int y) = ComputePosition();

            _hwnd = CreateWindowExW(
                WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
                ClassName,
                "Mapa do Mundo",
                WS_POPUP | WS_VISIBLE,
                x, y, PopupWidth, PopupHeight,
                IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);

            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SW_SHOW);
                SetForegroundWindow(_hwnd);
                WorldMapPopupPump.Ensure();
            }
#endif
        }

        public static void Hide()
        {
#if UNITY_EDITOR
            WorldMapPanel.Hide();
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

        // ── City layout (mirrors WorldMapPanel) ──────────────────────────
        struct CityDef
        {
            public string Name;
            public int    X, Y;        // pixel offset from the paper's centre
            public bool   Locked;
        }

        // Centred on the paper interior; the paper rect is computed in Paint.
        static readonly CityDef[] Cities =
        {
            new CityDef { Name = "Zulfarak", X = -180, Y =  10, Locked = false },
            new CityDef { Name = "???",      X =  -90, Y = -10, Locked = true  },
            new CityDef { Name = "???",      X =    0, Y =  10, Locked = true  },
            new CityDef { Name = "???",      X =   90, Y =  -8, Locked = true  },
            new CityDef { Name = "???",      X =  180, Y =   6, Locked = true  },
        };

        // ── Internal state ───────────────────────────────────────────────
        static IntPtr _hwnd = IntPtr.Zero;
        static WndProcDelegate _wndProcDelegate;
        static bool   _classRegistered;
        static IntPtr _brushBg, _brushBorder, _brushPaper, _brushPaperDk;
        static IntPtr _brushCityActive, _brushCityLocked, _brushCityEdge, _brushGlow;
        static IntPtr _penInk;
        static IntPtr _fontTitle, _fontCity, _fontHint;
        const string ClassName = "ZulfarakWorldMapPopup";

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
            if (_brushBg          == IntPtr.Zero) _brushBg          = CreateSolidBrush(Bgr(0.05f, 0.03f, 0.02f));
            if (_brushBorder      == IntPtr.Zero) _brushBorder      = CreateSolidBrush(Bgr(0.85f, 0.65f, 0.20f));
            if (_brushPaper       == IntPtr.Zero) _brushPaper       = CreateSolidBrush(Bgr(0.94f, 0.85f, 0.58f));
            if (_brushPaperDk     == IntPtr.Zero) _brushPaperDk     = CreateSolidBrush(Bgr(0.28f, 0.18f, 0.06f));
            if (_brushCityActive  == IntPtr.Zero) _brushCityActive  = CreateSolidBrush(Bgr(0.95f, 0.62f, 0.20f));
            if (_brushCityLocked  == IntPtr.Zero) _brushCityLocked  = CreateSolidBrush(Bgr(0.22f, 0.20f, 0.22f));
            if (_brushCityEdge    == IntPtr.Zero) _brushCityEdge    = CreateSolidBrush(Bgr(0.30f, 0.20f, 0.08f));
            if (_brushGlow        == IntPtr.Zero) _brushGlow        = CreateSolidBrush(Bgr(1.00f, 0.78f, 0.30f));
            if (_penInk           == IntPtr.Zero) _penInk           = CreatePen(PS_SOLID, 2, Bgr(0.30f, 0.18f, 0.05f));
            if (_fontTitle        == IntPtr.Zero) _fontTitle        = MakeFont(20, FW_BOLD);
            if (_fontCity         == IntPtr.Zero) _fontCity         = MakeFont(12, FW_BOLD);
            if (_fontHint         == IntPtr.Zero) _fontHint         = MakeFont(11, FW_NORMAL);
        }

        static IntPtr MakeFont(int sizePx, int weight)
        {
            var lf = new LOGFONT
            {
                lfHeight   = -sizePx,
                lfWeight   = weight,
                lfCharSet  = DEFAULT_CHARSET,
                lfQuality  = CLEARTYPE_QUALITY,
                lfFaceName = "Segoe UI",
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

        // ── WindowProc + Paint ───────────────────────────────────────────
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
            int w = PopupWidth;
            int h = PopupHeight;
            var full = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            FillRect(hdc, ref full, _brushBg);
            FrameRect(hdc, ref full, _brushBorder);
            var inset = new RECT { Left = 1, Top = 1, Right = w - 1, Bottom = h - 1 };
            FrameRect(hdc, ref inset, _brushBorder);

            // Paper rectangle (inset from the outer popup)
            int paperLeft   = 22, paperTop = 44, paperRight = w - 22, paperBottom = h - 30;
            // Dark border behind paper
            var paperBorder = new RECT { Left = paperLeft - 3, Top = paperTop - 3, Right = paperRight + 3, Bottom = paperBottom + 3 };
            FillRect(hdc, ref paperBorder, _brushPaperDk);
            var paper = new RECT { Left = paperLeft, Top = paperTop, Right = paperRight, Bottom = paperBottom };
            FillRect(hdc, ref paper, _brushPaper);

            int cx = (paperLeft + paperRight) / 2;
            int cy = (paperTop  + paperBottom) / 2;

            // Title above the paper
            SetBkMode(hdc, TRANSPARENT);
            var titleRc = new RECT { Left = 0, Top = 10, Right = w, Bottom = 36 };
            SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
            var prevFont = SelectObject(hdc, _fontTitle);
            DrawTextW(hdc, "Mapa do Mundo", -1, ref titleRc, DT_CENTER | DT_TOP | DT_SINGLELINE | DT_NOPREFIX);

            // Dashed connectors between consecutive cities (short LineTo segments
            // give us the dashed look without needing a custom PS_DASH pen style).
            var prevPen = SelectObject(hdc, _penInk);
            for (int i = 0; i < Cities.Length - 1; i++)
            {
                int x1 = cx + Cities[i].X,     y1 = cy + Cities[i].Y;
                int x2 = cx + Cities[i + 1].X, y2 = cy + Cities[i + 1].Y;
                DrawIrregularDashed(hdc, x1, y1, x2, y2, segmentLen: 6, gapLen: 4, wobblePx: 2);
            }
            SelectObject(hdc, prevPen);

            // City circles (outer dark ring + filled core)
            for (int i = 0; i < Cities.Length; i++)
            {
                int x = cx + Cities[i].X, y = cy + Cities[i].Y;
                int r = 9;
                // Outer ring
                var outer = new RECT { Left = x - r, Top = y - r, Right = x + r, Bottom = y + r };
                FillRect(hdc, ref outer, _brushCityEdge);
                // Inner fill
                int ri = r - 3;
                var inner = new RECT { Left = x - ri, Top = y - ri, Right = x + ri, Bottom = y + ri };
                FillRect(hdc, ref inner, Cities[i].Locked ? _brushCityLocked : _brushCityActive);
                if (!Cities[i].Locked)
                {
                    // Bright core dot for the active city ("you are here").
                    int rg = 2;
                    var glow = new RECT { Left = x - rg, Top = y - rg, Right = x + rg, Bottom = y + rg };
                    FillRect(hdc, ref glow, _brushGlow);
                }
            }

            // City labels below the circles
            SelectObject(hdc, _fontCity);
            for (int i = 0; i < Cities.Length; i++)
            {
                int x = cx + Cities[i].X, y = cy + Cities[i].Y;
                SetTextColor(hdc, Cities[i].Locked
                    ? Bgr(0.55f, 0.50f, 0.40f)
                    : Bgr(0.20f, 0.10f, 0.02f));
                var lblRc = new RECT { Left = x - 50, Top = y + 14, Right = x + 50, Bottom = y + 30 };
                DrawTextW(hdc, Cities[i].Name, -1, ref lblRc, DT_CENTER | DT_TOP | DT_SINGLELINE | DT_NOPREFIX);
                if (Cities[i].Locked)
                {
                    // "?" tag floating above the locked dot
                    SetTextColor(hdc, Bgr(0.65f, 0.60f, 0.50f));
                    var qRc = new RECT { Left = x - 8, Top = y - 22, Right = x + 8, Bottom = y - 6 };
                    DrawTextW(hdc, "?", -1, ref qRc, DT_CENTER | DT_TOP | DT_SINGLELINE | DT_NOPREFIX);
                }
            }

            // Footer hint
            SelectObject(hdc, _fontHint);
            SetTextColor(hdc, Bgr(0.60f, 0.60f, 0.66f));
            var hintRc = new RECT { Left = 0, Top = h - 22, Right = w, Bottom = h - 6 };
            DrawTextW(hdc, "Clique ou ESC para fechar", -1, ref hintRc,
                DT_CENTER | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);

            SelectObject(hdc, prevFont);
        }

        // Draws a hand-drawn-looking dashed line by chunking the segment into
        // short solid pieces (segmentLen) separated by gaps (gapLen), with each
        // segment offset perpendicular by a small wobble for an irregular feel.
        static void DrawIrregularDashed(IntPtr hdc, int x1, int y1, int x2, int y2,
                                         int segmentLen, int gapLen, int wobblePx)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1f) return;
            float ux = dx / len, uy = dy / len;
            float px = -uy, py = ux;

            float gap = 18f;                                  // keep a small gap near each city dot
            float drawnFrom = gap;
            float drawnTo   = len - gap;
            float t = drawnFrom;
            int seed = (x1 * 73856093) ^ (y1 * 19349663) ^ (x2 * 83492791);
            int wobbleStep = 0;
            while (t < drawnTo)
            {
                float a = t;
                float b = Mathf.Min(drawnTo, t + segmentLen);
                float w1 = ((seed + wobbleStep)     % 7 - 3) * wobblePx / 3f;
                float w2 = ((seed + wobbleStep + 1) % 5 - 2) * wobblePx / 2f;
                int sx = (int)(x1 + ux * a + px * w1);
                int sy = (int)(y1 + uy * a + py * w1);
                int ex = (int)(x1 + ux * b + px * w2);
                int ey = (int)(y1 + uy * b + py * w2);
                MoveToEx(hdc, sx, sy, IntPtr.Zero);
                LineTo(hdc, ex, ey);
                t = b + gapLen;
                wobbleStep++;
            }
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

        // ── WinAPI plumbing (mirrors MenuPopupWindow) ─────────────────────
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
        const uint DT_CENTER      = 0x00000001;
        const uint DT_BOTTOM      = 0x00000008;
        const uint DT_SINGLELINE  = 0x00000020;
        const uint DT_NOPREFIX    = 0x00000800;
        const uint CS_OWNDC       = 0x0020;
        const int  IDC_ARROW      = 32512;
        const byte DEFAULT_CHARSET    = 1;
        const byte CLEARTYPE_QUALITY  = 5;
        const int  FW_NORMAL = 400;
        const int  FW_BOLD   = 700;
        const int  PS_SOLID  = 0;
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
        [DllImport("gdi32.dll")] static extern IntPtr CreatePen(int style, int width, uint color);
        [DllImport("gdi32.dll")] static extern bool   DeleteObject(IntPtr hgdi);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern int    SetTextColor(IntPtr hdc, uint color);
        [DllImport("gdi32.dll")] static extern int    SetBkMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateFontIndirectW")] static extern IntPtr CreateFontIndirectW(ref LOGFONT lf);
        [DllImport("gdi32.dll")] static extern bool   MoveToEx(IntPtr hdc, int x, int y, IntPtr lpPoint);
        [DllImport("gdi32.dll")] static extern bool   LineTo(IntPtr hdc, int x, int y);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
        static extern IntPtr GetModuleHandleW(string name);
    }

    // Pumps the world-map popup messages on the Unity main thread.
    class WorldMapPopupPump : MonoBehaviour
    {
        static WorldMapPopupPump _instance;
        internal static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("WorldMapPopupPump");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<WorldMapPopupPump>();
        }
        void Update()
        {
            WorldMapPopup.Pump();
            if (WorldMapPopup.IsOpen && Input.GetKeyDown(KeyCode.Escape))
                WorldMapPopup.Hide();
        }
    }
}

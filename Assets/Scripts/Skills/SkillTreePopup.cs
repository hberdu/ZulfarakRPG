using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZulfarakRPG
{
    // Native Win32 vertical skill tree (Diablo IV-inspired): nodes stacked top→bottom,
    // 4 skills per node, connected by a spine. Click a skill's button to spend a point
    // and level it up; learned skills auto-cast in the dungeon (SkillAutoCaster). Floats
    // above the game strip like the other top popups; only one open at a time.
    public static class SkillTreePopup
    {
        public static int PopupWidth => _popupWidth;
        public const int PopupHeight = 380;
        static int _popupWidth = 400;
        static int CurrentWidth() => OverlayWindow.Instance != null ? OverlayWindow.Instance.windowWidth : 400;

        public static bool IsOpen => _hwnd != IntPtr.Zero;

        public static void Toggle() { if (IsOpen) Hide(); else Show(); }

        // ── Layout ────────────────────────────────────────────────────────
        const int HeaderH   = 34;
        const int FooterH   = 22;
        const int NodeLabelH = 18;
        const int CellH     = 78;
        const int NodeGap   = 16;
        static int ContentTop => HeaderH + 6;
        static int ListBottom => PopupHeight - FooterH - 2;
        static int NodeBlockH => NodeLabelH + CellH + NodeGap;
        static int ContentH   => SkillDefs.NodeCount * NodeBlockH;
        static int MaxScroll   => Mathf.Max(0, ContentH - (ListBottom - ContentTop));
        static int _scrollY;
        static string _hoverId;      // skill under the cursor (for tooltip)
        static int _hoverX, _hoverY;

        struct Cell
        {
            public string id; public int x, y, w, h;
            public int upX, upY, upW, upH;   // upgrade (learn) button
            public int eqX, eqY, eqW, eqH;   // equip toggle button (learned only)
            public bool canLearn, learned, equipped;
        }
        static readonly List<Cell> _cells = new List<Cell>(32);

        // ── Public API ──────────────────────────────────────────────────
        public static void Show()
        {
            TopPopups.CloseAllExcept(TopPopups.Kind.Skills);
            SkillManager.Ensure();
#if UNITY_EDITOR
            Debug.Log("[SkillTree] aberto (editor).");
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
            _popupWidth = CurrentWidth();
            (int x, int y) = ComputePosition();
            _hwnd = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW, ClassName, "Habilidades",
                WS_POPUP | WS_VISIBLE, x, y, PopupWidth, PopupHeight,
                IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SW_SHOW);
                SetForegroundWindow(_hwnd);
                SkillTreePump.Ensure();
            }
#endif
        }

        public static void Hide()
        {
#if UNITY_EDITOR
#else
            if (_hwnd == IntPtr.Zero) return;
            var h = _hwnd; _hwnd = IntPtr.Zero; DestroyWindow(h);
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
                SetWindowPos(_hwnd, HWND_TOPMOST, x, y, _popupWidth, PopupHeight, SWP_SHOWWINDOW | SWP_NOACTIVATE);
                InvalidateRect(_hwnd, IntPtr.Zero, true);
            }
            else SetWindowPos(_hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }

        static (int, int) ComputePosition()
        {
            int gx = OverlayWindow.WinX, gy = OverlayWindow.WinY, gw = CurrentWidth();
            int x = gx + (gw - PopupWidth) / 2;
            int y = gy - PopupHeight;
            int sw = Screen.currentResolution.width, sh = Screen.currentResolution.height;
            x = Mathf.Clamp(x, 0, Mathf.Max(0, sw - PopupWidth));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, sh - PopupHeight));
            return (x, y);
        }

        // ── Layout builder (shared by Paint + click hit-test) ─────────────
        static void BuildLayout()
        {
            _cells.Clear();
            var mgr = SkillManager.Instance;
            int w = PopupWidth;
            int marginX = 10;
            int contentW = w - marginX * 2;
            int gap = 6;
            int cellW = (contentW - gap * (SkillDefs.SkillsPerNode - 1)) / SkillDefs.SkillsPerNode;

            for (int node = 0; node < SkillDefs.NodeCount; node++)
            {
                int blockTop = ContentTop + node * NodeBlockH - _scrollY;
                int rowTop = blockTop + NodeLabelH;
                int col = 0;
                foreach (var def in SkillDefs.InNode(node))
                {
                    int cx = marginX + col * (cellW + gap);
                    int btnH = 16;
                    int rowY = rowTop + CellH - btnH - 6;
                    bool learned = mgr != null && mgr.GetLevel(def.id) > 0;

                    int upX, upW, eqX, eqW;
                    if (learned)
                    {
                        int half = (cellW - 12 - 4) / 2;
                        upX = cx + 6; upW = half;
                        eqX = cx + 6 + half + 4; eqW = half;
                    }
                    else { upX = cx + 6; upW = cellW - 12; eqX = 0; eqW = 0; }

                    _cells.Add(new Cell
                    {
                        id = def.id, x = cx, y = rowTop, w = cellW, h = CellH,
                        upX = upX, upY = rowY, upW = upW, upH = btnH,
                        eqX = eqX, eqY = rowY, eqW = eqW, eqH = btnH,
                        canLearn = mgr != null && mgr.CanLearn(def.id),
                        learned = learned,
                        equipped = mgr != null && mgr.IsEquipped(def.id)
                    });
                    col++;
                }
            }
        }

        // ── WndProc + Paint ──────────────────────────────────────────────
        static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_PAINT:
                    BeginPaint(hWnd, out var ps); Paint(ps.hdc); EndPaint(hWnd, ref ps);
                    return IntPtr.Zero;
                case WM_ERASEBKGND: return new IntPtr(1);
                case WM_KEYDOWN:
                    if (wParam.ToInt32() == VK_ESCAPE) Hide();
                    return IntPtr.Zero;
                case WM_MOUSEWHEEL:
                {
                    int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    _scrollY = Mathf.Clamp(_scrollY - (delta / 120) * 28, 0, MaxScroll);
                    _hoverId = null;
                    InvalidateRect(hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }
                case WM_MOUSEMOVE:
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    _hoverX = mx; _hoverY = my;
                    string prevHover = _hoverId;
                    _hoverId = null;
                    if (my >= ContentTop && my < ListBottom)
                    {
                        BuildLayout();
                        foreach (var c in _cells)
                            if (mx >= c.x && mx <= c.x + c.w && my >= c.y && my <= c.y + c.h) { _hoverId = c.id; break; }
                    }
                    if (_hoverId != prevHover) InvalidateRect(hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }
                case WM_LBUTTONDOWN:
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    if (my < HeaderH && mx >= PopupWidth - 30) { Hide(); return IntPtr.Zero; }
                    BuildLayout();
                    foreach (var c in _cells)
                    {
                        // Upgrade button
                        if (c.canLearn && mx >= c.upX && mx <= c.upX + c.upW && my >= c.upY && my <= c.upY + c.upH)
                        {
                            SkillManager.Instance?.Learn(c.id);
                            InvalidateRect(hWnd, IntPtr.Zero, true);
                            break;
                        }
                        // Equip toggle button (learned skills only)
                        if (c.learned && c.eqW > 0 && mx >= c.eqX && mx <= c.eqX + c.eqW && my >= c.eqY && my <= c.eqY + c.eqH)
                        {
                            SkillManager.Instance?.ToggleEquip(c.id);
                            InvalidateRect(hWnd, IntPtr.Zero, true);
                            break;
                        }
                    }
                    return IntPtr.Zero;
                }
                case WM_CLOSE:
                case WM_DESTROY:
                    _hwnd = IntPtr.Zero; return IntPtr.Zero;
            }
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        // True when the RPG UI Pack atlas is available — swaps the dark bevel frame/header
        // for the wood-trim board + ribbon skin (the interactive skill grid stays dark,
        // sitting on the board, so its learned/equipped colour semantics stay readable).
        static bool Rpg => RpgUiNative.Ready;

        static void Paint(IntPtr hdc)
        {
            BuildLayout();
            var mgr = SkillManager.Instance;
            int w = PopupWidth, h = PopupHeight;

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

            // Header (wood ribbon title when skinned).
            var prev = SelectObject(hdc, _fontTitle);
            if (Rpg)
            {
                int rbW = Mathf.Min(w - 200, 230);
                RpgUiNative.WoodRibbon(hdc, 6, 3, rbW, HeaderH - 2);
                SetTextColor(hdc, RpgUiNative.InkTitle);
                var t = new RECT { Left = 16, Top = 3, Right = rbW - 6, Bottom = HeaderH };
                DrawTextW(hdc, "Arvore de Habilidades", -1, ref t, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            }
            else
            {
                var headerBar = new RECT { Left = 3, Top = 3, Right = w - 3, Bottom = HeaderH };
                FillRect(hdc, ref headerBar, _brushDivider);
                // Arcane tome emblem left of the title when the themed art is present.
                var emb = NativeFrameImage.Get("UI/Emblem_Skills");
                int embW = emb.Ready ? HeaderH - 6 : 0;
                if (emb.Ready) emb.BlitAspect(hdc, 8, 4, embW, embW);
                SetTextColor(hdc, Bgr(1.00f, 0.82f, 0.32f));
                var titleRc = new RECT { Left = 12 + (embW > 0 ? embW + 4 : 0), Top = 6, Right = w - 190, Bottom = HeaderH };
                DrawTextW(hdc, "Arvore de Habilidades", -1, ref titleRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
            }

            // Points available + equipped count (top-right, before the close box)
            SelectObject(hdc, _fontRow);
            SetTextColor(hdc, Bgr(0.75f, 1f, 0.75f));
            int pts = mgr != null ? mgr.AvailablePoints : 0;
            int eq  = mgr != null ? mgr.EquippedCount : 0;
            var ptsRc = new RECT { Left = w - 186, Top = 6, Right = w - 30, Bottom = HeaderH };
            DrawTextW(hdc, $"Pontos: {pts}   Equip: {eq}/{SkillManager.MaxEquipped}", -1, ref ptsRc, DT_RIGHT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

            if (Rpg) RpgUiNative.DarkButton(hdc, w - 26, 7, 20, HeaderH - 11);
            else NativeFrameImage.PixelBevel(hdc, w - 26, 7, 20, HeaderH - 11, _brushOutline, _brushBevHi, _brushBevLo, _brushTag);
            var closeRc = new RECT { Left = w - 26, Top = 7, Right = w - 6, Bottom = HeaderH - 4 };
            SetTextColor(hdc, Bgr(1f, 0.92f, 0.85f));
            DrawTextW(hdc, "X", -1, ref closeRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            SelectObject(hdc, prev);

            // Clip the scrolling content so it never bleeds over the header/footer.
            IntersectClipRect(hdc, 0, ContentTop, w, ListBottom);

            // Vertical spine connecting the nodes.
            int spineX = w / 2;
            for (int node = 0; node < SkillDefs.NodeCount; node++)
            {
                int blockTop = ContentTop + node * NodeBlockH - _scrollY;
                int segTop = Mathf.Max(ContentTop, blockTop);
                int segBot = Mathf.Min(ListBottom, blockTop + NodeBlockH);
                if (segBot <= ContentTop || segTop >= ListBottom) continue;
                var spine = new RECT { Left = spineX - 1, Top = segTop, Right = spineX + 1, Bottom = segBot };
                FillRect(hdc, ref spine, _brushSpine);
            }

            // Node labels + skill cells.
            for (int node = 0; node < SkillDefs.NodeCount; node++)
            {
                int blockTop = ContentTop + node * NodeBlockH - _scrollY;
                if (blockTop + NodeBlockH <= ContentTop || blockTop >= ListBottom) continue;

                bool unlocked = mgr == null || mgr.IsNodeUnlocked(node);
                // Node label bar
                if (blockTop >= ContentTop - NodeLabelH && blockTop < ListBottom)
                {
                    SelectObject(hdc, _fontSection);
                    SetTextColor(hdc, unlocked ? Bgr(1f, 0.82f, 0.32f) : Bgr(0.55f, 0.55f, 0.58f));
                    var lblRc = new RECT { Left = 12, Top = blockTop, Right = w - 12, Bottom = blockTop + NodeLabelH };
                    string nodeName = NodeName(node) + (unlocked ? "" : "  (bloqueado)");
                    DrawTextW(hdc, nodeName, -1, ref lblRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                }
            }

            // Draw cells (clipped to the list viewport).
            foreach (var c in _cells)
            {
                if (c.y + c.h <= ContentTop || c.y >= ListBottom) continue;
                var def = SkillDefs.Get(c.id);
                if (def == null) continue;
                int level = mgr != null ? mgr.GetLevel(c.id) : 0;
                bool learned = level > 0;
                bool maxed = level >= def.maxLevel;
                bool nodeUnlocked = mgr == null || mgr.IsNodeUnlocked(def.node);

                // Cell frame — equipped skills get a green-lit fill so the 2 actives pop.
                IntPtr fill = c.equipped ? _brushCellEq : (learned ? _brushCellOn : _brushCellOff);
                NativeFrameImage.PixelBevel(hdc, c.x, c.y, c.w, c.h, _brushOutline,
                    (learned || c.equipped) ? _brushBevHi : _brushBevLo, _brushBevLo, fill);

                // Skill icon (top-left).
                int icoSz = 32;
                NativeFrameImage.PixelBevel(hdc, c.x + 4, c.y + 4, icoSz, icoSz, _brushOutline, _brushBevLo, _brushBevLo, _brushCellOff);
                var ico = IconLibrary.Gdi(def.iconPath);
                if (ico != null && ico.Ready) ico.BlitAspect(hdc, c.x + 6, c.y + 6, icoSz - 4, icoSz - 4);
                int tx = c.x + 4 + icoSz + 5;

                // Name (right of the icon)
                SelectObject(hdc, _fontTag);
                SetTextColor(hdc, !nodeUnlocked ? Bgr(0.5f, 0.5f, 0.52f)
                                 : learned ? Bgr(1f, 0.86f, 0.42f) : Bgr(0.92f, 0.92f, 0.94f));
                var nameRc = new RECT { Left = tx, Top = c.y + 4, Right = c.x + c.w - 5, Bottom = c.y + 20 };
                DrawTextW(hdc, def.name, -1, ref nameRc, DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

                // Level + cooldown
                SelectObject(hdc, _fontHint);
                SetTextColor(hdc, Bgr(0.72f, 0.72f, 0.78f));
                var lvlRc = new RECT { Left = tx, Top = c.y + 20, Right = c.x + c.w - 5, Bottom = c.y + 32 };
                DrawTextW(hdc, $"Nv {level}/{def.maxLevel}", -1, ref lvlRc, DT_LEFT | DT_TOP | DT_SINGLELINE | DT_NOPREFIX);
                var cdRc = new RECT { Left = tx, Top = c.y + 32, Right = c.x + c.w - 5, Bottom = c.y + 44 };
                float cd = def.CooldownAt(Mathf.Max(1, level));
                string effTag = def.effect == SkillEffect.Heal ? "cura" : "dano";
                DrawTextW(hdc, $"CD {cd:0.0}s · {effTag}", -1, ref cdRc, DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

                // Upgrade button (left / full width when not yet learned)
                string upLabel; IntPtr upBg; uint upTxt;
                if (maxed)              { upLabel = "MAX";       upBg = _brushTag;      upTxt = Bgr(1f, 0.86f, 0.42f); }
                else if (c.canLearn)    { upLabel = learned ? "Evoluir" : "Aprender"; upBg = _brushTagLearn; upTxt = Bgr(0.85f, 1f, 0.85f); }
                else if (!nodeUnlocked) { upLabel = "Bloqueado"; upBg = _brushTag;      upTxt = Bgr(0.6f, 0.6f, 0.62f); }
                else                    { upLabel = "Sem ponto"; upBg = _brushTag;      upTxt = Bgr(0.6f, 0.6f, 0.62f); }
                NativeFrameImage.PixelBevel(hdc, c.upX, c.upY, c.upW, c.upH, _brushOutline, _brushBevHi, _brushBevLo, upBg);
                SelectObject(hdc, _fontTag);
                SetTextColor(hdc, upTxt);
                var upRc = new RECT { Left = c.upX, Top = c.upY, Right = c.upX + c.upW, Bottom = c.upY + c.upH };
                DrawTextW(hdc, upLabel, -1, ref upRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

                // Equip toggle button (learned skills only)
                if (learned && c.eqW > 0)
                {
                    string eqLabel = c.equipped ? "Ativa" : "Equipar";
                    IntPtr eqBg    = c.equipped ? _brushTagLearn : _brushTag;
                    uint   eqTxt   = c.equipped ? Bgr(0.85f, 1f, 0.85f) : Bgr(1f, 0.86f, 0.42f);
                    NativeFrameImage.PixelBevel(hdc, c.eqX, c.eqY, c.eqW, c.eqH, _brushOutline, _brushBevHi, _brushBevLo, eqBg);
                    SelectObject(hdc, _fontTag);
                    SetTextColor(hdc, eqTxt);
                    var eqRc = new RECT { Left = c.eqX, Top = c.eqY, Right = c.eqX + c.eqW, Bottom = c.eqY + c.eqH };
                    DrawTextW(hdc, eqLabel, -1, ref eqRc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                }
            }

            // Done with the clipped content region.
            SelectClipRgn(hdc, IntPtr.Zero);

            // ornate frame LAST so it rings the window over the content
            NativeFrameImage.DrawWindowFrame(hdc, 0, 0, w, h);

            // Tooltip for the hovered skill (drawn over everything, unclipped).
            DrawTooltip(hdc, w, h);

            // Footer hint
            SelectObject(hdc, _fontHint);
            SetTextColor(hdc, Bgr(0.72f, 0.62f, 0.42f));
            var hintRc = new RECT { Left = 12, Top = h - FooterH, Right = w - 12, Bottom = h - 6 };
            DrawTextW(hdc, "Aprender/Evoluir gasta ponto · Equipar ativa a skill (max 2) · roda rola · ESC fecha", -1, ref hintRc,
                DT_LEFT | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);
            SelectObject(hdc, prev);
        }

        // Hover tooltip: skill name + description + effect/cooldown, near the cursor.
        static void DrawTooltip(IntPtr hdc, int w, int h)
        {
            if (string.IsNullOrEmpty(_hoverId)) return;
            var def = SkillDefs.Get(_hoverId);
            if (def == null) return;
            int level = Mathf.Max(1, SkillManager.Instance != null ? SkillManager.Instance.GetLevel(_hoverId) : 0);

            int tw = 214, th = 78;
            int tx = Mathf.Clamp(_hoverX + 14, 4, w - tw - 4);
            int ty = Mathf.Clamp(_hoverY + 14, HeaderH, h - th - 4);
            NativeFrameImage.PixelBevel(hdc, tx, ty, tw, th, _brushOutline, _brushBevHi, _brushBevLo, _brushCellOff);

            SelectObject(hdc, _fontSection);
            SetTextColor(hdc, Bgr(1f, 0.86f, 0.42f));
            var nameRc = new RECT { Left = tx + 8, Top = ty + 6, Right = tx + tw - 8, Bottom = ty + 22 };
            DrawTextW(hdc, def.name, -1, ref nameRc, DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            SelectObject(hdc, _fontHint);
            SetTextColor(hdc, Bgr(0.88f, 0.88f, 0.92f));
            var descRc = new RECT { Left = tx + 8, Top = ty + 24, Right = tx + tw - 8, Bottom = ty + 54 };
            DrawTextW(hdc, def.desc, -1, ref descRc, DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);

            SetTextColor(hdc, Bgr(0.70f, 1f, 0.70f));
            var statRc = new RECT { Left = tx + 8, Top = ty + th - 16, Right = tx + tw - 8, Bottom = ty + th - 4 };
            string fx = def.effect == SkillEffect.Heal ? "Cura" : "Dano";
            DrawTextW(hdc, $"{fx} {Mathf.RoundToInt(def.PowerAt(level))} · CD {def.CooldownAt(level):0.0}s", -1, ref statRc,
                DT_LEFT | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);
        }

        static string NodeName(int node) => SkillDefs.NodeName(node);

        internal static void Pump()
        {
            if (_hwnd == IntPtr.Zero) return;
            while (PeekMessageW(out var msg, _hwnd, 0, 0, PM_REMOVE) != 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
        }

        // ── GDI objects ──────────────────────────────────────────────────
        static IntPtr _hwnd = IntPtr.Zero;
        static WndProcDelegate _wndProcDelegate;
        static bool _classRegistered;
        static IntPtr _brushPanel, _brushOutline, _brushBevHi, _brushBevLo, _brushRuby, _brushDivider, _brushTag;
        static IntPtr _brushCellOn, _brushCellOff, _brushCellEq, _brushTagLearn, _brushSpine;
        static IntPtr _fontTitle, _fontSection, _fontRow, _fontTag, _fontHint;
        const string ClassName = "ZulfarakSkillTreePopup";

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
            if (_brushPanel     == IntPtr.Zero) _brushPanel     = CreateSolidBrush(Bgr(0.06f, 0.05f, 0.05f));
            if (_brushOutline   == IntPtr.Zero) _brushOutline   = CreateSolidBrush(Bgr(0.00f, 0.00f, 0.00f));
            // Dark-gray trim (was tarnished gold).
            if (_brushBevHi     == IntPtr.Zero) _brushBevHi     = CreateSolidBrush(Bgr(0.42f, 0.42f, 0.46f));
            if (_brushBevLo     == IntPtr.Zero) _brushBevLo     = CreateSolidBrush(Bgr(0.15f, 0.15f, 0.17f));
            if (_brushRuby      == IntPtr.Zero) _brushRuby      = CreateSolidBrush(Bgr(0.85f, 0.15f, 0.15f));
            if (_brushDivider   == IntPtr.Zero) _brushDivider   = CreateSolidBrush(Bgr(0.16f, 0.16f, 0.18f));
            if (_brushTag       == IntPtr.Zero) _brushTag       = CreateSolidBrush(Bgr(0.32f, 0.11f, 0.10f));
            if (_brushTagLearn  == IntPtr.Zero) _brushTagLearn  = CreateSolidBrush(Bgr(0.16f, 0.32f, 0.12f));
            if (_brushCellOn    == IntPtr.Zero) _brushCellOn    = CreateSolidBrush(Bgr(0.16f, 0.13f, 0.07f));
            if (_brushCellOff   == IntPtr.Zero) _brushCellOff   = CreateSolidBrush(Bgr(0.10f, 0.09f, 0.09f));
            if (_brushCellEq    == IntPtr.Zero) _brushCellEq    = CreateSolidBrush(Bgr(0.10f, 0.20f, 0.10f));
            if (_brushSpine     == IntPtr.Zero) _brushSpine     = CreateSolidBrush(Bgr(0.30f, 0.22f, 0.08f));
            if (_fontTitle      == IntPtr.Zero) _fontTitle      = MakeFont(16, FW_BOLD);
            if (_fontSection    == IntPtr.Zero) _fontSection    = MakeFont(13, FW_BOLD);
            if (_fontRow        == IntPtr.Zero) _fontRow        = MakeFont(12, FW_NORMAL);
            if (_fontTag        == IntPtr.Zero) _fontTag        = MakeFont(11, FW_BOLD);
            if (_fontHint       == IntPtr.Zero) _fontHint       = MakeFont(10, FW_NORMAL);
        }

        static IntPtr MakeFont(int sizePx, int weight)
        {
            var lf = new LOGFONT
            {
                lfHeight = -sizePx, lfWeight = weight, lfCharSet = DEFAULT_CHARSET,
                lfQuality = CLEARTYPE_QUALITY, lfFaceName = NativeFont.Face,
            };
            return CreateFontIndirectW(ref lf);
        }

        static uint Bgr(float r, float g, float b)
        {
            byte R = (byte)(Mathf.Clamp01(r) * 255), G = (byte)(Mathf.Clamp01(g) * 255), B = (byte)(Mathf.Clamp01(b) * 255);
            return (uint)(R | (G << 8) | (B << 16));
        }

        // ── WinAPI plumbing (mirrors FriendsListPopup) ───────────────────
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
        const int WM_PAINT = 0x000F, WM_KEYDOWN = 0x0100, WM_LBUTTONDOWN = 0x0201, WM_MOUSEWHEEL = 0x020A, WM_MOUSEMOVE = 0x0200;
        const int WM_DESTROY = 0x0002, WM_CLOSE = 0x0010, WM_ERASEBKGND = 0x0014;
        const int WS_POPUP = unchecked((int)0x80000000), WS_VISIBLE = 0x10000000;
        const int WS_EX_TOPMOST = 0x00000008, WS_EX_TOOLWINDOW = 0x00000080;
        const int SW_SHOW = 5; const uint PM_REMOVE = 0x0001;
        const int TRANSPARENT = 1, VK_ESCAPE = 0x1B;
        const uint DT_TOP = 0, DT_LEFT = 0, DT_CENTER = 1, DT_RIGHT = 2, DT_VCENTER = 4, DT_BOTTOM = 8;
        const uint DT_SINGLELINE = 0x20, DT_NOPREFIX = 0x800, DT_END_ELLIPSIS = 0x8000, DT_WORDBREAK = 0x0010;
        const uint CS_OWNDC = 0x0020; const int IDC_ARROW = 32512;
        const byte DEFAULT_CHARSET = 1, CLEARTYPE_QUALITY = 5;
        const int FW_NORMAL = 400, FW_BOLD = 700;
        const uint SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;

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

    class SkillTreePump : MonoBehaviour
    {
        static SkillTreePump _instance;
        internal static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("SkillTreePump");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SkillTreePump>();
        }
        void Update()
        {
            SkillTreePopup.Pump();
            if (SkillTreePopup.IsOpen && Input.GetKeyDown(KeyCode.Escape))
                SkillTreePopup.Hide();
        }
    }
}

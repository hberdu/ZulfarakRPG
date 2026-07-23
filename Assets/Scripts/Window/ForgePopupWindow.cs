using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace ZulfarakRPG
{
    // The blacksmith's FORGE — a native Win32 top popup (same family as the inventory / map /
    // friends windows, coordinated by TopPopups so only one is open). Left: the bag's equippable
    // items (click to select). Right: a pixel forge scene (glowing forge + anvil + two happy
    // fire/earth elementals that DANCE while enhancing). Enhance is SERVER-AUTHORITATIVE: the
    // backend (POST /api/forge/upgrade) spends the gold, rolls success (-8%/level, +10% stats per
    // level), destroys the item on failure, and records the ledger. The client just shows the
    // dance until the reply lands, then applies the returned inventory state + SUCCESS/FAILED banner.
    public static class ForgePopupWindow
    {
        public const int PopupHeight = 300;
        static int _popupWidth = 400;
        static int CurrentWidth() => OverlayWindow.Instance != null ? OverlayWindow.Instance.windowWidth : 400;
        public static int PopupWidth => _popupWidth;
        public static bool IsOpen => _hwnd != IntPtr.Zero;

        struct Row { public string id, name; public uint color; public string iconPath; public ItemRarity rarity; public int level; }
        static readonly List<Row> _rows = new List<Row>();
        static int _sel = -1, _scroll;

        // Item icon cells (left-pane grid), rebuilt by BuildGrid; shared by Paint + hit-tests.
        struct IconCell { public int index, x, y, w, h; }
        static readonly List<IconCell> _cells = new List<IconCell>();

        // Enhance animation state (driven from the Unity main thread by ForgePopupPump).
        static bool  _animating; static float _animT; static string _pendingId; static int _pendingLevel;
        static Task<ForgeUpgradeResultDto> _pendingTask;

        public static void Show()
        {
            TopPopups.CloseAllExcept(TopPopups.Kind.Forge);
            RebuildItems();
#if UNITY_EDITOR
            Debug.Log($"[Forge] itens equipáveis: {_rows.Count}");
#else
            if (_hwnd != IntPtr.Zero) { Reposition(); InvalidateRect(_hwnd, IntPtr.Zero, true); SetForegroundWindow(_hwnd); return; }
            EnsureClass(); EnsureGdi();
            _scroll = 0; _sel = _rows.Count > 0 ? 0 : -1;
            _popupWidth = CurrentWidth();
            (int x, int y) = ComputePosition();
            _hwnd = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW, ClassName, "Forja",
                WS_POPUP | WS_VISIBLE, x, y, PopupWidth, PopupHeight, IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
            if (_hwnd != IntPtr.Zero) { ShowWindow(_hwnd, SW_SHOW); SetForegroundWindow(_hwnd); ForgePopupPump.Ensure(); }
#endif
        }

        public static void Hide()
        {
#if !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero) return;
            var h = _hwnd; _hwnd = IntPtr.Zero; _animating = false; DestroyWindow(h);
#endif
        }

        static void RebuildItems()
        {
            _rows.Clear();
            var inv = Inventory.Instance; var db = ItemDatabase.Instance;
            if (inv == null) return;
            foreach (var it in inv.Items)
            {
                var data = db != null ? db.Get(it.itemId) : null;
                if (data == null || data.itemType == ItemType.Consumable) continue;
                _rows.Add(new Row { id = it.itemId,
                    name = string.IsNullOrWhiteSpace(data.itemName) ? it.itemId : data.itemName,
                    color = BgrOf(ItemData.QualityColor(data.rarity)),
                    iconPath = data.iconPath, rarity = data.rarity, level = it.upgradeLevel });
            }
            if (_sel >= _rows.Count) _sel = _rows.Count - 1;
        }

        // ── Upgrade math ──────────────────────────────────────────────────────
        // Level is now per-instance and server-authoritative (Inventory item), not PlayerPrefs.
        // Exposed so other windows (inventory) can show the same "+N" enhancement badge.
        public static int UpgradeLevel(string id) => Inventory.Instance != null ? Inventory.Instance.GetUpgradeLevel(id) : 0;
        // Mirrors the backend ForgeRules.SuccessChance so the % shown matches what the server rolls.
        static float Chance(int lvl) => Mathf.Clamp(0.90f - 0.08f * lvl, 0.10f, 0.90f);

        // ── Enhance flow (started on click, resolved by the server) ───────────
        // The roll, gold cost, scaling and ledger are all backend-authoritative now. The click
        // kicks off the request; the dance plays until BOTH the min animation time and the server
        // response are done, then the returned inventory state is applied.
        static void StartEnhance()
        {
            if (_animating || _sel < 0 || _sel >= _rows.Count) return;
            var inv = Inventory.Instance;
            var api = ServerApiClient.Instance;
            if (inv == null || !inv.HasItem(_rows[_sel].id)) { RebuildItems(); return; }
            if (api == null || !api.IsReady) { PixelBanner.Show("SEM CONEXAO", new Color(0.95f, 0.5f, 0.16f)); return; }
            _pendingId = _rows[_sel].id; _pendingLevel = _rows[_sel].level;
            _animating = true; _animT = 0f;
            _pendingTask = ForgeAsync(_pendingId, _pendingLevel);
        }

        // Push the current bag to the server (so the item exists at `level` there), then run the
        // authoritative forge. Exceptions surface via the faulted task and are shown as FALHOU.
        static async Task<ForgeUpgradeResultDto> ForgeAsync(string id, int level)
        {
            await Inventory.Instance.PersistToServerNowAsync();
            return await ServerApiClient.Instance.ForgeUpgradeAsync(id, level);
        }

        // Called every frame by the pump while the elementals dance; resolves once the server replies.
        internal static void Tick(float dt)
        {
            if (_hwnd == IntPtr.Zero) return;
            // Repaint every frame so the animated forge + floating spirits play live (not only
            // while enhancing). Double-buffered paint keeps it flicker-free.
            InvalidateRect(_hwnd, IntPtr.Zero, false);
            if (!_animating) return;
            _animT += dt;
            if (_animT < 1.3f) return;
            if (_pendingTask != null && !_pendingTask.IsCompleted) return;   // wait for the server

            _animating = false;
            var task = _pendingTask; _pendingTask = null;
            if (task == null || task.IsFaulted || task.Result == null)
            {
                Debug.LogWarning($"[Forge] upgrade falhou: {task?.Exception?.GetBaseException().Message ?? "resposta vazia"}");
                PixelBanner.Show("FALHOU", new Color(0.95f, 0.20f, 0.16f));
            }
            else
            {
                var r = task.Result;
                Inventory.Instance?.ApplyForgeResult(r.state);
                if (PlayerManager.Instance != null && PlayerManager.Instance.Data != null)
                    PlayerManager.Instance.Data.gold = r.gold;
                PixelBanner.Show(r.success ? "SUCCESS" : "FAILED",
                    r.success ? new Color(0.35f, 1f, 0.45f) : new Color(0.95f, 0.20f, 0.16f));
            }
            RebuildItems();
            if (_hwnd != IntPtr.Zero) InvalidateRect(_hwnd, IntPtr.Zero, true);
        }

        // ── Layout — mirrors the inventory popup (icon grid + RPG-UI skin) ───────
        const int HeaderH = 28, FooterH = 16, SectionHeaderH = 16;
        const int CellSize = 40, CellGap = 4;

        static int BodyTop    => HeaderH + 2;
        static int BodyBottom => PopupHeight - FooterH;
        static int LeftPaneW  => PopupWidth * 44 / 100;
        static int RightPaneX => LeftPaneW + 2;

        // Item icon grid (left pane).
        static int GridLeft  => 8;
        static int GridRight => LeftPaneW - 8;
        static int GridTop   => BodyTop + SectionHeaderH + 4;
        static int GridBot   => BodyBottom - 4;
        static int GridCols  => Mathf.Max(1, (GridRight - GridLeft + CellGap) / (CellSize + CellGap));
        static int GridRowsN => Mathf.CeilToInt(_rows.Count / (float)GridCols);
        static int MaxScroll => Mathf.Max(0, GridRowsN * (CellSize + CellGap) - (GridBot - GridTop));

        static bool Rpg => RpgUiNative.Ready;

        static void BuildGrid()
        {
            _cells.Clear();
            int cols = GridCols;
            for (int i = 0; i < _rows.Count; i++)
            {
                int col = i % cols, row = i / cols;
                int x = GridLeft + col * (CellSize + CellGap);
                int y = GridTop + row * (CellSize + CellGap) - _scroll;
                _cells.Add(new IconCell { index = i, x = x, y = y, w = CellSize, h = CellSize });
            }
        }

        static int CellAt(int mx, int my)
        {
            foreach (var c in _cells)
            {
                if (c.y + c.h <= GridTop || c.y >= GridBot) continue;
                if (mx < c.x || mx > c.x + c.w || my < c.y || my > c.y + c.h) continue;
                return c.index;
            }
            return -1;
        }

        // APRIMORAR button spans the bottom of the right (forge) pane.
        static void ForgeBtn(out int x, out int y, out int w, out int h)
        { x = RightPaneX + 6; w = PopupWidth - x - 8; h = 24; y = BodyBottom - h - 4; }

        // ── WndProc + Paint ───────────────────────────────────────────────────
        static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_PAINT:
                    BeginPaint(hWnd, out var ps);
                    {
                        // Double-buffer: compose off-screen then blit once, so the animated
                        // forge + spirits (repainted every frame) never flicker.
                        int bw2 = PopupWidth, bh2 = PopupHeight;
                        IntPtr mem = CreateCompatibleDC(ps.hdc);
                        IntPtr bmp = CreateCompatibleBitmap(ps.hdc, bw2, bh2);
                        if (mem != IntPtr.Zero && bmp != IntPtr.Zero)
                        {
                            IntPtr oldBmp = SelectObject(mem, bmp);
                            Paint(mem);
                            BitBlt(ps.hdc, 0, 0, bw2, bh2, mem, 0, 0, SRCCOPY);
                            SelectObject(mem, oldBmp);
                        }
                        else Paint(ps.hdc);
                        if (bmp != IntPtr.Zero) DeleteObject(bmp);
                        if (mem != IntPtr.Zero) DeleteDC(mem);
                    }
                    EndPaint(hWnd, ref ps); return IntPtr.Zero;
                case WM_ERASEBKGND: return new IntPtr(1);
                case WM_KEYDOWN: if (wParam.ToInt32() == VK_ESCAPE) Hide(); return IntPtr.Zero;
                case WM_MOUSEWHEEL:
                {
                    int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    RebuildItems();
                    _scroll = Mathf.Clamp(_scroll - (delta / 120) * (CellSize + CellGap), 0, MaxScroll);
                    InvalidateRect(hWnd, IntPtr.Zero, true); return IntPtr.Zero;
                }
                case WM_LBUTTONDOWN:
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF), my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    if (my < HeaderH && mx >= PopupWidth - 26) { Hide(); return IntPtr.Zero; }
                    RebuildItems(); BuildGrid();
                    int r = CellAt(mx, my);
                    if (r >= 0) { _sel = r; InvalidateRect(hWnd, IntPtr.Zero, true); return IntPtr.Zero; }
                    ForgeBtn(out int bx, out int by, out int bw, out int bh);
                    if (mx >= bx && mx <= bx + bw && my >= by && my <= by + bh) { StartEnhance(); InvalidateRect(hWnd, IntPtr.Zero, true); }
                    return IntPtr.Zero;
                }
                case WM_CLOSE: case WM_DESTROY: _hwnd = IntPtr.Zero; return IntPtr.Zero;
            }
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        static void Paint(IntPtr hdc)
        {
            RebuildItems();
            int w = PopupWidth, h = PopupHeight;

            // Outer frame — RPG-UI dark board (falls back to the pixel bevel).
            var full = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            FillRect(hdc, ref full, _bPanel);
            if (Rpg) RpgUiNative.DarkBoard(hdc, 0, 0, w, h);
            else if (!NativeFrameImage.DrawWindowTheme(hdc, 0, 0, w, h))
                NativeFrameImage.PixelBevel(hdc, 0, 0, w, h, _bOutline, _bBevHi, _bBevLo, _bPanel);
            SetBkMode(hdc, TRANSPARENT);

            // ── Header (wood ribbon title + close button, exactly like the inventory) ──
            SelectObject(hdc, _fTitle);
            if (Rpg)
            {
                int rbW = Mathf.Min(w - 40, 150);
                RpgUiNative.WoodRibbon(hdc, 6, 3, rbW, HeaderH - 2);
                var t = new RECT { Left = 16, Top = 3, Right = rbW - 6, Bottom = HeaderH };
                SetTextColor(hdc, RpgUiNative.InkTitle);
                DrawTextW(hdc, "FORJA", -1, ref t, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            }
            else
            {
                var hb = new RECT { Left = 3, Top = 3, Right = w - 3, Bottom = HeaderH };
                FillRect(hdc, ref hb, _bDivider);
                // Anvil emblem left of the title when the themed art is present.
                var emb = NativeFrameImage.Get("UI/Emblem_Forge");
                int embW = emb.Ready ? HeaderH - 6 : 0;
                if (emb.Ready) emb.BlitAspect(hdc, 8, 4, embW, embW);
                var trc = new RECT { Left = 10 + (embW > 0 ? embW + 6 : 0), Top = 6, Right = w - 30, Bottom = HeaderH };
                SetTextColor(hdc, Bgr(1f, 0.82f, 0.32f));
                DrawTextW(hdc, "FORJA", -1, ref trc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            }
            int clX = w - 24, clY = 6;
            if (Rpg) RpgUiNative.DarkButton(hdc, clX, clY, 18, HeaderH - 10);
            else NativeFrameImage.PixelBevel(hdc, clX, clY, 18, HeaderH - 10, _bOutline, _bBevHi, _bBevLo, _bTag);
            SelectObject(hdc, _fTag); SetTextColor(hdc, Bgr(1f, 1f, 1f));
            var xrc = new RECT { Left = clX, Top = clY, Right = clX + 18, Bottom = clY + HeaderH - 10 };
            DrawTextW(hdc, "X", -1, ref xrc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

            int bodyTop = BodyTop, bodyBot = BodyBottom;

            // Panes: left = item icon grid, right = forge scene + action.
            if (Rpg)
            {
                RpgUiNative.Parchment(hdc, 4, bodyTop, LeftPaneW - 6, bodyBot - bodyTop);
                RpgUiNative.Parchment(hdc, RightPaneX, bodyTop, w - RightPaneX - 4, bodyBot - bodyTop);
            }
            else
            {
                NativeFrameImage.PixelBevel(hdc, 4, bodyTop, LeftPaneW - 6, bodyBot - bodyTop, _bOutline, _bBevHi, _bBevLo, _bVoid);
                NativeFrameImage.PixelBevel(hdc, RightPaneX, bodyTop, w - RightPaneX - 4, bodyBot - bodyTop, _bOutline, _bBevHi, _bBevLo, _bVoid);
            }
            DrawPaneHeader(hdc, 8, LeftPaneW - 8, bodyTop, "Itens");
            DrawPaneHeader(hdc, RightPaneX + 4, w - 8, bodyTop, "Forja");

            // ── Item icon grid (left) — same cell style as the inventory bag ──
            BuildGrid();
            IntersectClipRect(hdc, 4, GridTop, LeftPaneW - 6, GridBot);
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                if (c.y + c.h <= GridTop || c.y >= GridBot) continue;
                var row = _rows[c.index];
                NativeFrameImage.PixelBevel(hdc, c.x, c.y, c.w, c.h, _bOutline, _bBevLo, _bBevLo, c.index == _sel ? _bTagUse : _bSlot);
                if (!string.IsNullOrEmpty(row.iconPath))
                {
                    var img = IconLibrary.Gdi(row.iconPath);
                    if (img != null && img.Ready) img.BlitAspect(hdc, c.x + 3, c.y + 3, c.w - 6, c.h - 6);
                }
                else
                {
                    SelectObject(hdc, _fTag); SetTextColor(hdc, row.color);
                    var rc = new RECT { Left = c.x, Top = c.y, Right = c.x + c.w, Bottom = c.y + c.h };
                    DrawTextW(hdc, row.name, -1, ref rc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
                }
                DrawQualityBorder(hdc, c.x, c.y, c.w, c.h, row.rarity);
                DrawUpgradeBadge(hdc, c.x, c.y, c.w, row.level);   // yellow +N when enhanced
            }
            SelectClipRgn(hdc, IntPtr.Zero);
            if (_rows.Count == 0)
            {
                SelectObject(hdc, _fRow); SetTextColor(hdc, Bgr(0.6f, 0.6f, 0.6f));
                var erc = new RECT { Left = 8, Top = GridTop + 8, Right = LeftPaneW - 8, Bottom = GridTop + 44 };
                DrawTextW(hdc, "(sem equipamentos)", -1, ref erc, DT_CENTER | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);
            }

            // ── Forge scene (right) — animated art + fire spirits that ORBIT the forge while
            // enhancing (falls back to the old blocky drawing if the art PNGs are absent). ──
            int spx = RightPaneX + 4, spw = w - spx - 8;
            int sceneTop = bodyTop + SectionHeaderH + 4, sceneH = 128;
            var sceneRc = new RECT { Left = spx, Top = sceneTop, Right = spx + spw, Bottom = sceneTop + sceneH };
            FillRect(hdc, ref sceneRc, _bVoid);
            if (!DrawForgeSceneArt(hdc, spx, sceneTop, spw, sceneH))
                DrawForgeSceneFallback(hdc, spx, sceneTop, spw, sceneH);

            // ── Selected item info ──
            SelectObject(hdc, _fRow); SetTextColor(hdc, Rpg ? RpgUiNative.InkDark : Bgr(0.9f, 0.9f, 0.95f));
            var slotRc = new RECT { Left = spx, Top = sceneTop + sceneH + 4, Right = w - 8, Bottom = sceneTop + sceneH + 40 };
            string info = _sel >= 0 && _sel < _rows.Count
                ? $"{_rows[_sel].name}  +{_rows[_sel].level}\nSucesso: {Mathf.RoundToInt(Chance(_rows[_sel].level) * 100f)}%"
                : "Escolha um item";
            DrawTextW(hdc, info, -1, ref slotRc, DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);

            // ── APRIMORAR button (dark atlas button when skinned) ──
            ForgeBtn(out int bx, out int by, out int bw, out int bh);
            if (Rpg) RpgUiNative.DarkButton(hdc, bx, by, bw, bh, _animating);
            else NativeFrameImage.PixelBevel(hdc, bx, by, bw, bh, _bOutline, _bBevHi, _bBevLo, _bForge);
            SelectObject(hdc, _fTag); SetTextColor(hdc, Bgr(1f, 0.95f, 0.82f));
            var brc = new RECT { Left = bx, Top = by, Right = bx + bw, Bottom = by + bh };
            DrawTextW(hdc, _animating ? "FORJANDO..." : "APRIMORAR", -1, ref brc, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

            // Footer.
            SelectObject(hdc, _fHint); SetTextColor(hdc, Rpg ? RpgUiNative.InkOnDark : Bgr(0.66f, 0.62f, 0.5f));
            var frc = new RECT { Left = 8, Top = h - FooterH, Right = w - 8, Bottom = h - 4 };
            DrawTextW(hdc, "-8% sucesso e +10% status por nivel · falha quebra o item · ESC fecha", -1, ref frc, DT_CENTER | DT_BOTTOM | DT_SINGLELINE | DT_NOPREFIX);

            // ornate frame LAST so it rings the window over the content
            NativeFrameImage.DrawWindowFrame(hdc, 0, 0, w, h);

            // Hover tooltip (drawn last so it floats on top). The forge repaints every frame, so we
            // just read the cursor here instead of tracking WM_MOUSEMOVE.
            if (GetCursorPos(out var cur))
            {
                ScreenToClient(_hwnd, ref cur);
                int hi = CellAt(cur.X, cur.Y);
                if (hi >= 0 && hi < _rows.Count)
                    DrawTooltip(hdc, ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(_rows[hi].id) : null, cur.X, cur.Y, w, h);
            }
        }

        // Section header bar over a pane (parchment ink when skinned, else gold-on-divider).
        static void DrawPaneHeader(IntPtr hdc, int left, int right, int bodyTop, string text)
        {
            SelectObject(hdc, _fSection);
            if (Rpg)
            {
                SetTextColor(hdc, RpgUiNative.InkDark);
                var rcp = new RECT { Left = left + 2, Top = bodyTop + 1, Right = right, Bottom = bodyTop + SectionHeaderH };
                DrawTextW(hdc, text, -1, ref rcp, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                return;
            }
            var bar = new RECT { Left = left, Top = bodyTop + 3, Right = right, Bottom = bodyTop + SectionHeaderH };
            FillRect(hdc, ref bar, _bDivider);
            SetTextColor(hdc, Bgr(1f, 0.82f, 0.32f));
            var rc = new RECT { Left = left + 4, Top = bodyTop + 1, Right = right, Bottom = bodyTop + SectionHeaderH };
            DrawTextW(hdc, text, -1, ref rc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
        }

        // 2px frame in the item's quality colour around a cell (same as the inventory).
        static void DrawQualityBorder(IntPtr hdc, int x, int y, int w, int h, ItemRarity r)
        {
            var b = QBrushOf(r);
            if (b == IntPtr.Zero) return;
            var t  = new RECT { Left = x + 1, Top = y + 1,      Right = x + w - 1, Bottom = y + 3 };
            var bo = new RECT { Left = x + 1, Top = y + h - 3,  Right = x + w - 1, Bottom = y + h - 1 };
            var l  = new RECT { Left = x + 1, Top = y + 1,      Right = x + 3,     Bottom = y + h - 1 };
            var rr = new RECT { Left = x + w - 3, Top = y + 1,  Right = x + w - 1, Bottom = y + h - 1 };
            FillRect(hdc, ref t, b); FillRect(hdc, ref bo, b); FillRect(hdc, ref l, b); FillRect(hdc, ref rr, b);
        }

        // Yellow "+N" in the icon's top-right corner = times the item was enhanced.
        static void DrawUpgradeBadge(IntPtr hdc, int x, int y, int cellW, int lvl)
        {
            if (lvl <= 0) return;
            SelectObject(hdc, _fTag);
            string t = "+" + lvl;
            var sh = new RECT { Left = x + 2, Top = y + 2, Right = x + cellW - 1, Bottom = y + 16 };
            SetTextColor(hdc, Bgr(0f, 0f, 0f));   // shadow for contrast on bright icons
            DrawTextW(hdc, t, -1, ref sh, DT_RIGHT | DT_TOP | DT_SINGLELINE | DT_NOPREFIX);
            var rc = new RECT { Left = x + 1, Top = y + 1, Right = x + cellW - 2, Bottom = y + 15 };
            SetTextColor(hdc, Bgr(1f, 0.90f, 0.20f));   // yellow
            DrawTextW(hdc, t, -1, ref rc, DT_RIGHT | DT_TOP | DT_SINGLELINE | DT_NOPREFIX);
        }

        static string TypeLabel(ItemType t) => t switch
        {
            ItemType.Weapon => "Arma", ItemType.Helmet => "Capacete", ItemType.Chest => "Peito",
            ItemType.Gloves => "Maos", ItemType.Boots  => "Pes",      ItemType.Cape  => "Capa",
            ItemType.Consumable => "Consumivel", _ => "Item"
        };

        // Floating item card — the SAME tooltip the bag shows: icon + name (quality-tinted) +
        // type · quality, then one row per non-zero attribute, then the required-level line
        // (red when the player can't equip it yet, white when they can).
        static void DrawTooltip(IntPtr hdc, ItemData item, int mx, int my, int w, int h)
        {
            if (item == null) return;
            var lines = item.StatLines();
            bool showReq = item.requiredLevel > 1;
            int playerLevel = PlayerManager.Instance != null && PlayerManager.Instance.Data != null ? PlayerManager.Instance.Data.level : 1;
            const int pad = 8, iconBox = 40, headH = 46, lineH = 15;
            int tw = 182;
            int th = pad + headH + (lines.Count > 0 ? lines.Count * lineH + 6 : 0) + (showReq ? lineH + 4 : 0) + pad;
            int tx = Mathf.Clamp(mx + 16, 4, w - tw - 4);
            int ty = Mathf.Clamp(my + 16, HeaderH, h - th - 4);

            if (Rpg) RpgUiNative.DarkBoard(hdc, tx, ty, tw, th);
            else NativeFrameImage.PixelBevel(hdc, tx, ty, tw, th, _bOutline, _bBevHi, _bBevLo, _bVoid);

            int ix = tx + pad, iy = ty + pad;
            NativeFrameImage.PixelBevel(hdc, ix, iy, iconBox, iconBox, _bOutline, _bBevLo, _bBevLo, _bSlot);
            if (!string.IsNullOrEmpty(item.iconPath))
            {
                var img = IconLibrary.Gdi(item.iconPath);
                if (img != null && img.Ready) img.BlitAspect(hdc, ix + 3, iy + 3, iconBox - 6, iconBox - 6);
            }
            DrawQualityBorder(hdc, ix, iy, iconBox, iconBox, item.rarity);

            int tex = ix + iconBox + 8;
            SelectObject(hdc, _fSection);
            SetTextColor(hdc, BgrOf(ItemData.QualityColor(item.rarity)));
            var nameRc = new RECT { Left = tex, Top = ty + pad, Right = tx + tw - pad, Bottom = ty + pad + 28 };
            DrawTextW(hdc, item.itemName, -1, ref nameRc, DT_LEFT | DT_TOP | DT_WORDBREAK | DT_NOPREFIX);

            SelectObject(hdc, _fHint);
            SetTextColor(hdc, Bgr(0.72f, 0.70f, 0.60f));
            var typeRc = new RECT { Left = tex, Top = ty + pad + 28, Right = tx + tw - pad, Bottom = ty + pad + iconBox + 2 };
            DrawTextW(hdc, $"{TypeLabel(item.itemType)} · {ItemData.QualityLabel(item.rarity)}", -1, ref typeRc, DT_LEFT | DT_BOTTOM | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            if (lines.Count == 0 && !showReq) return;

            var div = new RECT { Left = tx + pad, Top = ty + pad + headH - 4, Right = tx + tw - pad, Bottom = ty + pad + headH - 3 };
            FillRect(hdc, ref div, _bDivider);

            SelectObject(hdc, _fRow);
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

            if (showReq)
            {
                if (lines.Count > 0) ry += 2;
                SelectObject(hdc, _fRow);
                SetTextColor(hdc, playerLevel < item.requiredLevel ? Bgr(1f, 0.28f, 0.24f) : Bgr(1f, 1f, 1f));
                var reqRc = new RECT { Left = tx + pad, Top = ry, Right = tx + tw - pad, Bottom = ry + lineH };
                DrawTextW(hdc, $"Requer Nível {item.requiredLevel}", -1, ref reqRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            }
        }

        // Quality-colour brushes (index 0..3 = Comum/Raro/Mito/Lendario) + accessor.
        static readonly int[] QRarity = { (int)ItemRarity.Common, (int)ItemRarity.Rare, (int)ItemRarity.Epic, (int)ItemRarity.Legendary };
        static int QIndex(ItemRarity r) => r switch { ItemRarity.Rare => 1, ItemRarity.Epic => 2, ItemRarity.Legendary => 3, _ => 0 };
        static IntPtr QBrushOf(ItemRarity r) => _qBrush[QIndex(r)];

        static RECT RectOf(int x, int y, int w, int h) => new RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };

        // ── Pixel-art forge scene ──────────────────────────────────────────────
        // Animated forge (fire flickering) with fire spirits floating around it. During an
        // enhance the spirits ORBIT the forge (angle driven by _animT); otherwise they hover
        // and bob near the flames. Spirits on the far arc are drawn BEHIND the forge, near
        // ones in FRONT, so they read as circling around it. Returns false if art is missing.
        static NativeFrameImage _forgeImg, _spiritImg;

        static bool DrawForgeSceneArt(IntPtr hdc, int sx, int sy, int sw, int sh)
        {
            var forge = _forgeImg ??= NativeFrameImage.Get("Forge/Forge");
            if (forge == null || !forge.Ready) return false;
            var spirit = _spiritImg ??= NativeFrameImage.Get("Forge/Spirit");

            // Forge: square art, centered, bottom-anchored in the scene box.
            int fh = Mathf.Min(sh - 4, 132);
            int fw = fh;
            int fx = sx + (sw - fw) / 2;
            int fy = sy + sh - fh;

            // Orbit centered on the forge fire (upper-middle of the art), elliptical since
            // the scene reads side-on.
            int cx = fx + fw / 2;
            int cy = fy + Mathf.RoundToInt(fh * 0.42f);
            float rx = fw * 0.52f, ry = fh * 0.30f;
            int spSize = Mathf.RoundToInt(fh * 0.34f);
            const int N = 3;
            float baseAng = _animating ? _animT * 6f : Time.unscaledTime * 1.4f;
            float bobAmt  = _animating ? 0f : 4f;   // idle spirits bob a little extra

            // pass 0 = back-arc spirits, pass 1 = forge then front-arc spirits.
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) BlitFrame(forge, hdc, fx, fy, fw, fh, 9f, 0);
                if (spirit == null || !spirit.Ready) continue;
                for (int i = 0; i < N; i++)
                {
                    float a = baseAng + i * (Mathf.PI * 2f / N);
                    float s = Mathf.Sin(a);
                    bool front = s >= 0f;
                    if ((pass == 0) == front) continue;   // pass0 draws back (s<0), pass1 front
                    int px = cx + Mathf.RoundToInt(Mathf.Cos(a) * rx) - spSize / 2;
                    int py = cy + Mathf.RoundToInt(s * ry) - spSize / 2
                             + Mathf.RoundToInt(bobAmt * Mathf.Sin(Time.unscaledTime * 3f + i));
                    BlitFrame(spirit, hdc, px, py, spSize, spSize, 8f, i * 3);
                }
            }
            return true;
        }

        // Blits one frame of a horizontal square-frame strip (frame size = image height),
        // cycling at `fps` with an optional per-instance frame offset.
        static void BlitFrame(NativeFrameImage img, IntPtr hdc, int dx, int dy, int dw, int dh, float fps, int frameOffset)
        {
            if (img == null || !img.Ready) return;
            int fs = img.Height <= 0 ? 1 : img.Height;
            int n  = Mathf.Max(1, img.Width / fs);
            int f  = ((int)(Time.unscaledTime * fps) + frameOffset) % n;
            img.BlitRegion(hdc, dx, dy, dw, dh, f * fs, 0, fs, fs);
        }

        // Original blocky forge scene — kept as a fallback when the art PNGs are absent.
        static void DrawForgeSceneFallback(IntPtr hdc, int sx, int sy, int sw, int sh)
        {
            var glow = new RECT { Left = sx + sw / 2 - 40, Top = sy + sh - 70, Right = sx + sw / 2 + 40, Bottom = sy + sh - 18 };
            FillRect(hdc, ref glow, _bForge);
            int ax = sx + sw / 2;
            FillRect(hdc, RectOf(ax - 26, sy + sh - 36, 52, 8), _bAnvil);
            FillRect(hdc, RectOf(ax - 8, sy + sh - 50, 16, 14), _bAnvil);
            FillRect(hdc, RectOf(ax - 16, sy + sh - 22, 32, 8), _bAnvilD);
            float d = _animating ? _animT * 16f : Time.unscaledTime * 3f;
            int fy = Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(d)) * 12f);
            int ey = Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(d * 1.2f)) * 12f);
            DrawElemental(hdc, sx + 16, sy + sh - 46 - fy, _bFire);
            DrawElemental(hdc, sx + sw - 40, sy + sh - 46 - ey, _bEarth);
        }

        static void DrawElemental(IntPtr hdc, int x, int y, IntPtr body)
        {
            FillRect(hdc, RectOf(x, y, 22, 22), body);                    // body
            FillRect(hdc, RectOf(x + 5,  y + 6, 3, 3), _bOutline);        // eyes
            FillRect(hdc, RectOf(x + 13, y + 6, 3, 3), _bOutline);
            FillRect(hdc, RectOf(x + 6,  y + 14, 10, 2), _bOutline);      // smile
        }

        // ── GDI resources ─────────────────────────────────────────────────────
        static IntPtr _bPanel, _bOutline, _bBevHi, _bBevLo, _bTag, _bTagUse, _bDivider, _bRowA, _bRowB, _bVoid, _bForge, _bAnvil, _bAnvilD, _bFire, _bEarth, _bSlot;
        static readonly IntPtr[] _qBrush = new IntPtr[4];
        static IntPtr _fTitle, _fRow, _fHint, _fTag, _fSection;
        const string ClassName = "ZulfarakForgePopup";

        static void EnsureGdi()
        {
            if (_bPanel != IntPtr.Zero) return;
            _bPanel   = CreateSolidBrush(Bgr(0.06f, 0.05f, 0.05f));
            _bOutline = CreateSolidBrush(Bgr(0f, 0f, 0f));
            _bBevHi   = CreateSolidBrush(Bgr(0.42f, 0.42f, 0.46f));
            _bBevLo   = CreateSolidBrush(Bgr(0.15f, 0.15f, 0.17f));
            _bTag     = CreateSolidBrush(Bgr(0.32f, 0.11f, 0.10f));
            _bTagUse  = CreateSolidBrush(Bgr(0.16f, 0.32f, 0.12f));
            _bDivider = CreateSolidBrush(Bgr(0.16f, 0.16f, 0.18f));
            _bRowA    = CreateSolidBrush(Bgr(0.10f, 0.08f, 0.08f));
            _bRowB    = CreateSolidBrush(Bgr(0.14f, 0.11f, 0.10f));
            _bVoid    = CreateSolidBrush(Bgr(0.03f, 0.03f, 0.04f));
            _bForge   = CreateSolidBrush(Bgr(0.85f, 0.38f, 0.10f));
            _bAnvil   = CreateSolidBrush(Bgr(0.44f, 0.46f, 0.50f));
            _bAnvilD  = CreateSolidBrush(Bgr(0.24f, 0.25f, 0.28f));
            _bFire    = CreateSolidBrush(Bgr(1f, 0.50f, 0.14f));
            _bEarth   = CreateSolidBrush(Bgr(0.42f, 0.72f, 0.30f));
            _bSlot    = CreateSolidBrush(Bgr(0.09f, 0.08f, 0.10f));
            for (int i = 0; i < 4; i++) _qBrush[i] = CreateSolidBrush(BgrOf(ItemData.QualityColor((ItemRarity)QRarity[i])));
            _fTitle   = MakeFont(15, FW_BOLD); _fRow = MakeFont(12, FW_NORMAL); _fHint = MakeFont(10, FW_NORMAL);
            _fTag     = MakeFont(12, FW_BOLD); _fSection = MakeFont(12, FW_BOLD);
        }

        static bool _classReg; static WndProcDelegate _proc;
        static void EnsureClass()
        {
            if (_classReg) return;
            _proc = WndProc;
            var wc = new WNDCLASSEX { cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)), style = CS_OWNDC,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc), hInstance = GetModuleHandleW(null),
                hCursor = LoadCursorW(IntPtr.Zero, (IntPtr)IDC_ARROW), lpszClassName = ClassName };
            RegisterClassExW(ref wc); _classReg = true;
        }

        static IntPtr MakeFont(int px, int weight)
        {
            var lf = new LOGFONT { lfHeight = -px, lfWeight = weight, lfCharSet = 1, lfQuality = 5, lfFaceName = NativeFont.Face };
            return CreateFontIndirectW(ref lf);
        }
        static uint Bgr(float r, float g, float b)
        { byte R = (byte)(Mathf.Clamp01(r) * 255), G = (byte)(Mathf.Clamp01(g) * 255), B = (byte)(Mathf.Clamp01(b) * 255); return (uint)(R | (G << 8) | (B << 16)); }
        static uint BgrOf(Color c) => Bgr(c.r, c.g, c.b);

        public static void Reposition()
        {
            if (_hwnd == IntPtr.Zero) return;
            int newW = CurrentWidth(); bool sz = newW != _popupWidth; _popupWidth = newW;
            (int x, int y) = ComputePosition();
            SetWindowPos(_hwnd, HWND_TOPMOST, x, y, sz ? _popupWidth : 0, sz ? PopupHeight : 0,
                (sz ? 0u : SWP_NOSIZE) | SWP_SHOWWINDOW | SWP_NOACTIVATE);
            if (sz) InvalidateRect(_hwnd, IntPtr.Zero, true);
        }

        static (int, int) ComputePosition()
        {
            int gx = OverlayWindow.WinX, gy = OverlayWindow.WinY, gw = CurrentWidth();
            int x = gx + (gw - PopupWidth) / 2, y = gy - PopupHeight;
            int sw = Screen.currentResolution.width, sh = Screen.currentResolution.height;
            x = Mathf.Clamp(x, 0, Mathf.Max(0, sw - PopupWidth)); y = Mathf.Clamp(y, 0, Mathf.Max(0, sh - PopupHeight));
            return (x, y);
        }

        internal static void Pump()
        {
            if (_hwnd == IntPtr.Zero) return;
            while (PeekMessageW(out var msg, _hwnd, 0, 0, PM_REMOVE) != 0) { TranslateMessage(ref msg); DispatchMessageW(ref msg); }
        }

        // ── Win32 plumbing (mirrors FriendsListPopup) ─────────────────────────
        static IntPtr _hwnd = IntPtr.Zero;
        delegate IntPtr WndProcDelegate(IntPtr h, uint m, IntPtr w, IntPtr l);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASSEX { public uint cbSize, style; public IntPtr lpfnWndProc; public int cbClsExtra, cbWndExtra;
            public IntPtr hInstance, hIcon, hCursor, hbrBackground; [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName; public IntPtr hIconSm; }
        [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int pt_x, pt_y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct PAINTSTRUCT { public IntPtr hdc; public bool fErase; public RECT rcPaint; public bool fRestore, fIncUpdate; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct LOGFONT { public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight; public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName; }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const int WM_PAINT = 0x000F, WM_KEYDOWN = 0x0100, WM_LBUTTONDOWN = 0x0201, WM_MOUSEWHEEL = 0x020A, WM_DESTROY = 0x0002, WM_CLOSE = 0x0010, WM_ERASEBKGND = 0x0014;
        const int WS_POPUP = unchecked((int)0x80000000), WS_VISIBLE = 0x10000000, WS_EX_TOPMOST = 0x00000008, WS_EX_TOOLWINDOW = 0x00000080, SW_SHOW = 5;
        const uint PM_REMOVE = 0x0001; const int TRANSPARENT = 1, VK_ESCAPE = 0x1B;
        const uint DT_TOP = 0, DT_LEFT = 0, DT_RIGHT = 2, DT_CENTER = 1, DT_VCENTER = 4, DT_BOTTOM = 8, DT_WORDBREAK = 0x10, DT_SINGLELINE = 0x20, DT_NOPREFIX = 0x800, DT_END_ELLIPSIS = 0x8000;
        const uint CS_OWNDC = 0x0020; const int IDC_ARROW = 32512, FW_NORMAL = 400, FW_BOLD = 700;
        const uint SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
        const uint SRCCOPY = 0x00CC0020;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")] static extern ushort RegisterClassExW(ref WNDCLASSEX c);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
        static extern IntPtr CreateWindowExW(int ex, string cls, string name, int style, int x, int y, int w, int h, IntPtr par, IntPtr menu, IntPtr inst, IntPtr p);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
        [DllImport("user32.dll")] static extern bool InvalidateRect(IntPtr h, IntPtr r, bool erase);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr ins, int x, int y, int w, int hgt, uint flags);
        [DllImport("user32.dll", EntryPoint = "DefWindowProcW")] static extern IntPtr DefWindowProcW(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll", EntryPoint = "PeekMessageW")] static extern int PeekMessageW(out MSG m, IntPtr h, uint a, uint b, uint c);
        [DllImport("user32.dll")] static extern int TranslateMessage(ref MSG m);
        [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] static extern IntPtr DispatchMessageW(ref MSG m);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr h, out PAINTSTRUCT ps);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr h, ref PAINTSTRUCT ps);
        [DllImport("user32.dll")] static extern int FillRect(IntPtr dc, ref RECT r, IntPtr br);
        static int FillRect(IntPtr dc, RECT r, IntPtr br) => FillRect(dc, ref r, br);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DrawTextW")] static extern int DrawTextW(IntPtr dc, string s, int n, ref RECT r, uint f);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT p);
        [DllImport("user32.dll", EntryPoint = "LoadCursorW")] static extern IntPtr LoadCursorW(IntPtr i, IntPtr n);
        [DllImport("gdi32.dll")] static extern IntPtr CreateSolidBrush(uint c);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] static extern int SetTextColor(IntPtr dc, uint c);
        [DllImport("gdi32.dll")] static extern int SetBkMode(IntPtr dc, int m);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateFontIndirectW")] static extern IntPtr CreateFontIndirectW(ref LOGFONT lf);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")] static extern IntPtr GetModuleHandleW(string n);
        // Double-buffer + icon-grid clipping (mirrors the inventory popup).
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
        [DllImport("gdi32.dll")] static extern int IntersectClipRect(IntPtr hdc, int l, int t, int r, int b);
        [DllImport("gdi32.dll")] static extern int SelectClipRgn(IntPtr hdc, IntPtr rgn);
    }

    // Pumps the forge window + ticks its dance animation on the Unity main thread.
    class ForgePopupPump : MonoBehaviour
    {
        static ForgePopupPump _i;
        internal static void Ensure() { if (_i != null) return; var go = new GameObject("ForgePopupPump"); DontDestroyOnLoad(go); _i = go.AddComponent<ForgePopupPump>(); }
        void Update()
        {
            ForgePopupWindow.Pump();
            ForgePopupWindow.Tick(Time.unscaledDeltaTime);
            if (ForgePopupWindow.IsOpen && Input.GetKeyDown(KeyCode.Escape)) ForgePopupWindow.Hide();
        }
    }
}

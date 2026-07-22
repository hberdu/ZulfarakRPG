using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZulfarakRPG
{
    // Loads a pixel-art PNG from Resources and paints it — with per-pixel alpha —
    // as the background of a native Win32 popup, stretched to the window rect.
    //
    // Used to skin the fixed menus (Mestre de Classe / Ferreiro / Inventário) with
    // the Red-Eyes-Black-Dragon frame. If the PNG is missing or unreadable, Get()
    // returns an instance whose Ready is false and Blit() is a no-op, so the menus
    // simply keep their solid-fill look — no regression.
    //
    // The source Texture2D MUST be import-readable (Read/Write Enabled) so GetPixels32
    // works at runtime; the generated .meta sets isReadable: 1.
    public sealed class NativeFrameImage
    {
        // ── Cache: one DIB per resource path for the app lifetime ────────────────
        static readonly Dictionary<string, NativeFrameImage> _cache = new();

        static NativeFrameImage _empty;
        public static NativeFrameImage Get(string resourcePath)
        {
            // The Red-Eyes-Black-Dragon emblem was removed from every popup — never load it, so the
            // header emblem box stays empty (dragon.Ready == false at every call site).
            if (resourcePath != null && resourcePath.Contains("Dragon"))
                return _empty ??= new NativeFrameImage();
            if (_cache.TryGetValue(resourcePath, out var img)) return img;
            img = new NativeFrameImage();
            img.Load(resourcePath);
            _cache[resourcePath] = img;
            return img;
        }

        // Cache a DIB built from an arbitrary Texture2D under `cacheKey`. The provider is
        // only invoked on the first request (or after a failed load). Lets the inventory /
        // skill windows blit pack icons + the live hero sprite loaded at runtime from disk.
        public static NativeFrameImage GetTexture(string cacheKey, Func<Texture2D> provider)
        {
            if (_cache.TryGetValue(cacheKey, out var img) && img.Ready) return img;
            img = new NativeFrameImage();
            var tex = provider != null ? provider() : null;
            if (tex != null) img.BuildFromTexture(tex);
            _cache[cacheKey] = img;
            return img;
        }

        IntPtr _hbitmap = IntPtr.Zero;
        int    _w, _h;

        public bool Ready => _hbitmap != IntPtr.Zero;

        void Load(string resourcePath)
        {
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) { Debug.Log($"[NativeFrameImage] '{resourcePath}' not found — menus keep solid fill."); return; }
            BuildFromTexture(tex);
        }

        void BuildFromTexture(Texture2D tex)
        {
            if (tex == null) return;
            Color32[] px;
            try { px = tex.GetPixels32(); }
            catch (Exception e) { Debug.LogWarning($"[NativeFrameImage] texture not readable ({e.Message})."); return; }
            BuildFromPixels(px, tex.width, tex.height);
        }

        // Builds the premultiplied BGRA DIB from bottom→top Color32 rows (GetPixels32 order).
        void BuildFromPixels(Color32[] px, int w, int h)
        {
            _w = w; _h = h;
            if (_w <= 0 || _h <= 0 || px == null || px.Length < _w * _h) { _w = _h = 0; return; }

            // Build a top-down 32bpp premultiplied BGRA DIB section for AlphaBlend.
            var bmi = new BITMAPINFOHEADER
            {
                biSize        = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth       = _w,
                biHeight      = -_h,   // negative → top-down rows
                biPlanes      = 1,
                biBitCount    = 32,
                biCompression = 0,     // BI_RGB
            };

            _hbitmap = CreateDIBSection(IntPtr.Zero, ref bmi, 0 /*DIB_RGB_COLORS*/, out IntPtr bits, IntPtr.Zero, 0);
            if (_hbitmap == IntPtr.Zero || bits == IntPtr.Zero) { _hbitmap = IntPtr.Zero; return; }

            var buf = new byte[_w * _h * 4];
            for (int y = 0; y < _h; y++)
            {
                int srcRow = (_h - 1 - y) * _w;   // GetPixels32 rows run bottom→top
                int dstRow = y * _w * 4;
                for (int x = 0; x < _w; x++)
                {
                    Color32 c = px[srcRow + x];
                    int a = c.a;
                    // Premultiply RGB by alpha — required for AC_SRC_ALPHA AlphaBlend.
                    buf[dstRow + x * 4 + 0] = (byte)(c.b * a / 255);
                    buf[dstRow + x * 4 + 1] = (byte)(c.g * a / 255);
                    buf[dstRow + x * 4 + 2] = (byte)(c.r * a / 255);
                    buf[dstRow + x * 4 + 3] = (byte)a;
                }
            }
            Marshal.Copy(buf, 0, bits, buf.Length);
        }

        // Blit the frame stretched over the destination rect with per-pixel alpha.
        public void Blit(IntPtr hdc, int dstX, int dstY, int dstW, int dstH)
        {
            if (_hbitmap == IntPtr.Zero) return;
            IntPtr mem = CreateCompatibleDC(hdc);
            if (mem == IntPtr.Zero) return;
            IntPtr old = SelectObject(mem, _hbitmap);
            var bf = new BLENDFUNCTION
            {
                BlendOp             = AC_SRC_OVER,
                BlendFlags          = 0,
                SourceConstantAlpha = 255,
                AlphaFormat         = AC_SRC_ALPHA,
            };
            AlphaBlend(hdc, dstX, dstY, dstW, dstH, mem, 0, 0, _w, _h, bf);
            SelectObject(mem, old);
            DeleteDC(mem);
        }

        // Native size accessors so callers can preserve the source aspect ratio when
        // blitting the dragon as a compact emblem instead of stretching it to the window.
        public int Width  => _w;
        public int Height => _h;

        // Blit preserving aspect ratio, centered inside (dstX,dstY,dstW,dstH). Used to
        // paint the dragon as a compact emblem so the wings don't get squashed sideways.
        public void BlitAspect(IntPtr hdc, int dstX, int dstY, int dstW, int dstH)
        {
            if (_hbitmap == IntPtr.Zero || _w <= 0 || _h <= 0 || dstW <= 0 || dstH <= 0) return;
            float srcAspect = (float)_w / _h;
            float dstAspect = (float)dstW / dstH;
            int fw, fh;
            if (srcAspect > dstAspect) { fw = dstW; fh = Mathf.RoundToInt(dstW / srcAspect); }
            else                       { fh = dstH; fw = Mathf.RoundToInt(dstH * srcAspect); }
            int fx = dstX + (dstW - fw) / 2;
            int fy = dstY + (dstH - fh) / 2;
            Blit(hdc, fx, fy, fw, fh);
        }

        // Blit an arbitrary sub-rect of this DIB (atlas slice) into the destination rect,
        // stretched, with per-pixel alpha. Source coords are top-left origin (the DIB is
        // built top-down), matching how the RPG UI atlas was sampled.
        public void BlitRegion(IntPtr hdc, int dstX, int dstY, int dstW, int dstH,
                               int srcX, int srcY, int srcW, int srcH)
        {
            if (_hbitmap == IntPtr.Zero || dstW <= 0 || dstH <= 0 || srcW <= 0 || srcH <= 0) return;
            IntPtr mem = CreateCompatibleDC(hdc);
            if (mem == IntPtr.Zero) return;
            IntPtr old = SelectObject(mem, _hbitmap);
            var bf = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA,
            };
            AlphaBlend(hdc, dstX, dstY, dstW, dstH, mem, srcX, srcY, srcW, srcH, bf);
            SelectObject(mem, old);
            DeleteDC(mem);
        }

        // Tile this DIB at native pixel size across the destination rect — background texture
        // fill for the themed windows. Single DC for the whole loop (a paint may tile ~90 cells).
        public void BlitTiled(IntPtr hdc, int dstX, int dstY, int dstW, int dstH)
        {
            if (_hbitmap == IntPtr.Zero || _w <= 0 || _h <= 0 || dstW <= 0 || dstH <= 0) return;
            IntPtr mem = CreateCompatibleDC(hdc);
            if (mem == IntPtr.Zero) return;
            IntPtr old = SelectObject(mem, _hbitmap);
            var bf = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA,
            };
            for (int y = 0; y < dstH; y += _h)
            {
                int th = Math.Min(_h, dstH - y);
                for (int x = 0; x < dstW; x += _w)
                {
                    int tw = Math.Min(_w, dstW - x);
                    AlphaBlend(hdc, dstX + x, dstY + y, tw, th, mem, 0, 0, tw, th, bf);
                }
            }
            SelectObject(mem, old);
            DeleteDC(mem);
        }

        // One-call pixel-art window theme shared by every native popup: tiled dark texture
        // (UI/PanelTex) + gothic 9-slice frame (UI/PanelFrame, fine-grained hi-res art whose
        // border is a QUARTER of the source size, blitted 1:1). Returns false when neither PNG
        // is present so callers fall back to the procedural bevel — no regression.
        public static bool DrawWindowTheme(IntPtr hdc, int x, int y, int w, int h)
        {
            var tex   = Get("UI/PanelTex");
            var frame = Get("UI/PanelFrame");
            if (!tex.Ready && !frame.Ready) return false;
            if (tex.Ready)   tex.BlitTiled(hdc, x, y, w, h);
            if (frame.Ready)
            {
                int b = Mathf.Max(8, frame.Width / 4);   // 128px art → 32px border at 1:1
                frame.BlitNineSlice(hdc, x, y, w, h, 0, 0, frame.Width, frame.Height, b, b, b, b);
            }
            return true;
        }

        // 9-slice blit: keeps the (bl,bt,br,bb) borders at native size while stretching the
        // edges and centre — so an atlas panel/button scales to any window rect without the
        // corners distorting. Source rect is (srcX,srcY,srcW,srcH); borders are in source px.
        public void BlitNineSlice(IntPtr hdc, int dstX, int dstY, int dstW, int dstH,
                                  int srcX, int srcY, int srcW, int srcH,
                                  int bl, int bt, int br, int bb)
        {
            if (_hbitmap == IntPtr.Zero || dstW <= 0 || dstH <= 0 || srcW <= 0 || srcH <= 0) return;
            // Clamp borders so opposite pairs never exceed the source/dest extent.
            bl = Mathf.Clamp(bl, 0, srcW); br = Mathf.Clamp(br, 0, srcW - bl);
            bt = Mathf.Clamp(bt, 0, srcH); bb = Mathf.Clamp(bb, 0, srcH - bt);
            int dl = Mathf.Min(bl, dstW), dr = Mathf.Min(br, dstW - dl);
            int dtp = Mathf.Min(bt, dstH), dbt = Mathf.Min(bb, dstH - dtp);

            int smx = srcW - bl - br;   // source middle width
            int smy = srcH - bt - bb;   // source middle height
            int dmx = dstW - dl - dr;   // dest   middle width
            int dmy = dstH - dtp - dbt; // dest   middle height

            // Rows: top, middle, bottom. Cols: left, middle, right.
            // Top row
            if (dtp > 0)
            {
                if (dl  > 0) BlitRegion(hdc, dstX,           dstY, dl,  dtp, srcX,           srcY, bl,  bt);
                if (dmx > 0) BlitRegion(hdc, dstX + dl,      dstY, dmx, dtp, srcX + bl,      srcY, smx, bt);
                if (dr  > 0) BlitRegion(hdc, dstX + dl + dmx, dstY, dr,  dtp, srcX + bl + smx, srcY, br,  bt);
            }
            // Middle row
            if (dmy > 0)
            {
                int yy = dstY + dtp, sy = srcY + bt;
                if (dl  > 0) BlitRegion(hdc, dstX,           yy, dl,  dmy, srcX,           sy, bl,  smy);
                if (dmx > 0) BlitRegion(hdc, dstX + dl,      yy, dmx, dmy, srcX + bl,      sy, smx, smy);
                if (dr  > 0) BlitRegion(hdc, dstX + dl + dmx, yy, dr,  dmy, srcX + bl + smx, sy, br,  smy);
            }
            // Bottom row
            if (dbt > 0)
            {
                int yy = dstY + dtp + dmy, sy = srcY + bt + smy;
                if (dl  > 0) BlitRegion(hdc, dstX,           yy, dl,  dbt, srcX,           sy, bl,  bb);
                if (dmx > 0) BlitRegion(hdc, dstX + dl,      yy, dmx, dbt, srcX + bl,      sy, smx, bb);
                if (dr  > 0) BlitRegion(hdc, dstX + dl + dmx, yy, dr,  dbt, srcX + bl + smx, sy, br,  bb);
            }
        }

        // Chunky pixel-art bevel painted with plain solid FillRect calls (no alpha), so it
        // reads as blocky pixel art at any window size. Colours arranged in the classic
        // beveled-button pattern (bright top-left / dark bottom-right) with a black outline.
        //   outer  = 1px pitch-black outline
        //   hi/lo  = 2px gold bevels (hi = light gold top-left, lo = dark gold bottom-right)
        //   panel  = inner near-black fill (optional; skip by passing brushPanel == IntPtr.Zero)
        public static void PixelBevel(IntPtr hdc, int x, int y, int w, int h,
                                      IntPtr brushOutline, IntPtr brushHi, IntPtr brushLo,
                                      IntPtr brushPanel)
        {
            if (w <= 6 || h <= 6) return;
            // Outer 1px black outline
            var outer = new RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
            NativeFillRect(hdc, ref outer, brushOutline);
            // 2px bevel: top + left = highlight; right + bottom = shadow
            var top  = new RECT { Left = x + 1, Top = y + 1, Right = x + w - 1, Bottom = y + 3 };
            var left = new RECT { Left = x + 1, Top = y + 1, Right = x + 3,     Bottom = y + h - 1 };
            NativeFillRect(hdc, ref top,  brushHi);
            NativeFillRect(hdc, ref left, brushHi);
            var bot  = new RECT { Left = x + 1,     Top = y + h - 3, Right = x + w - 1, Bottom = y + h - 1 };
            var rght = new RECT { Left = x + w - 3, Top = y + 1,     Right = x + w - 1, Bottom = y + h - 1 };
            NativeFillRect(hdc, ref bot,  brushLo);
            NativeFillRect(hdc, ref rght, brushLo);
            // Inner panel fill (optional)
            if (brushPanel != IntPtr.Zero)
            {
                var panel = new RECT { Left = x + 3, Top = y + 3, Right = x + w - 3, Bottom = y + h - 3 };
                NativeFillRect(hdc, ref panel, brushPanel);
            }
        }

        // Four little 3×3 "gem" studs painted at the corners of a bevel — classic
        // pixel-art border decoration that reads as bolts on tarnished metal.
        public static void PixelCornerStuds(IntPtr hdc, int x, int y, int w, int h,
                                            IntPtr brush, int inset = 4, int size = 3)
        {
            return;   // red corner "gem" studs removed from every popup/modal per request
#pragma warning disable CS0162
            if (w < inset * 2 + size || h < inset * 2 + size) return;
            var tl = new RECT { Left = x + inset,         Top = y + inset,         Right = x + inset + size,         Bottom = y + inset + size };
            var tr = new RECT { Left = x + w - inset - size, Top = y + inset,      Right = x + w - inset,            Bottom = y + inset + size };
            var bl = new RECT { Left = x + inset,         Top = y + h - inset - size, Right = x + inset + size,      Bottom = y + h - inset };
            var br = new RECT { Left = x + w - inset - size, Top = y + h - inset - size, Right = x + w - inset,      Bottom = y + h - inset };
            NativeFillRect(hdc, ref tl, brush);
            NativeFillRect(hdc, ref tr, brush);
            NativeFillRect(hdc, ref bl, brush);
            NativeFillRect(hdc, ref br, brush);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll", EntryPoint = "FillRect")]
        static extern int NativeFillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

        // Blend a translucent solid colour over a rect — a poor-man's alpha FillRect for
        // GDI, used to draw the dark cards as semi-transparent panels so the dragon behind
        // them (and its glowing red eyes) still reads through. alpha 0=invisible, 255=opaque.
        public static void DimFill(IntPtr hdc, int x, int y, int w, int h, byte r, byte g, byte b, byte alpha)
        {
            if (w <= 0 || h <= 0) return;
            var bmi = new BITMAPINFOHEADER
            {
                biSize     = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth    = 1,
                biHeight   = -1,
                biPlanes   = 1,
                biBitCount = 32,
            };
            IntPtr hbmp = CreateDIBSection(IntPtr.Zero, ref bmi, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (hbmp == IntPtr.Zero || bits == IntPtr.Zero) { if (hbmp != IntPtr.Zero) DeleteObject(hbmp); return; }

            // Single premultiplied BGRA texel, stretched over the rect by AlphaBlend.
            var texel = new byte[4] { (byte)(b * alpha / 255), (byte)(g * alpha / 255), (byte)(r * alpha / 255), alpha };
            Marshal.Copy(texel, 0, bits, 4);

            IntPtr mem = CreateCompatibleDC(hdc);
            IntPtr old = SelectObject(mem, hbmp);
            var bf = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
            AlphaBlend(hdc, x, y, w, h, mem, 0, 0, 1, 1, bf);
            SelectObject(mem, old);
            DeleteDC(mem);
            DeleteObject(hbmp);
        }

        // ── WinAPI ───────────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint  biSize;
            public int   biWidth;
            public int   biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint  biCompression;
            public uint  biSizeImage;
            public int   biXPelsPerMeter;
            public int   biYPelsPerMeter;
            public uint  biClrUsed;
            public uint  biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        const byte AC_SRC_OVER  = 0x00;
        const byte AC_SRC_ALPHA = 0x01;

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern bool   DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool   DeleteObject(IntPtr h);

        [DllImport("msimg32.dll")]
        static extern bool AlphaBlend(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
                                      IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, BLENDFUNCTION bf);
    }
}

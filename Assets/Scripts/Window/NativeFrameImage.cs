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
    internal sealed class NativeFrameImage
    {
        // ── Cache: one DIB per resource path for the app lifetime ────────────────
        static readonly Dictionary<string, NativeFrameImage> _cache = new();

        public static NativeFrameImage Get(string resourcePath)
        {
            if (_cache.TryGetValue(resourcePath, out var img)) return img;
            img = new NativeFrameImage();
            img.Load(resourcePath);
            _cache[resourcePath] = img;
            return img;
        }

        IntPtr _hbitmap = IntPtr.Zero;
        int    _w, _h;

        public bool Ready => _hbitmap != IntPtr.Zero;

        void Load(string resourcePath)
        {
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) { Debug.Log($"[NativeFrameImage] '{resourcePath}' not found — menus keep solid fill."); return; }

            Color32[] px;
            try { px = tex.GetPixels32(); }
            catch (Exception e) { Debug.LogWarning($"[NativeFrameImage] '{resourcePath}' not readable ({e.Message}). Enable Read/Write on the texture."); return; }

            _w = tex.width;
            _h = tex.height;
            if (_w <= 0 || _h <= 0) return;

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

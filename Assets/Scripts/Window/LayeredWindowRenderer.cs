#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace ZulfarakRPG
{
    // TRUE per-pixel transparent overlay for Unity 6 / URP.
    //
    // Unity's DXGI swapchain clobbers the framebuffer alpha on present, so neither the DWM
    // "sheet of glass" nor the layered COLOR-KEY makes the window see-through (the magenta
    // clear stayed solid pink). This component sidesteps the swapchain entirely: at the end of
    // every frame it reads back the fully-composited image (camera + on-screen HUD), turns the
    // magenta clear colour into full transparency on the CPU (so it never depends on the
    // unreliable alpha channel), and pushes the result straight to the window with
    // UpdateLayeredWindow — the OS then composites it over the live desktop with per-pixel
    // alpha, and clicks on the transparent areas fall through to the desktop.
    //
    // The overlay strip is tiny (~480×120), so the per-frame read-back is cheap.
    public class LayeredWindowRenderer : MonoBehaviour
    {
        // Camera clear colour is magenta (255,0,255); Present() below soft-keys it per pixel,
        // recovering partial coverage so blended edges don't stay pink.
        IntPtr _hwnd, _screenDC, _memDC, _dib, _oldObj, _bits;
        Texture2D _tex;
        byte[] _out;
        int _w, _h;

        public static void Ensure()
        {
            if (FindAnyObjectByType<LayeredWindowRenderer>() != null) return;
            var go = new GameObject("LayeredWindowRenderer");
            DontDestroyOnLoad(go);
            go.AddComponent<LayeredWindowRenderer>();
        }

        void OnEnable()  { StartCoroutine(CaptureLoop()); }
        void OnDisable() { StopAllCoroutines(); Free(); }

        IEnumerator CaptureLoop()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                try { Present(); }
                catch (Exception e) { Debug.LogWarning("[LayeredWindow] " + e.Message); }
            }
        }

        void Present()
        {
            if (_hwnd == IntPtr.Zero)
                _hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (_hwnd == IntPtr.Zero) return;

            int w = Screen.width, h = Screen.height;
            if (w <= 0 || h <= 0) return;
            if (_tex == null || _w != w || _h != h) Realloc(w, h);

            // Grab the final composited backbuffer (camera + ScreenSpaceOverlay UI).
            _tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            var src = _tex.GetRawTextureData<byte>();   // RGBA, bottom-up, no allocation

            // Build a top-down, premultiplied BGRA image; magenta → fully transparent.
            for (int y = 0; y < h; y++)
            {
                int srcRow = (h - 1 - y) * w * 4;   // flip vertically (GL bottom-up → GDI top-down)
                int dstRow = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int s = srcRow + x * 4;
                    int r = src[s], g = src[s + 1], b = src[s + 2];
                    int d = dstRow + x * 4;

                    // SOFT magenta key. A hard "is it magenta?" test leaves every PARTIALLY blended
                    // pixel pink — anti-aliased sprite/text edges and semi-transparent overlays
                    // (damage backdrops, health bars, button outlines) are a mix of the art over the
                    // (255,0,255) clear, so they never match and keep a pink tint.
                    // The clear has max R and B and zero G, so the magenta contribution left in a
                    // pixel is (min(R,B) - G): 255 on pure clear, <=0 on fully opaque art.
                    int mag = r < b ? r : b;
                    mag -= g;
                    if (mag < 0) mag = 0; else if (mag > 255) mag = 255;

                    // Remove that contribution → premultiplied colour (what AC_SRC_ALPHA wants),
                    // and what's left of full coverage is the per-pixel alpha.
                    int pr = r - mag; if (pr < 0) pr = 0;
                    int pb = b - mag; if (pb < 0) pb = 0;
                    _out[d]     = (byte)pb;          // B
                    _out[d + 1] = (byte)g;           // G (the clear contributes none)
                    _out[d + 2] = (byte)pr;          // R
                    _out[d + 3] = (byte)(255 - mag); // A
                }
            }
            Marshal.Copy(_out, 0, _bits, _out.Length);

            var size  = new SIZE  { cx = w, cy = h };
            var ptSrc = new POINT { X = 0, Y = 0 };
            var ptDst = new POINT { X = OverlayWindow.WinX, Y = OverlayWindow.WinY };
            var blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER, BlendFlags = 0,
                SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA
            };
            UpdateLayeredWindow(_hwnd, _screenDC, ref ptDst, ref size, _memDC, ref ptSrc, 0, ref blend, ULW_ALPHA);
        }

        void Realloc(int w, int h)
        {
            Free();
            _w = w; _h = h;
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _out = new byte[w * h * 4];

            _screenDC = GetDC(IntPtr.Zero);
            _memDC    = CreateCompatibleDC(_screenDC);
            var bmi = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth = w, biHeight = -h,      // negative → top-down
                biPlanes = 1, biBitCount = 32, biCompression = 0
            };
            _dib    = CreateDIBSection(_memDC, ref bmi, 0, out _bits, IntPtr.Zero, 0);
            _oldObj = SelectObject(_memDC, _dib);
        }

        void Free()
        {
            if (_memDC != IntPtr.Zero && _oldObj != IntPtr.Zero) SelectObject(_memDC, _oldObj);
            if (_dib != IntPtr.Zero)      DeleteObject(_dib);
            if (_memDC != IntPtr.Zero)    DeleteDC(_memDC);
            if (_screenDC != IntPtr.Zero) ReleaseDC(IntPtr.Zero, _screenDC);
            _memDC = _screenDC = _dib = _oldObj = _bits = IntPtr.Zero;
            if (_tex != null) Destroy(_tex);
            _tex = null;
        }

        // ── Win32 ────────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct SIZE  { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth, biHeight;
            public ushort biPlanes, biBitCount; public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
        }

        const byte AC_SRC_OVER = 0x00, AC_SRC_ALPHA = 0x01;
        const uint ULW_ALPHA = 0x00000002;

        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]  static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]  static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]  static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")]  static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll")]  static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi,
            uint usage, out IntPtr bits, IntPtr hSection, uint offset);
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey,
            ref BLENDFUNCTION pblend, uint dwFlags);
    }
}
#endif

using System;
using UnityEngine;

namespace ZulfarakRPG
{
    // Native-GDI bridge to the "Pixel RPG UI Pack" atlas (Resources/rpg_ui.png), so the
    // native Win32 popups (Ferreiro / Mestre / Inventário) can be skinned with the SAME art
    // the Unity HUD uses (see RpgUiSprites) instead of procedural pixel bevels.
    //
    // The whole atlas is loaded once as a premultiplied DIB (NativeFrameImage) and each
    // helper paints one 9-sliced piece into a destination rect. Coordinates are top-left
    // origin, copied from RpgUiSprites (sampled off the 984×640 sheet), so the two renderers
    // stay in sync. If the atlas can't load (Ready == false) callers fall back to their old
    // bevel look, so nothing ever breaks.
    public static class RpgUiNative
    {
        const string AtlasRes = "rpg_ui";

        static NativeFrameImage Atlas => NativeFrameImage.Get(AtlasRes);

        public static bool Ready
        {
            get { var a = Atlas; return a != null && a.Ready; }
        }

        // Dark speckled bulletin board with a wood trim — the outer "frame" of a menu.
        public static void DarkBoard(IntPtr hdc, int x, int y, int w, int h)
            => Atlas.BlitNineSlice(hdc, x, y, w, h, 780, 24, 200, 148, bl: 20, bt: 12, br: 20, bb: 24);

        // Papyrus page — the light parchment content panel that sits inside the dark frame.
        public static void Parchment(IntPtr hdc, int x, int y, int w, int h)
            => Atlas.BlitNineSlice(hdc, x, y, w, h, 380, 24, 84, 108, bl: 10, bt: 12, br: 10, bb: 10);

        // Big open-book parchment — wide alternative for two-column content.
        public static void Book(IntPtr hdc, int x, int y, int w, int h)
            => Atlas.BlitNineSlice(hdc, x, y, w, h, 208, 344, 272, 168, bl: 24, bt: 24, br: 24, bb: 24);

        // Brown wood ribbon/sign — used as a title tag on top of a panel.
        public static void WoodRibbon(IntPtr hdc, int x, int y, int w, int h)
            => Atlas.BlitNineSlice(hdc, x, y, w, h, 828, 320, 60, 26, bl: 10, bt: 4, br: 10, bb: 4);

        // Dark square button chassis (atlas group A). Every atlas cell bakes a glyph, so
        // callers draw their own label/glyph centred on top. pressed → the alt/darker shade.
        public static void DarkButton(IntPtr hdc, int x, int y, int w, int h, bool pressed = false)
        {
            int sx = pressed ? 135 : 24;   // group B (pressed) vs group A (normal)
            Atlas.BlitNineSlice(hdc, x, y, w, h, sx, 57, 20, 24, bl: 4, bt: 4, br: 4, bb: 4);
        }

        // Convenience BGR colours for text drawn over the two panel tones, so every window
        // uses the same ink and nothing is illegible on parchment.
        public static uint InkDark   => Bgr(0.20f, 0.12f, 0.06f);   // body text on parchment
        public static uint InkTitle  => Bgr(0.98f, 0.92f, 0.72f);   // title text on wood ribbon
        public static uint InkMuted  => Bgr(0.42f, 0.32f, 0.20f);   // hints/labels on parchment
        public static uint InkOnDark => Bgr(0.94f, 0.88f, 0.70f);   // text on the dark board

        static uint Bgr(float r, float g, float b)
        {
            byte R = (byte)(Mathf.Clamp01(r) * 255);
            byte G = (byte)(Mathf.Clamp01(g) * 255);
            byte B = (byte)(Mathf.Clamp01(b) * 255);
            return (uint)(R | (G << 8) | (B << 16));
        }
    }
}

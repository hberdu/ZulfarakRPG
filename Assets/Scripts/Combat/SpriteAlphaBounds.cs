using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Per-sprite alpha pixel scan, cached. Used to:
    //  - place decoration sprites so their VISIBLE art touches the ground tile
    //    (regardless of how much transparent padding the sprite frame has)
    //  - size HP bars to the visible character width
    //  - position HP bars just above the topmost opaque pixel
    public static class SpriteAlphaBounds
    {
        public struct Result
        {
            public float bottomFromBottom;   // Y of bottommost opaque pixel (sprite-local WU, 0 = sprite bottom)
            public float topFromBottom;      // Y of topmost opaque pixel    (sprite-local WU)
            public float width;              // width of opaque area         (sprite-local WU)
            public float centerXFromLeft;    // center X of opaque area      (sprite-local WU)
            public float feetFromBottom;     // Y of feet (bottommost row with width >= 40% of widest) — ignores thin staff/cloak/shadow tips
        }

        static readonly Dictionary<Sprite, Result> _cache = new Dictionary<Sprite, Result>();

        public static Result Get(Sprite sp)
        {
            if (sp == null)
                return new Result {
                    bottomFromBottom = 0f,
                    topFromBottom    = 1f,
                    width            = 0.5f,
                    centerXFromLeft  = 0.5f,
                };
            if (_cache.TryGetValue(sp, out var cached)) return cached;

            // Fallback when texture isn't Read/Write enabled (no pixel access).
            var fallback = new Result {
                bottomFromBottom = 0f,
                topFromBottom    = sp.bounds.size.y,
                width            = sp.bounds.size.x * 0.30f,
                centerXFromLeft  = sp.bounds.size.x * 0.5f,
                feetFromBottom   = 0f,
            };
            var tex = sp.texture;
            if (tex == null) { _cache[sp] = fallback; return fallback; }

            try
            {
                var rect = sp.textureRect;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(rect.x));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(rect.y));
                int w  = Mathf.Min(tex.width  - x0, Mathf.CeilToInt(rect.width));
                int h  = Mathf.Min(tex.height - y0, Mathf.CeilToInt(rect.height));
                if (w <= 0 || h <= 0) { _cache[sp] = fallback; return fallback; }

                var pixels = tex.GetPixels(x0, y0, w, h);
                int minX = int.MaxValue, maxX = -1;
                int minY = int.MaxValue, maxY = -1;
                var rowWidth = new int[h]; // opaque pixel count per row (y)
                for (int y = 0; y < h; y++)
                {
                    int rowMinX = int.MaxValue, rowMaxX = -1;
                    for (int x = 0; x < w; x++)
                        if (pixels[y * w + x].a > 0.1f)
                        {
                            if (x < rowMinX) rowMinX = x;
                            if (x > rowMaxX) rowMaxX = x;
                        }
                    if (rowMaxX >= 0)
                    {
                        rowWidth[y] = rowMaxX - rowMinX + 1;
                        if (rowMinX < minX) minX = rowMinX;
                        if (rowMaxX > maxX) maxX = rowMaxX;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
                if (minX > maxX || minY > maxY) { _cache[sp] = fallback; return fallback; }

                // Find feet: start at the WIDEST row (chest/cloak — definitely part of the main body),
                // then scan DOWN until hitting an empty row. That row's lower neighbour is the feet.
                // This skips shadows that sit below the feet (often separated by a transparent gap).
                int maxWidthY = minY;
                for (int y = minY; y <= maxY; y++)
                    if (rowWidth[y] > rowWidth[maxWidthY]) maxWidthY = y;

                int feetY = maxWidthY;
                for (int y = maxWidthY - 1; y >= 0; y--)
                {
                    if (rowWidth[y] == 0) break;  // gap reached — stop, don't include shadow below
                    feetY = y;
                }

                float ppu = sp.pixelsPerUnit;
                var result = new Result {
                    bottomFromBottom = minY              / ppu,
                    topFromBottom    = (maxY + 1)        / ppu,
                    width            = (maxX - minX + 1) / ppu,
                    centerXFromLeft  = ((minX + maxX + 1) * 0.5f) / ppu,
                    feetFromBottom   = feetY             / ppu,
                };
                _cache[sp] = result;
                return result;
            }
            catch
            {
                _cache[sp] = fallback;
                return fallback;
            }
        }
    }
}

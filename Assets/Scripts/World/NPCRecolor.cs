using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Runtime palette-swap used to visually distinguish the class-master NPCs from the
    // player, who share the exact same class sprites. It rebuilds each frame's texture:
    //   • the top of the head (hair / hat / helmet) is bleached to WHITE, so every
    //     master reads as a white-haired elder, and
    //   • the coloured clothing is hue-shifted to a master-specific colour that differs
    //     from the player's outfit.
    // Skin tones are preserved. If a source texture isn't CPU-readable the swap is
    // skipped and the caller falls back to a plain tint, so nothing ever breaks.
    public static class NPCRecolor
    {
        // Cache keyed by (source sprite instance id, clothes hue ×1000) so repeated
        // frames / multiple masters don't re-bake the same textures.
        static readonly Dictionary<long, Sprite> _cache = new Dictionary<long, Sprite>();

        // Recolours a whole animation strip. Returns a new array; any frame that can't
        // be read is passed through unchanged. clothesHue is 0..1 (HSV hue).
        public static Sprite[] Recolor(Sprite[] frames, float clothesHue)
        {
            if (frames == null || frames.Length == 0) return frames;
            var outFrames = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                outFrames[i] = RecolorOne(frames[i], clothesHue) ?? frames[i];
            return outFrames;
        }

        static Sprite RecolorOne(Sprite src, float clothesHue)
        {
            if (src == null || src.texture == null) return null;
            long key = ((long)src.GetHashCode() << 12) ^ Mathf.RoundToInt(clothesHue * 1000f);
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            Color[] pixels;
            int w = Mathf.RoundToInt(src.rect.width);
            int h = Mathf.RoundToInt(src.rect.height);
            try
            {
                pixels = src.texture.GetPixels(
                    Mathf.RoundToInt(src.rect.x), Mathf.RoundToInt(src.rect.y), w, h);
            }
            catch
            {
                // Texture isn't Read/Write enabled — signal the caller to use a flat tint.
                return null;
            }

            // Opaque vertical bounds so "top of head" is measured on the actual art.
            int minY = h, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (pixels[y * w + x].a > 0.35f) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxY < minY) return null;
            float span = Mathf.Max(1, maxY - minY);

            for (int y = 0; y < h; y++)
            {
                float ny = (y - minY) / span;          // 0 = feet, 1 = top of head
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    var c = pixels[idx];
                    if (c.a <= 0.35f) continue;

                    bool skin = IsSkin(c);

                    // Head band (top ~36%): bleach non-skin pixels toward white → white hair.
                    if (ny >= 0.64f && !skin)
                    {
                        float luma = 0.30f * c.r + 0.59f * c.g + 0.11f * c.b;
                        float v = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(luma));   // keep a little shading
                        pixels[idx] = new Color(v, v, v, c.a);
                        continue;
                    }

                    // Body: hue-shift saturated, non-skin pixels to the master's colour.
                    if (!skin)
                    {
                        Color.RGBToHSV(c, out _, out float s, out float val);
                        if (s > 0.22f)
                        {
                            var recol = Color.HSVToRGB(clothesHue, Mathf.Clamp(s, 0.35f, 0.95f), val);
                            recol.a = c.a;
                            pixels[idx] = recol;
                        }
                    }
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels(pixels);
            tex.Apply();

            var pivot = new Vector2(
                src.pivot.x / Mathf.Max(1f, src.rect.width),
                src.pivot.y / Mathf.Max(1f, src.rect.height));
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, src.pixelsPerUnit);
            _cache[key] = sprite;
            return sprite;
        }

        // Warm, fairly bright, red-dominant tone → treat as skin and leave untouched.
        static bool IsSkin(Color c)
        {
            return c.r > 0.55f && c.r >= c.g && c.g >= c.b && (c.r - c.b) > 0.12f && (c.r - c.b) < 0.55f;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZulfarakRPG
{
    // Icon packs bundled with the project under Assets/StreamingAssets/Icons, so the game
    // loads them from the repository (not from a developer's PC) on any machine. Loaded at
    // runtime as raw PNGs, so no Unity import step is needed. StreamingAssets is copied
    // verbatim into the build and resolves to a real directory on desktop.
    public static class IconPaths
    {
        static string IconsRoot => Path.Combine(Application.streamingAssetsPath, "Icons");

        public static string WeaponsDir => Path.Combine(IconsRoot, "Weapons");
        public static string ArmorDir   => Path.Combine(IconsRoot, "Armor");
        public static string SkillsDir  => Path.Combine(IconsRoot, "Skills");
        public static string SkillFxDir => Path.Combine(IconsRoot, "SkillFx");
        // Pixel RPG UI Pack sheet (buttons, panels…).
        public static string UiSheet    => Path.Combine(IconsRoot, "Ui.png");

        // Weapons pack uses numbered files (1.png, 2.png, …).
        public static string Weapon(int n) => Path.Combine(WeaponsDir, n + ".png");
        // Armor + skills packs use tileNNN.png (zero-padded to 3 digits).
        public static string Armor(int tile) => Path.Combine(ArmorDir, "tile" + tile.ToString("000") + ".png");
        public static string Skill(int tile) => Path.Combine(SkillsDir, "tile" + tile.ToString("000") + ".png");
        // PixelEffect skill animation sheets: Skill_Effect_01.png .. _10.png.
        public static string SkillFx(int n) => Path.Combine(SkillFxDir, "Skill_Effect_" + n.ToString("00") + ".png");
    }

    // Runtime loader/cache for icon PNGs. Provides Unity Sprites (for in-world use like the
    // weapon-in-hand) and native GDI images (for the inventory / skill windows), plus a
    // helper to snapshot the live hero sprite for the inventory paper-doll.
    public static class IconLibrary
    {
        static readonly Dictionary<string, Texture2D> _tex = new();
        static readonly Dictionary<string, Sprite> _spr = new();

        public static Texture2D Tex(string absPath)
        {
            if (string.IsNullOrEmpty(absPath)) return null;
            if (_tex.TryGetValue(absPath, out var t)) return t;
            t = null;
            try
            {
                if (File.Exists(absPath))
                {
                    var bytes = File.ReadAllBytes(absPath);
                    var tx = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    if (tx.LoadImage(bytes)) t = tx;
                    else UnityEngine.Object.Destroy(tx);
                }
                else Debug.LogWarning($"[IconLibrary] Icone nao encontrado: {absPath}");
            }
            catch (Exception e) { Debug.LogWarning($"[IconLibrary] Falha ao carregar '{absPath}': {e.Message}"); }
            _tex[absPath] = t;
            return t;
        }

        public static Sprite Spr(string absPath)
        {
            if (string.IsNullOrEmpty(absPath)) return null;
            if (_spr.TryGetValue(absPath, out var s)) return s;
            var t = Tex(absPath);
            s = t != null ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 16f) : null;
            _spr[absPath] = s;
            return s;
        }

        // Native GDI image for a pack icon (cached for the app lifetime).
        public static NativeFrameImage Gdi(string absPath)
        {
            if (string.IsNullOrEmpty(absPath)) return null;
            return NativeFrameImage.GetTexture("iconfile:" + absPath, () => Tex(absPath));
        }

        // Snapshots a sprite's sub-rect into a standalone readable texture (cached by name)
        // so the native inventory can blit the hero's current frame in its doll.
        public static Texture2D TexFromSprite(Sprite s)
        {
            if (s == null || s.texture == null) return null;
            string key = "sprite:" + s.name;
            if (_tex.TryGetValue(key, out var cached) && cached != null) return cached;
            Texture2D tx = null;
            try
            {
                var r = s.rect;
                var px = s.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                tx = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                tx.SetPixels(px); tx.Apply();
            }
            catch (Exception e) { Debug.LogWarning($"[IconLibrary] sprite '{s.name}' nao legivel: {e.Message}"); }
            _tex[key] = tx;
            return tx;
        }

        public static NativeFrameImage GdiFromSprite(Sprite s)
        {
            if (s == null) return null;
            return NativeFrameImage.GetTexture("spr:" + s.name, () => TexFromSprite(s));
        }
    }
}

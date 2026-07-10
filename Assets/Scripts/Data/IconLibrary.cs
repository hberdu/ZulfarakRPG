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

    // Runtime loader/cache for icon PNGs. Provides native GDI images (for the inventory /
    // skill windows) built from PNGs loaded at runtime from StreamingAssets.
    public static class IconLibrary
    {
        static readonly Dictionary<string, Texture2D> _tex = new();

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

        // Native GDI image for a pack icon (cached for the app lifetime).
        public static NativeFrameImage Gdi(string absPath)
        {
            if (string.IsNullOrEmpty(absPath)) return null;
            return NativeFrameImage.GetTexture("iconfile:" + absPath, () => Tex(absPath));
        }
    }
}

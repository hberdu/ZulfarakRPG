using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

// Tools > ZulfarakRPG > Import Character Sprites
// Copies Tiny RPG Full Pack sprites from Downloads into the project.
public static class CharacterSpriteImporter
{
    private const string SrcBase   = @"C:\Users\henri\Downloads\Tiny RPG Character Asset Pack v1.03b -Full 20 Characters\Tiny RPG Character Asset Pack v1.03 -Full 20 Characters\Characters(100x100)";
    private const string DestBase  = "Assets/Art/Characters";
    private const int    FrameSize = 100;

    // charName = asset folder name, srcFolder = folder name inside pack
    private static readonly CharDef[] Characters = {
        new CharDef("Wizard", "Wizard", new[] {
            ("Idle", 6), ("Walk", 6), ("Attack01", 6), ("Attack02", 6),
            ("Hurt", 4), ("DEATH", 4),
        }),
        new CharDef("Archer", "Archer", new[] {
            ("Idle", 6), ("Walk", 6), ("Attack01", 6), ("Attack02", 6),
            ("Hurt", 4), ("Death", 4),
        }),
        new CharDef("Soldier", "Soldier", new[] {
            ("Idle", 6), ("Walk", 8), ("Attack01", 6), ("Attack02", 6), ("Attack03", 6),
            ("Hurt", 4), ("Death", 4),
        }),
        // Dungeon enemies
        new CharDef("Skeleton", "Skeleton", new[] {
            ("Idle", 6), ("Walk", 8), ("Attack01", 6), ("Attack02", 7),
            ("Hurt", 4), ("Death", 4),
        }),
        new CharDef("ArmoredSkeleton", "Armored Skeleton", new[] {
            ("Idle", 6), ("Walk", 8), ("Attack01", 8), ("Attack02", 9),
            ("Hurt", 4), ("Death", 4),
        }),
    };

    [MenuItem("Tools/ZulfarakRPG/Import Character Sprites")]
    public static void ImportAll()
    {
        foreach (var def in Characters)
        {
            Directory.CreateDirectory(Application.dataPath + "/../" + DestBase + "/" + def.Name);
            string srcDir = $@"{SrcBase}\{def.SrcFolder}\{def.SrcFolder}";

            foreach (var (anim, frames) in def.Anims)
            {
                string srcFile  = $@"{srcDir}\{def.SrcFolder}-{anim}.png";
                string destFile = $"{DestBase}/{def.Name}/{def.Name}-{anim}.png";
                string destAbs  = Application.dataPath + "/../" + destFile;

                if (!File.Exists(srcFile))
                {
                    Debug.LogWarning($"[ZulfarakRPG] Not found: {srcFile}");
                    continue;
                }

                File.Copy(srcFile, destAbs, overwrite: true);
                AssetDatabase.ImportAsset(destFile);
                ConfigureSprite(destFile, def.Name, anim, frames);
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("[ZulfarakRPG] Character sprites imported to " + DestBase);
    }

    // ── Public loaders (used by SceneSetupWizard) ───────────────────────────

    // First idle frame for static portrait display
    public static Sprite GetIdleSprite(string charName) => GetFrame(charName, "Idle", 0);

    // All frames of a specific animation
    public static Sprite[] GetFrames(string charName, string anim)
    {
        string path = $"{DestBase}/{charName}/{charName}-{anim}.png";
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        var result = new List<Sprite>();
        int i = 0;
        while (true)
        {
            string spriteName = $"{charName}-{anim}_{i}";
            bool found = false;
            foreach (var a in all)
                if (a is Sprite s && s.name == spriteName) { result.Add(s); found = true; break; }
            if (!found) break;
            i++;
        }
        return result.ToArray();
    }

    // Concatenate multiple animation sequences into one flat array
    public static Sprite[] ConcatFrames(params string[][] animNames)
    {
        // Not used externally; see overload below
        return new Sprite[0];
    }

    // Merge sprite arrays (use for combining Attack01 + Attack02 etc.)
    public static Sprite[] MergeFrames(params Sprite[][] arrays)
    {
        var result = new List<Sprite>();
        foreach (var arr in arrays)
            if (arr != null) result.AddRange(arr);
        return result.ToArray();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    static Sprite GetFrame(string charName, string anim, int frameIndex)
    {
        string path = $"{DestBase}/{charName}/{charName}-{anim}.png";
        string name = $"{charName}-{anim}_{frameIndex}";
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            if (a is Sprite s && s.name == name) return s;
        return null;
    }

    static void ConfigureSprite(string assetPath, string charName, string anim, int frameCount)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return;

        imp.textureType       = TextureImporterType.Sprite;
        imp.spriteImportMode  = SpriteImportMode.Multiple;
        imp.filterMode        = FilterMode.Point;
        imp.textureCompression= TextureImporterCompression.Uncompressed;
        imp.maxTextureSize    = 2048;
        imp.mipmapEnabled     = false;
        imp.alphaIsTransparency = true;
        imp.isReadable        = true;  // WorldHealthBar pixel-scans the sprite for exact visible width

        var sprites = new SpriteMetaData[frameCount];
        for (int i = 0; i < frameCount; i++)
            sprites[i] = new SpriteMetaData {
                name      = $"{charName}-{anim}_{i}",
                rect      = new Rect(i * FrameSize, 0, FrameSize, FrameSize),
                pivot     = new Vector2(0.5f, 0f),
                alignment = (int)SpriteAlignment.BottomCenter
            };
        imp.spritesheet = sprites;
        imp.SaveAndReimport();
    }

    // ── Data struct ─────────────────────────────────────────────────────────
    class CharDef
    {
        public string Name;
        public string SrcFolder;
        public (string anim, int frames)[] Anims;
        public CharDef(string name, string src, (string, int)[] anims)
        { Name = name; SrcFolder = src; Anims = anims; }
    }
}

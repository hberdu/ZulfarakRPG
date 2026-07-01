using UnityEngine;
using UnityEditor;
using System.IO;

// Tools > ZulfarakRPG > Import Zulfarak Textures
// Copies GameAssets textures, slices them and sets Point filter.
public static class ZulfarakTextureImporter
{
    private const string SrcBase  = @"C:\Users\henri\Downloads\GameAssets\Texture";
    private const string DestBase = "Assets/Art/Tileset";

    // ── Named sprite rects ───────────────────────────────────────────────────
    // Unity Texture2D origin is bottom-left; image viewers are top-left.
    // Formula: unityY = textureH - imageY - spriteH

    // TX Struct (512x512): buildings and arch
    static SpriteMetaData[] StructSprites() => new[]
    {
        Spr("struct_building1",  0,   320, 128, 192),   // intact building 1
        Spr("struct_building2",  128, 320, 128, 192),   // intact building 2
        Spr("struct_building3",  256, 320, 128, 192),   // intact building 3
        Spr("struct_arch_top",   384, 320, 128, 192),   // archway top
        Spr("struct_dmg1",       0,   144, 128, 176),   // damaged 1
        Spr("struct_dmg2",       128, 144, 128, 176),   // damaged 2
        Spr("struct_arch_sm",    384, 144, 128, 176),   // small arch
        Spr("struct_stairs1",    0,   0,   128, 144),   // stairs left
        Spr("struct_stairs2",    128, 0,   128, 144),   // stairs right
    };

    // TX Tileset Wall (512x512): building facades
    static SpriteMetaData[] WallSprites() => new[]
    {
        Spr("wall_bldg1",    0,   352, 128, 160),   // building window left
        Spr("wall_bldg2",    128, 352, 128, 160),   // building window arch
        Spr("wall_thin",     320, 352,  32, 160),   // thin wall pillar
        Spr("wall_top",      352, 448,  96,  32),   // wall crenellation
        Spr("wall_bldg3",    384, 352, 128, 160),   // corner building
        Spr("wall_wide",     0,   288, 192,  64),   // wide wall segment
        Spr("wall_small",    192, 288,  64,  64),   // small building
        Spr("wall_block1",   0,   224,  64,  64),   // block 1
        Spr("wall_block2",   64,  224,  64,  64),   // block 2
    };

    // TX Props (512x512): desert decoration objects
    static SpriteMetaData[] PropsSprites() => new[]
    {
        Spr("prop_tablet",    0,   432,  48,  80),  // stone tablet
        Spr("prop_chest",     48,  432,  64,  80),  // chest
        Spr("prop_crate",     112, 432,  64,  80),  // wooden crate
        Spr("prop_bench",     256, 448, 128,  64),  // bench
        Spr("prop_statue",    448, 432,  48,  80),  // stone statue
        Spr("prop_door",      0,   352,  48,  80),  // door
        Spr("prop_barrel",    96,  272,  64,  80),  // barrel
        Spr("prop_vase_lg",   0,   192,  48,  80),  // large vase (desert jar)
        Spr("prop_vase_sm",   96,  208,  48,  64),  // small vase
        Spr("prop_column",    192, 272,  48, 112),  // stone column
        Spr("prop_fountain",  320, 160, 160, 112),  // fountain/well
        Spr("prop_rock_lg",   0,   64,  80,  48),  // large rock
        Spr("prop_rock_sm",   80,  64,  64,  32),  // small rock
        Spr("prop_pebbles",   0,   0,  192,  32),  // pebble row
    };

    // TX Plant (512x512): trees and bushes
    static SpriteMetaData[] PlantSprites() => new[]
    {
        Spr("plant_tree1",  0,   320, 128, 192),  // tree 1
        Spr("plant_tree2",  128, 320, 128, 192),  // tree 2
        Spr("plant_tree3",  256, 320, 128, 192),  // tree 3
        Spr("plant_bush1",  0,   256,  64,  64),  // bush 1
        Spr("plant_bush2",  64,  256,  64,  64),  // bush 2
        Spr("plant_bush3",  128, 256,  96,  64),  // bush 3 (wider)
        Spr("plant_grass",  0,   0,   192,  48),  // grass tufts row
    };

    // TX Tileset Stone Ground (256x256): floor tiles
    static SpriteMetaData[] StoneGroundSprites() => new[]
    {
        Spr("sg_full",       0,  128, 128, 128),  // full stone tile (top-left quadrant of image)
        Spr("sg_corner_tl",  128, 128, 64,  64),  // corner TL
        Spr("sg_corner_tr",  192, 128, 64,  64),  // corner TR
        Spr("sg_corner_bl",  128, 64,  64,  64),  // corner BL
        Spr("sg_corner_br",  192, 64,  64,  64),  // corner BR
        Spr("sg_edge_t",     0,   64, 128,  64),  // top edge
        Spr("sg_edge_b",     0,    0, 128,  64),  // bottom edge
    };

    // ── Main import method ───────────────────────────────────────────────────
    [MenuItem("Tools/ZulfarakRPG/Import Zulfarak Textures")]
    public static void ImportAll()
    {
        Directory.CreateDirectory(Application.dataPath + "/../" + DestBase);

        Import("TX Struct.png",              StructSprites());
        Import("TX Tileset Wall.png",        WallSprites());
        Import("TX Props.png",               PropsSprites());
        Import("TX Plant.png",               PlantSprites());
        Import("TX Tileset Stone Ground.png",StoneGroundSprites());

        // Stone ground as whole sprite too (for tiling background)
        ImportSingle("TX Tileset Stone Ground.png", "StoneGround_bg");

        AssetDatabase.Refresh();
        Debug.Log("[ZulfarakRPG] Texturas importadas em " + DestBase);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    static void Import(string fileName, SpriteMetaData[] sprites)
    {
        string src  = Path.Combine(SrcBase, fileName);
        string dest = DestBase + "/" + fileName;
        string abs  = Application.dataPath + "/../" + dest;
        if (!File.Exists(src)) { Debug.LogWarning("[ZulfarakRPG] Not found: " + src); return; }
        File.Copy(src, abs, overwrite: true);
        AssetDatabase.ImportAsset(dest);

        var imp = AssetImporter.GetAtPath(dest) as TextureImporter;
        if (imp == null) return;
        imp.textureType       = TextureImporterType.Sprite;
        imp.spriteImportMode  = SpriteImportMode.Multiple;
        imp.filterMode        = FilterMode.Point;
        imp.textureCompression= TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled     = false;
        imp.isReadable        = true;  // ParallaxLayer + PlaceDecoration pixel-scan the alpha to seat sprites on the ground
        imp.spritesheet       = sprites;
        imp.SaveAndReimport();
        Debug.Log("[ZulfarakRPG] Importado: " + fileName + " (" + sprites.Length + " sprites)");
    }

    static void ImportSingle(string fileName, string spriteName)
    {
        string src  = Path.Combine(SrcBase, fileName);
        string dest = DestBase + "/bg_" + fileName;
        string abs  = Application.dataPath + "/../" + dest;
        File.Copy(src, abs, overwrite: true);
        AssetDatabase.ImportAsset(dest);
        var imp = AssetImporter.GetAtPath(dest) as TextureImporter;
        if (imp == null) return;
        imp.textureType       = TextureImporterType.Sprite;
        imp.spriteImportMode  = SpriteImportMode.Single;
        imp.filterMode        = FilterMode.Point;
        imp.textureCompression= TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled     = false;
        imp.SaveAndReimport();
    }

    static SpriteMetaData Spr(string name, int x, int y, int w, int h) =>
        new SpriteMetaData {
            name      = name,
            rect      = new Rect(x, y, w, h),
            pivot     = new Vector2(0.5f, 0f),
            alignment = (int)SpriteAlignment.BottomCenter
        };

    // ── Public sprite loaders (used by SceneSetupWizard) ────────────────────
    public static Sprite Load(string fileName, string spriteName)
    {
        string path = DestBase + "/" + fileName;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            if (a is Sprite s && s.name == spriteName) return s;
        Debug.LogWarning("[ZulfarakRPG] Sprite not found: " + spriteName + " in " + path);
        return null;
    }

    public static Sprite LoadBg(string fileName)
    {
        string path = DestBase + "/bg_" + fileName;
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}

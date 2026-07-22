using UnityEditor;
using UnityEngine;

// Normalises import settings for the generated pixel-art so new PNGs dropped into
// Resources/CityDecor (props), Resources/Ground (floor tiles) and Resources/UI (window art)
// come in game-ready: Point filter, PPU 100, uncompressed, readable (GDI blits and the
// alpha-aware ground seating both need CPU pixel access).
public class ZulfarakArtPostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool decor  = p.StartsWith("Assets/Resources/CityDecor/");
        bool ground = p.StartsWith("Assets/Resources/Ground/");
        bool ui     = p.StartsWith("Assets/Resources/UI/");
        if (!decor && !ground && !ui) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        // UI art is fine-grained hi-res ("pixel art, but not that pixelated") — Bilinear so the
        // Unity-rendered screens (loading / character creation) downscale smoothly. World art
        // stays chunky Point. GDI windows read raw pixels, unaffected either way.
        ti.filterMode          = ui ? FilterMode.Bilinear : FilterMode.Point;
        ti.textureCompression  = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled       = false;
        ti.isReadable          = true;
        ti.wrapMode            = ground ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        var st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        // Props seat on the ground line → BottomCenter; ground tiles / UI art stay centred.
        st.spriteAlignment = (int)(decor ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
        st.spriteMeshType  = SpriteMeshType.FullRect;
        ti.SetTextureSettings(st);
    }
}

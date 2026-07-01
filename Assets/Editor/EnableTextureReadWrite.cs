using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > ZulfarakRPG > Enable Read/Write on Character Textures
// SpriteAlphaBounds needs CPU pixel access to detect the visible feet of each
// frame and fit the collider to it. Tiny RPG textures import with Read/Write
// disabled by default, which causes the fallback path (collider = full frame)
// and characters appear to float because the frame has transparent padding
// below the feet. Running this once flips the flag on every character texture.
public static class EnableTextureReadWrite
{
    static readonly string[] Folders =
    {
        "Assets/Art/Characters",
    };

    [MenuItem("Tools/ZulfarakRPG/Enable Read-Write on Character Textures")]
    public static void Run()
    {
        int touched = 0;
        foreach (var folder in Folders)
        {
            if (!Directory.Exists(folder)) continue;
            string[] guids = AssetDatabase.FindAssets("t:texture2D", new[] { folder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                if (imp.isReadable) continue;
                imp.isReadable = true;
                imp.SaveAndReimport();
                touched++;
            }
        }
        Debug.Log($"[ZulfarakRPG] Enabled Read/Write on {touched} character texture(s). Restart Play mode.");
    }
}

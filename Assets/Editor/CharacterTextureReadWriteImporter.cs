using UnityEditor;
using UnityEngine;

// Auto-enables Read/Write on every character texture under Assets/Art/Characters.
// SpriteAlphaBounds needs CPU pixel access to detect each frame's visible feet so
// the collider can fit and GroundSnap can position characters on the floor.
// Without Read/Write, alpha scans fall back to "full sprite", which makes
// characters float because the wizard-assumed FEET_OFFSET doesn't match the
// actual sprite content.
public class CharacterTextureReadWriteImporter : AssetPostprocessor
{
    const string Folder = "Assets/Art/Characters";

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;
        var imp = assetImporter as TextureImporter;
        if (imp == null || imp.isReadable) return;
        imp.isReadable = true;
    }

    // Runs on every editor load. Cheap when textures already readable (just an
    // `isReadable` check per asset); only reimports the ones that still aren't.
    [InitializeOnLoadMethod]
    static void FixExistingOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            int touched = 0;
            string[] guids = AssetDatabase.FindAssets("t:texture2D", new[] { Folder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null || imp.isReadable) continue;
                imp.isReadable = true;
                imp.SaveAndReimport();
                touched++;
            }
            if (touched > 0)
                Debug.Log($"[ZulfarakRPG] Enabled Read/Write on {touched} character texture(s). Re-run 'Setup All Scenes' so GroundSnap picks up real alpha bounds.");
        };
    }
}



using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > ZulfarakRPG > Redress City (fix cut-off + align to ground)
//
// The AI-generated props used arbitrary 128x192 CROPS of a building tilesheet
// (TX Struct), so each one shows a cut-off fragment. This swaps them for COMPLETE,
// single-sprite GandalfHardcore art (a VARIETY of trees, tents, a statue, torches),
// scales each to a sensible world height, and — accounting for the sprites' CENTER
// pivot — rests each one's visible BOTTOM exactly on the Ground top so nothing is cut
// off or floating. (The previous version placed the sprite CENTER on the ground line,
// sinking the bottom half = the cut-off look that got the earlier retheme reverted.)
//
// Runs in the Editor so the result is visible and saved into the scene — no risky
// hand-editing of the .unity YAML. Re-runnable and non-destructive to object identity:
// only SpriteRenderer.sprite/color/drawMode, localScale and Y change — never adds or
// removes GameObjects, so NPCs/portal/colliders stay intact. Use Edit > Undo to revert.
public static class CityForestRetheme
{
    const string Dir = "Assets/Art/Gandalf/";

    // objectName → (gandalf sprite asset path, target world height)
    struct Map { public string obj; public string path; public float height; }

    // A VARIETY of distinct sprites — no two objects share the same art where avoidable.
    static readonly Map[] Mapping =
    {
        new Map { obj = "Pyramid_C",   path = Dir + "Trees/Large Pine Tree.png",  height = 1.30f },
        new Map { obj = "Pyramid_L",   path = Dir + "Trees/Tree1.png",            height = 1.15f },
        new Map { obj = "Pyramid_R",   path = Dir + "Trees/Tree3.png",            height = 1.15f },
        new Map { obj = "Dune_FarL",   path = Dir + "Trees/Tree4.png",            height = 0.75f },
        new Map { obj = "Dune_FarR",   path = Dir + "Trees/Tree2.png",            height = 0.75f },
        new Map { obj = "Dune_NearL",  path = Dir + "Trees/Flowering Tree.png",   height = 0.60f },
        new Map { obj = "Dune_NearR",  path = Dir + "Trees/Birch1.png",           height = 0.85f },
        new Map { obj = "Column_L",    path = Dir + "Trees/Birch2.png",           height = 1.00f },
        new Map { obj = "Column_R",    path = Dir + "Trees/Weeping Willow1.png",  height = 1.05f },
        new Map { obj = "Vase_L",      path = Dir + "Decor/Small Tent.png",       height = 0.45f },
        new Map { obj = "Vase_R",      path = Dir + "Decor/Large Tent.png",       height = 0.55f },
        new Map { obj = "Statue",      path = Dir + "Decor/Angel Statue.png",     height = 0.70f },
        new Map { obj = "Tablet",      path = Dir + "Decor/Torch.png",            height = 0.40f },
        new Map { obj = "Gate_Arch",   path = Dir + "Decor/Large Tent.png",       height = 0.80f },
    };

    [MenuItem("Tools/ZulfarakRPG/Redress City (fix cut-off + align)")]
    public static void Retheme()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Zulfarak")
        {
            EditorUtility.DisplayDialog("Redress City",
                "Abra a cena Zulfarak antes de rodar este comando.\n(Cena ativa: " + scene.name + ")", "OK");
            return;
        }

        float groundTop = FindGroundTop();
        int changed = 0, missing = 0;

        foreach (var m in Mapping)
        {
            var go = GameObject.Find(m.obj);
            if (go == null) { Debug.LogWarning($"[Redress] objeto não encontrado: {m.obj}"); missing++; continue; }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) { Debug.LogWarning($"[Redress] sem SpriteRenderer: {m.obj}"); missing++; continue; }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(m.path);
            if (sprite == null) { Debug.LogWarning($"[Redress] sprite não carregou: {m.path}"); missing++; continue; }

            Undo.RecordObject(sr, "Redress sprite");
            Undo.RecordObject(go.transform, "Redress transform");

            sr.sprite   = sprite;
            sr.color    = Color.white;             // drop the old desert tint fragment
            sr.flipX    = false;
            sr.drawMode = SpriteDrawMode.Simple;   // complete sprite — never tiled/cropped

            // Uniform scale so the sprite stands `height` world-units tall.
            float spriteH = sprite.bounds.size.y;
            float scale   = spriteH > 0.0001f ? m.height / spriteH : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            // Rest the sprite's FRAME BOTTOM on the ground line. bounds.min.y is where the
            // frame bottom sits relative to the pivot (negative for a center pivot), so
            // scaling it and subtracting places the bottom exactly on groundTop — pivot
            // agnostic, so it can't sink (the old "cut off") or float.
            var p = go.transform.position;
            float bottomLocal = sprite.bounds.min.y * scale;
            go.transform.position = new Vector3(p.x, groundTop - bottomLocal, p.z);

            EditorUtility.SetDirty(sr);
            EditorUtility.SetDirty(go.transform);
            changed++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Redress] concluído: {changed} objetos trocados e alinhados, {missing} ausentes. Salve (Ctrl+S).");
        EditorUtility.DisplayDialog("Redress City",
            $"Pronto: {changed} sprites completos, alinhados ao solo. {missing} ausentes.\n\nSalve a cena (Ctrl+S).", "OK");
    }

    static float FindGroundTop()
    {
        var ground = GameObject.Find("Ground");
        if (ground != null)
        {
            var sr = ground.GetComponent<SpriteRenderer>();
            if (sr != null) return sr.bounds.max.y;
            var col = ground.GetComponent<Collider2D>();
            if (col != null) return col.bounds.max.y;
        }
        return -0.344f;   // measured fallback for Zulfarak
    }
}

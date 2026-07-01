using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > ZulfarakRPG > Retheme City to Forest
//
// Swaps the AI-generated desert decor in the open Zulfarak scene (dunes, pyramids,
// columns, vases, statue, tablet, gate) for GandalfHardcore forest/medieval sprites
// (trees, tents, a stone statue). Each sprite is scaled to a sensible world height
// computed from its real pixel size and snapped so its feet sit on the Ground top —
// so nothing is mis-sized or floating. Runs in the Editor, so the result is visible
// and saved straight into the scene (no risky hand-editing of the .unity YAML).
//
// Re-runnable and non-destructive to object identity: it only changes each object's
// SpriteRenderer.sprite, localScale and Y — never adds/removes GameObjects, so the
// NPCs/portal/colliders wired in the scene stay intact. Use Edit > Undo to revert.
public static class CityForestRetheme
{
    const string Dir = "Assets/Art/Gandalf/";

    // objectName → (gandalf sprite asset path, target world height)
    struct Map { public string obj; public string path; public float height; }

    static readonly Map[] Mapping =
    {
        new Map { obj = "Pyramid_C",   path = Dir + "Trees/Large Pine Tree.png", height = 1.30f },
        new Map { obj = "Pyramid_L",   path = Dir + "Trees/Tree1.png",           height = 1.15f },
        new Map { obj = "Pyramid_R",   path = Dir + "Trees/Tree3.png",           height = 1.15f },
        new Map { obj = "Dune_FarL",   path = Dir + "Trees/Tree4.png",           height = 0.75f },
        new Map { obj = "Dune_FarR",   path = Dir + "Trees/Tree2.png",           height = 0.75f },
        new Map { obj = "Dune_NearL",  path = Dir + "Trees/Flowering Tree.png",  height = 0.60f },
        new Map { obj = "Dune_NearR",  path = Dir + "Trees/Birch1.png",          height = 0.85f },
        new Map { obj = "Column_L",    path = Dir + "Trees/Birch2.png",          height = 1.00f },
        new Map { obj = "Column_R",    path = Dir + "Trees/Birch2.png",          height = 1.00f },
        new Map { obj = "Vase_L",      path = Dir + "Decor/Small Tent.png",      height = 0.45f },
        new Map { obj = "Vase_R",      path = Dir + "Decor/Small Tent.png",      height = 0.45f },
        new Map { obj = "Statue",      path = Dir + "Decor/Angel Statue.png",    height = 0.70f },
        new Map { obj = "Tablet",      path = Dir + "Decor/Small Tent.png",      height = 0.40f },
        new Map { obj = "Gate_Arch",   path = Dir + "Decor/Large Tent.png",      height = 0.75f },
    };

    [MenuItem("Tools/ZulfarakRPG/Retheme City to Forest")]
    public static void Retheme()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Zulfarak")
        {
            EditorUtility.DisplayDialog("Retheme City",
                "Abra a cena Zulfarak antes de rodar este comando.\n(Cena ativa: " + scene.name + ")", "OK");
            return;
        }

        float groundTop = FindGroundTop();
        int changed = 0, missing = 0;

        foreach (var m in Mapping)
        {
            var go = GameObject.Find(m.obj);
            if (go == null) { Debug.LogWarning($"[Retheme] objeto não encontrado: {m.obj}"); missing++; continue; }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) { Debug.LogWarning($"[Retheme] sem SpriteRenderer: {m.obj}"); missing++; continue; }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(m.path);
            if (sprite == null) { Debug.LogWarning($"[Retheme] sprite não carregou: {m.path}"); missing++; continue; }

            Undo.RecordObject(sr, "Retheme sprite");
            Undo.RecordObject(go.transform, "Retheme transform");

            sr.sprite   = sprite;
            sr.color    = Color.white;   // drop desert tint
            sr.flipX    = false;
            sr.drawMode = SpriteDrawMode.Simple;   // desert props used tiled/sliced

            // Uniform scale so the sprite stands `height` world-units tall.
            float spriteH = sprite.bounds.size.y;
            float scale   = spriteH > 0.0001f ? m.height / spriteH : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            // Bottom-center pivot → sprite bottom is at transform.y; rest feet on ground.
            var p = go.transform.position;
            go.transform.position = new Vector3(p.x, groundTop, p.z);

            EditorUtility.SetDirty(sr);
            EditorUtility.SetDirty(go.transform);
            changed++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Retheme] concluído: {changed} objetos trocados, {missing} ausentes. " +
                  "Salve a cena (Ctrl+S) para persistir. O planeta de IA está desativado em BackgroundPlanet.");
        EditorUtility.DisplayDialog("Retheme City",
            $"Pronto: {changed} objetos viraram floresta, {missing} ausentes.\n\nSalve a cena (Ctrl+S).", "OK");
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
        return -0.162f;   // measured fallback for Zulfarak
    }
}

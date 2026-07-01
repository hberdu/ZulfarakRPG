using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Runtime city dresser (a few frames after Zulfarak loads, once scene Starts + the
    // code-spawned Ferreiro have settled):
    //   1. Swap each cut-off tilesheet-FRAGMENT prop for a COMPLETE decor sprite from
    //      Resources/CityDecor — ONLY autumn trees (orange/red), tents and statues.
    //   2. Ground every prop (visible bottom → ground) and every static NPC (foot collider
    //      → ground), and strip any yellow/tan tint off the NPCs so they look natural.
    //
    // Runs at runtime, so it survives scene re-saves (unlike direct .unity edits, which the
    // open editor overwrites). Physics characters (player/enemies) self-ground; skipped.
    public class SceneGrounder : MonoBehaviour
    {
        struct Deco { public string obj; public string res; public float height; }

        // Trees use ONLY the autumn sprites — Tree3 (orange-red) & Birch2 (orange). No
        // green / yellow-olive / icy-blue trees. Torches removed; extra statues added.
        static readonly Deco[] Decor =
        {
            new Deco{ obj="Pyramid_C",  res="Tree3",       height=1.30f },
            new Deco{ obj="Pyramid_L",  res="Birch2",      height=1.15f },
            new Deco{ obj="Pyramid_R",  res="Tree3",       height=1.15f },
            new Deco{ obj="Dune_FarL",  res="Birch2",      height=0.75f },
            new Deco{ obj="Dune_FarR",  res="Tree3",       height=0.70f },
            new Deco{ obj="Dune_NearL", res="Birch2",      height=0.60f },
            new Deco{ obj="Dune_NearR", res="Tree3",       height=0.85f },
            new Deco{ obj="Column_L",   res="Birch2",      height=1.00f },
            new Deco{ obj="Column_R",   res="Tree3",       height=1.05f },
            new Deco{ obj="Vase_L",     res="SmallTent",   height=0.45f },
            new Deco{ obj="Vase_R",     res="LargeTent",   height=0.55f },
            new Deco{ obj="Statue",     res="AngelStatue", height=0.72f },
            new Deco{ obj="Tablet",     res="AngelStatue", height=0.55f },   // was Torch (removed)
            new Deco{ obj="Gate_Arch",  res="AngelStatue", height=0.88f },   // extra statue
        };

        static readonly string[] Npcs = { "Kael_NPC", "ClassMaster_NPC", "Ferreiro_NPC" };

        static SceneGrounder _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Zulfarak") return;
            if (_instance == null)
            {
                var go = new GameObject("SceneGrounder");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SceneGrounder>();
            }
            _instance.StopAllCoroutines();
            _instance.StartCoroutine(_instance.Run());
        }

        IEnumerator Run()
        {
            yield return null;
            yield return null;
            yield return null;

            float groundTop = GroundAlignUtil.FindGroundTopY();

            int treeIdx = 0;
            foreach (var d in Decor)
            {
                var go = GameObject.Find(d.obj);
                if (go == null) continue;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) continue;

                var sprite = Resources.Load<Sprite>("CityDecor/" + d.res);
                if (sprite != null)
                {
                    sr.sprite   = sprite;
                    sr.color    = Color.white;
                    bool isTree = d.res == "Tree3" || d.res.StartsWith("Birch");
                    sr.flipX    = isTree && (treeIdx++ % 2 == 1);   // mirror alternate trees for variety
                    sr.drawMode = SpriteDrawMode.Simple;            // complete sprite — never cropped
                    float sh = sprite.bounds.size.y;
                    float s  = sh > 0.0001f ? d.height / sh : 1f;
                    go.transform.localScale = new Vector3(s, s, 1f);
                }
                GroundBySprite(go, sr, groundTop);
            }

            foreach (var n in Npcs)
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = Color.white;   // strip the yellow/tan tint → natural
                GroundByCollider(go, groundTop);
            }
        }

        // Props: snap the sprite's visible bottom (alpha-aware, frame-bottom fallback) to ground.
        static void GroundBySprite(GameObject go, SpriteRenderer sr, float groundTop)
        {
            if (sr == null || sr.sprite == null) return;
            var ab = SpriteAlphaBounds.Get(sr.sprite);
            float scale = Mathf.Max(0.0001f, go.transform.lossyScale.y);
            float visibleBottom = sr.bounds.min.y + ab.feetFromBottom * scale;
            float shift = groundTop - visibleBottom;
            if (Mathf.Abs(shift) > 0.001f) go.transform.position += new Vector3(0f, shift, 0f);
        }

        // NPCs: snap their FOOT COLLIDER bottom to the ground — deterministic, matches how
        // the player grounds. (The alpha-feet path floated them high when the character
        // texture wasn't pixel-readable at runtime.)
        static void GroundByCollider(GameObject go, float groundTop)
        {
            var col = go.GetComponent<Collider2D>();
            if (col == null) return;
            Physics2D.SyncTransforms();
            float shift = groundTop - col.bounds.min.y;
            if (Mathf.Abs(shift) > 0.001f)
                go.transform.position += new Vector3(0f, shift, 0f);
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Runtime city dresser. Two jobs, done a few frames after Zulfarak loads (so scene
    // Starts and the code-spawned Ferreiro have settled):
    //   1. Swap each cut-off tilesheet-FRAGMENT prop for a COMPLETE decor sprite (loaded
    //      from Resources/CityDecor), scaled to a sensible height — a VARIETY of trees,
    //      tents, a statue and a torch.
    //   2. Ground every prop AND every static NPC so its visible bottom rests on the
    //      ground line (alpha-aware) — nothing floats or sinks.
    //
    // Runs at runtime, so it survives scene re-saves (unlike direct .unity edits, which the
    // open editor overwrites). Physics characters (player/enemies) self-ground and are
    // left alone.
    public class SceneGrounder : MonoBehaviour
    {
        struct Deco { public string obj; public string res; public float height; }

        static readonly Deco[] Decor =
        {
            new Deco{ obj="Pyramid_C",  res="LargePineTree",  height=1.30f },
            new Deco{ obj="Pyramid_L",  res="Tree1",          height=1.15f },
            new Deco{ obj="Pyramid_R",  res="Tree3",          height=1.15f },
            new Deco{ obj="Dune_FarL",  res="Tree4",          height=0.75f },
            new Deco{ obj="Dune_FarR",  res="Tree2",          height=0.75f },
            new Deco{ obj="Dune_NearL", res="FloweringTree",  height=0.60f },
            new Deco{ obj="Dune_NearR", res="Birch1",         height=0.85f },
            new Deco{ obj="Column_L",   res="Birch2",         height=1.00f },
            new Deco{ obj="Column_R",   res="WeepingWillow1", height=1.05f },
            new Deco{ obj="Vase_L",     res="SmallTent",      height=0.45f },
            new Deco{ obj="Vase_R",     res="LargeTent",      height=0.55f },
            new Deco{ obj="Statue",     res="AngelStatue",    height=0.70f },
            new Deco{ obj="Tablet",     res="Torch",          height=0.40f },
            new Deco{ obj="Gate_Arch",  res="LargeTent",      height=0.80f },
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
                    sr.flipX    = false;
                    sr.drawMode = SpriteDrawMode.Simple;   // complete sprite — never cropped
                    float sh = sprite.bounds.size.y;
                    float s  = sh > 0.0001f ? d.height / sh : 1f;
                    go.transform.localScale = new Vector3(s, s, 1f);
                }
                GroundObject(go, sr, groundTop);
            }

            foreach (var n in Npcs)
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) GroundObject(go, sr, groundTop);
            }
        }

        // Snap so the sprite's visible bottom sits on the ground line (alpha-aware when the
        // texture is readable — e.g. the NPCs' feet — else the frame bottom).
        static void GroundObject(GameObject go, SpriteRenderer sr, float groundTop)
        {
            if (sr.sprite == null) return;
            var ab = SpriteAlphaBounds.Get(sr.sprite);
            float scale = Mathf.Max(0.0001f, go.transform.lossyScale.y);
            float visibleBottom = sr.bounds.min.y + ab.feetFromBottom * scale;
            float shift = groundTop - visibleBottom;
            if (Mathf.Abs(shift) > 0.001f)
                go.transform.position += new Vector3(0f, shift, 0f);
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Runtime city dresser (a few frames after Zulfarak loads):
    //   • Places a SPREAD-OUT set of complete props from Resources/CityDecor — just TWO
    //     autumn trees (far edges), three statues and two tents across the map; the rest of
    //     the old prop objects are hidden.
    //   • Grounds every prop and every static NPC, disables prop colliders so they can't
    //     block the player, strips the NPC yellow tint, and widens the player's walk range
    //     (and drops the boundary walls) so the hero can roam the whole city.
    //
    // Runtime, so it survives scene re-saves. Physics characters self-ground and are skipped.
    public class SceneGrounder : MonoBehaviour
    {
        struct Deco { public string obj; public string res; public float height; public float x; }

        // 2 trees (edges) + tent + cart, spread across the ~0..5 city width — all from the
        // ORIGINAL generated pack (Gandalf art retired). (Statues removed.)
        static readonly Deco[] Decor =
        {
            new Deco{ obj="Column_L",  res="AutumnOak",   height=1.10f, x=0.30f },   // far-left tree
            new Deco{ obj="Vase_L",    res="TraderTent",  height=0.50f, x=1.80f },
            new Deco{ obj="Vase_R",    res="HayCart",     height=0.45f, x=3.40f },
            new Deco{ obj="Column_R",  res="AutumnOak",   height=1.15f, x=4.70f },   // far-right tree
        };

        // Extra prop objects we no longer use — hidden so the city isn't crowded.
        static readonly string[] Hidden =
        {
            "Pyramid_L", "Pyramid_C", "Pyramid_R",
            "Dune_FarL", "Dune_FarR", "Dune_NearL", "Dune_NearR",
            "Tablet",
            "Statue", "Gate_Arch",   // statues removed from the city per request
            "Kael_NPC",   // Kael removed — only the class master and blacksmith remain
            "Chest",      // chest removed from the city
        };

        // Only the class master and the blacksmith remain in the city.
        static readonly string[] Npcs = { "ClassMaster_NPC", "Ferreiro_NPC" };

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

            foreach (var h in Hidden)
            {
                var go = GameObject.Find(h);
                if (go != null) go.SetActive(false);
            }

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
                    bool isTree = d.res == "AutumnOak";
                    sr.flipX    = isTree && (treeIdx++ % 2 == 1);
                    sr.drawMode = SpriteDrawMode.Simple;
                    float shp   = sprite.bounds.size.y;
                    float s     = shp > 0.0001f ? d.height / shp : 1f;
                    go.transform.localScale = new Vector3(s, s, 1f);
                }

                var p = go.transform.position;
                go.transform.position = new Vector3(d.x, p.y, p.z);      // spread across the map
                foreach (var c in go.GetComponents<Collider2D>())        // never block the player
                    if (!c.isTrigger) c.enabled = false;
                GroundBySprite(go, sr, groundTop);
            }

            // Scale every static NPC to the HERO's size so the player and NPCs share one
            // proportion in the city (the hero was shrunk slightly in his Start()).
            var player = FindAnyObjectByType<PlayerController2D>();
            float npcScale = player != null ? Mathf.Abs(player.transform.lossyScale.y) : 1.7f;

            foreach (var n in Npcs)
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = Color.white;
                go.transform.localScale = new Vector3(npcScale, npcScale, 1f);
                GroundAlignUtil.SeatCharacterOnGround(go.transform, sr);
            }

            // Let the hero roam the whole city width, edge to edge — and re-seat him
            // through the SAME path/ground value as the NPCs so all characters share
            // one visual ground height (his own Start() seat ran frames earlier).
            if (player != null)
            {
                player.sceneBoundsMinX = 0.15f;
                player.sceneBoundsMaxX = 4.85f;
                GroundAlignUtil.SeatCharacterOnGround(player.transform, player.GetComponent<SpriteRenderer>());
            }
            foreach (var w in new[] { "WallLeft", "WallRight" })
            {
                var go = GameObject.Find(w);
                if (go == null) continue;
                foreach (var c in go.GetComponents<Collider2D>()) c.enabled = false;
            }
        }

        static void GroundBySprite(GameObject go, SpriteRenderer sr, float groundTop)
        {
            if (sr == null || sr.sprite == null) return;
            var ab = SpriteAlphaBounds.Get(sr.sprite);
            float scale = Mathf.Max(0.0001f, go.transform.lossyScale.y);
            float visibleBottom = sr.bounds.min.y + ab.feetFromBottom * scale;
            float shift = groundTop - visibleBottom;
            if (Mathf.Abs(shift) > 0.001f) go.transform.position += new Vector3(0f, shift, 0f);
        }
    }
}

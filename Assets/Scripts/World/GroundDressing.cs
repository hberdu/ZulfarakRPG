using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Re-skins the scene's "Ground" object into a continuous BRIGHT DESERT SAND floor at
    // runtime (this overrides whatever sprite/colour the scene authored, so the desert
    // look survives scene re-saves):
    //   • The "Ground" renderer becomes a warm sand fill spanning the whole view width and
    //     reaching far below the camera — a procedural sand texture (fine grain + faint
    //     dune ripples), not a flat colour, so it doesn't look plain.
    //   • A brighter sunlit sand band sits on top as a single continuous surface line.
    //
    // The physics standing line (FindGroundTopY == "Ground" sprite top) is preserved
    // exactly, so every character that snaps to it stays consistent. Auto-runs on
    // Zulfarak + Dungeon load — no scene editing required.
    public class GroundDressing : MonoBehaviour
    {
        const float SurfaceBandH = 0.05f;                                     // sunlit surface strip height (world)
        static readonly Color SandBody    = new Color(0.95f, 0.82f, 0.60f, 1f); // warm sand body tint
        static readonly Color SandSurface = new Color(1.00f, 0.94f, 0.72f, 1f); // brighter sunlit surface

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Zulfarak" && scene.name != "Dungeon") return;

            var ground = GameObject.Find("Ground");
            if (ground == null) return;
            var sr = ground.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            // Preserve the existing standing line (top edge) so alignment is unchanged.
            float standLine = sr.bounds.max.y;

            var cam = Camera.main;
            float camX       = cam != null ? cam.transform.position.x : 2.5f;
            float viewBottom = cam != null ? cam.transform.position.y - cam.orthographicSize : -0.75f;
            float halfW      = cam != null ? cam.orthographicSize * cam.aspect : 2.5f;
            float worldW     = Mathf.Max(halfW * 2f + 2f, 8f);

            // ── Sand body: fills well below the view so it always reads as deep ground.
            float dirtBottom = viewBottom - 3f;
            float dirtH      = standLine - dirtBottom;
            float dirtCenter = (standLine + dirtBottom) * 0.5f;

            ground.transform.localScale = Vector3.one;
            ground.transform.position   = new Vector3(camX, dirtCenter, ground.transform.position.z);

            sr.sprite       = SandSprite();
            sr.color        = SandBody;
            sr.drawMode     = SpriteDrawMode.Tiled;
            sr.tileMode     = SpriteTileMode.Continuous;
            sr.size         = new Vector2(worldW, dirtH);
            sr.sortingOrder = Mathf.Min(sr.sortingOrder, -6);

            // Collider: top edge exactly on the standing line (GroundFloorEnsurer backs it up).
            var col = ground.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.enabled   = true;
                col.isTrigger = false;
                col.size      = new Vector2(worldW, dirtH);
                col.offset    = Vector2.zero;   // box top = position.y + dirtH/2 = standLine
            }

            // ── Bright sunlit sand surface band along the top.
            var bandGO = ground.transform.Find("GroundGrass")?.gameObject
                          ?? new GameObject("GroundGrass");
            bandGO.transform.SetParent(ground.transform, false);
            var bsr = bandGO.GetComponent<SpriteRenderer>() ?? bandGO.AddComponent<SpriteRenderer>();

            bandGO.transform.localScale = Vector3.one;
            bandGO.transform.position   = new Vector3(camX, standLine - SurfaceBandH * 0.5f,
                                                      ground.transform.position.z - 0.01f);

            bsr.sprite       = SandSprite();
            bsr.color        = SandSurface;
            bsr.drawMode     = SpriteDrawMode.Tiled;
            bsr.tileMode     = SpriteTileMode.Continuous;
            bsr.size         = new Vector2(worldW, SurfaceBandH);
            bsr.sortingOrder = sr.sortingOrder + 1;                            // above body, behind characters

            // Consumers recompute the (unchanged) ground top against the new sand fill.
            GroundAlignUtil.InvalidateCache();
        }

        // Procedural sand: warm near-white base + fine random grain + faint horizontal dune
        // ripples (seamless vertically). Tinted per use, tiled continuously across the floor.
        static Sprite _sand;
        static Sprite SandSprite()
        {
            if (_sand != null) return _sand;
            const int W = 48, H = 32;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var rnd = new System.Random(7);
            for (int y = 0; y < H; y++)
            {
                float ripple = Mathf.Sin(y / (float)H * Mathf.PI * 4f) * 0.05f;   // 4 seamless dune bands
                for (int x = 0; x < W; x++)
                {
                    float grain = (float)(rnd.NextDouble() - 0.5) * 0.10f;
                    float v     = 1f + ripple + grain;
                    t.SetPixel(x, y, new Color(Mathf.Clamp01(0.96f * v),
                                               Mathf.Clamp01(0.87f * v),
                                               Mathf.Clamp01(0.68f * v), 1f));
                }
            }
            t.Apply();
            _sand = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 24f);  // ~2 world-units/tile
            return _sand;
        }
    }
}

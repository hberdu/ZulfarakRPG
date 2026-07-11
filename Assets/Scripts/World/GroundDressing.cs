using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Re-skins the scene's "Ground" object into one CONTINUOUS golden desert-sand floor at
    // runtime (overrides whatever the scene authored, so the look survives scene re-saves).
    // The sand is a large, fully-seamless texture with only subtle periodic shading — no
    // grid, no visible tile seams, one unbroken surface across the whole city. The physics
    // standing line (FindGroundTopY == "Ground" sprite top) is preserved exactly.
    // Auto-runs on Zulfarak + Dungeon load — no scene editing required.
    public class GroundDressing : MonoBehaviour
    {
        static readonly Color SandTint = new Color(1f, 1f, 1f, 1f);   // texture already carries the colour

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
            // Wide enough that the floor still fills the view once the camera FOLLOWS the hero
            // across the arena (the ground is centred on the load-time camera X and stays put).
            float worldW     = Mathf.Max(halfW * 2f + 2f, 40f);

            // Sand fills well below the view so it always reads as deep, continuous ground.
            float dirtBottom = viewBottom - 3f;
            float dirtH      = standLine - dirtBottom;
            float dirtCenter = (standLine + dirtBottom) * 0.5f;

            ground.transform.localScale = Vector3.one;
            ground.transform.position   = new Vector3(camX, dirtCenter, ground.transform.position.z);

            sr.sprite       = SandSprite();
            sr.color        = SandTint;
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

            // Remove the old two-tone grass/surface band if a previous build left one — the
            // floor is now a single continuous surface.
            var stale = ground.transform.Find("GroundGrass");
            if (stale != null) Destroy(stale.gameObject);

            GroundAlignUtil.InvalidateCache();
        }

        // One large SEAMLESS golden-sand texture: warm yellow base with only gentle periodic
        // shading (all integer-frequency sines over [0,1) → wraps perfectly, no seam). Tiled
        // at a big world size so it barely repeats, reading as one continuous desert floor.
        static Sprite _sand;
        static Sprite SandSprite()
        {
            if (_sand != null) return _sand;
            const int W = 128, H = 64;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            const float TAU = Mathf.PI * 2f;
            for (int y = 0; y < H; y++)
            {
                float ny = y / (float)H;
                for (int x = 0; x < W; x++)
                {
                    float nx = x / (float)W;
                    float n = Mathf.Sin(nx * TAU * 2f + ny * TAU) * 0.025f
                            + Mathf.Sin(nx * TAU * 3f - ny * TAU * 2f) * 0.018f
                            + Mathf.Cos(ny * TAU * 2f) * 0.020f;
                    float v = 1f + n;
                    t.SetPixel(x, y, new Color(Mathf.Clamp01(0.94f * v),
                                               Mathf.Clamp01(0.82f * v),
                                               Mathf.Clamp01(0.52f * v), 1f));
                }
            }
            t.Apply();
            _sand = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 32f);  // ~4 world-units/tile
            return _sand;
        }
    }
}

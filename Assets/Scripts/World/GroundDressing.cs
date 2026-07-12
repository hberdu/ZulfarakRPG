using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Re-skins the scene's "Ground" object into one CONTINUOUS floor at runtime (overrides
    // whatever the scene authored, so the look survives scene re-saves). The floor is themed
    // PER SCENE: golden desert sand for the Zulfarak hub (matching its pyramids/dunes), mossy
    // forest grass for the phase-1 Dungeon. Both are large, fully-seamless textures with only
    // subtle periodic shading — no grid, no visible tile seams, one unbroken surface. The
    // physics standing line (FindGroundTopY == "Ground" sprite top) is preserved exactly.
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
            if (!MapBounds.IsGameplayScene(scene.name)) return;

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

            // Desert sand for the Zulfarak hub (matches its pyramids/dunes/vases/columns); mossy
            // forest grass for the phase-1 Dungeon (matches the forest dungeons/camps).
            sr.sprite       = GroundSprite(scene.name == "Zulfarak");
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

        // Two large SEAMLESS ground textures, chosen per scene: golden desert SAND for the
        // Zulfarak hub, mossy forest GRASS for the phase-1 Dungeon. Both use only gentle
        // integer-frequency sine shading over [0,1) → wraps perfectly, no visible seam. Tiled at
        // a big world size so they barely repeat, reading as one continuous floor.
        static readonly Color SandBase  = new Color(0.84f, 0.70f, 0.42f);   // warm golden desert sand
        static readonly Color GrassBase = new Color(0.34f, 0.52f, 0.24f);   // mossy forest grass

        static Sprite _sandSprite;
        static Sprite _grassSprite;

        static Sprite GroundSprite(bool desert)
        {
            if (desert)
            {
                if (_sandSprite == null) _sandSprite = BuildGround(SandBase);
                return _sandSprite;
            }
            if (_grassSprite == null) _grassSprite = BuildGround(GrassBase);
            return _grassSprite;
        }

        static Sprite BuildGround(Color baseColor)
        {
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
                    t.SetPixel(x, y, new Color(Mathf.Clamp01(baseColor.r * v),
                                               Mathf.Clamp01(baseColor.g * v),
                                               Mathf.Clamp01(baseColor.b * v), 1f));
                }
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 32f);  // ~4 world-units/tile
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Re-skins the scene's "Ground" object into a natural, continuous forest floor:
    //   • The "Ground" renderer becomes a SOLID dark-earth fill that spans the whole
    //     view width and reaches far below the camera — no tiling pattern, no seams,
    //     no repeated grass line in the dirt body.
    //   • A thin GRASS FRINGE (top band of the GandalfHardcore foliage tile) is laid
    //     as a SINGLE row on top, its surface nudged a few pixels ABOVE the physics
    //     standing line so character feet nestle into the grass instead of floating.
    //
    // The physics standing line (FindGroundTopY == "Ground" sprite top) is preserved
    // exactly on the earth fill's top edge, so every character that snaps to it stays
    // consistent. Auto-runs on Zulfarak + Dungeon load — no scene editing required.
    public class GroundDressing : MonoBehaviour
    {
        const float GrassBandPx = 34f;    // top slice of the 96px tile = grass + a little dirt
        const float GrassScale  = 1.5f;   // upscale the fringe → fewer, larger horizontal repeats
        const float GrassLift   = 0f;     // 0 = grass sits on earth top; any lift creates a transparent gap the desktop bleeds through
        static readonly Color Earth = new Color(0.14f, 0.12f, 0.10f, 1f); // matches the tile's dirt

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

            var foliage = Resources.Load<Sprite>("GroundFoliage");
            if (foliage == null) { Debug.LogWarning("[GroundDressing] Resources/GroundFoliage not found."); return; }

            // Preserve the existing standing line (top edge) so alignment is unchanged.
            float standLine = sr.bounds.max.y;

            var cam = Camera.main;
            float camX       = cam != null ? cam.transform.position.x : 2.5f;
            float viewBottom = cam != null ? cam.transform.position.y - cam.orthographicSize : -0.75f;
            float halfW      = cam != null ? cam.orthographicSize * cam.aspect : 2.5f;
            float worldW     = Mathf.Max(halfW * 2f + 2f, 8f);

            // ── Solid earth fill (the "Ground" renderer). Reaches well below the view so
            //    it always reads as deep, continuous ground — no pattern, no bottom edge.
            float dirtBottom = viewBottom - 3f;
            float dirtH      = standLine - dirtBottom;
            float dirtCenter = (standLine + dirtBottom) * 0.5f;

            ground.transform.localScale = Vector3.one;
            ground.transform.position   = new Vector3(camX, dirtCenter, ground.transform.position.z);

            sr.sprite       = SolidSprite();
            sr.color        = Earth;
            sr.drawMode     = SpriteDrawMode.Tiled;
            sr.tileMode     = SpriteTileMode.Continuous;
            sr.size         = new Vector2(worldW, dirtH);
            sr.sortingOrder = Mathf.Min(sr.sortingOrder, -6);

            // Collider: top edge exactly on the standing line, so feet-fitted characters
            // rest on the visible surface (GroundFloorEnsurer also backs this up).
            var col = ground.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.enabled   = true;
                col.isTrigger = false;
                col.size      = new Vector2(worldW, dirtH);
                col.offset    = Vector2.zero;   // box top = position.y + dirtH/2 = standLine
            }

            // ── Grass fringe: a single row of the tile's grassy top band, surface lifted
            //    slightly above the feet line so characters plant into the grass.
            var grassGO = ground.transform.Find("GroundGrass")?.gameObject
                          ?? new GameObject("GroundGrass");
            grassGO.transform.SetParent(ground.transform, false);
            var gsr = grassGO.GetComponent<SpriteRenderer>() ?? grassGO.AddComponent<SpriteRenderer>();

            float grassNativeH = GrassBandPx / 100f;          // sprite units before scaling
            float grassWorldH  = grassNativeH * GrassScale;
            float grassTop     = standLine + GrassLift;

            grassGO.transform.localScale = new Vector3(GrassScale, GrassScale, 1f);
            grassGO.transform.position   = new Vector3(camX, grassTop - grassWorldH * 0.5f,
                                                       ground.transform.position.z - 0.01f);

            gsr.sprite       = GrassStrip(foliage);
            gsr.color        = Color.white;
            gsr.drawMode     = SpriteDrawMode.Tiled;
            gsr.tileMode     = SpriteTileMode.Continuous;
            gsr.size         = new Vector2(worldW / GrassScale, grassNativeH); // one vertical tile
            gsr.sortingOrder = sr.sortingOrder + 1;                            // in front of earth, behind characters

            // Consumers recompute the (unchanged) ground top against the new earth fill.
            GroundAlignUtil.InvalidateCache();
        }

        // Solid 4×4 white sprite at 4 PPU (native 1×1 unit) — tiled + tinted for a
        // flat colour fill with a low, sane tile count.
        static Sprite _solid;
        static Sprite SolidSprite()
        {
            if (_solid != null) return _solid;
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            t.SetPixels(px); t.Apply();
            _solid = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _solid;
        }

        // Crops the top GrassBandPx rows of the foliage tile (grass + a little dirt) so
        // the fringe never shows a second grass line lower down.
        static Sprite _grass;
        static Sprite GrassStrip(Sprite foliage)
        {
            if (_grass != null) return _grass;
            var tex = foliage.texture;
            int px  = Mathf.Clamp((int)GrassBandPx, 1, tex.height);
            _grass  = Sprite.Create(tex, new Rect(0, tex.height - px, tex.width, px),
                                    new Vector2(0.5f, 0.5f), 100f);
            return _grass;
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Re-skins the scene's "Ground" object into a natural, continuous forest floor:
    //   • The "Ground" renderer becomes a SOLID dark-earth fill that spans the whole
    //     view width and reaches far below the camera — no tiling pattern, no seams,
    //     no repeated grass line in the dirt body.
    //   • A SOLID GRASS BAND is laid on top as a single continuous horizontal strip
    //     (no per-tile alpha gaps), so the surface reads as one continuous grass
    //     line rather than a row of separated tufts.
    //
    // The physics standing line (FindGroundTopY == "Ground" sprite top) is preserved
    // exactly on the earth fill's top edge, so every character that snaps to it stays
    // consistent. Auto-runs on Zulfarak + Dungeon load — no scene editing required.
    public class GroundDressing : MonoBehaviour
    {
        const float GrassBandH = 0.06f;   // world-unit height of the solid grass strip on top
        static readonly Color Earth = new Color(0.14f, 0.12f, 0.10f, 1f); // matches the tile's dirt
        static readonly Color Grass = new Color(0.42f, 0.32f, 0.12f, 1f); // warm mossy-brown grass line

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

            // ── Solid grass band: a continuous horizontal strip on top of the earth
            //    fill. No per-tile alpha, no gaps — reads as one unbroken grass line.
            var grassGO = ground.transform.Find("GroundGrass")?.gameObject
                          ?? new GameObject("GroundGrass");
            grassGO.transform.SetParent(ground.transform, false);
            var gsr = grassGO.GetComponent<SpriteRenderer>() ?? grassGO.AddComponent<SpriteRenderer>();

            grassGO.transform.localScale = Vector3.one;
            grassGO.transform.position   = new Vector3(camX, standLine - GrassBandH * 0.5f,
                                                       ground.transform.position.z - 0.01f);

            gsr.sprite       = SolidSprite();
            gsr.color        = Grass;
            gsr.drawMode     = SpriteDrawMode.Tiled;
            gsr.tileMode     = SpriteTileMode.Continuous;
            gsr.size         = new Vector2(worldW, GrassBandH);
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
    }
}

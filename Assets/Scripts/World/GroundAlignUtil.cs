using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    public static class GroundAlignUtil
    {
        static float? _cachedGroundY;
        static int    _cachedScene = -1;   // build index the cache belongs to

        // Cache is keyed to the active scene's build index, so a Zulfarak↔Dungeon
        // transition automatically recomputes the ground line — no callback-ordering
        // races between this and the components that consume FindGroundTopY().
        public static float FindGroundTopY()
        {
            int active = SceneManager.GetActiveScene().buildIndex;
            if (_cachedGroundY.HasValue && _cachedScene == active) return _cachedGroundY.Value;
            _cachedScene = active;

            var go = GameObject.Find("Ground");
            if (go != null)
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) { _cachedGroundY = sr.bounds.max.y; return _cachedGroundY.Value; }
                var col = go.GetComponent<Collider2D>();
                if (col != null) { _cachedGroundY = col.bounds.max.y; return _cachedGroundY.Value; }
            }
            _cachedGroundY = -0.334f;   // sensible fallback for Zulfarak
            return _cachedGroundY.Value;
        }

        public static void InvalidateCache() { _cachedGroundY = null; _cachedScene = -1; }

        // Snaps `t.position.y` so the sprite's visible feet (alpha-aware) sit
        // exactly on the ground top. No-op when the texture isn't Read/Write
        // enabled (alpha scan would be unreliable).
        // Pivot-AGNOSTIC: works regardless of where the sprite's pivot sits (center,
        // bottom, …) by using the renderer's WORLD bounds. The visible feet are
        // `feetFromBottom` (sprite-local units × scale) above the frame's bottom edge
        // (sr.bounds.min.y). We shift the transform so the feet land on groundTop.
        public static void SnapToGround(Transform t, SpriteRenderer sr)
        {
            if (t == null || sr == null || sr.sprite == null) return;
            var ab = SpriteAlphaBounds.Get(sr.sprite);
            float spriteH = sr.sprite.bounds.size.y;
            float scale   = Mathf.Max(0.0001f, t.lossyScale.y);
            bool reliable = !(ab.bottomFromBottom <= 0.001f && ab.topFromBottom >= spriteH - 0.001f);

            float groundTop = FindGroundTopY();
            // World Y of the visible feet. When the alpha scan is unreliable, fall back
            // to the frame's bottom edge (assume the art reaches near the bottom).
            float feetWorld = reliable
                ? sr.bounds.min.y + ab.feetFromBottom * scale
                : sr.bounds.min.y;
            float shift = groundTop - feetWorld;

            var p = t.position;
            p.y += shift;
            t.position = p;
        }

        // Seats a CHARACTER (player / NPC / enemy) on the ground line. Two-step, fail-safe:
        //   1) Collider rest (known-safe): collider bottom → ground top. May leave a small
        //      visible float (authored foot colliders sit below the art's feet) but can
        //      never strand a character off-screen.
        //   2) Bounded correction: alpha-scanned visible feet, computed from the sprite
        //      PIVOT — NOT renderer bounds, which differ between Tight and FullRect
        //      meshes (and atlas trimming in builds) by the frame's transparent padding
        //      and flung characters a screen-height away. Clamped to ±0.25 WU so bad
        //      alpha data can only ever cause a small seat error, never a fly-away.
        // Non-trigger colliders are re-aimed so their bottom returns to the line
        // (otherwise gravity + the ground collider re-float the sprite).
        public static void SeatCharacterOnGround(Transform tr, SpriteRenderer sr)
        {
            if (tr == null) return;
            var col = tr.GetComponent<BoxCollider2D>();
            Physics2D.SyncTransforms();

            float groundTop = FindGroundTopY();
            float target    = groundTop + 0.002f;

            float baseline;
            if      (col != null)                     baseline = col.bounds.min.y;
            else if (sr != null && sr.sprite != null) baseline = sr.bounds.min.y;
            else return;
            tr.position += new Vector3(0f, target - baseline, 0f);
            Physics2D.SyncTransforms();

            if (sr != null && sr.sprite != null)
            {
                var sp = sr.sprite;
                var ab = SpriteAlphaBounds.Get(sp);
                float spriteH  = sp.bounds.size.y;
                bool  reliable = !(ab.bottomFromBottom <= 0.001f && ab.topFromBottom >= spriteH - 0.001f);
                if (reliable)
                {
                    float scale = Mathf.Max(0.0001f, Mathf.Abs(tr.lossyScale.y));
                    float ppu   = Mathf.Max(1f, sp.pixelsPerUnit);
                    // Feet relative to the pivot (== transform position), in world units.
                    float feetWorld  = tr.position.y + (ab.feetFromBottom - sp.pivot.y / ppu) * scale;
                    float correction = Mathf.Clamp(target - feetWorld, -0.25f, 0.25f);
                    if (Mathf.Abs(correction) > 0.0005f)
                    {
                        tr.position += new Vector3(0f, correction, 0f);
                        if (col != null && !col.isTrigger)
                        {
                            float feetLocal = (target - tr.position.y) / scale;
                            col.offset = new Vector2(col.offset.x, feetLocal + col.size.y * 0.5f);
                        }
                    }
                }
            }
            Physics2D.SyncTransforms();
        }
    }
}

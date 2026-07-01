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
    }
}

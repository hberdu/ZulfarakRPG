using UnityEngine;

namespace ZulfarakRPG
{
    // Snaps a static (no Rigidbody) GameObject so its sprite's visible bottom
    // pixels sit on `groundY`. Defensive: refuses to move if the alpha scan
    // returns fallback OR an implausible value, which previously stuck NPCs
    // at the top of the screen.
    [RequireComponent(typeof(SpriteRenderer))]
    public class GroundSnap : MonoBehaviour
    {
        public float groundY;
        public bool  verbose = true;  // DEBUG: enabled to diagnose top-of-screen issue

        void Start() { Snap(); }

        public void Snap()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;

            // If groundY wasn't set in the Inspector (still at default 0), fall back to
            // the scene's Ground sprite top edge so NPCs land on the correct floor line.
            float effectiveGroundY = (Mathf.Abs(groundY) < 0.001f)
                ? GroundAlignUtil.FindGroundTopY()
                : groundY;

            var ab = SpriteAlphaBounds.Get(sr.sprite);
            float spriteH = sr.sprite.bounds.size.y;
            float scale   = Mathf.Max(0.0001f, transform.lossyScale.y);

            bool isFallback = ab.bottomFromBottom <= 0.001f && ab.topFromBottom >= spriteH - 0.001f;

            float newY;
            if (isFallback)
            {
                float pivotYNorm = sr.sprite.rect.height > 0
                    ? sr.sprite.pivot.y / sr.sprite.rect.height : 0f;
                float pivotToBottom = pivotYNorm * spriteH * scale;
                newY = effectiveGroundY + pivotToBottom;
                if (verbose) Debug.Log($"[GroundSnap] {name}: pivot fallback groundY={effectiveGroundY:F3} pivotY={pivotYNorm:F2} newY={newY:F3}");
            }
            else
            {
                newY = effectiveGroundY - ab.feetFromBottom * scale;
                if (verbose) Debug.Log($"[GroundSnap] {name}: alpha snap groundY={effectiveGroundY:F3} feet={ab.feetFromBottom:F3} newY={newY:F3}");
            }

            var p = transform.position;
            transform.position = new Vector3(p.x, newY, p.z);
        }
    }
}

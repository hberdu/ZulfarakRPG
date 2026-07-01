using UnityEngine;

namespace ZulfarakRPG
{
    // "Got hit" feedback: a WHITE OUTLINE around the character that fades out over
    // time. Implemented as a slightly-larger white copy of the current sprite drawn
    // just BEHIND the real one — so only the enlarged border shows as a white edge.
    // The copy mirrors the live animation frame + facing every LateUpdate, so it
    // works for any sprite/animation. Used by PlayerController2D and SkeletonEnemy.
    //
    // Auto-attaches to the SpriteRenderer's GameObject the first time Flash is called;
    // subsequent calls just restart the fade on the same component.
    public class HurtFlash : MonoBehaviour
    {
        public static void Flash(SpriteRenderer sr, float duration = 0.35f, Color? tint = null)
        {
            if (sr == null) return;
            var c = sr.GetComponent<HurtFlash>();
            if (c == null) c = sr.gameObject.AddComponent<HurtFlash>();
            c.Begin(sr, duration, tint ?? Color.white);
        }

        SpriteRenderer _sr;
        SpriteRenderer _outline;
        Color _color;
        float _duration;
        float _t;
        bool  _running;

        void Begin(SpriteRenderer sr, float duration, Color tint)
        {
            _sr       = sr;
            _color    = tint;
            _duration = Mathf.Max(0.01f, duration);
            _t        = 0f;
            _running  = true;
            EnsureOutline();
            if (_outline != null) _outline.enabled = true;
        }

        void EnsureOutline()
        {
            if (_outline != null) return;
            var go = new GameObject("HurtOutline");
            go.transform.SetParent(_sr.transform, false);
            go.transform.localPosition = Vector3.zero;
            // 8% bigger so a white rim shows around the visible silhouette.
            go.transform.localScale = Vector3.one * 1.08f;
            _outline = go.AddComponent<SpriteRenderer>();
            _outline.sortingLayerID = _sr.sortingLayerID;
            _outline.sortingOrder   = _sr.sortingOrder - 1;   // behind the real sprite
            _outline.enabled        = false;
        }

        void LateUpdate()
        {
            if (!_running || _sr == null) return;
            _t += Time.deltaTime;
            float p = _t / _duration;
            if (p >= 1f)
            {
                if (_outline != null) _outline.enabled = false;
                _running = false;
                return;
            }
            if (_outline != null)
            {
                // Mirror the live frame + facing so the outline tracks the animation.
                _outline.sprite = _sr.sprite;
                _outline.flipX  = _sr.flipX;
                float a = 1f - p;                                  // fade out over time
                _outline.color = new Color(_color.r, _color.g, _color.b, a);
            }
        }
    }
}

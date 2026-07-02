using UnityEngine;
using TMPro;

namespace ZulfarakRPG
{
    // Sits as a child of any character GameObject.
    // After the character's sprite is known, call AttachAbove(sr) once to
    // resize the bar proportionally to the sprite width and position it
    // just above the character's head.
    public class WorldHealthBar : MonoBehaviour
    {
        [Header("Layout (overridden by AttachAbove)")]
        public float barWidth  = 0.70f;
        public float barHeight = 0.0333f;

        private Transform      _bgT;
        private Transform      _fillT;
        private Transform      _outlineT;
        private SpriteRenderer _fillSr;
        private float          _maxWidth;

        // Optional small name label rendered above the bar (player + remote
        // players + named NPCs). Created lazily by SetName.
        private TextMeshPro    _nameLabel;

        // Saved by AttachAbove so SetStaggerOffset() can re-apply without re-running
        // the alpha scan. Local-space (parent of this bar).
        private Vector3 _baseLocalPos;
        private float   _staggerY;

        void Awake()
        {
            // Background bar (dark red)
            var bg = MakeBar("HPBg",   new Color(0.20f, 0.0f, 0.0f, 0.90f), 0);
            _bgT = bg.transform;

            // Foreground fill (bright red)
            var fill = MakeBar("HPFill", new Color(0.85f, 0.10f, 0.10f, 1.0f), 2);
            _fillT  = fill.transform;
            _fillSr = fill.GetComponent<SpriteRenderer>();

            // Thin black outline around bg
            var outline = MakeBar("HPOutline", new Color(0f, 0f, 0f, 0.55f), 1);
            _outlineT = outline.transform;

            ApplyLayout();
        }

        // Reusable helper: bar width matches the VISIBLE character pixels (alpha scan,
        // cached); bar Y is ANCHORED to the parent's Collider2D.bounds.max.y — since the
        // wizard sizes the collider to match the visible character footprint, this puts
        // the bar exactly at the head top regardless of sprite frame padding.
        public void AttachAbove(SpriteRenderer target,
                                float padding         = 0.005f,
                                Color? fillColor      = null,
                                float widthMultiplier = 0.67f)
        {
            if (target == null || target.sprite == null || transform.parent == null) return;

            var ab     = SpriteAlphaBounds.Get(target.sprite);
            Vector3 parentScale = transform.parent.lossyScale;
            float parentScaleX  = parentScale.x > 1e-4f ? parentScale.x : 1f;
            float parentScaleY  = parentScale.y > 1e-4f ? parentScale.y : 1f;
            Vector3 targetScale = target.transform.lossyScale;

            // Width matches the visible character pixels exactly
            float visibleWidthWorld = ab.width * targetScale.x;
            float visibleWidthLocal = visibleWidthWorld / parentScaleX;
            barWidth = Mathf.Max(0.05f, visibleWidthLocal * widthMultiplier);

            // Anchor: parent's collider TOP (= visible head top by design).
            // Fallback to sprite bounds 60% if the parent has no collider.
            var parentCol = transform.parent.GetComponent<Collider2D>();
            float worldHeadY;
            float worldCenterX;
            if (parentCol != null)
            {
                worldHeadY   = parentCol.bounds.max.y;
                worldCenterX = parentCol.bounds.center.x;
            }
            else
            {
                var b = target.bounds;
                worldHeadY   = b.min.y + b.size.y * 0.60f;
                worldCenterX = b.center.x;
            }
            float worldBarY  = worldHeadY + padding + (barHeight * 0.5f * parentScaleY);
            Vector3 localPos = transform.parent.InverseTransformPoint(
                new Vector3(worldCenterX, worldBarY, 0f));
            _baseLocalPos = new Vector3(localPos.x, localPos.y, -0.1f);
            transform.localPosition = _baseLocalPos + new Vector3(0f, _staggerY, 0f);

            if (fillColor.HasValue && _fillSr != null)
                _fillSr.color = fillColor.Value;

            ApplyLayout();
        }

        // Lifts the bar by a small Y delta so overlapping enemies' bars don't stack
        // perfectly on top of each other. Caller passes a per-frame value.
        public void SetStaggerOffset(float yOffset)
        {
            _staggerY = yOffset;
            transform.localPosition = _baseLocalPos + new Vector3(0f, _staggerY, 0f);
        }

        // Renders a small bold label just above the bar (player + remote players +
        // named NPCs). Pass null/empty to hide it.
        public void SetName(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                if (_nameLabel != null) _nameLabel.gameObject.SetActive(false);
                return;
            }
            if (_nameLabel == null)
            {
                var go = new GameObject("Name");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.045f, -0.1f);
                _nameLabel              = go.AddComponent<TextMeshPro>();
                _nameLabel.fontSize     = 0.32f;
                _nameLabel.alignment    = TextAlignmentOptions.Center;
                _nameLabel.color        = new Color(0.98f, 0.94f, 0.80f, 1f);
                _nameLabel.fontStyle    = FontStyles.Bold;
                _nameLabel.enableWordWrapping = false;
                // Set the final font FIRST so GameFont's re-skin pass skips this label and
                // doesn't reset the material (which would wipe the outline below).
                if (GameFont.Tmp != null) _nameLabel.font = GameFont.Tmp;
                // Solid black outline so the name reads over any background.
                var nmat = _nameLabel.fontMaterial;
                nmat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
                nmat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                _nameLabel.UpdateMeshPadding();
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 10;
            }
            _nameLabel.text = label;
            _nameLabel.gameObject.SetActive(true);
        }

        // Visible bounds (width + head Y) are pixel-scanned and cached by
        // SpriteAlphaBounds; AttachAbove just consumes the result.

        public void SetHealth(float current, float max)
        {
            if (_fillT == null) return;
            float pct = max > 0 ? Mathf.Clamp01(current / max) : 0f;
            float w   = _maxWidth * pct;
            var s     = _fillT.localScale;
            s.x       = w;
            _fillT.localScale    = s;
            _fillT.localPosition = new Vector3((w - _maxWidth) * 0.5f, 0f, 0f);
        }

        void ApplyLayout()
        {
            if (_bgT == null) return;
            _bgT.localScale       = new Vector3(barWidth,         barHeight,         1f);
            _fillT.localScale     = new Vector3(barWidth,         barHeight * 0.65f, 1f);
            _outlineT.localScale  = new Vector3(barWidth + 0.03f, barHeight + 0.02f, 1f);
            _bgT.localPosition       = Vector3.zero;
            _fillT.localPosition     = Vector3.zero;
            _outlineT.localPosition  = Vector3.zero;
            _maxWidth = barWidth;
        }

        GameObject MakeBar(string name, Color color, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = Pixel();
            sr.color        = color;
            sr.sortingOrder = sortOrder;
            return go;
        }

        static Sprite Pixel()
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}

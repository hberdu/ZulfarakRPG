using TMPro;
using UnityEngine;

namespace ZulfarakRPG
{
    // Floating damage number that rises above a character and fades out.
    // Has a black outline + drop shadow for readability over any background, and a
    // dedicated CRITICAL style: bright yellow, a "*" prefix, and a bigger pop.
    public class DamagePopup : MonoBehaviour
    {
        public float duration     = 0.45f;
        public float riseDistance = 0.35f;

        private Vector3      _startPos;
        private TextMeshPro  _tmp;
        private Transform    _shadow;
        private Color        _baseColor;
        private float        _t;
        private float        _popScale;

        // Back-compat overload (no crit) — defaults to a normal hit.
        public static void Spawn(Transform target, float amount, Color color)
            => Spawn(target, amount, color, false);

        public static void Spawn(Transform target, float amount, Color color, bool crit)
        {
            if (target == null || amount <= 0f) return;

            Vector3 headPos;
            var col = target.GetComponent<Collider2D>();
            if (col != null)
                headPos = new Vector3(col.bounds.center.x, col.bounds.center.y, target.position.z);
            else
                headPos = target.position + Vector3.up * 0.4f;

            var go = new GameObject("DamagePopup");
            go.transform.position = headPos + new Vector3(
                Random.Range(-0.10f, 0.10f), 0f, -0.4f);

            string text  = Mathf.RoundToInt(amount).ToString();
            Color  shown = crit ? new Color(1f, 0.92f, 0.20f, 1f) : color;  // crits = yellow
            if (crit) text = "*" + text;                                    // crit marker

            // Shadow copy (black, offset down-right, drawn just behind the main label).
            var shadowGO = new GameObject("Shadow");
            shadowGO.transform.SetParent(go.transform, false);
            shadowGO.transform.localPosition = new Vector3(0.035f, -0.035f, 0.05f);
            var shTmp = StyleText(shadowGO.AddComponent<TextMeshPro>(), text,
                                  crit ? 2.9f : 2.2f, new Color(0f, 0f, 0f, 0.85f), outline: false);
            var shMr = shadowGO.GetComponent<MeshRenderer>();
            if (shMr != null) shMr.sortingOrder = 19;

            // Main label with black outline.
            var tmp = StyleText(go.AddComponent<TextMeshPro>(), text,
                                crit ? 2.9f : 2.2f, shown, outline: true);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 20;   // above HP bars and tooltips

            var pop = go.AddComponent<DamagePopup>();
            pop._shadow   = shadowGO.transform;
            pop._popScale = crit ? 1.35f : 1f;   // crits start bigger and settle
        }

        static TextMeshPro StyleText(TextMeshPro tmp, string text, float size, Color color, bool outline)
        {
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = color;
            tmp.fontStyle = FontStyles.Bold;
            if (outline)
            {
                // outlineWidth/outlineColor instance the material — a thin black ring.
                tmp.outlineWidth = 0.25f;
                tmp.outlineColor = new Color(0f, 0f, 0f, 1f);
            }
            return tmp;
        }

        void Start()
        {
            _startPos  = transform.position;
            _tmp       = GetComponent<TextMeshPro>();
            _baseColor = _tmp != null ? _tmp.color : Color.white;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float p = _t / duration;
            if (p >= 1f) { Destroy(gameObject); return; }

            transform.position = _startPos + Vector3.up * (riseDistance * p);

            // Crit "pop": overshoot then settle to 1.0 in the first ~25% of the life.
            float settle = Mathf.Clamp01(p / 0.25f);
            float scale  = Mathf.Lerp(_popScale, 1f, settle);
            transform.localScale = Vector3.one * scale;

            float alpha = 1f - p;
            if (_tmp != null)
            {
                var c = _baseColor; c.a = alpha; _tmp.color = c;
            }
            if (_shadow != null)
            {
                var sTmp = _shadow.GetComponent<TextMeshPro>();
                if (sTmp != null) { var sc = sTmp.color; sc.a = 0.85f * alpha; sTmp.color = sc; }
            }
        }
    }
}

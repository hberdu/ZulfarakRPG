using UnityEngine;

namespace ZulfarakRPG
{
    // A homing arrow projectile spawned by the Archer class. Travels toward a
    // SkeletonEnemy target, deals damage on hit, and spawns an ImpactEffect at
    // the impact point. Sprite + impact are generated procedurally (no assets).
    public class Arrow : MonoBehaviour
    {
        public float speed       = 14f;
        public float damage      = 25f;
        public float maxLifetime = 1.5f;
        public float hitDistance = 0.45f;

        private SkeletonEnemy _target;
        private float          _spawnTime;
        private SpriteRenderer _sr;
        private bool           _isCrit;

        static Sprite _arrowSprite;
        static Sprite _impactSprite;

        public void Init(SkeletonEnemy target, float damage, bool isCrit = false)
        {
            _target     = target;
            this.damage = damage;
            _isCrit     = isCrit;
            _spawnTime  = Time.time;

            if (_arrowSprite == null) _arrowSprite = LoadArrowSprite();
            _sr             = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite      = _arrowSprite;
            _sr.sortingOrder = 5;
            // Arrow03 is a 32×32 frame — scale down so it reads as a slim projectile.
            transform.localScale = Vector3.one * 0.7f;
        }

        void Update()
        {
            if (Time.time - _spawnTime > maxLifetime) { Destroy(gameObject); return; }
            if (_target == null || !_target.IsAlive)  { Destroy(gameObject); return; }

            // Aim at the enemy's COLLIDER CENTER — since the wizard sizes the collider
            // to match the visible character, the collider center is the visible body.
            var col = _target.GetComponent<Collider2D>();
            Vector3 targetPos = col != null
                ? col.bounds.center
                : _target.transform.position + Vector3.up * 0.5f;

            Vector3 toTarget = targetPos - transform.position;
            float   dist     = toTarget.magnitude;

            if (dist < hitDistance)
            {
                _target.TakeDamage(damage, _isCrit);
                SpawnImpact(targetPos);
                Destroy(gameObject);
                return;
            }

            Vector3 dir = toTarget / dist;
            transform.position += dir * speed * Time.deltaTime;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        static void SpawnImpact(Vector3 pos)
        {
            if (_impactSprite == null) _impactSprite = MakeImpactSprite();
            var go = new GameObject("ArrowImpact");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _impactSprite;
            sr.color        = new Color(1f, 0.95f, 0.45f, 1f);
            sr.sortingOrder = 8;
            go.AddComponent<ImpactEffect>();
        }

        // Arrow03 from the Tiny RPG pack (Resources/Arrow03), points right like the
        // procedural one, so the existing aim-rotation works unchanged. Falls back to
        // the procedural sprite if the asset is missing.
        static Sprite LoadArrowSprite()
        {
            var s = Resources.Load<Sprite>("Arrow03");
            return s != null ? s : MakeArrowSprite();
        }

        // ── Procedural sprites ────────────────────────────────────────────────
        // 32×9 at 100 PPU so each arrow pixel matches a character-sprite pixel
        // (every character PNG in the project is imported at 100 PPU, Point filter).
        static Sprite MakeArrowSprite()
        {
            const int W = 32, H = 9;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    t.SetPixel(x, y, Color.clear);

            var shaft   = new Color(0.42f, 0.26f, 0.10f, 1f); // dark wood
            var shaftHi = new Color(0.65f, 0.45f, 0.20f, 1f); // top-lit wood highlight
            var headDk  = new Color(0.45f, 0.45f, 0.50f, 1f); // steel edge / base
            var headLt  = new Color(0.82f, 0.84f, 0.88f, 1f); // steel face
            var headHi  = new Color(1.00f, 1.00f, 1.00f, 1f); // specular highlight
            var fletch  = new Color(0.85f, 0.20f, 0.20f, 1f); // feather red
            var fletchD = new Color(0.55f, 0.10f, 0.10f, 1f); // feather shadow

            // Shaft: 3 px tall, columns 3..24
            for (int x = 3; x <= 24; x++)
            {
                t.SetPixel(x, 5, shaftHi);
                t.SetPixel(x, 4, shaft);
                t.SetPixel(x, 3, shaft);
            }

            // Arrowhead (leaf shape, tip on the right at x=31)
            for (int y = 2; y <= 6; y++) t.SetPixel(25, y, headDk);
            t.SetPixel(26, 1, headDk); t.SetPixel(26, 7, headDk);
            for (int y = 2; y <= 6; y++) t.SetPixel(26, y, headLt);
            for (int y = 2; y <= 6; y++) t.SetPixel(27, y, headLt);
            for (int y = 3; y <= 5; y++) t.SetPixel(28, y, headLt);
            t.SetPixel(28, 4, headHi);
            for (int y = 3; y <= 5; y++) t.SetPixel(29, y, headLt);
            t.SetPixel(30, 4, headLt);
            t.SetPixel(31, 4, headHi);

            // Fletching: V-shape feathers on the back, columns 0..2
            t.SetPixel(0, 0, fletchD); t.SetPixel(0, 8, fletchD);
            for (int y = 1; y <= 7; y++) t.SetPixel(0, y, fletch);
            t.SetPixel(1, 1, fletchD); t.SetPixel(1, 7, fletchD);
            for (int y = 2; y <= 6; y++) t.SetPixel(1, y, fletch);
            t.SetPixel(2, 2, fletchD); t.SetPixel(2, 6, fletchD);
            for (int y = 3; y <= 5; y++) t.SetPixel(2, y, fletch);

            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeImpactSprite()
        {
            const int W = 24, H = 24;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
            float maxR = W * 0.5f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > maxR) { t.SetPixel(x, y, Color.clear); continue; }
                    float a = 1f - d / maxR;
                    // Hot core → orange → faint outer ring
                    Color c = a > 0.80f ? new Color(1f, 1f, 0.85f, 1f)
                            : a > 0.40f ? new Color(1f, 0.80f, 0.25f, a)
                            :              new Color(1f, 0.45f, 0.10f, a * 0.85f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 64f);
        }
    }

    // Brief expanding burst that fades out — attached to the impact GO.
    public class ImpactEffect : MonoBehaviour
    {
        public float duration   = 0.28f;
        public float startScale = 0.35f;
        public float endScale   = 1.10f;

        private float _t;
        private SpriteRenderer _sr;

        void Start()
        {
            _sr = GetComponent<SpriteRenderer>();
            transform.localScale = Vector3.one * startScale;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float p = _t / duration;
            if (p >= 1f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, p);
            if (_sr != null)
            {
                var c = _sr.color;
                c.a = 1f - p;
                _sr.color = c;
            }
        }
    }
}

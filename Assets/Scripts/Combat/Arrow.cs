using UnityEngine;

namespace ZulfarakRPG
{
    // A homing arrow projectile spawned by the Archer class. Travels toward a
    // SkeletonEnemy target, deals damage on hit, and spawns an ImpactEffect at
    // the impact point. Sprite + impact are generated procedurally (no assets).
    public class Arrow : MonoBehaviour
    {
        public float speed       = 5f;   // much slower so the shot reads as a deliberate charge
        public float damage      = 25f;
        public float maxLifetime = 1.5f;
        public float hitDistance = 0.45f;

        private SkeletonEnemy _target;
        private float          _spawnTime;
        private SpriteRenderer _sr;
        private bool           _isCrit;

        static Sprite _arrowSprite;
        static Sprite _impactSprite;

        // On-screen length of the arrow, in WORLD units (the sprite's longest side is scaled
        // to span this). ONE number used by the basic shot AND every skill projectile so all
        // arrows are the exact same size. Kept small (~the archer's width) for a realistic shot.
        public const float TargetWorldSize = 0.75f;

        // customSprite: a specific arrow from the pack's Arrow(Projectile) folder, cycled
        // by the archer so successive shots differ. Null → the shared default arrow.
        public void Init(SkeletonEnemy target, float damage, bool isCrit = false, Sprite customSprite = null)
        {
            _target     = target;
            this.damage = damage;
            _isCrit     = isCrit;
            _spawnTime  = Time.time;

            Sprite sprite = customSprite != null ? customSprite : (_arrowSprite ??= LoadArrowSprite());
            _sr             = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite      = sprite;
            _sr.sortingOrder = 300;  // foreground: arrow always overlaps player + enemies + FX
            ApplyWorldSize(transform, sprite, TargetWorldSize);
        }

        // The arrow art the basic shot falls back to (Resources/Arrow03, else the procedural
        // arrow). Exposed so the Archer's SKILL projectiles (Serpe, Chuva) can fly the SAME
        // arrow sprite instead of a separate dart.
        public static Sprite SharedSprite => _arrowSprite ??= LoadArrowSprite();

        // Scales `t` so `sprite`'s longest side spans `worldSize` world units, normalising away
        // the different pixel dimensions of the pack arrows vs the fallback. Static so the skill
        // projectiles can size their arrows exactly like the basic shot.
        public static void ApplyWorldSize(Transform t, Sprite sprite, float worldSize)
        {
            float px  = sprite != null ? Mathf.Max(sprite.rect.width, sprite.rect.height) : 0f;
            float ppu = sprite != null ? sprite.pixelsPerUnit : 0f;
            t.localScale = (px <= 0f || ppu <= 0f) ? Vector3.one : Vector3.one * (worldSize / (px / ppu));
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
                // Point-blank (enemy in melee): hover a moment so the arrow is actually SEEN
                // before it strikes, instead of hitting + despawning on the very first frame.
                if (Time.time - _spawnTime < 0.05f) return;
                _target.TakeDamage(damage, _isCrit);
                MultiplayerSync.Instance?.BroadcastDamage(_target.netInstanceId, damage, _isCrit);
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

        // The pack's Arrow02 (Archer/Arrow(projectile)/Arrow02(32x32)) — the real arrow art,
        // points right (+x). Cropped tight to its 19×7 body so TargetWorldSize sizes the visible
        // arrow directly. Used by the basic shot AND every skill. Falls back to the procedural one.
        static Sprite LoadArrowSprite()
        {
            var tex = Resources.Load<Texture2D>("Arrow02");
            if (tex == null) return MakeArrowSprite();
            // Unity sprite rects are bottom-up; the arrow bbox is x8..26, y13..19 (top-down).
            return Sprite.Create(tex, new Rect(8, 12, 19, 8), new Vector2(0.5f, 0.5f), 100f);
        }

        // ── Procedural sprite (flat, no shading) ──────────────────────────────
        // 32×9 at 100 PPU so each arrow pixel matches a character-sprite pixel.
        static Sprite MakeArrowSprite()
        {
            const int W = 32, H = 9;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    t.SetPixel(x, y, Color.clear);

            var shaft  = new Color(0.42f, 0.26f, 0.10f, 1f); // flat wood
            var head   = new Color(0.78f, 0.80f, 0.84f, 1f); // flat steel
            var fletch = new Color(0.82f, 0.20f, 0.20f, 1f); // flat feather red

            // Shaft: 3 px tall, columns 3..24 (single flat colour)
            for (int x = 3; x <= 24; x++)
                for (int y = 3; y <= 5; y++) t.SetPixel(x, y, shaft);

            // Arrowhead (flat leaf, tip on the right at x=31)
            for (int y = 2; y <= 6; y++) t.SetPixel(25, y, head);
            t.SetPixel(26, 1, head); t.SetPixel(26, 7, head);
            for (int y = 2; y <= 6; y++) t.SetPixel(26, y, head);
            for (int y = 2; y <= 6; y++) t.SetPixel(27, y, head);
            for (int y = 3; y <= 5; y++) t.SetPixel(28, y, head);
            for (int y = 3; y <= 5; y++) t.SetPixel(29, y, head);
            t.SetPixel(30, 4, head); t.SetPixel(31, 4, head);

            // Fletching: flat V-shape feathers on the back, columns 0..2
            for (int y = 0; y <= 8; y++) t.SetPixel(0, y, fletch);
            for (int y = 1; y <= 7; y++) t.SetPixel(1, y, fletch);
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

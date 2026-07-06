using UnityEngine;

namespace ZulfarakRPG
{
    // Fireball projectile spawned by the Mage class — long-range, mirroring the Archer's
    // Arrow. Travels toward a SkeletonEnemy, deals damage on hit, and bursts into a fiery
    // ImpactEffect (reused from Arrow.cs).
    //
    // The visual AUTO-UPGRADES: drop an animated flame sheet at Resources/Fireball (a
    // Godot-exported horizontal strip of square frames) and it is sliced + looped. Until
    // then a procedurally-drawn, flickering orb is used so the ranged attack is fully
    // functional without any art.
    public class Fireball : MonoBehaviour
    {
        public float speed       = 11f;
        public float damage      = 25f;
        public float maxLifetime = 2.0f;
        public float hitDistance = 0.45f;

        private SkeletonEnemy  _target;
        private float          _spawnTime;
        private SpriteRenderer _sr;
        private bool           _isCrit;
        // Per-cast magic-effect sheet from the pack's Magic(Projectile) folder. When set
        // the mage's actual spell art is used (and the projectile is only flipped toward
        // the target, not rotated, since the effect is authored horizontally); otherwise
        // the shared procedural flame orb below is used.
        private Sprite[]       _frames;
        private bool           _usingArt;

        const float Fps = 14f;
        static Sprite[] _procFrames;
        static Sprite   _impactSprite;
        static Sprite   _emberSprite;

        public void Init(SkeletonEnemy target, float damage, bool isCrit = false, Sprite[] magicFrames = null)
        {
            _target     = target;
            this.damage = damage;
            _isCrit     = isCrit;
            _spawnTime  = Time.time;

            _usingArt = magicFrames != null && magicFrames.Length > 0;
            _frames   = _usingArt ? magicFrames : (_procFrames ??= LoadFrames());
            _sr              = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite       = _frames[0];
            _sr.sortingOrder = 6;   // above the arrow (5) so a mixed party still reads clearly
            // Effect art is 100px @100PPU (1 world unit) — scale down to projectile size;
            // the procedural orb is already small.
            transform.localScale = Vector3.one * (_usingArt ? 0.55f : 1f);
            if (_usingArt && _target != null)
                _sr.flipX = _target.transform.position.x < transform.position.x;
        }

        void Update()
        {
            if (Time.time - _spawnTime > maxLifetime) { Destroy(gameObject); return; }
            if (_target == null || !_target.IsAlive)  { Destroy(gameObject); return; }

            // Loop the flame animation (magic sheet, Godot sheet, or procedural flicker frames).
            if (_frames.Length > 1)
                _sr.sprite = _frames[(int)((Time.time - _spawnTime) * Fps) % _frames.Length];

            // Aim at the enemy's COLLIDER CENTER (visible body — collider matches art).
            var col = _target.GetComponent<Collider2D>();
            Vector3 targetPos = col != null
                ? col.bounds.center
                : _target.transform.position + Vector3.up * 0.5f;

            Vector3 toTarget = targetPos - transform.position;
            float   dist     = toTarget.magnitude;

            if (dist < hitDistance)
            {
                _target.TakeDamage(damage, _isCrit);
                MultiplayerSync.Instance?.BroadcastDamage(_target.netInstanceId, damage, _isCrit);
                SpawnImpact(targetPos);
                Destroy(gameObject);
                return;
            }

            Vector3 dir = toTarget / dist;
            transform.position += dir * speed * Time.deltaTime;
            // Hand-drawn magic art is authored horizontally, so only flip it toward travel;
            // the procedural orb has a tail, so rotate it to aim.
            if (!_usingArt)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);   // tail trails behind
            }
        }

        // ── Frame loading ──────────────────────────────────────────────────────
        // Resources/Fireball as a horizontal strip of square frames (Godot export),
        // else the procedural flickering orb.
        static Sprite[] LoadFrames()
        {
            var tex = Resources.Load<Texture2D>("Fireball");
            if (tex == null)
            {
                var s = Resources.Load<Sprite>("Fireball");
                if (s != null) tex = s.texture;
            }
            if (tex != null && tex.height > 0 && tex.width >= tex.height)
            {
                int fh    = tex.height;
                int count = Mathf.Max(1, tex.width / fh);
                var frames = new Sprite[count];
                for (int i = 0; i < count; i++)
                    frames[i] = Sprite.Create(tex, new Rect(i * fh, 0, fh, fh),
                                              new Vector2(0.5f, 0.5f), 100f);
                return frames;
            }
            return MakeProceduralFrames(4);
        }

        void SpawnImpact(Vector3 pos)
        {
            if (_impactSprite == null) _impactSprite = MakeImpactSprite();
            var go = new GameObject("FireballImpact");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _impactSprite;
            sr.color        = new Color(1f, 0.55f, 0.18f, 1f);
            sr.sortingOrder = 8;
            go.AddComponent<ImpactEffect>();   // expanding-fade burst, defined in Arrow.cs
        }

        // ── Staff cast flash ────────────────────────────────────────────────────
        // A punchy fiery burst rendered at the staff tip the moment the mage releases a
        // fireball: a hot expanding bloom, a white-hot inner core, and a spray of embers
        // shooting toward the target. Called from PlayerController2D.FireFireball so the
        // cast reads as a genuine surge of flame from the cajado.
        public static void SpawnCastFlash(Vector3 pos, float faceDir)
        {
            if (_impactSprite == null) _impactSprite = MakeImpactSprite();
            if (_emberSprite  == null) _emberSprite  = MakeEmberSprite();

            // Outer bloom — warm orange, expands wide and fades quickly.
            var bloom = new GameObject("FireballCastBloom");
            bloom.transform.position = pos;
            var bsr = bloom.AddComponent<SpriteRenderer>();
            bsr.sprite       = _impactSprite;
            bsr.color        = new Color(1f, 0.55f, 0.18f, 1f);
            bsr.sortingOrder = 9;
            var bfx = bloom.AddComponent<ImpactEffect>();
            bfx.duration = 0.24f; bfx.startScale = 0.25f; bfx.endScale = 1.8f;

            // Inner core — white-hot, smaller, sharper, on top of the bloom.
            var core = new GameObject("FireballCastCore");
            core.transform.position = pos;
            var csr = core.AddComponent<SpriteRenderer>();
            csr.sprite       = _impactSprite;
            csr.color        = new Color(1f, 0.97f, 0.82f, 1f);
            csr.sortingOrder = 11;
            var cfx = core.AddComponent<ImpactEffect>();
            cfx.duration = 0.16f; cfx.startScale = 0.12f; cfx.endScale = 0.85f;

            // Ember spray — a cone of little flames launched toward the target (+faceDir),
            // arcing up then falling under gravity as they cool from yellow to red.
            const int emberCount = 12;
            float baseAngle = faceDir >= 0f ? 0f : 180f;
            for (int i = 0; i < emberCount; i++)
            {
                var e = new GameObject("FireballEmber");
                e.transform.position = pos;
                var esr = e.AddComponent<SpriteRenderer>();
                esr.sprite       = _emberSprite;
                esr.sortingOrder = 10;
                float ang = (baseAngle + Random.Range(-50f, 50f)) * Mathf.Deg2Rad;
                Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.7f + 0.55f)
                              * Random.Range(1.6f, 3.6f);
                var ep = e.AddComponent<EmberParticle>();
                ep.velocity = vel;
                ep.lifetime = Random.Range(0.26f, 0.52f);
            }
        }

        // ── Procedural fireball ─────────────────────────────────────────────────
        // Deliberately CHUNKY: 11×9 @ 40 PPU. Fewer, bigger pixels than a smooth orb —
        // 3 flat colour bands + a short 2-pixel tail that flickers between two spread
        // widths per frame so the projectile reads as blocky pixel art at any zoom.
        // Orb sits toward the front (+x); once rotated to face travel the tail trails.
        static Sprite[] MakeProceduralFrames(int count)
        {
            const int W = 11, H = 9;
            int cx = W - 4;                         // orb centre near the leading edge
            int cy = H / 2;
            var frames = new Sprite[count];
            var white  = new Color(1f, 0.95f, 0.72f, 1f);
            var amber  = new Color(1f, 0.66f, 0.16f, 1f);
            var scarlet= new Color(0.95f, 0.34f, 0.08f, 1f);
            var tail   = new Color(1f, 0.55f, 0.12f, 1f);
            var tailDk = new Color(0.90f, 0.30f, 0.06f, 1f);

            for (int f = 0; f < count; f++)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        t.SetPixel(x, y, Color.clear);

                // Orb: three concentric flat bands using integer distances (chunky steps).
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int dx = x - cx, dy = y - cy;
                    int d2 = dx * dx + dy * dy;
                    if (d2 <= 1)      t.SetPixel(x, y, white);
                    else if (d2 <= 4) t.SetPixel(x, y, amber);
                    else if (d2 <= 9) t.SetPixel(x, y, scarlet);
                }

                // Tail flickers between two heights over the frames so it feels alive.
                bool wide = f % 2 == 0;
                int tailLen = 4;
                for (int i = 1; i <= tailLen; i++)
                {
                    int tx = cx - 2 - i;
                    if (tx < 0) break;
                    Color c = i <= 1 ? tail : tailDk;
                    t.SetPixel(tx, cy, c);
                    if (wide && i <= 2)
                    {
                        t.SetPixel(tx, cy + 1, tailDk);
                        t.SetPixel(tx, cy - 1, tailDk);
                    }
                }

                t.Apply();
                frames[f] = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 40f);
            }
            return frames;
        }

        // Chunky pixel impact: 11×11 @ 24 PPU (roughly the same world size as the old
        // 26/64 sprite, but with FAR bigger blocks) drawn as an 8-ray plus-shaped burst
        // — cardinals + diagonals — instead of a smooth ring. Cross rays taper from a
        // hot white-yellow core through orange to deep red, giving the classic pixel-art
        // "starburst" that reads instantly at any zoom.
        static Sprite MakeImpactSprite()
        {
            const int W = 11, H = 11;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            int cx = W / 2, cy = H / 2;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                t.SetPixel(x, y, Color.clear);

            var core = new Color(1f, 0.97f, 0.80f, 1f);
            var mid  = new Color(1f, 0.62f, 0.20f, 1f);
            var edge = new Color(0.95f, 0.28f, 0.08f, 1f);

            // Cardinal + diagonal rays as stepped pixel lines with a hot core.
            for (int r = 0; r <= 5; r++)
            {
                Color c = r <= 1 ? core : (r <= 3 ? mid : edge);
                if (r == 0)
                {
                    t.SetPixel(cx, cy, core);
                    continue;
                }
                // Cardinal arms.
                t.SetPixel(cx + r, cy,     c);
                t.SetPixel(cx - r, cy,     c);
                t.SetPixel(cx,     cy + r, c);
                t.SetPixel(cx,     cy - r, c);
                // Diagonal arms (shorter — reach r-1 out) so the star has a plus emphasis.
                if (r <= 4)
                {
                    int d = r - 1;
                    if (d >= 0)
                    {
                        t.SetPixel(cx + d, cy + d, c);
                        t.SetPixel(cx - d, cy + d, c);
                        t.SetPixel(cx + d, cy - d, c);
                        t.SetPixel(cx - d, cy - d, c);
                    }
                }
            }
            // Chunky core square so the centre reads as solid pixels rather than a dot.
            for (int y = cy - 1; y <= cy + 1; y++)
                for (int x = cx - 1; x <= cx + 1; x++)
                    t.SetPixel(x, y, core);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 24f);
        }

        // Small chunky dot for the cast-flash embers (3×3 @ 20 PPU) — one flat colour
        // with a tiny highlight top-left so it reads as a stubby cube of flame, not a
        // sub-pixel dot.
        static Sprite MakeEmberSprite()
        {
            const int N = 3;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var body = new Color(1f, 0.70f, 0.25f, 1f);
            var hi   = new Color(1f, 0.95f, 0.65f, 1f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    t.SetPixel(x, y, body);
            t.SetPixel(0, N - 1, hi);   // top-left highlight
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 20f);
        }
    }

    // A single flame ember flung from the staff on cast: arcs under gravity and cools
    // from yellow-white to deep red as it fades out. Purely cosmetic, self-destructs.
    public class EmberParticle : MonoBehaviour
    {
        public Vector2 velocity;
        public float   lifetime = 0.4f;
        public float   gravity  = 5f;

        float          _t;
        SpriteRenderer _sr;

        void Start()
        {
            _sr = GetComponent<SpriteRenderer>();
            transform.localScale = Vector3.one * Random.Range(0.55f, 1.15f);
        }

        void Update()
        {
            _t += Time.deltaTime;
            float p = _t / lifetime;
            if (p >= 1f) { Destroy(gameObject); return; }

            velocity.y -= gravity * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);

            if (_sr != null)
            {
                Color c = Color.Lerp(new Color(1f, 0.9f, 0.55f), new Color(0.9f, 0.22f, 0.05f), p);
                c.a = 1f - p;
                _sr.color = c;
            }
        }
    }
}

using UnityEngine;

namespace ZulfarakRPG
{
    // Projectiles for the Archer's reworked skills. All are self-contained (procedural
    // sprites, no assets) and route their damage through SkeletonEnemy + MultiplayerSync
    // so co-op stays in sync.
    public static class SkillDart
    {
        static Sprite _dart;

        // A small chunky arrow/dart pointing +x (right), tinted at runtime by the caller.
        public static Sprite Get()
        {
            if (_dart != null) return _dart;
            const int W = 13, H = 7;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) t.SetPixel(x, y, Color.clear);
            var body = Color.white; // tinted via SpriteRenderer.color
            // Shaft
            for (int x = 1; x <= 8; x++) t.SetPixel(x, 3, body);
            for (int x = 2; x <= 7; x++) t.SetPixel(x, 2, body);
            // Arrow head (right)
            t.SetPixel(9, 3, body); t.SetPixel(10, 2, body); t.SetPixel(10, 3, body); t.SetPixel(10, 4, body);
            t.SetPixel(11, 3, body); t.SetPixel(9, 2, body); t.SetPixel(9, 4, body);
            // Fletching (left)
            t.SetPixel(0, 1, body); t.SetPixel(0, 5, body); t.SetPixel(1, 2, body); t.SetPixel(1, 4, body);
            t.Apply();
            _dart = UnityEngine.Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return _dart;
        }
    }

    // A white spread-winged EAGLE used as the Tiro Concentrado charge telegraph (the archer's
    // counterpart to the Serpe's serpent). Procedural + self-contained: a symmetric silhouette
    // redrawn per wing-raise so a short frame loop reads as a slow, powerful wing-beat. Faces +x;
    // the caster flips it toward the target.
    public static class SkillEagle
    {
        const int W = 27, H = 22, CX = 13;
        static Sprite[] _frames;
        static Sprite   _aura;

        public static Sprite[] Frames()
        {
            if (_frames != null) return _frames;
            int[] raise = { 16, 14, 12, 14 };   // wing-tip Y: high → mid → low → mid (beat loop)
            _frames = new Sprite[raise.Length];
            for (int i = 0; i < raise.Length; i++) _frames[i] = Build(raise[i]);
            Debug.Assert(_frames.Length > 0 && _frames[0] != null, "[SkillEagle] frame build failed");
            return _frames;
        }

        static Sprite Build(int wingTipY)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var m = new bool[W, H];

            // Body (tail fan → chest → neck → head), symmetric about CX.
            FillCol(m, 0, 0, 0, 3);      // tail feathers (widest at the bottom)
            FillCol(m, 1, 1, 0, 2);
            FillCol(m, 2, 2, 0, 1);
            FillCol(m, 3, 5, 0, 1);      // body
            FillCol(m, 6, 9, 0, 2);      // chest (wider)
            FillCol(m, 10, 10, 0, 2);    // shoulders (wing roots)
            FillCol(m, 11, 13, 0, 1);    // neck
            FillCol(m, 14, 17, 0, 1);    // head
            Plot(m, CX, 18);             // crown
            Plot(m, CX + 2, 15); Plot(m, CX + 3, 15);   // beak (points +x)

            // Wings: thick bands from the shoulders out+up to the tips, mirrored.
            ThickLine(m, CX + 2, 10, W - 2, wingTipY, 2);
            ThickLine(m, CX - 2, 10, 1,     wingTipY, 2);

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    t.SetPixel(x, y, m[x, y] ? Color.white : Color.clear);
            t.Apply();
            return UnityEngine.Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

        // Soft white radial aura placed behind the eagle.
        public static Sprite Aura()
        {
            if (_aura != null) return _aura;
            const int S = 32;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (S - 1) * 0.5f, maxR = S * 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(1f - d / maxR);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));   // soft falloff
                }
            t.Apply();
            _aura = UnityEngine.Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return _aura;
        }

        static void FillCol(bool[,] m, int y0, int y1, int dx0, int dx1)
        {
            for (int y = y0; y <= y1; y++)
                for (int dx = dx0; dx <= dx1; dx++) { Plot(m, CX + dx, y); Plot(m, CX - dx, y); }
        }

        static void ThickLine(bool[,] m, int x0, int y0, int x1, int y1, int r)
        {
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy;
            while (true)
            {
                for (int ox = -r; ox <= r; ox++)
                    for (int oy = -r; oy <= r; oy++) Plot(m, x0 + ox, y0 + oy);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 <  dx) { err += dx; y0 += sy; }
            }
        }

        static void Plot(bool[,] m, int x, int y)
        {
            if (x >= 0 && x < W && y >= 0 && y < H) m[x, y] = true;
        }
    }

    // "Tiro de Serpe": a green arrow that snakes toward the target in a zig-zag, deals the
    // archer's normal attack damage on hit, and leaves a poison DoT (30% of attack per
    // second for 4 s).
    public class SerpentArrow : MonoBehaviour
    {
        SkeletonEnemy _target;       // real cast (applies damage); null for cosmetic replay
        Vector3 _fixedTarget;        // used when _target == null
        bool _cosmetic;
        float _hitDamage, _poisonDps, _poisonDuration;
        float _speed = 5f, _elapsed, _life;   // slower, subtle venom shot
        SpriteRenderer _sr;

        public static void Spawn(Vector3 pos, SkeletonEnemy target, float hitDamage,
                                 float poisonDps, float poisonDuration, Sprite sprite = null)
        {
            var a = Make(pos, sprite);
            a._target = target; a._hitDamage = hitDamage;
            a._poisonDps = poisonDps; a._poisonDuration = poisonDuration;
        }

        // Visual-only replay on a partner's screen: snakes to a fixed point, no damage.
        public static void SpawnCosmetic(Vector3 from, Vector3 to, Sprite sprite = null)
        {
            var a = Make(from, sprite);
            a._cosmetic = true; a._fixedTarget = to;
        }

        static SerpentArrow Make(Vector3 pos, Sprite sprite)
        {
            var go = new GameObject("SerpentArrow");
            go.transform.position = pos;
            var a = go.AddComponent<SerpentArrow>();
            a._sr = go.AddComponent<SpriteRenderer>();
            // Same arrow art as the basic shot, tinted venom-green for the poison read.
            a._sr.sprite = sprite != null ? sprite : Arrow.SharedSprite;
            a._sr.color = Color.white;  // same arrow as the basic shot
            a._sr.sortingOrder = 300;   // foreground, above everything
            Arrow.ApplyWorldSize(go.transform, a._sr.sprite, Arrow.TargetWorldSize);
            // Neon-green outline: a larger green silhouette behind the arrow (poison read).
            var outline = new GameObject("NeonOutline");
            outline.transform.SetParent(go.transform, false);
            var osr = outline.AddComponent<SpriteRenderer>();
            osr.sprite = a._sr.sprite;
            osr.color = new Color(0.22f, 1f, 0.08f, 1f);
            osr.sortingOrder = 299;
            outline.transform.localScale = Vector3.one * 1.4f;
            return a;
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            _life    += Time.deltaTime;
            if (_life > 2.5f) { Destroy(gameObject); return; }
            if (!_cosmetic && (_target == null || !_target.IsAlive)) { Destroy(gameObject); return; }

            Vector3 tp;
            if (_cosmetic) tp = _fixedTarget;
            else { var col = _target.GetComponent<Collider2D>();
                   tp = col != null ? col.bounds.center : _target.transform.position + Vector3.up * 0.5f; }
            Vector3 to = tp - transform.position;
            float dist = to.magnitude;
            // Point-blank: keep snaking a moment so the arrow is visible before it lands.
            if (dist < 0.4f && _life > 0.08f)
            {
                if (!_cosmetic)
                {
                    _target.TakeDamage(_hitDamage, false);
                    MultiplayerSync.Instance?.BroadcastDamage(_target.netInstanceId, _hitDamage, false);
                    _target.ApplyPoison(_poisonDps, _poisonDuration);
                }
                SpawnPoisonBurst(tp);
                Destroy(gameObject);
                return;
            }

            Vector3 dir = to / dist;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
            float wobble = Mathf.Cos(_elapsed * 20f) * 1.1f;   // serpentine
            Vector3 vel = dir * _speed + perp * wobble * _speed * 0.35f;
            transform.position += vel * Time.deltaTime;
            float ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);

            // Faint green trail.
            if (Mathf.Repeat(_elapsed, 0.05f) < Time.deltaTime) SpawnTrail(transform.position);
        }

        static void SpawnTrail(Vector3 p)
        {
            var g = new GameObject("SerpentTrail");
            g.transform.position = p;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = SkillDart.Get();
            sr.color = new Color(0.4f, 1f, 0.3f, 0.5f);
            sr.sortingOrder = 6;
            g.transform.localScale = Vector3.one * 0.35f;
            var f = g.AddComponent<FadeAndDie>();
            f.life = 0.3f;
        }

        static void SpawnPoisonBurst(Vector3 p)
        {
            for (int i = 0; i < 6; i++)
            {
                var g = new GameObject("PoisonBit");
                g.transform.position = p;
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = SkillDart.Get();
                sr.color = new Color(0.4f, 1f, 0.3f, 0.9f);
                sr.sortingOrder = 8;
                g.transform.localScale = Vector3.one * 0.3f;
                var mv = g.AddComponent<FadeAndDie>();
                mv.life = 0.4f;
                mv.velocity = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 2.5f));
                mv.gravity = 4f;
            }
        }
    }

    // "Chuva de Flechas": an arrow that LOBS in a parabolic arc from the archer and comes down
    // DIAGONALLY onto the target's centre (not a 90° drop). Slow/subtle. Spawned on several enemies.
    public class FallingArrow : MonoBehaviour
    {
        SkeletonEnemy _target;   // null for cosmetic replay
        bool _cosmetic;
        Vector3 _origin, _landing, _aimOffset;
        float _damage, _delay, _t;
        const float Dur = 0.95f;       // slow, subtle arc
        const float ArcHeight = 1.8f;  // apex above the straight line

        // Impact point = the enemy's collider CENTRE (body centre, not the head).
        static Vector3 CenterOf(SkeletonEnemy e)
        {
            var col = e != null ? e.GetComponent<Collider2D>() : null;
            return col != null ? col.bounds.center
                               : (e != null ? e.transform.position + Vector3.up * 0.5f : Vector3.zero);
        }

        // aimOffset spreads each arrow of a volley to a distinct point around the target so two
        // arrows landing on the SAME enemy fly separate arcs instead of overlapping into one
        // "duplicated" animation.
        public static void Spawn(SkeletonEnemy target, Vector3 origin, float damage, float delay, Sprite sprite = null, Vector3 aimOffset = default)
        {
            var a = Make(origin, CenterOf(target) + aimOffset, delay, sprite);
            a._target = target; a._damage = damage; a._aimOffset = aimOffset;
        }

        // Visual-only replay on a partner's screen: lobs onto a fixed point, no damage.
        public static void SpawnCosmetic(Vector3 landing, float delay, Sprite sprite = null)
        {
            Vector3 origin = landing + new Vector3(-2.0f, 2.2f, 0f);
            var a = Make(origin, landing, delay, sprite);
            a._cosmetic = true;
        }

        static FallingArrow Make(Vector3 origin, Vector3 landing, float delay, Sprite sprite)
        {
            var go = new GameObject("FallingArrow");
            var a = go.AddComponent<FallingArrow>();
            a._origin = origin; a._landing = landing; a._delay = Mathf.Max(0f, delay);
            go.transform.position = origin;
            var sr = go.AddComponent<SpriteRenderer>();
            // FLAT arrow (no baked shading) — the pack arrow's shading read as a wrong shadow at
            // steep falling angles over common enemies.
            sr.sprite = sprite != null ? sprite : Arrow.FlatSprite;
            sr.color = Color.white;
            sr.sortingOrder = 300;   // foreground, above everything
            Arrow.ApplyWorldSize(go.transform, sr.sprite, Arrow.TargetWorldSize);
            return a;
        }

        void Update()
        {
            // Stagger without disabling the component — hover until the delay elapses.
            if (_delay > 0f) { _delay -= Time.deltaTime; return; }

            // Home onto a MOVING target: re-aim the landing point at the enemy's LIVE centre every
            // frame, so the arrow comes down ON the enemy even while it walks. (It used to lock the
            // landing to the cast-time position, so a moving enemy dodged the falling arrow.)
            if (!_cosmetic && _target != null && _target.IsAlive)
                _landing = CenterOf(_target) + _aimOffset;

            _t += Time.deltaTime / Dur;
            float u = Mathf.Clamp01(_t);
            Vector3 prev = transform.position;
            // Parabolic lob: straight-line interp + an arch, so it rises from the archer and
            // descends DIAGONALLY onto the target centre.
            float x = Mathf.Lerp(_origin.x, _landing.x, u);
            float y = Mathf.Lerp(_origin.y, _landing.y, u) + ArcHeight * 4f * u * (1f - u);
            transform.position = new Vector3(x, y, _landing.z);

            Vector3 d = transform.position - prev;
            if (d.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);

            if (u >= 1f)
            {
                if (!_cosmetic && _target != null && _target.IsAlive)
                {
                    _target.TakeDamage(_damage, false);
                    MultiplayerSync.Instance?.BroadcastDamage(_target.netInstanceId, _damage, false);
                }
                Destroy(gameObject);
            }
        }
    }

    // Visual-only projectile replayed on remote clients when a partner shoots an arrow or
    // fireball, so you SEE their ranged attacks (the actual damage is synced separately via
    // MultiplayerSync "damage" packets). Flies from `from` to `to`, then despawns.
    public class CosmeticProjectile : MonoBehaviour
    {
        Vector3 _to; float _speed; float _life; SpriteRenderer _sr;

        public static void Spawn(Vector3 from, Vector3 to, bool fireball)
        {
            var go = new GameObject("CosmeticProjectile");
            go.transform.position = from;
            var p = go.AddComponent<CosmeticProjectile>();
            p._to = to; p._speed = fireball ? 11f : 15f;
            p._sr = go.AddComponent<SpriteRenderer>();
            p._sr.sprite = SkillDart.Get();
            p._sr.color = fireball ? new Color(1f, 0.5f, 0.16f, 1f) : new Color(0.95f, 0.95f, 1f, 1f);
            p._sr.sortingOrder = 6;
            go.transform.localScale = Vector3.one * (fireball ? 0.7f : 0.6f);
        }

        void Update()
        {
            _life += Time.deltaTime;
            Vector3 to = _to - transform.position;
            float d = to.magnitude;
            if (_life > 2f || d < 0.18f) { Destroy(gameObject); return; }
            Vector3 dir = to / d;
            transform.position += dir * _speed * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
    }

    // Tiny helper: fade a sprite out over `life` seconds, optionally drifting under gravity.
    public class FadeAndDie : MonoBehaviour
    {
        public float life = 0.3f;
        public Vector2 velocity;
        public float gravity;
        float _t; SpriteRenderer _sr; Color _c;

        void Start() { _sr = GetComponent<SpriteRenderer>(); if (_sr != null) _c = _sr.color; }

        void Update()
        {
            _t += Time.deltaTime;
            float p = _t / life;
            if (p >= 1f) { Destroy(gameObject); return; }
            velocity.y -= gravity * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            if (_sr != null) { var c = _c; c.a = _c.a * (1f - p); _sr.color = c; }
        }
    }
}

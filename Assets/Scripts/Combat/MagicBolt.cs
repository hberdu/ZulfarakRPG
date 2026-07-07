using UnityEngine;

namespace ZulfarakRPG
{
    // Magic projectile hurled by the NecromancerBoss at whichever lobby member is nearest
    // (local hero or a remote avatar). Flies toward the target's collider center and bursts
    // on contact. Only the LOCAL hero takes real damage — a remote victim's own client
    // fires its own authoritative bolt, so here a non-local bolt is purely visual.
    // Animated by the necromancer's Magic(projectile) effect frames; if none are supplied
    // it falls back to a procedural chunky purple orb so it always reads.
    public class MagicBolt : MonoBehaviour
    {
        public float speed       = 6.5f;
        public float damage       = 12f;
        public float maxLifetime  = 3.0f;
        public float hitDistance  = 0.42f;

        private Transform      _targetTf;
        private bool           _targetIsLocal = true;
        private float          _spawnTime;
        private SpriteRenderer _sr;
        private Sprite[]       _frames;
        private bool           _usingArt;

        const float Fps = 12f;
        static Sprite[] _procFrames;
        static Sprite   _impact;

        public void Init(Transform target, bool targetIsLocal, float damage, Sprite[] frames)
        {
            _targetTf      = target;
            _targetIsLocal = targetIsLocal;
            this.damage    = damage;
            _spawnTime     = Time.time;
            _usingArt      = frames != null && frames.Length > 0;
            _frames        = _usingArt ? frames : (_procFrames ??= MakeProcFrames(4));

            _sr              = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite       = _frames[0];
            _sr.sortingOrder = 7;
            // The imported effect frames are 100px @100PPU (1 world unit) — scale down so the
            // bolt reads at character-projectile size; the procedural orb is already small.
            transform.localScale = Vector3.one * (_usingArt ? 0.55f : 1f);
            if (!_usingArt) _sr.color = new Color(0.72f, 0.42f, 1f, 1f);
            // Effect art is authored facing one way — flip it toward the target.
            if (_usingArt && _targetTf != null)
                _sr.flipX = _targetTf.position.x < transform.position.x;
        }

        void Update()
        {
            if (Time.time - _spawnTime > maxLifetime) { Destroy(gameObject); return; }
            if (_targetTf == null) { Destroy(gameObject); return; }

            if (_frames.Length > 1)
                _sr.sprite = _frames[(int)((Time.time - _spawnTime) * Fps) % _frames.Length];

            var col = _targetTf.GetComponent<Collider2D>();
            Vector3 targetPos = col != null ? col.bounds.center
                                            : _targetTf.position + Vector3.up * 0.5f;

            Vector3 to   = targetPos - transform.position;
            float   dist = to.magnitude;
            if (dist < hitDistance)
            {
                // Only the local hero is damaged here; remote avatars are hit authoritatively
                // by their own client's bolt (this one just bursts for the visual).
                if (_targetIsLocal)
                {
                    var player = _targetTf.GetComponent<PlayerController2D>();
                    if (player != null) player.TakeDamage(damage);
                }
                SpawnImpact(targetPos);
                Destroy(gameObject);
                return;
            }
            transform.position += (to / dist) * speed * Time.deltaTime;
        }

        void SpawnImpact(Vector3 pos)
        {
            if (_impact == null) _impact = MakeImpactSprite();
            var go = new GameObject("MagicBoltImpact");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _impact;
            sr.color        = new Color(0.72f, 0.42f, 1f, 1f);
            sr.sortingOrder = 9;
            go.AddComponent<ImpactEffect>();   // expanding-fade burst, defined in Arrow.cs
        }

        // ── Procedural fallback (chunky pixel purple orb) ────────────────────────
        static Sprite[] MakeProcFrames(int count)
        {
            const int W = 13, H = 11;
            float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f, coreR = 4.2f;
            var frames = new Sprite[count];
            for (int f = 0; f < count; f++)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                float phase = f / (float)count;
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float wob = coreR + Mathf.Sin((phase + (x + y) * 0.15f) * 6.2832f) * 0.6f;
                    Color c = Color.clear;
                    if (d <= wob)
                    {
                        float a = d / wob;
                        c = a < 0.4f ? new Color(0.92f, 0.80f, 1f, 1f)
                          : a < 0.75f ? new Color(0.70f, 0.40f, 1f, 1f)
                          :             new Color(0.42f, 0.16f, 0.72f, 1f);
                    }
                    t.SetPixel(x, y, c);
                }
                t.Apply();
                frames[f] = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 40f);
            }
            return frames;
        }

        static Sprite MakeImpactSprite()
        {
            const int W = 13, H = 13;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f, maxR = W * 0.5f;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > maxR) { t.SetPixel(x, y, Color.clear); continue; }
                float a = 1f - d / maxR;
                t.SetPixel(x, y, a > 0.66f ? new Color(0.95f, 0.88f, 1f, 1f)
                               : a > 0.33f ? new Color(0.68f, 0.40f, 1f, 1f)
                               :             new Color(0.40f, 0.15f, 0.70f, 1f));
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}

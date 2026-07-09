using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Purple portal smoke that blooms around the player when the Dungeon scene opens
    // (right after the portal absorb) and dissipates over a couple of seconds as the
    // first wave begins. Auto-spawns on Dungeon scene load when the player just came
    // through a portal (PortalSmoke.PendingAtWaveStart set by PlayerController2D).
    public class PortalSmoke : MonoBehaviour
    {
        // Set true by PortalAbsorbRoutine right before the scene swap; consumed once
        // on the next Dungeon load so smoke only shows when arriving via the portal.
        public static bool PendingAtWaveStart;

        static readonly Color Violet  = new Color(0.62f, 0.34f, 0.95f, 1f);
        static readonly Color Magenta = new Color(0.85f, 0.30f, 0.90f, 1f);
        static readonly Color Ember   = new Color(0.98f, 0.72f, 1.00f, 1f);   // bright glow core

        // Random violet↔magenta mix for a richer, layered mystic fog.
        static Color MysticTint()
        {
            float k = Random.value;
            return Color.Lerp(Violet, Magenta, k);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Dungeon") return;
            if (!PendingAtWaveStart) return;
            PendingAtWaveStart = false;

            var go = new GameObject("PortalSmoke");
            go.AddComponent<PortalSmoke>();
        }

        IEnumerator Start()
        {
            // Anchor on the player (fallback to a sensible battlefield spawn point).
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            Vector3 center = player != null ? player.transform.position : new Vector3(2.5f, -0.9f, 0f);

            // Small, brief pixel-art bloom (reduced from the old dense cloud).
            for (int wave = 0; wave < 2; wave++)
            {
                const int puffsPerWave = 7;
                for (int i = 0; i < puffsPerWave; i++)
                    SpawnPuff(center + new Vector3(Random.Range(-0.8f, 0.8f),
                                                   Random.Range(-0.25f, 0.7f), 0f));
                yield return new WaitForSeconds(0.12f);
            }

            yield return new WaitForSeconds(2.2f);   // let the last puffs fade out
            Destroy(gameObject);
        }

        void SpawnPuff(Vector3 pos)
        {
            SpawnPuffStatic(pos);
        }

        // Public entry point so callers outside the Dungeon scene (e.g. the portal
        // absorb routine in Zulfarak) can burst violet smoke around any world point.
        public static void BurstAt(Vector3 center, int count)
        {
            // Fewer puffs, pixel-art outlined (no soft glow cores).
            int reduced = Mathf.Max(1, count / 2);
            for (int i = 0; i < reduced; i++)
                SpawnPuffStatic(center + new Vector3(Random.Range(-0.5f, 0.5f),
                                                     Random.Range(-0.2f, 0.6f), 0f));
        }

        // Mystic fog sweep rolling across the whole battlefield to punctuate a phase
        // change. A wide band of swirling violet/magenta puffs drifts and curls upward,
        // with glow cores twinkling through it, then dissipates on its own.
        public static void PhaseTransition(Vector3 center)
        {
            var go = new GameObject("PhaseTransitionFog");
            go.transform.position = center;
            go.AddComponent<PhaseFog>().Init(center);
        }

        // Lightweight mystic wisp curling up from an open portal mouth. One small
        // swirling puff (occasionally a faint glow) — cheap enough to emit continuously.
        public static void WispAt(Vector3 center)
        {
            var pos = center + new Vector3(Random.Range(-0.22f, 0.22f), Random.Range(-0.18f, 0.12f), -0.5f);
            var go = new GameObject("PortalWisp");
            go.transform.position      = pos;
            go.transform.localScale    = Vector3.one * Random.Range(0.16f, 0.30f);   // small
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = PuffSprite();
            var tint        = MysticTint();
            sr.color        = new Color(tint.r, tint.g, tint.b, Random.Range(0.30f, 0.45f));
            sr.sortingOrder = 6;   // above the portal rings, below gameplay sprites
            go.AddComponent<SmokePuff>().Init(
                drift: new Vector3(Random.Range(-0.10f, 0.10f), Random.Range(0.30f, 0.55f), 0f),
                life:  Random.Range(0.9f, 1.4f),
                grow:  Random.Range(1.3f, 1.8f),
                spin:  0f,                              // no spin → fluid rise
                curl:  Random.Range(0.06f, 0.18f));
        }

        // Small pale-white puff burst — for UI hovers (map icon / friends invite).
        public static void WhiteBurst(Vector3 center, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var pos = center + new Vector3(Random.Range(-0.14f, 0.14f), Random.Range(-0.04f, 0.18f), -0.5f);
                var go = new GameObject("HoverSmoke");
                go.transform.position   = pos;
                go.transform.localScale = Vector3.one * Random.Range(0.22f, 0.42f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = PuffSprite();
                sr.color        = new Color(1f, 1f, 1f, Random.Range(0.55f, 0.85f));
                sr.sortingOrder = 40;
                go.AddComponent<SmokePuff>().Init(
                    drift: new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.25f, 0.55f), 0f),
                    life:  Random.Range(0.8f, 1.4f),
                    grow:  Random.Range(1.6f, 2.4f));
            }
        }

        static void SpawnPuffStatic(Vector3 pos)
        {
            var go = new GameObject("SmokePuff");
            go.transform.position       = pos + new Vector3(0f, 0f, -0.5f);
            go.transform.localScale     = Vector3.one * Random.Range(0.35f, 0.6f);   // smaller
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = PuffSprite();
            var tint        = MysticTint();
            sr.color        = new Color(tint.r, tint.g, tint.b, Random.Range(0.55f, 0.8f));
            sr.sortingOrder = 40;
            // Gentle fluid rise — soft drift + slight curl, no spin.
            go.AddComponent<SmokePuff>().Init(
                drift: new Vector3(Random.Range(-0.30f, 0.30f), Random.Range(0.4f, 0.85f), 0f),
                life:  Random.Range(1.6f, 2.4f),
                grow:  Random.Range(1.5f, 2.1f),
                spin:  0f,
                curl:  Random.Range(0.1f, 0.3f));
        }

        // Bright, fast-fading glow core that reads as a magical spark inside the fog.
        static void SpawnGlowCore(Vector3 pos, float scale)
        {
            var go = new GameObject("SmokeGlow");
            go.transform.position   = pos + new Vector3(0f, 0f, -0.55f);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = PuffSprite();
            sr.color        = new Color(Ember.r, Ember.g, Ember.b, Random.Range(0.55f, 0.85f));
            sr.sortingOrder = 41;   // above the smoke body
            go.AddComponent<SmokePuff>().Init(
                drift: new Vector3(Random.Range(-0.30f, 0.30f), Random.Range(0.55f, 1.20f), 0f),
                life:  Random.Range(0.7f, 1.2f),
                grow:  Random.Range(1.8f, 2.8f),
                spin:  Random.Range(-40f, 40f),
                curl:  Random.Range(0.15f, 0.45f));
        }

        // Soft, small puff: a smooth radial blob (dense core → feathered edge, no hard
        // outline) so the portal smoke reads as fluid wisps rather than chunky rings.
        static Sprite _puff;
        static Sprite PuffSprite()
        {
            if (_puff != null) return _puff;
            const int N = 24;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);   // smoothstep — soft feathered edge
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            _puff = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 48f);
            return _puff;
        }
    }

    // One drifting, growing, fading smoke puff. Optionally spins and curls sideways
    // as it rises so a cloud of them churns like living, swirling fog.
    public class SmokePuff : MonoBehaviour
    {
        Vector3 _drift;
        float   _life, _grow, _t;
        float   _spin;        // degrees/sec
        float   _curl;        // horizontal swirl amplitude
        float   _phase;       // per-puff swirl phase offset
        SpriteRenderer _sr;
        Vector3 _baseScale;
        float   _baseAlpha;

        public void Init(Vector3 drift, float life, float grow, float spin = 0f, float curl = 0f)
        {
            _drift = drift; _life = Mathf.Max(0.1f, life); _grow = grow;
            _spin  = spin;  _curl = curl; _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        void Start()
        {
            _sr        = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            _baseAlpha = _sr != null ? _sr.color.a : 1f;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float p = _t / _life;
            if (p >= 1f) { Destroy(gameObject); return; }

            // Curl adds a sideways swirl on top of the base drift.
            float curlX = _curl != 0f ? Mathf.Cos(_phase + _t * 3.0f) * _curl : 0f;
            transform.position   += (_drift + new Vector3(curlX, 0f, 0f)) * Time.deltaTime;
            if (_spin != 0f) transform.Rotate(0f, 0f, _spin * Time.deltaTime);
            transform.localScale  = _baseScale * Mathf.Lerp(1f, _grow, p);
            if (_sr != null)
            {
                var col = _sr.color;
                // Ease-in-out alpha so puffs bloom in rather than pop.
                col.a = _baseAlpha * Mathf.SmoothStep(1f, 0f, p);
                _sr.color = col;
            }
        }
    }

    // Drives a wide, screen-crossing mystic fog bank for wave/phase transitions.
    // Emits swirling violet/magenta puffs plus glow cores across a band, sweeping
    // left→right, then lets them dissipate.
    public class PhaseFog : MonoBehaviour
    {
        Vector3 _center;

        public void Init(Vector3 center) { _center = center; }

        System.Collections.IEnumerator Start()
        {
            // Sweep a band of fog across ~6 world units centred on the player, in a
            // few staggered pulses so it rolls in like a wall of mist.
            const float halfWidth = 3.2f;
            const int   pulses     = 5;
            for (int pulse = 0; pulse < pulses; pulse++)
            {
                float sweep = Mathf.Lerp(-halfWidth, halfWidth, pulse / (float)(pulses - 1));
                for (int i = 0; i < 5; i++)
                {
                    var p = _center + new Vector3(sweep + Random.Range(-0.7f, 0.7f),
                                                  Random.Range(-0.45f, 1.15f), 0f);
                    PortalSmoke.BurstAt(p, 2);
                }
                yield return new WaitForSeconds(0.09f);
            }
            // Let the last puffs finish fading before cleaning up the anchor object.
            yield return new WaitForSeconds(3.4f);
            Destroy(gameObject);
        }
    }
}

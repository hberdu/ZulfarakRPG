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

        static readonly Color Violet = new Color(0.62f, 0.34f, 0.95f, 1f);

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

            // MASSIVE bloom: three staggered waves of large, drifting puffs so the
            // effect reads as a dense violet explosion instead of a subtle wisp.
            for (int wave = 0; wave < 3; wave++)
            {
                const int puffsPerWave = 22;
                for (int i = 0; i < puffsPerWave; i++)
                    SpawnPuff(center + new Vector3(Random.Range(-1.4f, 1.4f),
                                                   Random.Range(-0.35f, 1.10f), 0f));
                yield return new WaitForSeconds(0.15f);
            }

            yield return new WaitForSeconds(3.6f);   // enough for the last puffs to fade out
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
            for (int i = 0; i < count; i++)
                SpawnPuffStatic(center + new Vector3(Random.Range(-0.6f, 0.6f),
                                                     Random.Range(-0.2f, 0.7f), 0f));
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
            go.transform.position   = pos + new Vector3(0f, 0f, -0.5f);
            go.transform.localScale = Vector3.one * Random.Range(0.9f, 1.7f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = PuffSprite();
            sr.color        = new Color(Violet.r, Violet.g, Violet.b, Random.Range(0.75f, 0.95f));
            sr.sortingOrder = 40;
            go.AddComponent<SmokePuff>().Init(
                drift: new Vector3(Random.Range(-0.55f, 0.55f), Random.Range(0.45f, 1.10f), 0f),
                life:  Random.Range(2.4f, 3.4f),
                grow:  Random.Range(2.4f, 3.6f));
        }

        // Soft smoke puff: 32×32 bilinear with a smooth radial alpha falloff, at
        // 64 PPU (same ~0.5 world-unit footprint as the old 8×8 @ 16 PPU sprite).
        static Sprite _puff;
        static Sprite PuffSprite()
        {
            if (_puff != null) return _puff;
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    float a  = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);   // smoothstep — dense core, feathered edge
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            _puff = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 64f);
            return _puff;
        }
    }

    // One drifting, growing, fading smoke puff.
    public class SmokePuff : MonoBehaviour
    {
        Vector3 _drift;
        float   _life, _grow, _t;
        SpriteRenderer _sr;
        Vector3 _baseScale;
        float   _baseAlpha;

        public void Init(Vector3 drift, float life, float grow)
        {
            _drift = drift; _life = Mathf.Max(0.1f, life); _grow = grow;
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
            transform.position   += _drift * Time.deltaTime;
            transform.localScale  = _baseScale * Mathf.Lerp(1f, _grow, p);
            if (_sr != null)
            {
                var col = _sr.color;
                col.a = _baseAlpha * (1f - p);
                _sr.color = col;
            }
        }
    }
}

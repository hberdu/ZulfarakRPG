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

            // Bloom a cluster of drifting puffs, then let them fade as the wave starts.
            const int puffs = 14;
            for (int i = 0; i < puffs; i++)
                SpawnPuff(center + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.15f, 0.5f), 0f));

            yield return new WaitForSeconds(2.6f);   // roughly the wave warm-up
            Destroy(gameObject);
        }

        void SpawnPuff(Vector3 pos)
        {
            var go = new GameObject("SmokePuff");
            go.transform.position   = pos + new Vector3(0f, 0f, -0.5f);
            go.transform.localScale = Vector3.one * Random.Range(0.35f, 0.7f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = PuffSprite();
            sr.color        = new Color(Violet.r, Violet.g, Violet.b, Random.Range(0.55f, 0.85f));
            sr.sortingOrder = 40;
            go.AddComponent<SmokePuff>().Init(
                drift: new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.25f, 0.6f), 0f),
                life:  Random.Range(1.6f, 2.4f),
                grow:  Random.Range(1.6f, 2.4f));
        }

        // Soft round puff (radial alpha falloff).
        static Sprite _puff;
        static Sprite PuffSprite()
        {
            if (_puff != null) return _puff;
            const int N = 48;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply();
            _puff = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
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

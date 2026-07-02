using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace ZulfarakRPG
{
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [Header("Prefabs")]
        public GameObject skeletonPrefab;
        public GameObject armoredSkeletonPrefab;

        [Header("Spawn")]
        public Transform[] spawnPoints;
        public float spawnInterval = 0.5f;

        [Header("Portal")]
        public Portal2D exitPortal;

        [Header("UI")]
        public TextMeshProUGUI waveAnnounceText;  // unused — left for backwards compat
        public TextMeshProUGUI clearText;
        public TextMeshProUGUI defeatText;

        [Header("Inter-wave run")]
        public ParallaxLayer[] parallaxLayers;
        public float runScrollSpeed    = 4.0f;
        public float runScrollDistance = 15.0f;  // ~50 "passos" of scroll

        // ── State ──────────────────────────────────────────────────────────
        private int  _wave        = 0;
        private int  _totalWaves  = 2;
        private bool _waveDone    = false;
        private List<SkeletonEnemy> _alive = new();
        private PlayerController2D _player;

        void Awake()
        {
            Instance = this;
            _player  = FindAnyObjectByType<PlayerController2D>();
        }

        void Start()
        {
            if (clearText)        clearText.gameObject.SetActive(false);
            if (defeatText)       defeatText.gameObject.SetActive(false);
            if (waveAnnounceText) waveAnnounceText.gameObject.SetActive(false);
            StartCoroutine(StartNextWave());
        }

        // ── Called by SkeletonEnemy on death ───────────────────────────────
        public void OnEnemyDied(SkeletonEnemy enemy)
        {
            _alive.Remove(enemy);
            if (_alive.Count == 0 && !_waveDone)
                StartCoroutine(WaveCleared());
        }

        IEnumerator WaveCleared()
        {
            _waveDone = true;
            yield return new WaitForSeconds(0.5f);

            if (_wave < _totalWaves)
            {
                // Player runs to the next battlefield while BG layers scroll past.
                yield return StartCoroutine(RunToNextWave());
                _waveDone = false;
                yield return StartCoroutine(StartNextWave());
            }
            else
            {
                // All waves cleared
                ShowClearText();
                yield return new WaitForSeconds(0.5f);
                _player?.Celebrate();
            }
        }

        IEnumerator RunToNextWave()
        {
            _player?.SetRunning(true);
            float scrolled = 0f;
            while (scrolled < runScrollDistance)
            {
                float dx = runScrollSpeed * Time.deltaTime;
                scrolled += dx;
                if (parallaxLayers != null)
                    for (int i = 0; i < parallaxLayers.Length; i++)
                        if (parallaxLayers[i] != null) parallaxLayers[i].Scroll(dx);
                // Drift the far scenic backdrop too (subtle, per-layer speeds).
                BackgroundLayers.DungeonScroll += dx * 0.03f;
                yield return null;
            }
            _player?.SetRunning(false);
        }

        IEnumerator StartNextWave()
        {
            _wave++;
            _alive.Clear();
            // No wave-announcement banner — the only HUD strings are CLEAR and DEFEAT.
            yield return new WaitForSeconds(0.25f);

            var prefab = (_wave == 1) ? skeletonPrefab : armoredSkeletonPrefab;
            if (prefab == null) prefab = skeletonPrefab;

            for (int i = 0; i < 4; i++)
            {
                var sp  = spawnPoints != null && spawnPoints.Length > 0
                        ? spawnPoints[i % spawnPoints.Length].position
                        : new Vector3(40 + i * 2f, -1.5f, 0);
                var go  = Instantiate(prefab, sp, Quaternion.identity);
                var sk  = go.GetComponent<SkeletonEnemy>();
                if (sk) _alive.Add(sk);
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        // Called by PlayerController2D.DieRoutine() the moment HP reaches 0.
        public void OnPlayerDied()
        {
            StopAllCoroutines();
            if (clearText)  clearText.gameObject.SetActive(false);
            if (defeatText)
            {
                defeatText.gameObject.SetActive(true);
                StartCoroutine(PulseText(defeatText));
            }
        }

        // ── Called by PlayerController2D after celebration jumps ───────────
        public void OnCelebrationDone()
        {
            if (clearText) clearText.gameObject.SetActive(false);
            exitPortal?.Open();
            _player?.WalkToPortal(exitPortal != null
                ? exitPortal.transform.position
                : new Vector3(-5, -1.5f, 0));
        }

        // ── Visual ─────────────────────────────────────────────────────────
        void ShowClearText()
        {
            if (!clearText) return;
            clearText.text = "CLEAR";
            clearText.gameObject.SetActive(true);
            StartCoroutine(PulseText(clearText));
        }

        IEnumerator PulseText(TextMeshProUGUI label)
        {
            float t = 0;
            while (label && label.gameObject.activeSelf)
            {
                t += Time.deltaTime;
                float s = 1f + Mathf.Sin(t * 5f) * 0.06f;
                label.transform.localScale = Vector3.one * s;
                yield return null;
            }
        }
    }
}

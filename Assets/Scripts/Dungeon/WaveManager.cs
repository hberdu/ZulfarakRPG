using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        private EnemyDefinitionDto[] _enemyCatalog = Array.Empty<EnemyDefinitionDto>();
        private bool _enemyCatalogLoaded;

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
            StartCoroutine(StartWavesRoutine());
        }

        private IEnumerator StartWavesRoutine()
        {
            yield return LoadEnemyCatalogFromServer();
            yield return StartCoroutine(StartNextWave());
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
                // Drift the far scenic backdrop too, at ~the city's parallax rate.
                BackgroundLayers.DungeonScroll += dx * 0.40f;
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
                if (sk)
                {
                    ApplyServerEnemyMapping(sk, prefab != null ? prefab.name : string.Empty);
                    _alive.Add(sk);
                }
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        private IEnumerator LoadEnemyCatalogFromServer()
        {
            if (_enemyCatalogLoaded)
            {
                yield break;
            }

            var timeout = Time.unscaledTime + 20f;
            while ((ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady) && Time.unscaledTime < timeout)
            {
                yield return null;
            }

            if (ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady)
            {
                yield break;
            }

            var task = ServerApiClient.Instance.LoadEnemyDefinitionsAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted || task.IsCanceled)
            {
                var error = task.Exception?.GetBaseException();
                Debug.LogWarning($"[WaveManager] Falha ao carregar catálogo de inimigos: {error?.Message}");
                yield break;
            }

            _enemyCatalog = task.Result ?? Array.Empty<EnemyDefinitionDto>();
            _enemyCatalogLoaded = _enemyCatalog.Length > 0;
            if (_enemyCatalogLoaded)
            {
                Debug.Log($"[WaveManager] Catálogo carregado ({_enemyCatalog.Length} inimigos).");
            }
        }

        private void ApplyServerEnemyMapping(SkeletonEnemy enemy, string prefabName)
        {
            if (enemy == null || _enemyCatalog == null || _enemyCatalog.Length == 0)
            {
                return;
            }

            var match = ResolveBestEnemy(prefabName, enemy.maxHealth, enemy.attackDamage);
            if (match == null)
            {
                return;
            }

            enemy.enemyId = match.enemyId;
            enemy.maxHealth = Mathf.Max(1f, match.hp);
            enemy.attackDamage = Mathf.Max(1f, match.attack);
            Debug.Log($"[WaveManager] Inimigo mapeado prefab='{prefabName}' -> enemyId='{match.enemyId}' (hp={match.hp}, atk={match.attack}).");
        }

        private EnemyDefinitionDto ResolveBestEnemy(string prefabName, float hp, float attack)
        {
            var prefabKey = NormalizeKey(prefabName);
            if (prefabKey.EndsWith("enemy", StringComparison.Ordinal))
            {
                prefabKey = prefabKey.Substring(0, prefabKey.Length - "enemy".Length);
            }

            EnemyDefinitionDto best = null;
            var bestScore = float.MinValue;

            foreach (var candidate in _enemyCatalog)
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.enemyId))
                {
                    continue;
                }

                var idKey = NormalizeKey(candidate.enemyId);
                var nameKey = NormalizeKey(candidate.name);

                var nameScore = 0f;
                if (!string.IsNullOrWhiteSpace(prefabKey))
                {
                    if (idKey == prefabKey || nameKey == prefabKey) nameScore = 100f;
                    else if (idKey.Contains(prefabKey) || nameKey.Contains(prefabKey)) nameScore = 70f;
                    else if (prefabKey.Contains(idKey) || prefabKey.Contains(nameKey)) nameScore = 60f;
                }

                var hpDelta = Mathf.Abs(candidate.hp - hp);
                var attackDelta = Mathf.Abs(candidate.attack - attack);
                var statScore = -((hpDelta * 0.7f) + (attackDelta * 5f));

                var score = nameScore + statScore;
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return bestScore > -120f ? best : null;
        }

        private static string NormalizeKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var normalized = raw.Replace("(Clone)", "").Trim().ToLowerInvariant();
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
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

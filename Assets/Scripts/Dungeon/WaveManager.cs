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
        public GameObject necromancerPrefab;

        [Header("Spawn")]
        public Transform[] spawnPoints;
        // Time between successive enemy spawns within a wave — kept short so the arena
        // fills up quickly ("inimigos nascem mais rápido").
        public float spawnInterval = 0.2f;

        [Header("Portal")]
        public Portal2D exitPortal;

        [Header("UI")]
        public TextMeshProUGUI waveAnnounceText;  // unused — left for backwards compat
        public TextMeshProUGUI clearText;
        public TextMeshProUGUI defeatText;
        public TextMeshProUGUI bossText;

        [Header("Inter-wave run")]
        public ParallaxLayer[] parallaxLayers;
        public float runScrollSpeed    = 4.0f;
        public float runScrollDistance = 15.0f;  // ~50 "passos" of scroll

        // ── State ──────────────────────────────────────────────────────────
        private int  _wave        = 0;
        private int  _totalWaves  = 10;
        private bool _waveDone    = false;
        private List<SkeletonEnemy> _alive = new();
        private PlayerController2D _player;
        private DungeonProgressBar _progressBar;
        private EnemyDefinitionDto[] _enemyCatalog = Array.Empty<EnemyDefinitionDto>();
        private bool _enemyCatalogLoaded;

        // (normal, armored) skeletons per wave 1-9; wave 10 is the Necromancer.
        private static readonly int[,] WaveComp = {
            {4,0}, {2,2}, {4,1}, {3,2}, {5,1}, {3,3}, {5,2}, {4,3}, {3,4}
        };

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
            if (bossText)         bossText.gameObject.SetActive(false);
            // Dungeon progress bar removed. (_progressBar stays null; SetWave calls are no-ops.)
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
            _progressBar?.SetWave(_wave);
            yield return new WaitForSeconds(0.5f);

            if (_wave < _totalWaves)
            {
                // Transition animation: the hero marches in place while the parallax
                // scrolls to the next battlefield (+ a mystic fog sweep between phases).
                yield return StartCoroutine(RunToNextWave());
                // The moment the transition animation ends, hand control straight back.
                // The hero now stands free and only steps FORWARD once the freshly
                // spawned enemies actually walk on-screen (see PlayerController2D
                // .HandleMovement) — no more marching in place at the mobs.
                _waveDone = false;
                _player?.SetRunning(false);
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
            // First regroup at the start (left edge) of the screen, then march in
            // place while the parallax scrolls. SetRunning(false) is issued by
            // WaveCleared the instant this transition animation finishes.
            if (_player != null)
                yield return StartCoroutine(_player.WalkBackToStart(_player.sceneBoundsMinX + 0.1f));
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
        }

        IEnumerator StartNextWave()
        {
            _wave++;
            _alive.Clear();
            _progressBar?.SetWave(_wave - 1);
            // No wave-announcement banner — the only HUD strings are BOSS, CLEAR and DEFEAT.
            yield return new WaitForSeconds(0.25f);

            if (_wave >= _totalWaves)
            {
                yield return StartCoroutine(SpawnBossWave());
                yield break;
            }

            int idx      = Mathf.Clamp(_wave - 1, 0, WaveComp.GetLength(0) - 1);
            int normals  = WaveComp[idx, 0];
            int armored  = WaveComp[idx, 1];
            int total    = normals + armored;

            for (int i = 0; i < total; i++)
            {
                var prefab = i < normals ? skeletonPrefab : armoredSkeletonPrefab;
                if (prefab == null) prefab = skeletonPrefab;

                var sp  = spawnPoints != null && spawnPoints.Length > 0
                        ? spawnPoints[i % spawnPoints.Length].position
                        : new Vector3(40 + i * 2f, -1.5f, 0);
                var go  = Instantiate(prefab, sp, Quaternion.identity);
                var sk  = go.GetComponent<SkeletonEnemy>();
                if (sk)
                {
                    ApplyServerEnemyMapping(sk, prefab != null ? prefab.name : string.Empty);
                    // Deterministic per-wave/per-index ID so co-op damage packets from
                    // the other client can find the exact same skeleton locally.
                    sk.netInstanceId = $"w{_wave}_i{i}";
                    _alive.Add(sk);
                }
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        IEnumerator SpawnBossWave()
        {
            if (bossText == null && clearText != null)
            {
                // Scene built before bossText existed — clone the CLEAR label.
                bossText = Instantiate(clearText, clearText.transform.parent);
                bossText.name  = "BossText";
                bossText.color = new Color(0.9f, 0.15f, 0.15f);
                bossText.gameObject.SetActive(false);
            }
            if (bossText)
            {
                bossText.text = "BOSS";
                yield return StartCoroutine(ScrollBanner(bossText));
            }

            var prefab = necromancerPrefab;
            if (prefab == null)
            {
                Debug.LogError("[WaveManager] necromancerPrefab NÃO está atribuído — o boss vai usar um esqueleto comum. " +
                               "Rode Tools > ZulfarakRPG > Import Character Sprites e depois o Scene Setup Wizard " +
                               "para gerar Assets/Prefabs/NecromancerBoss.prefab e religar a cena Dungeon.");
                prefab = armoredSkeletonPrefab != null ? armoredSkeletonPrefab : skeletonPrefab;
            }
            if (prefab == null) yield break;

            var sp = spawnPoints != null && spawnPoints.Length > 0
                   ? spawnPoints[0].position
                   : new Vector3(40, -1.5f, 0);
            var go = Instantiate(prefab, sp, Quaternion.identity);
            var boss = go.GetComponent<SkeletonEnemy>();
            if (boss)
            {
                ApplyServerEnemyMapping(boss, prefab.name);
                boss.netInstanceId = $"w{_wave}_boss";
                _alive.Add(boss);
            }
        }

        // Called by NecromancerBoss so summoned minions gate wave completion too.
        public void RegisterSummon(SkeletonEnemy sk)
        {
            if (sk != null && !_alive.Contains(sk)) _alive.Add(sk);
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
            if (bossText)   bossText.gameObject.SetActive(false);
            if (defeatText) StartCoroutine(ScrollBanner(defeatText));
        }

        // ── Called by PlayerController2D after the victory pause ───────────
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
            StartCoroutine(ScrollBanner(clearText));
        }

        // Big banner sliding in from the left, holding center, then sliding out
        // to the right. Works with stretch anchors via anchoredPosition offset.
        IEnumerator ScrollBanner(TextMeshProUGUI label)
        {
            if (!label) yield break;
            var rt = label.rectTransform;
            var basePos = rt.anchoredPosition;
            const float off = 460f;

            rt.anchoredPosition = basePos + new Vector2(-off, 0);
            label.gameObject.SetActive(true);

            float t = 0f;
            const float inDur = 0.45f;
            while (t < inDur)
            {
                t += Time.deltaTime;
                float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / inDur), 3f);   // ease-out
                rt.anchoredPosition = basePos + new Vector2(-off * (1f - p), 0);
                yield return null;
            }
            rt.anchoredPosition = basePos;

            yield return new WaitForSeconds(0.9f);

            t = 0f;
            const float outDur = 0.45f;
            while (t < outDur)
            {
                t += Time.deltaTime;
                float p = Mathf.Pow(Mathf.Clamp01(t / outDur), 3f);            // ease-in
                rt.anchoredPosition = basePos + new Vector2(off * p, 0);
                yield return null;
            }

            label.gameObject.SetActive(false);
            rt.anchoredPosition = basePos;
        }
    }
}

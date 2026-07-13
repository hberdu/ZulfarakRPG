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
        public float runScrollDistance = 12.0f;  // slightly shorter gap between waves

        // ── State ──────────────────────────────────────────────────────────
        private int  _wave        = 0;
        private int  _totalWaves  = 5;   // 4 combat waves + the phase boss on wave 5
        private bool _waveDone    = false;
        private List<SkeletonEnemy> _alive = new();
        private PlayerController2D _player;
        private DungeonProgressBar _progressBar;
        private EnemyDefinitionDto[] _enemyCatalog = Array.Empty<EnemyDefinitionDto>();
        private bool _enemyCatalogLoaded;

        // (normal, armored) enemies per wave 1-4; the final wave (5) is the phase boss.
        // Shared by every dungeon phase — each scene's WaveManager wires its own enemy/boss
        // prefabs (skeletons, orcs, slimes, werewolves), so this composition drives them all.
        private static readonly int[,] WaveComp = {
            {4,0}, {2,2}, {4,1}, {3,2}
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

            if (_wave < _totalWaves)
            {
                // Walk to the next battlefield: the hero marches while the world scrolls past for
                // a fixed 2 seconds at his current move speed, THEN the next wave spawns.
                yield return StartCoroutine(RunToNextWave());
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

        // Inter-wave journey: the hero marches in place (walk anim) while the parallax + backdrop
        // scroll past for a FIXED 2 seconds at his CURRENT move speed — so the distance travelled
        // is exactly (moveSpeed × 2) world units, regardless of gear/level. The next wave spawns
        // only after this, and SetRunning(false) drops the hero back to idle.
        IEnumerator RunToNextWave()
        {
            const float travelSeconds = 2f;
            float speed = _player != null ? _player.moveSpeed : 1.6f;
            _player?.SetRunning(true);

            float t = 0f;
            while (t < travelSeconds)
            {
                float dx = speed * Time.deltaTime;
                if (parallaxLayers != null)
                    for (int i = 0; i < parallaxLayers.Length; i++)
                        if (parallaxLayers[i] != null) parallaxLayers[i].Scroll(dx);
                // Drift the far scenic backdrop too, at ~the city's parallax rate.
                BackgroundLayers.DungeonScroll += dx * 0.40f;
                t += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator StartNextWave()
        {
            _wave++;
            _alive.Clear();
            _progressBar?.SetWave(_wave - 1);
            // No wave-announcement banner — the only HUD strings are BOSS, CLEAR and DEFEAT.
            yield return new WaitForSeconds(0.05f);   // near-instant hand-off to the next wave

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

            // Open a server encounter for the whole wave so each kill can be claimed authoritatively.
            BeginEncounterForBatch(new List<SkeletonEnemy>(_alive));
        }

        IEnumerator SpawnBossWave()
        {
            // Pixel-art "BOSS" banner (same font as the damage numbers).
            PixelBanner.Show("BOSS", new Color(0.95f, 0.20f, 0.20f));
            yield return new WaitForSeconds(1.5f);

            var prefab = necromancerPrefab;
            if (prefab == null)
            {
                Debug.LogError("[WaveManager] O slot do boss (necromancerPrefab) NÃO está atribuído nesta cena — " +
                               "o boss vai cair para o inimigo comum. Atribua o prefab do boss desta fase no WaveManager.");
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
                // Encounter for the boss alone; its summoned minions get their own (see NecromancerBoss).
                BeginEncounterForBatch(new List<SkeletonEnemy> { boss });
            }
        }

        // Called by NecromancerBoss so summoned minions gate wave completion too.
        public void RegisterSummon(SkeletonEnemy sk)
        {
            if (sk != null && !_alive.Contains(sk)) _alive.Add(sk);
        }

        // Opens a server encounter for a freshly-spawned batch of enemies and stamps the
        // returned token onto every one of them, so their kills can be claimed authoritatively.
        // Used for each wave and for every summon batch the boss raises.
        public void BeginEncounterForBatch(List<SkeletonEnemy> batch)
        {
            if (batch == null || batch.Count == 0) return;
            StartCoroutine(BeginEncounterRoutine(batch));
        }

        private IEnumerator BeginEncounterRoutine(List<SkeletonEnemy> batch)
        {
            if (ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady)
            {
                yield break;
            }

            var enemyIds = new List<string>(batch.Count);
            foreach (var e in batch)
            {
                if (e == null) continue;
                var id = e.GetServerEnemyId();
                if (!string.IsNullOrWhiteSpace(id)) enemyIds.Add(id);
            }
            if (enemyIds.Count == 0) yield break;

            var task = ServerApiClient.Instance.StartEncounterAsync(enemyIds.ToArray());
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted || task.IsCanceled || task.Result == null || string.IsNullOrWhiteSpace(task.Result.encounterId))
            {
                var error = task.Exception?.GetBaseException();
                Debug.LogWarning($"[WaveManager] Falha ao iniciar encounter ({enemyIds.Count} inimigo(s)): {error?.Message}");
                yield break;
            }

            var encounterId = task.Result.encounterId;
            foreach (var e in batch)
                if (e != null) e.encounterId = encounterId;

            Debug.Log($"[WaveManager] Encounter iniciado id={encounterId} inimigos={enemyIds.Count}");
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
            PixelBanner.Show("DEFEAT", new Color(0.92f, 0.16f, 0.16f));
        }

        // ── Called by PlayerController2D after the victory pause ───────────
        public void OnCelebrationDone()
        {
            if (clearText) clearText.gameObject.SetActive(false);

            // Open the exit portal at the FAR-LEFT edge — well BEHIND the party (who fought to the
            // right), so everyone visibly walks LEFT to it instead of it popping on top of them.
            if (exitPortal != null)
            {
                float portalX = MapBounds.MinX;
                var pp = exitPortal.transform.position;
                exitPortal.transform.position = new Vector3(portalX, pp.y, pp.z);
            }
            exitPortal?.Open();

            // Final dungeon: drop a RANK A (red) challenge portal right beside the purple exit and
            // let the hero CHOOSE — walk into the purple to leave, or the red to face the Minotaur.
            // (Other dungeons keep auto-walking the hero home.)
            if (IsLastDungeon())
            {
                SpawnRankAPortal();
                _player?.SetRunning(false);   // celebration over → hand control back so the hero can pick a portal
                return;
            }

            string dest = exitPortal != null ? exitPortal.destinationScene : "Zulfarak";
            _player?.WalkToPortal(exitPortal != null ? exitPortal.transform.position
                                                     : new Vector3(-5, -1.5f, 0), dest);
        }

        // The last dungeon in the progression — where the RANK A red portal appears. Uses THIS
        // WaveManager's own scene (robust even if a persistent/bootstrap scene is the "active" one).
        // ponytail: single scene-name gate; update if a later dungeon (Dungeon_5_1…) becomes the end.
        bool IsLastDungeon() => gameObject.scene.name == "Dungeon_4_1";

        private bool _rankASpawned;
        void SpawnRankAPortal()
        {
            if (_rankASpawned) return;
            _rankASpawned = true;

            Vector3 pp = exitPortal != null ? exitPortal.transform.position
                                            : new Vector3(MapBounds.CenterX, -0.025f, 0f);
            float x = Mathf.Clamp(pp.x + 1.3f, MapBounds.MinX + 0.3f, MapBounds.MaxX - 0.3f);

            var go = new GameObject("RankAPortal");
            go.transform.position   = new Vector3(x, pp.y, pp.z);
            go.transform.localScale = Vector3.one * 0.8f;   // same size as every portal
            // Give it a glow-core SpriteRenderer like the authored portals — without one Portal2D
            // only drew the outer rings, so the red portal read as empty/half-missing.
            var core = go.AddComponent<SpriteRenderer>();
            core.sortingOrder = 2;
            core.color        = new Color(1f, 0.45f, 0.40f, 1f);   // red-hot core (rings add the glow)
            var p = go.AddComponent<Portal2D>();            // RequireComponent adds the trigger collider
            p.glowSprite    = core;                          // Start fills its sprite + shows it
            p.rankA         = true;
            p.openOnStart   = true;
            p.destinationScene = "";
            p.tooltipText   = "RANK A";
            Debug.Log($"[WaveManager] Portal RANK A (vermelho) criado em x={x:F2} (cena {gameObject.scene.name}).");
        }

        // ── Visual ─────────────────────────────────────────────────────────
        void ShowClearText()
        {
            // Pixel-art "CLEAR" banner (same font as the damage numbers).
            PixelBanner.Show("CLEAR", new Color(1f, 0.85f, 0.30f));
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

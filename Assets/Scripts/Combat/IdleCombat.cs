using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace ZulfarakRPG
{
    // Drives the idle auto-combat loop for individual missions.
    public class IdleCombat : MonoBehaviour
    {
        public static IdleCombat Instance { get; private set; }

        public event Action<CombatTick> OnCombatTick;
        public event Action<CombatResult> OnCombatEnd;

        private bool _running;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartCombat(PlayerData player, EnemyData[] enemies, Action<CombatResult> callback)
        {
            if (_running) return;
            StartCoroutine(CombatLoop(player, enemies, callback));
        }

        private IEnumerator CombatLoop(PlayerData player, EnemyData[] enemies, Action<CombatResult> callback)
        {
            _running = true;
            if (ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady)
            {
                var enemyIds = enemies
                    .Where(x => x != null)
                    .Select(x => x.GetServerEnemyId())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

                if (enemyIds.Length > 0)
                {
                    Debug.Log($"[IdleCombat] Enviando combate para servidor com {enemyIds.Length} inimigo(s): {string.Join(", ", enemyIds)}");
                    CombatResultDto remote = null;
                    Exception requestError = null;
                    yield return WaitForTask(
                        ServerApiClient.Instance.ResolveCombatAsync(enemyIds),
                        x => remote = x,
                        e => requestError = e);

                    if (requestError == null && remote != null)
                    {
                        Debug.Log($"[IdleCombat] Combate remoto OK. victory={remote.victory} exp={remote.expGained} gold={remote.goldGained} kills={remote.killCount}");
                        PlayerManager.Instance.Load();
                        Inventory.Instance.Load();

                        var remoteResult = new CombatResult
                        {
                            victory = remote.victory,
                            expGained = remote.expGained,
                            goldGained = remote.goldGained,
                            killCount = remote.killCount,
                            resolvedByServer = true
                        };

                        OnCombatTick?.Invoke(new CombatTick
                        {
                            playerDamageDealt = 0,
                            enemyDamageDealt = 0,
                            playerCurrentHp = remote.playerRemainingHp,
                            enemyCurrentHp = 0,
                            enemyName = "Servidor"
                        });

                        OnCombatEnd?.Invoke(remoteResult);
                        callback?.Invoke(remoteResult);
                        _running = false;
                        yield break;
                    }

                    Debug.LogWarning($"[IdleCombat] Falha no combate remoto: {requestError?.Message}");

                    var failed = new CombatResult
                    {
                        victory = false,
                        expGained = 0,
                        goldGained = 0,
                        killCount = 0,
                        resolvedByServer = true
                    };

                    OnCombatEnd?.Invoke(failed);
                    callback?.Invoke(failed);
                    _running = false;
                    yield break;
                }

                Debug.LogWarning("[IdleCombat] EnemyIds vazios para combate remoto. Defina enemyId ou enemyName nos EnemyData.");
                var invalid = new CombatResult
                {
                    victory = false,
                    expGained = 0,
                    goldGained = 0,
                    killCount = 0,
                    resolvedByServer = true
                };
                OnCombatEnd?.Invoke(invalid);
                callback?.Invoke(invalid);
                _running = false;
                yield break;
            }

            // Fallback local (somente quando servidor não estiver pronto).
            int playerHp = player.maxHp;
            int enemyIndex = 0;
            int currentEnemyHp = enemies[enemyIndex].hp;
            int totalKills = 0;
            long totalExp = 0;
            long totalGold = 0;

            while (enemyIndex < enemies.Length && playerHp > 0)
            {
                EnemyData enemy = enemies[enemyIndex];

                // Player attacks
                int playerDmg = Mathf.Max(1, player.attack - enemy.defense);
                currentEnemyHp -= playerDmg;

                // Enemy attacks back
                int enemyDmg = Mathf.Max(1, enemy.attack - player.defense);
                playerHp -= enemyDmg;

                OnCombatTick?.Invoke(new CombatTick
                {
                    playerDamageDealt = playerDmg,
                    enemyDamageDealt = enemyDmg,
                    playerCurrentHp = playerHp,
                    enemyCurrentHp = currentEnemyHp,
                    enemyName = enemy.enemyName
                });

                if (currentEnemyHp <= 0)
                {
                    totalKills++;
                    totalExp += enemy.expReward;
                    totalGold += enemy.goldReward;
                    enemyIndex++;
                    if (enemyIndex < enemies.Length)
                        currentEnemyHp = enemies[enemyIndex].hp;
                }

                yield return new WaitForSeconds(1f / player.speed);
            }

            bool victory = playerHp > 0;
            var result = new CombatResult
            {
                victory = victory,
                expGained = victory ? totalExp : totalExp / 2,
                goldGained = victory ? totalGold : totalGold / 4,
                killCount = totalKills,
                resolvedByServer = false
            };

            OnCombatEnd?.Invoke(result);
            callback?.Invoke(result);
            _running = false;
        }

        private static IEnumerator WaitForTask<T>(System.Threading.Tasks.Task<T> task, Action<T> onSuccess, Action<Exception> onError)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                onError?.Invoke(task.Exception?.GetBaseException() ?? new Exception("Task faulted."));
                yield break;
            }

            if (task.IsCanceled)
            {
                onError?.Invoke(new Exception("Task canceled."));
                yield break;
            }

            onSuccess?.Invoke(task.Result);
        }
    }

    [Serializable]
    public struct CombatTick
    {
        public int playerDamageDealt;
        public int enemyDamageDealt;
        public int playerCurrentHp;
        public int enemyCurrentHp;
        public string enemyName;
    }

    [Serializable]
    public struct CombatResult
    {
        public bool victory;
        public long expGained;
        public long goldGained;
        public int killCount;
        public bool resolvedByServer;
    }
}

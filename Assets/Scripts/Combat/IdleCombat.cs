using System;
using System.Collections;
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
                killCount = totalKills
            };

            OnCombatEnd?.Invoke(result);
            callback?.Invoke(result);
            _running = false;
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
    }
}

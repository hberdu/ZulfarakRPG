using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZulfarakRPG
{
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Header("Mission Catalog")]
        public MissionData[] allMissions;

        public event Action<MissionData> OnMissionStarted;
        public event Action<MissionData, CombatResult> OnIndividualMissionCompleted;
        public event Action<MissionData, bool> OnGuildMissionCompleted;

        private MissionData _activeSoloMission;
        private bool _soloMissionRunning;
        private bool _enemyCatalogSynced;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(SyncEnemyCatalog());
        }

        public MissionData[] GetIndividualMissions() =>
            System.Array.FindAll(allMissions, m => m.missionType == MissionType.Individual);

        public MissionData[] GetGuildMissions() =>
            System.Array.FindAll(allMissions, m => m.missionType == MissionType.Guild);

        // --- Individual Mission ---

        public bool StartIndividualMission(MissionData mission)
        {
            if (_soloMissionRunning) return false;
            var player = PlayerManager.Instance.Data;
            if (player.level < mission.requiredLevel) return false;

            if (ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady && !_enemyCatalogSynced)
            {
                if (!TrySyncEnemyCatalogNow())
                {
                    Debug.LogWarning("[MissionManager] Não foi possível sincronizar catálogo de inimigos com o servidor.");
                    return false;
                }
            }

            _activeSoloMission = mission;
            _soloMissionRunning = true;
            OnMissionStarted?.Invoke(mission);
            IdleCombat.Instance.StartCombat(player, mission.enemies, OnSoloCombatFinished);
            return true;
        }

        private void OnSoloCombatFinished(CombatResult result)
        {
            _soloMissionRunning = false;

            if (!result.resolvedByServer)
            {
                var player = PlayerManager.Instance.Data;
                player.AddExp(result.expGained);
                player.gold += result.goldGained;
                PlayerManager.Instance.Save();
            }
            else
            {
                PlayerManager.Instance.Load();
                Inventory.Instance.Load();
            }

            OnIndividualMissionCompleted?.Invoke(_activeSoloMission, result);
            _activeSoloMission = null;
        }

        // --- Guild Mission ---
        // Called by NetworkManager when server confirms all 5 players are ready.
        public void StartGuildMission(MissionData mission, List<PlayerData> partyMembers)
        {
            OnMissionStarted?.Invoke(mission);
            StartCoroutine(ResolveGuildMission(mission, partyMembers));
        }

        private IEnumerator ResolveGuildMission(MissionData mission, List<PlayerData> party)
        {
            yield return new WaitForSeconds(mission.durationSeconds);

            float successChance = CalculateGuildSuccessChance(mission, party);
            bool victory = UnityEngine.Random.value <= successChance;

            if (victory)
            {
                foreach (var member in party)
                {
                    member.AddExp(mission.expReward);
                    member.gold += mission.goldReward;
                }
                PlayerManager.Instance.Save();
            }

            OnGuildMissionCompleted?.Invoke(mission, victory);
        }

        private float CalculateGuildSuccessChance(MissionData mission, List<PlayerData> party)
        {
            float chance = 0.5f; // base 50%
            int avgLevel = 0;

            foreach (var p in party)
            {
                avgLevel += p.level;
                var sub = ClassDatabase.Instance.GetSubclass(p.subclassType);
                if (sub == null) continue;
                if (sub.role == Role.Tank)        chance += mission.tankSuccessBonus;
                else if (sub.role == Role.Healer) chance += mission.healerSuccessBonus;
                else                              chance += mission.dpsSuccessBonus;
            }

            avgLevel /= Mathf.Max(1, party.Count);
            chance += (avgLevel - mission.requiredLevel) * 0.02f;

            return Mathf.Clamp01(chance);
        }

        private IEnumerator SyncEnemyCatalog()
        {
            float timeout = Time.unscaledTime + 30f;
            while ((ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady) && Time.unscaledTime < timeout)
            {
                yield return null;
            }

            if (ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady)
            {
                yield break;
            }

            var payload = allMissions
                .Where(x => x != null && x.enemies != null)
                .SelectMany(x => x.enemies)
                .Where(x => x != null)
                .GroupBy(x => x.GetServerEnemyId())
                .Select(x => EnemyDefinitionDto.FromEnemyData(x.First()))
                .Where(x => !string.IsNullOrWhiteSpace(x.enemyId))
                .ToArray();

            if (payload.Length == 0)
            {
                yield break;
            }

            var task = ServerApiClient.Instance.SyncEnemyDefinitionsAsync(payload);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                var error = task.Exception?.GetBaseException();
                Debug.LogWarning($"[MissionManager] Sync de inimigos falhou: {error?.Message}");
                yield break;
            }

            _enemyCatalogSynced = true;
            Debug.Log($"[MissionManager] Catálogo de inimigos sincronizado ({payload.Length} registros).");
        }

        private bool TrySyncEnemyCatalogNow()
        {
            var payload = allMissions
                .Where(x => x != null && x.enemies != null)
                .SelectMany(x => x.enemies)
                .Where(x => x != null)
                .GroupBy(x => x.GetServerEnemyId())
                .Select(x => EnemyDefinitionDto.FromEnemyData(x.First()))
                .Where(x => !string.IsNullOrWhiteSpace(x.enemyId))
                .ToArray();

            if (payload.Length == 0)
            {
                return false;
            }

            try
            {
                ServerApiClient.Instance.SyncEnemyDefinitionsAsync(payload).GetAwaiter().GetResult();
                _enemyCatalogSynced = true;
                Debug.Log($"[MissionManager] Catálogo de inimigos sincronizado ({payload.Length} registros).");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MissionManager] Sync imediato de inimigos falhou: {e.Message}");
                return false;
            }
        }
    }
}

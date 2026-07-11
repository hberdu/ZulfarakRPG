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

            if (!result.victory)
            {
                PlayerManager.Instance?.RestoreFullHealthAndSave();
            }

            OnIndividualMissionCompleted?.Invoke(_activeSoloMission, result);
            _activeSoloMission = null;
        }

        // --- Guild Mission ---
        // Guild missions are resolved AUTHORITATIVELY by the server: it computes success
        // from the persisted guild roster and grants exp/gold to every member. The client
        // only triggers it (leader) and reloads the resulting state.
        public void StartGuildMission(MissionData mission, List<PlayerData> partyMembers)
        {
            OnMissionStarted?.Invoke(mission);
            StartCoroutine(ResolveGuildMission(mission));
        }

        public void StartGuildMission(MissionData mission)
        {
            OnMissionStarted?.Invoke(mission);
            StartCoroutine(ResolveGuildMission(mission));
        }

        private IEnumerator ResolveGuildMission(MissionData mission)
        {
            // Keep the flavour delay so the UI can show "em andamento".
            yield return new WaitForSeconds(mission.durationSeconds);

            if (ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady)
            {
                Debug.LogWarning("[MissionManager] Servidor indisponível — missão de guilda não pôde ser resolvida.");
                OnGuildMissionCompleted?.Invoke(mission, false);
                yield break;
            }

            var task = ServerApiClient.Instance.ResolveGuildMissionAsync(mission.missionId);
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted || task.IsCanceled || task.Result == null)
            {
                var err = task.Exception?.GetBaseException()?.Message;
                Debug.LogWarning($"[MissionManager] Missão de guilda falhou no servidor: {err}");
                OnGuildMissionCompleted?.Invoke(mission, false);
                yield break;
            }

            var result = task.Result;
            // Server granted the rewards to all members — refresh our own authoritative state.
            PlayerManager.Instance.Load();
            Inventory.Instance.Load();
            GuildManager.Instance?.Load();

            Debug.Log($"[MissionManager] Missão de guilda '{result.missionId}' victory={result.victory} chance={result.successChance:0.00} exp/membro={result.expPerMember} ouro/membro={result.goldPerMember}");
            OnGuildMissionCompleted?.Invoke(mission, result.victory);
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

            // Dev-only: push guild-mission definitions so the server can resolve them.
            // No-op for players (SyncGuildMissionsAsync requires the admin key).
            if (ServerApiClient.Instance.HasAdminKey)
            {
                var guildPayload = BuildGuildMissionPayload();
                if (guildPayload.Length > 0)
                {
                    var gtask = ServerApiClient.Instance.SyncGuildMissionsAsync(guildPayload);
                    while (!gtask.IsCompleted) yield return null;
                    if (gtask.IsFaulted)
                        Debug.LogWarning($"[MissionManager] Sync de missões de guilda falhou: {gtask.Exception?.GetBaseException()?.Message}");
                    else
                        Debug.Log($"[MissionManager] Missões de guilda sincronizadas ({guildPayload.Length}).");
                }
            }
        }

        private GuildMissionDefinitionDto[] BuildGuildMissionPayload()
        {
            if (allMissions == null) return System.Array.Empty<GuildMissionDefinitionDto>();
            return allMissions
                .Where(m => m != null && m.missionType == MissionType.Guild && !string.IsNullOrWhiteSpace(m.missionId))
                .GroupBy(m => m.missionId.Trim())
                .Select(g => g.First())
                .Select(m => new GuildMissionDefinitionDto
                {
                    missionId = m.missionId.Trim(),
                    name = string.IsNullOrWhiteSpace(m.missionName) ? m.missionId : m.missionName,
                    requiredLevel = Mathf.Max(1, m.requiredLevel),
                    requiredPlayers = Mathf.Max(1, m.requiredPlayers),
                    expReward = m.expReward,
                    goldReward = m.goldReward,
                    baseSuccessChance = 0.5,
                    tankBonus = m.tankSuccessBonus,
                    healerBonus = m.healerSuccessBonus,
                    dpsBonus = m.dpsSuccessBonus
                })
                .ToArray();
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

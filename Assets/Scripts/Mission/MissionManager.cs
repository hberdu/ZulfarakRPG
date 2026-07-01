using System;
using System.Collections;
using System.Collections.Generic;
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

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
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

            _activeSoloMission = mission;
            _soloMissionRunning = true;
            OnMissionStarted?.Invoke(mission);
            IdleCombat.Instance.StartCombat(player, mission.enemies, OnSoloCombatFinished);
            return true;
        }

        private void OnSoloCombatFinished(CombatResult result)
        {
            _soloMissionRunning = false;
            var player = PlayerManager.Instance.Data;
            player.AddExp(result.expGained);
            player.gold += result.goldGained;
            PlayerManager.Instance.Save();
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
    }
}

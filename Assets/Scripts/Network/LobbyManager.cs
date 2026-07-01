using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Manages the guild mission lobby — waiting room until 5 players are ready.
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        public const int RequiredPlayers = 5;

        public List<string> ReadyPlayerIds { get; } = new List<string>();
        public MissionData SelectedMission { get; private set; }

        public event Action<List<string>> OnLobbyUpdated;
        public event Action OnAllPlayersReady;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SelectMission(MissionData mission)
        {
            SelectedMission = mission;
            ReadyPlayerIds.Clear();
            NetworkManager.Instance.Send("lobby_select_mission", new { missionId = mission.missionId });
        }

        public void MarkReady()
        {
            var steamId = SteamIntegration.Instance.SteamId;
            if (!ReadyPlayerIds.Contains(steamId))
            {
                ReadyPlayerIds.Add(steamId);
                NetworkManager.Instance.Send("lobby_ready", new { steamId });
                OnLobbyUpdated?.Invoke(ReadyPlayerIds);
            }
            CheckAllReady();
        }

        // Called via NetworkManager when server broadcasts lobby state
        public void ReceiveLobbyUpdate(List<string> readyIds)
        {
            ReadyPlayerIds.Clear();
            ReadyPlayerIds.AddRange(readyIds);
            OnLobbyUpdated?.Invoke(ReadyPlayerIds);
            CheckAllReady();
        }

        private void CheckAllReady()
        {
            if (ReadyPlayerIds.Count >= RequiredPlayers)
                OnAllPlayersReady?.Invoke();
        }
    }
}

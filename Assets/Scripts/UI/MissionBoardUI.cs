using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ZulfarakRPG
{
    public class MissionBoardUI : MonoBehaviour
    {
        [Header("Tabs")]
        public Button individualTab;
        public Button guildTab;

        [Header("Mission List")]
        public Transform missionListContainer;
        public GameObject missionCardPrefab;

        [Header("Active Mission HUD")]
        public GameObject activeMissionHUD;
        public TextMeshProUGUI activeMissionName;
        public Slider missionProgressBar;
        public TextMeshProUGUI combatLogText;
        public Button cancelButton;

        [Header("Guild Lobby")]
        public GameObject guildLobbyPanel;
        public TextMeshProUGUI lobbyStatusText;
        public Button readyButton;

        private bool _showingGuild;

        private void Start()
        {
            individualTab.onClick.AddListener(() => ShowTab(false));
            guildTab.onClick.AddListener(() => ShowTab(true));

            MissionManager.Instance.OnMissionStarted += OnMissionStarted;
            MissionManager.Instance.OnIndividualMissionCompleted += OnSoloMissionDone;
            MissionManager.Instance.OnGuildMissionCompleted += OnGuildMissionDone;

            IdleCombat.Instance.OnCombatTick += OnCombatTick;

            LobbyManager.Instance.OnLobbyUpdated += ids =>
                lobbyStatusText.text = $"Jogadores prontos: {ids.Count}/{LobbyManager.RequiredPlayers}";

            LobbyManager.Instance.OnAllPlayersReady += () =>
            {
                guildLobbyPanel.SetActive(false);
                activeMissionHUD.SetActive(true);
            };

            readyButton.onClick.AddListener(LobbyManager.Instance.MarkReady);

            ShowTab(false);
        }

        private void ShowTab(bool guild)
        {
            _showingGuild = guild;
            PopulateMissions();
        }

        private void PopulateMissions()
        {
            foreach (Transform child in missionListContainer)
                Destroy(child.gameObject);

            var missions = _showingGuild
                ? MissionManager.Instance.GetGuildMissions()
                : MissionManager.Instance.GetIndividualMissions();

            foreach (var m in missions)
            {
                var card = Instantiate(missionCardPrefab, missionListContainer);
                card.GetComponentInChildren<TextMeshProUGUI>().text =
                    $"<b>{m.missionName}</b>\nNÃ­vel {m.requiredLevel}+  |  +{m.expReward} XP  |  +{m.goldReward} ouro";

                var btn = card.GetComponentInChildren<Button>();
                MissionData captured = m;
                btn.onClick.AddListener(() => OnMissionCardClicked(captured));
            }
        }

        private void OnMissionCardClicked(MissionData m)
        {
            if (m.missionType == MissionType.Individual)
            {
                MissionManager.Instance.StartIndividualMission(m);
            }
            else
            {
                LobbyManager.Instance.SelectMission(m);
                guildLobbyPanel.SetActive(true);
                lobbyStatusText.text = $"Jogadores prontos: 0/{LobbyManager.RequiredPlayers}";
            }
        }

        private void OnMissionStarted(MissionData m)
        {
            activeMissionName.text = m.missionName;
            missionProgressBar.value = 0;
            combatLogText.text = "";
            if (m.missionType == MissionType.Individual)
                activeMissionHUD.SetActive(true);
        }

        private void OnCombatTick(CombatTick tick)
        {
            combatLogText.text =
                $"Atacou {tick.enemyName} por {tick.playerDamageDealt}\n" +
                $"{tick.enemyName} atacou por {tick.enemyDamageDealt}\n" +
                $"Seu HP: {tick.playerCurrentHp}  HP inimigo: {tick.enemyCurrentHp}";
        }

        private void OnSoloMissionDone(MissionData m, CombatResult result)
        {
            activeMissionHUD.SetActive(false);
            string outcome = result.victory ? "VitÃ³ria!" : "Derrota!";
            combatLogText.text = $"{outcome}  +{result.expGained} XP  +{result.goldGained} ouro";
            FindAnyObjectByType<CityUI>()?.RefreshPlayerInfo();
        }

        private void OnGuildMissionDone(MissionData m, bool victory)
        {
            activeMissionHUD.SetActive(false);
            string msg = victory ? "Masmorra conquistada!" : "Grupo derrotado!";
            combatLogText.text = msg;
            FindAnyObjectByType<CityUI>()?.RefreshPlayerInfo();
        }
    }
}


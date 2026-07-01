using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ZulfarakRPG
{
    // Main HUD while the player is in Zulfarak city.
    public class CityUI : MonoBehaviour
    {
        [Header("Player Info")]
        public TextMeshProUGUI playerNameText;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI classText;
        public TextMeshProUGUI goldText;
        public Slider expBar;

        [Header("Navigation Buttons")]
        public Button missionBoardButton;
        public Button guildButton;
        public Button profileButton;

        [Header("Panels")]
        public GameObject missionBoardPanel;
        public GameObject guildPanel;
        public GameObject profilePanel;

        [Header("Connection Status")]
        public TextMeshProUGUI connectionStatusText;
        public Image connectionDot;
        public Color connectedColor = Color.green;
        public Color disconnectedColor = Color.red;

        private void Start()
        {
            missionBoardButton?.onClick.AddListener(() => ShowPanel(missionBoardPanel));
            guildButton?.onClick.AddListener(() => ShowPanel(guildPanel));
            profileButton?.onClick.AddListener(() => ShowPanel(profilePanel));

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnected    += () => SetConnectionStatus(true);
                NetworkManager.Instance.OnDisconnected += () => SetConnectionStatus(false);
                SetConnectionStatus(NetworkManager.Instance.IsConnected);
            }
            else
            {
                SetConnectionStatus(false);
            }

            RefreshPlayerInfo();
            ShowPanel(missionBoardPanel);
        }

        public void ShowMissionBoard() => ShowPanel(missionBoardPanel);
        public void ShowGuildPanel()   => ShowPanel(guildPanel);
        public void ShowProfile()      => ShowPanel(profilePanel);

        private void ShowPanel(GameObject target)
        {
            missionBoardPanel.SetActive(false);
            guildPanel.SetActive(false);
            profilePanel.SetActive(false);
            target.SetActive(true);
        }

        public void RefreshPlayerInfo()
        {
            if (PlayerManager.Instance == null) return;
            var data = PlayerManager.Instance.Data;
            if (data == null) return;

            playerNameText.text = data.playerName;
            levelText.text = $"Nível {data.level}";
            classText.text = $"{data.classType} › {data.subclassType}";
            goldText.text = $"{data.gold:N0} ouro";
            expBar.value = (float)data.currentExp / data.expToNextLevel;
        }

        private void SetConnectionStatus(bool connected)
        {
            connectionStatusText.text = connected ? "Online" : "Offline";
            connectionDot.color = connected ? connectedColor : disconnectedColor;
        }
    }
}

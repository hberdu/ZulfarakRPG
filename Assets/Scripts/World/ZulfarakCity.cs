using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Root controller for the Zulfarak city scene.
    // Place on an empty GameObject named "CityController" in the Zulfarak scene.
    public class ZulfarakCity : MonoBehaviour
    {
        [Header("References")]
        public CityUI cityUI;
        public MissionBoardUI missionBoardUI;
        public GuildUI guildUI;
        public MissionManager missionManager;

        [Header("Ambient")]
        public AudioSource ambientAudio;
        public ParticleSystem sandParticles;

        // Points of interest the player can click in the city
        [Header("City Hotspots")]
        public GameObject guildHallHighlight;
        public GameObject marketHighlight;

        private void Start()
        {
            // Ensure player data is loaded (redirect to Bootstrap if managers missing)
            if (PlayerManager.Instance == null || PlayerManager.Instance.Data == null)
            {
                SceneManager.LoadScene("Bootstrap");
                return;
            }

            // Wire mission board to mission manager's catalog
            // (drag-assign in Inspector, but fallback: find in scene)
            if (missionManager == null)
                missionManager = FindAnyObjectByType<MissionManager>();

            cityUI?.RefreshPlayerInfo();

            // Announce player presence to server
            if (NetworkManager.Instance.IsConnected)
            {
                NetworkManager.Instance.Send("player_connect", new
                {
                    steamId  = PlayerManager.Instance.Data.steamId,
                    name     = PlayerManager.Instance.Data.playerName,
                    level    = PlayerManager.Instance.Data.level,
                    guildId  = PlayerManager.Instance.Data.guildId
                });
            }

            GuildManager.Instance.Load();

            StartAmbient();
        }

        private void StartAmbient()
        {
            if (ambientAudio != null && !ambientAudio.isPlaying)
                ambientAudio.Play();

            if (sandParticles != null)
                sandParticles.Play();
        }

        // Called by UI buttons in the city background
        public void OnGuildHallClicked()  => cityUI?.ShowGuildPanel();
        public void OnMissionBoardClicked() => cityUI?.ShowMissionBoard();
    }
}


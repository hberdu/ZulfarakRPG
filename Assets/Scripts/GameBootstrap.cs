using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // First MonoBehaviour that runs. Decides which scene to load.
    public class GameBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            // Ensure singleton managers exist
            EnsureManager<OverlayWindow>("OverlayWindow");
            EnsureManager<SteamIntegration>("SteamIntegration");
            EnsureManager<SteamLobbyManager>("SteamLobbyManager");
            EnsureManager<SteamP2P>("SteamP2P");
            EnsureManager<MultiplayerSync>("MultiplayerSync");
            EnsureManager<PlayerManager>("PlayerManager");
            EnsureManager<Inventory>("Inventory");
            EnsureManager<GuildManager>("GuildManager");
            EnsureManager<MissionManager>("MissionManager");
            EnsureManager<NetworkManager>("NetworkManager");
            EnsureManager<LobbyManager>("LobbyManager");
            EnsureManager<IdleCombat>("IdleCombat");
        }

        private void Start()
        {
            // Connect to server
            NetworkManager.Instance.Connect();

            if (!SteamIntegration.Instance.IsInitialized)
            {
                Debug.LogError("Steam não inicializado. Abra o jogo pelo Steam.");
                // In production: show error screen
                return;
            }

            PlayerManager.Instance.Load();
            Inventory.Instance.Load();

            if (!PlayerManager.Instance.HasSavedData())
                SceneManager.LoadScene("CharacterCreation");
            else
                SceneManager.LoadScene("Zulfarak");
        }

        private void EnsureManager<T>(string goName) where T : MonoBehaviour
        {
            if (FindAnyObjectByType<T>() == null)
            {
                var go = new GameObject(goName);
                go.AddComponent<T>();
            }
        }
    }
}

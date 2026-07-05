using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System;

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
            EnsureManager<ServerApiClient>("ServerApiClient");
            EnsureManager<LobbyManager>("LobbyManager");
            EnsureManager<IdleCombat>("IdleCombat");
        }

        private async void Start()
        {
            Debug.Log("[Bootstrap] Start iniciado.");
            // Connect to server
            NetworkManager.Instance.Connect();

            if (!SteamIntegration.Instance.IsInitialized)
            {
                Debug.LogError("Steam não inicializado. Abra o jogo pelo Steam.");
                // In production: show error screen
                return;
            }

            bool authOk = await AuthenticateBackend();
            if (!authOk)
            {
                Debug.LogError("[Bootstrap] Falha ao autenticar no backend. Encerrando fluxo de inicialização.");
                return;
            }
            PlayerManager.Instance.Load();
            Inventory.Instance.Load();

            bool hasChar = PlayerManager.Instance.HasSavedData();
            Debug.Log($"[Bootstrap] steamInit={SteamIntegration.Instance.IsInitialized} " +
                      $"hasSavedCharacter={hasChar} → {(hasChar ? "Zulfarak" : "CharacterCreation")}");
            if (!hasChar)
                SceneManager.LoadScene("CharacterCreation");
            else
                SceneManager.LoadScene("Zulfarak");
        }

        private static async Task<bool> AuthenticateBackend()
        {
            var steam = SteamIntegration.Instance;
            var api = ServerApiClient.Instance;
            if (steam == null || api == null) return false;

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var authTask = api.AuthenticateWithSteamAsync(steam.SteamId, steam.SteamName);
                var completed = await Task.WhenAny(authTask, Task.Delay(TimeSpan.FromSeconds(25)));
                if (completed == authTask && await authTask)
                {
                    return true;
                }

                Debug.LogWarning($"[Bootstrap] Tentativa de autenticação {attempt}/3 falhou.");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            Debug.LogError("[Bootstrap] Timeout/falha ao autenticar no backend após 3 tentativas.");
            return false;
        }

        private void EnsureManager<T>(string goName) where T : MonoBehaviour
        {
            var existing = FindAnyObjectByType<T>();
            if (existing != null)
            {
                if (existing.transform.parent != null)
                    existing.transform.SetParent(null, true);
                return;
            }

            if (existing == null)
            {
                var go = new GameObject(goName);
                go.AddComponent<T>();
            }
        }
    }
}

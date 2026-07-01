using UnityEngine;

namespace ZulfarakRPG
{
    public class SteamIntegration : MonoBehaviour
    {
        public static SteamIntegration Instance { get; private set; }

        public bool IsInitialized { get; private set; }
        public string SteamId { get; private set; }
        public string SteamName { get; private set; }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
#if STEAMWORKS_NET
            if (!Steamworks.SteamAPI.Init())
            {
                Debug.LogError("Steam API failed to initialize. Make sure Steam is running.");
                return;
            }
            IsInitialized = true;
            SteamId   = Steamworks.SteamUser.GetSteamID().ToString();
            SteamName = Steamworks.SteamFriends.GetPersonaName();
            Debug.Log($"Steam: {SteamName} ({SteamId})");
#else
            // Stub for builds without Steamworks — use for testing without Steam
            IsInitialized = true;
            SteamId   = "local_test_player";
            SteamName = "Jogador Local";
            Debug.LogWarning("[Steam] Steamworks.NET não encontrado — usando identidade de teste.");
#endif
        }

        private void Update()
        {
#if STEAMWORKS_NET
            if (IsInitialized) Steamworks.SteamAPI.RunCallbacks();
#endif
        }

        private void OnDestroy()
        {
#if STEAMWORKS_NET
            if (IsInitialized) Steamworks.SteamAPI.Shutdown();
#endif
        }
    }
}

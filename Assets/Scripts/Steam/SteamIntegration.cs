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

        // Requests a Steam auth session ticket and returns it as a hex string so the
        // backend can validate the identity via Steam's Web API (anti-impersonation).
        // Returns null when Steamworks isn't available (editor/dev without Steam), in
        // which case the server falls back to the insecure steamId path.
        public string GetAuthSessionTicketHex()
        {
#if STEAMWORKS_NET
            if (!IsInitialized) return null;
            try
            {
                var buffer = new byte[1024];
                var identity = new Steamworks.SteamNetworkingIdentity();
                var handle = Steamworks.SteamUser.GetAuthSessionTicket(buffer, buffer.Length, out uint written, ref identity);
                if (handle == Steamworks.HAuthTicket.Invalid || written == 0) return null;
                var sb = new System.Text.StringBuilder((int)written * 2);
                for (int i = 0; i < written; i++) sb.Append(buffer[i].ToString("x2"));
                return sb.ToString();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Steam] GetAuthSessionTicket falhou: {e.Message}");
                return null;
            }
#else
            return null;
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

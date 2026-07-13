using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace ZulfarakRPG
{
    // Steam lobby lifecycle for the "invite a friend to my Zulfarak world"
    // feature. Owns a friends-only Steam lobby capped at MaxPlayers, exposes
    // an Invite Overlay trigger, and routes "+connect_lobby <id>" launches
    // and in-app overlay invites into JoinLobby automatically.
    //
    // This component handles MEMBERSHIP only — the actual remote-player
    // position / animation sync over Steam P2P is a follow-up (see the
    // TODOs in BroadcastLobbyMembers).
    public class SteamLobbyManager : MonoBehaviour
    {
        public static SteamLobbyManager Instance { get; private set; }

        public const int MaxPlayers = 5;

        // Snapshot of lobby member Steam IDs (as strings) — leader is index 0.
        public readonly List<string> MemberSteamIds = new List<string>();
        public string  LeaderSteamId  { get; private set; }
        public bool    InLobby        => !string.IsNullOrEmpty(LobbyIdString);
        public bool    IsLeader       => InLobby && LeaderSteamId == SteamIntegration.Instance?.SteamId;
        public string  LobbyIdString  { get; private set; }

        public event Action OnLobbyChanged;   // raised on member join/leave/leader-change

        // ── Local test BOTS ──────────────────────────────────────────────────
        // Fake guests added to the lobby for visual testing of the party relationship. They live
        // in MemberSteamIds (so the party frame + gating treat them like real members) but carry no
        // Steam P2P — a BotPlayer drives their avatar locally.
        readonly List<string> _bots = new List<string>();
        public bool IsBot(string id) => id != null && _bots.Contains(id);

        public void AddBot(string botId)
        {
            EnsureLobby();
            if (!_bots.Contains(botId)) _bots.Add(botId);
            if (!MemberSteamIds.Contains(botId)) MemberSteamIds.Add(botId);
            OnLobbyChanged?.Invoke();
        }

        public void RemoveBot(string botId)
        {
            _bots.Remove(botId);
            MemberSteamIds.Remove(botId);
            OnLobbyChanged?.Invoke();
        }

        // ── Debug: fake lobby member (bot) ───────────────────────────────────────
        // Injects a synthetic member id so the party frame / aggro order / remote-avatar systems
        // can be exercised SOLO (no real Steam friend). MultiplayerSync spawns an avatar for it on
        // the OnLobbyChanged event; MageBot drives that avatar. Real Steam callbacks aren't touched.
        public void AddDebugBot(string botId)
        {
            if (string.IsNullOrEmpty(botId)) return;
            var me = SteamIntegration.Instance?.SteamId;
            if (string.IsNullOrEmpty(LobbyIdString)) { LobbyIdString = "DEBUG_LOBBY"; LeaderSteamId = me; }
            if (!string.IsNullOrEmpty(me) && !MemberSteamIds.Contains(me)) MemberSteamIds.Insert(0, me);
            if (!MemberSteamIds.Contains(botId)) MemberSteamIds.Add(botId);
            OnLobbyChanged?.Invoke();
        }

        public void RemoveDebugBot(string botId)
        {
            MemberSteamIds.Remove(botId);
            if (LobbyIdString == "DEBUG_LOBBY")
            {
                LobbyIdString = null; LeaderSteamId = null; MemberSteamIds.Clear();
            }
            OnLobbyChanged?.Invoke();
        }

#if STEAMWORKS_NET
        CSteamID _lobbyId;
        // Friends the user clicked "Convidar" for before the lobby finished being
        // created (CreateLobby is async). Flushed the moment OnLobbyCreated fires,
        // so an invite is never silently dropped by the create-lobby race.
        readonly List<CSteamID> _pendingInvites = new List<CSteamID>();
        // CreateLobby's result is a CallResult (tied to the specific call), NOT a
        // broadcast Callback — a Callback<LobbyCreated_t> does not fire reliably for
        // it, which is why the lobby was never being created. CallResult also reports
        // bIOFailure when Steam can't reach its servers.
        CallResult<LobbyCreated_t>         _crLobbyCreated;
        Callback<LobbyEnter_t>             _cbLobbyEnter;
        Callback<LobbyChatUpdate_t>        _cbLobbyChatUpdate;
        Callback<GameLobbyJoinRequested_t> _cbJoinRequested;
#endif

        // True between CreateLobby() and its OnLobbyCreated callback — prevents
        // repeated EnsureLobby() calls (popup open + scene load + invite click)
        // from spawning several lobbies and sending invites to the wrong one.
        bool _creatingLobby;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

#if STEAMWORKS_NET
            RegisterCallbacks();   // idempotent; retried lazily if Steam isn't ready yet
#endif
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Pre-create the lobby the moment the player reaches a gameplay scene, so
        // it's ready long before they open the invite popup — the invite then
        // sends instantly instead of racing the async CreateLobby.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Zulfarak" || scene.name == "Dungeon")
                EnsureLobby();
        }

        void Start()
        {
#if STEAMWORKS_NET
            RegisterCallbacks();   // Steam is initialized by now if it wasn't at Awake
            // Steam can launch the game with "+connect_lobby <id>" when a friend
            // accepts an invite from outside the game. Parse the command line
            // once at boot so the joiner ends up in the host's lobby instead of
            // sitting in their own empty world.
            string lobbyArg = FindCommandLineLobby();
            if (!string.IsNullOrEmpty(lobbyArg) && ulong.TryParse(lobbyArg, out ulong raw))
                JoinLobby(new CSteamID(raw));
#endif
        }

#if STEAMWORKS_NET
        bool _callbacksRegistered;

        // Registers the Steam callbacks. Idempotent and safe to call before Steam
        // finishes initializing — it simply no-ops until Steam is ready and is
        // retried lazily from EnsureLobby(), so Awake ordering between managers
        // (SteamIntegration vs SteamLobbyManager) can never leave it unregistered.
        void RegisterCallbacks()
        {
            if (_callbacksRegistered) return;
            if (SteamIntegration.Instance == null || !SteamIntegration.Instance.IsInitialized) return;
            _crLobbyCreated     = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            _cbLobbyEnter       = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            _cbLobbyChatUpdate  = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            _cbJoinRequested    = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _callbacksRegistered = true;
        }

        static string FindCommandLineLobby()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby") return args[i + 1];
            return null;
        }
#endif

        // ── Public API ────────────────────────────────────────────────────

        // Ensure the local player has a lobby (creates one on demand). Idempotent.
        public void EnsureLobby()
        {
            if (InLobby) return;
#if STEAMWORKS_NET
            if (_creatingLobby) return;   // a CreateLobby is already in flight
            if (SteamIntegration.Instance == null || !SteamIntegration.Instance.IsInitialized)
            {
                Debug.LogWarning("[SteamLobby] EnsureLobby abortado: Steam não inicializado.");
                return;
            }
            RegisterCallbacks();   // lazy: guarantees _crLobbyCreated exists even if Awake ran before Steam init
            if (_crLobbyCreated == null)
            {
                Debug.LogWarning("[SteamLobby] EnsureLobby abortado: callbacks não registrados.");
                return;
            }
            var call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers);
            if (call == SteamAPICall_t.Invalid)
            {
                _creatingLobby = false;
                Debug.LogWarning("[SteamLobby] CreateLobby não pôde ser chamado (SteamAPICall inválido).");
                return;
            }
            _crLobbyCreated.Set(call);   // receive the result via CallResult (reliable)
            _creatingLobby = true;
            Debug.Log("[SteamLobby] CreateLobby chamado (FriendsOnly). Aguardando OnLobbyCreated…");
#else
            // Stub: pretend we created a lobby so single-player flows that gate
            // on InLobby continue to work without Steamworks.
            LobbyIdString = "stub_lobby";
            LeaderSteamId = SteamIntegration.Instance?.SteamId ?? "local";
            MemberSteamIds.Clear();
            MemberSteamIds.Add(LeaderSteamId);
            OnLobbyChanged?.Invoke();
#endif
        }

        // Invite a specific friend to this lobby. Returns false only when Steam
        // itself is unavailable; otherwise the invite is either sent right away or
        // queued until the (async) lobby creation completes — so the UI can always
        // show "Enviado!" instead of the click doing nothing.
        public bool InviteFriend(ulong friendSteamId)
        {
#if STEAMWORKS_NET
            if (SteamIntegration.Instance == null || !SteamIntegration.Instance.IsInitialized)
            {
                Debug.LogWarning("[SteamLobby] Convite ignorado: Steam não inicializado " +
                                 "(steam_appid.txt ausente ao lado do .exe? Steam fechado?).");
                return false;
            }
            var friend = new CSteamID(friendSteamId);
            if (InLobby)
            {
                bool ok = SteamMatchmaking.InviteUserToLobby(_lobbyId, friend);
                Debug.Log($"[SteamLobby] InviteUserToLobby(lobby={_lobbyId}, friend={friend}) → {ok}. " +
                          $"Se ok=False: você não está no lobby, ou {friend} não é seu amigo na Steam.");
                return ok;
            }
            if (!_pendingInvites.Contains(friend)) _pendingInvites.Add(friend);
            Debug.Log($"[SteamLobby] Lobby ainda criando — convite p/ {friend} enfileirado (dispara em OnLobbyCreated).");
            EnsureLobby();   // fires the queued invites from OnLobbyCreated
            return true;
#else
            Debug.Log($"[SteamLobby] Stub build — would invite {friendSteamId}.");
            return true;
#endif
        }

        // Opens the Steam friends invite overlay scoped to the current lobby.
        // Creates the lobby first if the user hasn't been put in one yet.
        public void OpenInviteOverlay()
        {
#if STEAMWORKS_NET
            if (!InLobby) { EnsureLobby(); return; }   // pop overlay on the next call
            SteamFriends.ActivateGameOverlayInviteDialog(_lobbyId);
#else
            Debug.Log("[SteamLobby] Stub build — invite overlay unavailable.");
#endif
        }

#if STEAMWORKS_NET
        public void JoinLobby(CSteamID lobbyId)
        {
            SteamMatchmaking.JoinLobby(lobbyId);
        }
#endif

        public void LeaveLobby()
        {
#if STEAMWORKS_NET
            if (InLobby) SteamMatchmaking.LeaveLobby(_lobbyId);
            SteamFriends.ClearRichPresence();   // hide the "Entrar no jogo" button
#endif
            LobbyIdString = null;
            LeaderSteamId = null;
            MemberSteamIds.Clear();
            OnLobbyChanged?.Invoke();
        }

#if STEAMWORKS_NET
        // Publishes the "connect" rich-presence string for the current lobby. This
        // is what makes the "Entrar no jogo / Join Game" button appear on the Steam
        // friends list and what the invite/join carries as its launch argument.
        // (With AppID 480 this works for friends who already have the game open; a
        // published AppID is still required to auto-launch/download it.)
        void PublishConnectPresence()
        {
            if (!InLobby) return;
            SteamFriends.SetRichPresence("connect", $"+connect_lobby {LobbyIdString}");
            SteamFriends.SetRichPresence("steam_player_group",      LobbyIdString);
            SteamFriends.SetRichPresence("steam_player_group_size", MaxPlayers.ToString());
        }
#endif

#if STEAMWORKS_NET
        // ── Steam callbacks ──────────────────────────────────────────────
        void OnLobbyCreated(LobbyCreated_t r, bool bIOFailure)
        {
            _creatingLobby = false;
            if (bIOFailure)
            {
                Debug.LogWarning("[SteamLobby] CreateLobby FALHOU: IO failure — a Steam não " +
                                 "conseguiu falar com os servidores (Steam offline/sem rede?).");
                _pendingInvites.Clear();
                return;
            }
            if (r.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[SteamLobby] CreateLobby FALHOU: {r.m_eResult} " +
                                 "(Steam offline/sem conexão? tente novamente).");
                _pendingInvites.Clear();
                return;
            }
            _lobbyId      = new CSteamID(r.m_ulSteamIDLobby);
            LobbyIdString = _lobbyId.ToString();
            // Make the lobby discoverable to the leader's name so server-side
            // logs / debug overlays can map IDs to humans.
            SteamMatchmaking.SetLobbyData(_lobbyId, "leader_name",
                SteamIntegration.Instance.SteamName ?? "Unknown");

            Debug.Log($"[SteamLobby] Lobby criado: {LobbyIdString}. Enviando {_pendingInvites.Count} convite(s) enfileirado(s).");
            // Flush any invites the user clicked before the lobby existed.
            foreach (var friend in _pendingInvites)
            {
                bool ok = SteamMatchmaking.InviteUserToLobby(_lobbyId, friend);
                Debug.Log($"[SteamLobby] InviteUserToLobby(friend={friend}) → {ok}");
            }
            _pendingInvites.Clear();

            PublishConnectPresence();
            BroadcastLobbyMembers();
        }

        void OnLobbyEnter(LobbyEnter_t r)
        {
            _lobbyId      = new CSteamID(r.m_ulSteamIDLobby);
            LobbyIdString = _lobbyId.ToString();
            PublishConnectPresence();
            BroadcastLobbyMembers();
        }

        void OnLobbyChatUpdate(LobbyChatUpdate_t r)
        {
            // Member joined / left / disconnected — refresh the cached list.
            BroadcastLobbyMembers();
        }

        void OnJoinRequested(GameLobbyJoinRequested_t r)
        {
            JoinLobby(r.m_steamIDLobby);
        }

        void BroadcastLobbyMembers()
        {
            MemberSteamIds.Clear();
            int n = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
            for (int i = 0; i < n; i++)
            {
                var id = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyId, i);
                MemberSteamIds.Add(id.ToString());
            }
            LeaderSteamId = SteamMatchmaking.GetLobbyOwner(_lobbyId).ToString();

            // Keep local test bots in the roster across any Steam-driven refresh, so an active bot
            // never silently vanishes from the party on a lobby update.
            foreach (var b in _bots) if (!MemberSteamIds.Contains(b)) MemberSteamIds.Add(b);

            // TODO(multiplayer): when a new member appears, spawn a remote-player
            // avatar in the current scene and start syncing position / animation
            // / damage events over SteamNetworking / SteamNetworkingMessages.
            // For now we just expose the membership list so other systems
            // (e.g. PortalEnter coordination) can read it.

            OnLobbyChanged?.Invoke();
        }
#endif
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
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

#if STEAMWORKS_NET
        CSteamID _lobbyId;
        Callback<LobbyCreated_t>           _cbLobbyCreated;
        Callback<LobbyEnter_t>             _cbLobbyEnter;
        Callback<LobbyChatUpdate_t>        _cbLobbyChatUpdate;
        Callback<GameLobbyJoinRequested_t> _cbJoinRequested;
#endif

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if STEAMWORKS_NET
            if (SteamIntegration.Instance != null && SteamIntegration.Instance.IsInitialized)
                RegisterCallbacks();
#endif
        }

        void Start()
        {
#if STEAMWORKS_NET
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
        void RegisterCallbacks()
        {
            _cbLobbyCreated     = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _cbLobbyEnter       = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            _cbLobbyChatUpdate  = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            _cbJoinRequested    = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
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
            if (SteamIntegration.Instance == null || !SteamIntegration.Instance.IsInitialized) return;
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers);
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
#endif
            LobbyIdString = null;
            LeaderSteamId = null;
            MemberSteamIds.Clear();
            OnLobbyChanged?.Invoke();
        }

#if STEAMWORKS_NET
        // ── Steam callbacks ──────────────────────────────────────────────
        void OnLobbyCreated(LobbyCreated_t r)
        {
            if (r.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[SteamLobby] CreateLobby failed: {r.m_eResult}");
                return;
            }
            _lobbyId      = new CSteamID(r.m_ulSteamIDLobby);
            LobbyIdString = _lobbyId.ToString();
            // Make the lobby discoverable to the leader's name so server-side
            // logs / debug overlays can map IDs to humans.
            SteamMatchmaking.SetLobbyData(_lobbyId, "leader_name",
                SteamIntegration.Instance.SteamName ?? "Unknown");
            BroadcastLobbyMembers();
        }

        void OnLobbyEnter(LobbyEnter_t r)
        {
            _lobbyId      = new CSteamID(r.m_ulSteamIDLobby);
            LobbyIdString = _lobbyId.ToString();
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

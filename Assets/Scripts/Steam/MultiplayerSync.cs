using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace ZulfarakRPG
{
    // High-level multiplayer protocol on top of SteamP2P.
    //
    // Once per StateBroadcastInterval the local player's position + facing +
    // class + name + current anim key is JSON-encoded and reliably sent to
    // every other lobby member. Incoming STATE packets drive the matching
    // RemotePlayer avatar. The leader also emits a PORTAL packet right
    // before transitioning so every follower transits to the same scene.
    public class MultiplayerSync : MonoBehaviour
    {
        public static MultiplayerSync Instance { get; private set; }

        public const float StateBroadcastInterval = 0.1f;   // 10 Hz

        // ── Singleton + lifetime ─────────────────────────────────────────
        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SteamP2P.Instance != null) SteamP2P.Instance.OnMessage += OnP2PMessage;
            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.OnLobbyChanged += OnLobbyChanged;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (SteamP2P.Instance != null) SteamP2P.Instance.OnMessage -= OnP2PMessage;
            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.OnLobbyChanged -= OnLobbyChanged;
        }

        // ── Scene + lobby state ──────────────────────────────────────────
        readonly Dictionary<string, RemotePlayer> _remotes = new Dictionary<string, RemotePlayer>();
        PlayerController2D _localPlayer;
        float              _nextBroadcastAt;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Avatars are owned by the unloaded scene — clear the cache and let
            // the new scene re-spawn them from the current lobby membership.
            _remotes.Clear();
            _localPlayer = null;
            if (IsGameplayScene()) RefreshRemotesFromLobby();
        }

        void OnLobbyChanged()
        {
            if (IsGameplayScene()) RefreshRemotesFromLobby();
        }

        bool IsGameplayScene()
        {
            var n = SceneManager.GetActiveScene().name;
            return n == "Zulfarak" || n == "Dungeon";
        }

        void RefreshRemotesFromLobby()
        {
            var lobby = SteamLobbyManager.Instance;
            if (lobby == null) return;

            var myId   = SteamIntegration.Instance?.SteamId;
            var wanted = new HashSet<string>();
            foreach (var id in lobby.MemberSteamIds)
                if (id != myId) wanted.Add(id);

            // Spawn newcomers.
            foreach (var id in wanted)
            {
                if (_remotes.ContainsKey(id)) continue;
                var rp = SpawnRemoteAvatar(id);
                if (rp != null) _remotes[id] = rp;
            }

            // Despawn anyone who left.
            var stale = new List<string>();
            foreach (var kv in _remotes) if (!wanted.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var id in stale)
            {
                if (_remotes[id] != null) Destroy(_remotes[id].gameObject);
                _remotes.Remove(id);
            }
        }

        RemotePlayer SpawnRemoteAvatar(string steamId)
        {
            // Spawn just to the right of the local player so they're visible
            // immediately; position is overwritten by the first STATE packet.
            Vector3 spawn = new Vector3(2.5f, -1.144f, 0f);
            var lp = _localPlayer ?? UnityEngine.Object.FindAnyObjectByType<PlayerController2D>();
            if (lp != null) spawn = lp.transform.position + new Vector3(0.6f, 0f, 0f);

            var go = new GameObject($"Remote_{steamId}");
            go.transform.position = spawn;
            var rp = go.AddComponent<RemotePlayer>();
            rp.SteamId = steamId;
            rp.Configure(ClassType.Warrior, $"Player {steamId.Substring(System.Math.Max(0, steamId.Length - 4))}");
            return rp;
        }

        // ── Outbound broadcast ───────────────────────────────────────────
        void Update()
        {
            if (!IsGameplayScene()) return;
            if (_localPlayer == null) _localPlayer = UnityEngine.Object.FindAnyObjectByType<PlayerController2D>();
            if (_localPlayer == null) return;

            if (Time.unscaledTime < _nextBroadcastAt) return;
            _nextBroadcastAt = Time.unscaledTime + StateBroadcastInterval;

            BroadcastLocalState();
        }

        void BroadcastLocalState()
        {
            var lobby = SteamLobbyManager.Instance;
            if (lobby == null || lobby.MemberSteamIds.Count <= 1) return;

            var sr = _localPlayer.GetComponent<SpriteRenderer>();
            var rb = _localPlayer.GetComponent<Rigidbody2D>();
            // Priority: attacking > walking > idle, so the partner sees swings/casts.
            string anim = "idle";
            if (_localPlayer.IsAttacking) anim = "atk";
            else if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.05f) anim = "walk";

            var pd = PlayerManager.Instance?.Data;
            var msg = new P2PMessage
            {
                op    = "state",
                x     = _localPlayer.transform.position.x,
                y     = _localPlayer.transform.position.y,
                flipX = sr != null && sr.flipX,
                anim  = anim,
                cls   = pd != null ? (int)pd.classType : (int)ClassType.Warrior,
                name  = pd?.playerName ?? SteamIntegration.Instance?.SteamName ?? "Player",
                hp    = _localPlayer.Health,
                maxHp = _localPlayer.MaxHealthValue,
            };
            SendToAllExceptMe(msg);
        }

        public void BroadcastPortalEnter(string destinationScene)
        {
            SendToAllExceptMe(new P2PMessage { op = "portal", dest = destinationScene });
        }

        // Co-op combat sync: whenever the local player lands a hit (melee, arrow,
        // fireball), broadcast the damage so the remote client applies the same hit
        // to their local copy of the enemy. Both clients spawn identical enemies
        // deterministically via WaveManager, so netInstanceId matches on both sides.
        public void BroadcastDamage(string netInstanceId, float dmg, bool crit)
        {
            if (string.IsNullOrEmpty(netInstanceId)) return;
            SendToAllExceptMe(new P2PMessage
            {
                op    = "damage",
                netId = netInstanceId,
                dmg   = dmg,
                crit  = crit,
            });
        }

        void SendToAllExceptMe(P2PMessage msg)
        {
            if (SteamP2P.Instance == null || SteamLobbyManager.Instance == null) return;
            var myId = SteamIntegration.Instance?.SteamId;
            var json = JsonConvert.SerializeObject(msg);
            var data = Encoding.UTF8.GetBytes(json);
            foreach (var id in SteamLobbyManager.Instance.MemberSteamIds)
            {
                if (id == myId) continue;
                SteamP2P.Instance.SendTo(id, data);
            }
        }

        // ── Inbound dispatch ─────────────────────────────────────────────
#if STEAMWORKS_NET
        void OnP2PMessage(CSteamID sender, byte[] data)
        {
            string senderId = sender.ToString();
#else
        void OnP2PMessage(string senderId, byte[] data)
        {
#endif
            P2PMessage msg;
            try
            {
                msg = JsonConvert.DeserializeObject<P2PMessage>(Encoding.UTF8.GetString(data));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MPSync] decode failed: {e.Message}");
                return;
            }
            if (msg == null || string.IsNullOrEmpty(msg.op)) return;

            switch (msg.op)
            {
                case "state":
                    if (!_remotes.TryGetValue(senderId, out var rp))
                    {
                        rp = SpawnRemoteAvatar(senderId);
                        if (rp != null) _remotes[senderId] = rp;
                    }
                    if (rp != null)
                    {
                        rp.Configure((ClassType)msg.cls, msg.name);
                        rp.ApplyState(msg.x, msg.y, msg.flipX, msg.anim);
                        rp.SetHealth(msg.hp, msg.maxHp);
                    }
                    break;

                case "portal":
                    // A leader triggered the group transit — follow them.
                    if (!string.IsNullOrEmpty(msg.dest))
                        SceneManager.LoadScene(msg.dest);
                    break;

                case "damage":
                    // Remote player hit an enemy — apply the same damage to our
                    // local copy so both clients see synchronized HP/kills.
                    if (!string.IsNullOrEmpty(msg.netId))
                    {
                        foreach (var e in UnityEngine.Object.FindObjectsByType<SkeletonEnemy>(FindObjectsSortMode.None))
                        {
                            if (e != null && e.IsAlive && e.netInstanceId == msg.netId)
                            {
                                e.TakeDamage(msg.dmg, msg.crit);
                                break;
                            }
                        }
                    }
                    break;
            }
        }

        [Serializable]
        class P2PMessage
        {
            public string op;
            public float  x;
            public float  y;
            public bool   flipX;
            public string anim;
            public int    cls;
            public string name;
            public string dest;
            public string netId;   // per-instance enemy id for damage packets
            public float  dmg;     // damage amount for "damage" op
            public bool   crit;    // crit flag for "damage" op
            public float  hp;      // sender's current HP ("state" op)
            public float  maxHp;   // sender's max HP ("state" op)
        }
    }
}

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

        // The remote avatar for a lobby member's Steam ID (or null) — used by the party frame.
        public RemotePlayer GetRemote(string steamId)
            => (steamId != null && _remotes.TryGetValue(steamId, out var rp)) ? rp : null;
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
            return n == "Zulfarak" || n == "Dungeon" || n.StartsWith("Camp_") || n.StartsWith("Dungeon_");
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
            // IsRunning covers the inter-wave march, where velocity is zero but the
            // hero is visibly walking (parallax scroll) — broadcast "walk" so the
            // remote avatar marches too instead of idling.
            string anim = "idle";
            if (_localPlayer.IsAttacking) anim = "atk";
            else if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.05f) anim = "walk";
            else if (_localPlayer.IsRunning) anim = "walk";

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
                scene = SceneManager.GetActiveScene().name,   // survivors hide partners in other scenes
            };
            SendToAllExceptMe(msg);
        }

        public void BroadcastPortalEnter(string destinationScene)
        {
            SendToAllExceptMe(new P2PMessage { op = "portal", dest = destinationScene });
        }

        // Party aggro order changed (a portrait was dragged) — share it so every client's enemies
        // focus the same "player #1".
        public void BroadcastPartyOrder(List<string> ids)
            => SendToAllExceptMe(new P2PMessage { op = "order", order = string.Join(",", ids) });

        // Co-op VISUAL sync: when the local ranged hero shoots (arrow / fireball), tell the
        // others so a cosmetic projectile flies from this avatar toward the target on their
        // screens — the partner can actually SEE the shot. Damage syncs via BroadcastDamage.
        public void BroadcastShoot(ClassType cls, Vector3 targetPos)
        {
            SendToAllExceptMe(new P2PMessage { op = "shoot", cls = (int)cls, tx = targetPos.x, ty = targetPos.y });
        }

        // Warrior's melee hit — replayed as a slash spark at the impact point so the swing lands
        // visibly on every screen (the melee counterpart to the ranged classes' "shoot").
        public void BroadcastMelee(Vector3 hitPos, bool crit)
            => SendToAllExceptMe(new P2PMessage { op = "melee", tx = hitPos.x, ty = hitPos.y, crit = crit });

        // Co-op combat sync: whenever the local player lands a hit (melee, arrow,
        // fireball), broadcast the damage so the remote client applies the same hit
        // to their local copy of the enemy. Both clients spawn identical enemies
        // deterministically via WaveManager, so netInstanceId matches on both sides.
        public void BroadcastDamage(string netInstanceId, float dmg, bool crit, bool poison = false)
        {
            if (string.IsNullOrEmpty(netInstanceId)) return;
            SendToAllExceptMe(new P2PMessage
            {
                op     = "damage",
                netId  = netInstanceId,
                dmg    = dmg,
                crit   = crit,
                poison = poison,
            });
        }

        // ── Skill VFX sync (both players see each other's magic) ─────────────
        // The big skill burst (SkillEffectAnim). Replayed at the same world point.
        public void BroadcastSkillBurst(Vector3 pos, int sheet, int cols, int rows, Color color, float scale)
            => SendToAllExceptMe(new P2PMessage
            {
                op = "vfx", vfx = "burst", tx = pos.x, ty = pos.y,
                fxSheet = sheet, fxCols = cols, fxRows = rows,
                col = ColorUtility.ToHtmlStringRGB(color), scale = scale
            });

        // Archer's zig-zag venom arrow (Tiro de Serpe).
        public void BroadcastSerpent(Vector3 from, Vector3 to)
            => SendToAllExceptMe(new P2PMessage { op = "vfx", vfx = "serpent", ox = from.x, oy = from.y, tx = to.x, ty = to.y });

        // Archer's charged white shot (Tiro Concentrado).
        public void BroadcastConcentrated(Vector3 from, Vector3 to)
            => SendToAllExceptMe(new P2PMessage { op = "vfx", vfx = "concentrated", ox = from.x, oy = from.y, tx = to.x, ty = to.y });

        // Tiro Concentrado's eagle CHARGE telegraph (wind-up) — replayed on the partner's avatar so
        // the whole skill cast is visible, not just the shot. scale = duration, oy = archer height.
        public void BroadcastEagleCharge(float dur, float height)
            => SendToAllExceptMe(new P2PMessage { op = "vfx", vfx = "eagle_charge", scale = dur, oy = height });

        // One falling arrow of the archer's Chuva de Flechas.
        public void BroadcastArrowFall(Vector3 landing)
            => SendToAllExceptMe(new P2PMessage { op = "vfx", vfx = "rain", tx = landing.x, ty = landing.y });

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
                        // Only show a partner who is in the SAME scene. A player who died (or took a
                        // portal) is now in another scene — hide their avatar so they leave the
                        // dungeon cleanly for the survivors instead of ghosting at stale coords.
                        bool sameScene = string.IsNullOrEmpty(msg.scene)
                                      || msg.scene == SceneManager.GetActiveScene().name;
                        if (rp.gameObject.activeSelf != sameScene) rp.gameObject.SetActive(sameScene);
                        if (sameScene)
                        {
                            rp.Configure((ClassType)msg.cls, msg.name);
                            rp.ApplyState(msg.x, msg.y, msg.flipX, msg.anim);
                            rp.SetHealth(msg.hp, msg.maxHp);
                        }
                    }
                    break;

                case "portal":
                    // A leader triggered the group transit — follow them.
                    if (!string.IsNullOrEmpty(msg.dest))
                        SceneManager.LoadScene(msg.dest);
                    break;

                case "order":
                    // A partner re-sorted the party aggro order — adopt it (enemies re-focus).
                    if (!string.IsNullOrEmpty(msg.order))
                        PartyOrder.Receive(msg.order.Split(','));
                    break;

                case "shoot":
                    // A partner fired a ranged attack — spawn a cosmetic projectile from
                    // their avatar toward the target so we see the shot.
                    if (_remotes.TryGetValue(senderId, out var shooter) && shooter != null)
                    {
                        bool fireball = (ClassType)msg.cls == ClassType.Mage;
                        Vector3 from = shooter.transform.position + Vector3.up * 0.5f;
                        Vector3 to   = new Vector3(msg.tx, msg.ty, 0f);
                        CosmeticProjectile.Spawn(from, to, fireball);
                    }
                    break;

                case "melee":
                    // A partner's warrior swing landed — show the same slash spark at the hit point.
                    MeleeHit.Spawn(new Vector3(msg.tx, msg.ty, 0f), msg.crit);
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
                                if (msg.poison) e.ApplyRemotePoisonTick(msg.dmg);  // green, no flash/stun
                                else            e.TakeDamage(msg.dmg, msg.crit);
                                break;
                            }
                        }
                    }
                    break;

                case "vfx":
                    // A partner's skill effect — replay it here (COSMETIC; damage syncs via
                    // "damage" packets) so both players see each other's magic.
                    if (msg.vfx == "eagle_charge")
                    {
                        // Charge telegraph follows the caster's avatar, so it needs the sender.
                        if (_remotes.TryGetValue(senderId, out var caster) && caster != null)
                            RemoteCharge.Spawn(caster.transform, msg.scale, msg.oy);
                    }
                    else ReplayVfx(msg);
                    break;
            }
        }

        // Rebuilds a partner's skill effect locally (visual only).
        void ReplayVfx(P2PMessage msg)
        {
            Vector3 a = new Vector3(msg.ox, msg.oy, 0f);
            Vector3 b = new Vector3(msg.tx, msg.ty, 0f);
            switch (msg.vfx)
            {
                case "burst":
                    Color color = Color.white;
                    if (!string.IsNullOrEmpty(msg.col)) ColorUtility.TryParseHtmlString("#" + msg.col, out color);
                    SkillEffectAnim.Spawn(b, msg.fxSheet, msg.fxCols, msg.fxRows, color, msg.scale);
                    break;
                case "serpent":
                    SerpentArrow.SpawnCosmetic(a, b);
                    break;
                case "concentrated":
                    SkillCastFX.Spawn(a, Color.white);
                    CosmeticProjectile.Spawn(a, b, false);
                    break;
                case "rain":
                    FallingArrow.SpawnCosmetic(b, 0f, Arrow.FlatSprite);   // same flat arrow as local
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
            public string scene;   // sender's active scene ("state" op) — hide out-of-scene avatars
            public string dest;
            public string order;   // comma-joined party aggro order ("order" op)
            public string netId;   // per-instance enemy id for damage packets
            public float  dmg;     // damage amount for "damage" op
            public bool   crit;    // crit flag for "damage" op
            public float  hp;      // sender's current HP ("state" op)
            public float  maxHp;   // sender's max HP ("state" op)
            public float  tx;      // shot/vfx target X ("shoot"/"vfx" op)
            public float  ty;      // shot/vfx target Y ("shoot"/"vfx" op)
            public bool   poison;  // poison-tick flag ("damage" op)
            public string vfx;     // skill-vfx kind ("vfx" op): burst/serpent/concentrated/rain
            public int    fxSheet; // PixelEffect sheet for "burst"
            public int    fxCols;  // sheet grid cols for "burst"
            public int    fxRows;  // sheet grid rows for "burst"
            public string col;     // hex RGB colour for "burst"
            public float  scale;   // effect scale for "burst"
            public float  ox;      // vfx origin X (serpent/concentrated)
            public float  oy;      // vfx origin Y (serpent/concentrated)
        }
    }
}

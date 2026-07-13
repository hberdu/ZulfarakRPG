using UnityEngine;

namespace ZulfarakRPG
{
    // DEBUG helper: press F9 to add / remove a MAGE BOT that behaves like a normal lobby player.
    // It shows up in the party frame (numbered, drag-reorderable, part of the aggro order), follows
    // you around, fights enemies with fireballs, takes damage while it tanks (aggro #1) and
    // dies / revives — all driven LOCALLY so the whole lobby scheme can be tested without a second
    // Steam player. Registered as a fake lobby member; MultiplayerSync spawns its avatar, this
    // drives it.
    public class MageBot : MonoBehaviour
    {
        const string BotId   = "BOT_MAGE_TEST";
        const string BotName = "Mago Bot";

        RemotePlayer _rp;
        bool    _active;
        float   _hp = 100f, _maxHp = 100f;
        float   _castTimer, _hitTimer, _respawnAt = -1f;
        Vector3 _pos;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot()
        {
            var go = new GameObject("MageBotDriver");
            DontDestroyOnLoad(go);
            go.AddComponent<MageBot>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) Toggle();
            if (!_active) return;

            var rp = MultiplayerSync.Instance?.GetRemote(BotId);
            if (rp == null) return;                 // avatar not spawned yet (or scene still loading)
            if (rp != _rp)                          // (re)acquired — e.g. after a scene change
            {
                _rp = rp;
                _rp.Configure(ClassType.Mage, BotName);
                var lp0 = Object.FindAnyObjectByType<PlayerController2D>();
                _pos = lp0 != null ? lp0.transform.position + new Vector3(-0.8f, 0f, 0f) : _rp.transform.position;
                _hp  = _maxHp;
            }
            DriveAI();
        }

        void Toggle()
        {
            _active = !_active;
            if (_active) { SteamLobbyManager.Instance?.AddDebugBot(BotId); _hp = _maxHp; _respawnAt = -1f; }
            else         { SteamLobbyManager.Instance?.RemoveDebugBot(BotId); _rp = null; }
            Debug.Log($"[MageBot] {(_active ? "ligado" : "desligado")} (F9).");
        }

        void DriveAI()
        {
            var lp = Object.FindAnyObjectByType<PlayerController2D>();
            float groundY = lp != null ? lp.transform.position.y : _pos.y;

            // Dead → hold the death pose, then revive after a beat.
            if (_hp <= 0f)
            {
                if (_respawnAt < 0f) _respawnAt = Time.time + 3f;
                _rp.SetHealth(0f, _maxHp);
                _rp.ApplyState(_pos.x, groundY, false, "idle");
                if (Time.time >= _respawnAt) { _hp = _maxHp; _respawnAt = -1f; }
                return;
            }

            var enemy = NearestEnemy(_pos, 22f);
            string anim = "idle";
            bool   flip = false;

            if (enemy != null)
            {
                float dx = enemy.transform.position.x - _pos.x;
                flip = dx < 0f;
                if (Mathf.Abs(dx) > 3.0f) { _pos.x += Mathf.Sign(dx) * 2.2f * Time.deltaTime; anim = "walk"; }
                else
                {
                    anim = "atk";
                    _castTimer -= Time.deltaTime;
                    if (_castTimer <= 0f) { CastFireball(enemy, groundY); _castTimer = 1.5f; }
                }
                // While tanking (aggro #1) enemies close in — take DISCRETE hits (each fires one
                // hurt anim + damage popup, not a per-frame spam); otherwise regenerate.
                bool tanking = Mathf.Abs(dx) < 1.3f;
                _hitTimer -= Time.deltaTime;
                if (tanking) { if (_hitTimer <= 0f) { _hp = Mathf.Max(0f, _hp - 12f); _hitTimer = 0.7f; } }
                else _hp = Mathf.Min(_maxHp, _hp + 4f * Time.deltaTime);
            }
            else if (lp != null)
            {
                float dx = (lp.transform.position.x - 0.8f) - _pos.x;   // idle just left of the hero
                if (Mathf.Abs(dx) > 0.15f) { _pos.x += Mathf.Sign(dx) * 1.6f * Time.deltaTime; anim = "walk"; flip = dx < 0f; }
                _hp = Mathf.Min(_maxHp, _hp + 6f * Time.deltaTime);
            }

            _pos.x = Mathf.Clamp(_pos.x, MapBounds.MinX, MapBounds.MaxX);
            _rp.ApplyState(_pos.x, groundY, flip, anim);
            _rp.SetHealth(_hp, _maxHp);
        }

        void CastFireball(SkeletonEnemy enemy, float groundY)
        {
            if (enemy == null) return;
            var go = new GameObject("BotFireball");
            go.transform.position = new Vector3(_pos.x, groundY + 0.45f, 0f);
            go.AddComponent<Fireball>().Init(enemy, 35f, false, null);
        }

        static SkeletonEnemy NearestEnemy(Vector3 from, float radius)
        {
            SkeletonEnemy best = null; float bd = float.MaxValue;
            foreach (var e in SkeletonEnemy.AliveInRadius(from, radius))
            {
                float d = Mathf.Abs(e.transform.position.x - from.x);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }
    }
}

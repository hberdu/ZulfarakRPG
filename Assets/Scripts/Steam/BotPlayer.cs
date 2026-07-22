using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Local fake teammate for VISUAL testing of the party / co-op relationship. Joins the lobby as a
    // guest (SteamLobbyManager.AddBot) so it shows in the party frame + world HP bar like a real
    // member — but is driven entirely locally: a MAGE that lines up in the PARTY-ORDER slot (the #1
    // "tank" walks in front toward the enemies, #2 behind, …), lobs fireballs, and plays its full
    // idle / walk / attack / hurt / death animations so a co-op fight reads fluidly. Level mirrors the
    // player; no equipped items. Follows the party across scenes (into the dungeon).
    public class BotPlayer : MonoBehaviour
    {
        public const string BotId = "BOT_1";
        static BotPlayer _instance;
        static readonly Dictionary<string, BotPlayer> _registry = new Dictionary<string, BotPlayer>();
        public static BotPlayer Get(string id) => (id != null && _registry.TryGetValue(id, out var b)) ? b : null;
        public static bool Active => _instance != null;

        public string    PlayerName    => $"BOT (Lv {Level})";
        public ClassType ClassType     => ClassType.Mage;
        public float     HpFraction    => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 1f;
        public int       Level         => PlayerManager.Instance != null && PlayerManager.Instance.Data != null
                                          ? PlayerManager.Instance.Data.level : 1;
        public Sprite    PortraitSprite => (_idle != null && _idle.Length > 0) ? _idle[0] : null;

        SpriteRenderer _sr;
        WorldHealthBar _hpBar;
        Sprite[] _idle, _walk, _atk, _hurt, _death;
        Coroutine _animCo; string _curKey = "idle"; bool _locked, _dead;
        float _castTimer, _rebindTimer, _hitTimer, _reviveTimer, _hp, _maxHp = 100f;

        public static void Toggle()
        {
            if (_instance != null) { _instance.Despawn(); return; }
            var go = new GameObject("BotPlayer");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BotPlayer>();
        }

        void Awake()
        {
            _registry[BotId] = this;
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 5;
            var hb = new GameObject("HealthBar");
            hb.transform.SetParent(transform, false);
            hb.transform.localPosition = new Vector3(0f, 0.62f, -0.1f);
            _hpBar = hb.AddComponent<WorldHealthBar>();

            SteamLobbyManager.Instance?.AddBot(BotId);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Rebind();
            SnapToParty();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_registry.TryGetValue(BotId, out var b) && b == this) _registry.Remove(BotId);
            if (_instance == this) _instance = null;
        }

        void Despawn()
        {
            SteamLobbyManager.Instance?.RemoveBot(BotId);
            Destroy(gameObject);
        }

        // The party is pulled into the dungeon together — re-borrow the new hero's sprites and
        // snap into position so the bot actually comes along instead of being left behind.
        void OnSceneLoaded(Scene s, LoadSceneMode m) => StartCoroutine(SnapNextFrames());
        IEnumerator SnapNextFrames()
        {
            // Wait for the new scene's hero to exist before rebinding + snapping. Firing after a
            // fixed 2 frames often ran BEFORE the dungeon hero spawned, so the bot bound nothing
            // and stayed invisible/offscreen in the dungeon (only reappearing on a manual
            // re-toggle). Cap the wait so a heroless scene can't spin forever.
            for (int i = 0; i < 240 && Object.FindAnyObjectByType<PlayerController2D>() == null; i++)
                yield return null;
            Rebind();
            SnapToParty();
        }

        void Rebind()
        {
            var lp = Object.FindAnyObjectByType<PlayerController2D>();
            if (lp == null) return;
            // Size is synced to the hero every frame in Update (a SET, so it converges to the
            // hero's CURRENT scale each scene and never accumulates → no more ballooning on map
            // change). No one-time capture here — that caught the pre-shrink scale at bad frames.
            var lpSr = lp.GetComponent<SpriteRenderer>();
            // Copy the hero's sorting layer AND material — a runtime SpriteRenderer keeps the default
            // material, which the dungeon's URP 2D renderer can drop (the bot "vanished" on entry).
            if (_sr != null && lpSr != null) { _sr.sortingLayerID = lpSr.sortingLayerID; _sr.sharedMaterial = lpSr.sharedMaterial; }

            _idle  = lp.wizardIdleFrames;  _walk = lp.wizardWalkFrames;
            _atk   = (lp.wizardAttack1Frames != null && lp.wizardAttack1Frames.Length > 0) ? lp.wizardAttack1Frames : lp.wizardAttackFrames;
            _hurt  = lp.wizardHurtFrames;   _death = lp.wizardDeathFrames;
            if (_idle == null || _idle.Length == 0) _idle = lp.soldierIdleFrames;
            if (_walk == null || _walk.Length == 0) _walk = _idle;
            if (_atk  == null || _atk.Length  == 0) _atk  = _idle;

            _maxHp = 40f + 20f * Mathf.Max(0, Level - 1);
            if (_hp <= 0f || _hp > _maxHp) _hp = _maxHp;

            // Show the first idle frame BEFORE sizing the bar: AttachAbove measures the sprite's
            // visible pixels, and a still-null sprite makes it early-out leaving the huge default
            // bar width — that's why the bot's HP bar looked oversized/disproportional.
            if (_sr != null && _idle != null && _idle.Length > 0) _sr.sprite = _idle[0];
            _hpBar?.AttachAbove(_sr, fillColor: new Color(0.42f, 0.62f, 1f, 1f), widthMultiplier: 0.6f);   // blue = allied bot
            _hpBar?.SetHealth(_hp, _maxHp);
            _hpBar?.SetName(PlayerName);
            _locked = false; _curKey = ""; SwitchAnim("idle", true);
        }

        void SnapToParty()
        {
            var lp = Object.FindAnyObjectByType<PlayerController2D>();
            if (lp != null) transform.position = lp.transform.position + new Vector3(SlotOffsetX(lp), 0f, 0f);
        }

        // X offset from the hero based on the PARTY ORDER: #1 leads toward the enemies (+x), each
        // later slot sits one step behind. (Enemies enter from +x, so "front" = +x.)
        float SlotOffsetX(PlayerController2D lp)
        {
            const float spacing = 0.7f;
            int botIdx = PartyOrder.IndexOf(BotId);
            int myIdx  = PartyOrder.IndexOf(SteamIntegration.Instance?.SteamId);
            if (botIdx < 0) botIdx = 1;
            if (myIdx  < 0) myIdx  = 0;
            return (myIdx - botIdx) * spacing;
        }

        public bool IsAlive => !_dead;

        // Real hit from an enemy that aggro'd the bot (the party tank). Drives its hurt / death.
        public void TakeDamage(float dmg)
        {
            if (_dead || dmg <= 0f) return;
            _hp = Mathf.Max(0f, _hp - dmg);
            _hpBar?.SetHealth(_hp, _maxHp);
            DamagePopup.Spawn(transform, dmg, new Color(1f, 0.45f, 0.45f, 1f));
            if (_hp <= 0f) { _dead = true; _reviveTimer = 3f; PlayOneShot(_death, 12f, hold: true); }
            else PlayOneShot(_hurt, 14f, hold: false);
        }

        // Seat the bot's VISIBLE feet on the ground line (bottom-centre pivot), so it never floats —
        // copying the hero's Y misaligned because the mage sprite's feet sit at a different frame row.
        void GroundSeat()
        {
            float g = GroundAlignUtil.FindGroundTopY();
            float scale = Mathf.Abs(transform.lossyScale.y);
            // Seat by the actual FEET (feetFromBottom), like the hero's GroundAlignUtil — using the
            // lowest visible pixel (bottomFromBottom) sat any shadow/aura on the ground and left the
            // feet floating a bit ABOVE the line.
            float feet = (_sr != null && _sr.sprite != null) ? SpriteAlphaBounds.Get(_sr.sprite).feetFromBottom * scale : 0f;
            transform.position = new Vector3(transform.position.x, g - feet, transform.position.z);
        }

        void Update()
        {
            var lp = Object.FindAnyObjectByType<PlayerController2D>();
            if (lp == null) return;

            _rebindTimer -= Time.deltaTime;
            if (_rebindTimer <= 0f) { _rebindTimer = 2f; if (_idle == null) Rebind(); _maxHp = 40f + 20f * Mathf.Max(0, Level - 1); }

            _hpBar?.SetName(PlayerName);

            // Dead → hold the death pose, then revive at full.
            if (_dead)
            {
                _reviveTimer -= Time.deltaTime;
                if (_reviveTimer <= 0f) { _dead = false; _hp = _maxHp; _hpBar?.SetHealth(_hp, _maxHp); _locked = false; SwitchAnim("idle", true); }
                return;
            }

            // Follow the party-order slot.
            Vector3 hp = lp.transform.position;
            float wantX = hp.x + SlotOffsetX(lp);
            float dx = wantX - transform.position.x;
            bool moving = Mathf.Abs(dx) > 0.06f;
            if (moving)
            {
                float step = Mathf.Clamp(dx, -2.8f * Time.deltaTime, 2.8f * Time.deltaTime);
                transform.position += new Vector3(step, 0f, 0f);
                if (_sr != null) _sr.flipX = dx < 0f;
            }
            transform.localScale = lp.transform.lossyScale;   // match the hero's CURRENT size every frame (no accumulation → never balloons across maps)
            GroundSeat();   // seat by OWN visible feet so it never floats (city/camp/dungeon)
            if (!_locked) SwitchAnim(moving ? "walk" : "idle", false);
            // Real damage now comes from enemies that aggro the bot (see SkeletonEnemy) — no more
            // simulated self-hits.

            // Cast the mage's FIRST TWO spells (level 1), alternating — exactly like a player's
            // SkillAutoCaster: the same cast animation + SkillEffectAnim burst + area damage, and
            // the hit/effect are broadcast so partners see it too. Casts even while repositioning
            // (gating on !moving meant the ever-trailing bot almost never fired).
            _castTimer -= Time.deltaTime;
            if (!_locked && _castTimer <= 0f)
            {
                var cast = SkeletonEnemy.AliveInRadius(transform.position, 12f);
                if (cast.Count > 0 && cast[0] != null)
                {
                    var col = cast[0].GetComponent<Collider2D>();
                    Vector3 center = col != null ? col.bounds.center : cast[0].transform.position + Vector3.up * 0.5f;
                    if (_sr != null) _sr.flipX = center.x < transform.position.x;
                    PlayOneShot(_atk, 12f, hold: false);
                    StartCoroutine(CastSpellAtApex(BotSkills[_skillIdx], center));
                    _skillIdx = (_skillIdx + 1) % BotSkills.Length;
                    _castTimer = 2.2f;
                }
            }
        }

        // The bot's equipped spells: the mage's first two, level 1.
        static readonly string[] BotSkills = { "m_fogo", "m_gelo" };
        int _skillIdx;

        // Land the AoE spell at the cast apex (after the wind-up), mirroring SkillAutoCaster's
        // ApplyAreaAtApex: damage every enemy in the blast + spawn the pixel FX burst + broadcast.
        IEnumerator CastSpellAtApex(string skillId, Vector3 center)
        {
            var def = SkillDefs.Get(skillId);
            if (def == null) yield break;
            yield return new WaitForSeconds(0.18f);
            float atk = 10f + 2f * Mathf.Max(0, Level - 1);
            float dmg = def.PowerAt(1) + atk * 0.5f;
            foreach (var e in SkeletonEnemy.AliveInRadius(center, 1.9f))
            {
                if (e == null || !e.IsAlive) continue;
                e.TakeDamage(dmg, false);
                MultiplayerSync.Instance?.BroadcastDamage(e.netInstanceId, dmg, false);
            }
            SkillEffectAnim.Spawn(center, def.fxSheet, def.fxCols, def.fxRows, def.color, 2.2f);
            MultiplayerSync.Instance?.BroadcastSkillBurst(center, def.fxSheet, def.fxCols, def.fxRows, def.color, 2.2f);
        }

        // ── Animation ─────────────────────────────────────────────────────────
        void SwitchAnim(string key, bool force)
        {
            if (_locked && !force) return;
            if (!force && key == _curKey) return;
            _curKey = key;
            var frames = key == "walk" ? _walk : _idle;
            if (frames == null || frames.Length == 0) return;
            _locked = false;
            if (_animCo != null) StopCoroutine(_animCo);
            _animCo = StartCoroutine(Loop(frames, key == "walk" ? 10f : 8f));
        }

        void PlayOneShot(Sprite[] frames, float fps, bool hold)
        {
            if (frames == null || frames.Length == 0) return;
            _locked = true;
            if (_animCo != null) StopCoroutine(_animCo);
            _animCo = StartCoroutine(OneShot(frames, fps, hold));
        }

        IEnumerator OneShot(Sprite[] frames, float fps, bool hold)
        {
            float dt = 1f / fps;
            for (int i = 0; i < frames.Length; i++) { if (_sr != null) _sr.sprite = frames[i]; yield return new WaitForSeconds(dt); }
            if (!hold) { _locked = false; _curKey = ""; SwitchAnim("idle", true); }
        }

        IEnumerator Loop(Sprite[] frames, float fps)
        {
            float dt = 1f / fps; int i = 0;
            while (true) { if (_sr != null) _sr.sprite = frames[i % frames.Length]; i++; yield return new WaitForSeconds(dt); }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ZulfarakRPG
{
    // Visual avatar for a non-local lobby member. Sprites are borrowed from
    // the local PlayerController2D's serialized arrays (so we don't need a
    // second prefab with all classes pre-wired). Position is interpolated
    // smoothly between received network updates; the displayed animation key
    // is set by MultiplayerSync from incoming STATE packets.
    public class RemotePlayer : MonoBehaviour
    {
        public string    SteamId;
        public string    PlayerName = "Player";
        public ClassType ClassType  = ClassType.Warrior;

        SpriteRenderer  _sr;
        WorldHealthBar  _hpBar;
        Sprite[]        _idle, _walk, _atk, _hurt, _death;
        string          _animKey = "idle";
        Coroutine       _animCo;

        Vector3 _targetPos;
        Vector3 _smoothedPos;
        bool    _flipX;
        bool    _hasFirstState;
        bool    _dead;           // playing/held on the death anim (revives when HP > 0 again)
        bool    _cdHud;          // cooldown HUD attached once
        float   _lastHp = -1f;   // -1 = no health packet received yet

        // Skill cooldown fill fractions (0 = just cast, 1 = ready), synced from the owner — drawn
        // above this avatar by SkillCooldownHUD.AttachRemote so you see partners' cooldowns too.
        public readonly List<float> CooldownFractions = new List<float>();

        public void SetCooldowns(string csv)
        {
            CooldownFractions.Clear();
            if (string.IsNullOrEmpty(csv)) return;
            foreach (var s in csv.Split(','))
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    CooldownFractions.Add(f);
        }

        // Exposed for the party frame (top-left group UI).
        public float  HpFraction    { get; private set; } = 1f;
        public Sprite PortraitSprite => (_idle != null && _idle.Length > 0) ? _idle[0] : null;

        void Awake()
        {
            transform.localScale = new Vector3(2f, 2f, 1f);
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 5;

            // Lazy-add an HP bar; we don't sync health yet, so it stays full.
            var hbGO = new GameObject("HealthBar");
            hbGO.transform.SetParent(transform, false);
            hbGO.transform.localPosition = new Vector3(0f, 0.62f, -0.1f);
            _hpBar = hbGO.AddComponent<WorldHealthBar>();
        }

        public void Configure(ClassType cls, string name)
        {
            bool classChanged = cls != ClassType || _idle == null;
            ClassType  = cls;
            PlayerName = string.IsNullOrEmpty(name) ? PlayerName : name;

            if (classChanged) RebindSprites();
            if (_hpBar != null)
            {
                _hpBar.AttachAbove(_sr,
                    padding:         0.005f,
                    fillColor:       new Color(0.95f, 0.85f, 0.30f, 1f), // gold = remote
                    widthMultiplier: 0.67f);
                if (_lastHp < 0f) _hpBar.SetHealth(1, 1);   // full until first health packet
                _hpBar.SetName(PlayerName);
            }

            if (!_cdHud) { _cdHud = true; SkillCooldownHUD.AttachRemote(this); }   // partner cooldown bars
        }

        // Health arrives with every STATE packet. When HP drops, flash + red popup
        // so the partner visibly takes damage on this screen too.
        public void SetHealth(float hp, float maxHp)
        {
            if (maxHp <= 0f) return;
            float dmg = (_lastHp >= 0f && hp < _lastHp - 0.01f) ? _lastHp - hp : 0f;
            _lastHp = hp;
            HpFraction = Mathf.Clamp01(hp / maxHp);
            _hpBar?.SetHealth(hp, maxHp);

            if (hp <= 0f)
            {
                if (!_dead) { _dead = true; PlayOneShot(_death, 12f, hold: true); }   // death anim, held
                return;
            }
            if (_dead) { _dead = false; SwitchAnim(_animKey, true); }                 // revived

            if (dmg > 0f)
            {
                DamagePopup.Spawn(transform, dmg, new Color(1f, 0.25f, 0.25f, 1f));
                HurtFlash.Flash(_sr);
                PlayOneShot(_hurt, 14f, hold: false);   // hurt reaction anim (then resume)
            }
        }

        // Only LOBBY members show a world-space HP bar + Steam name. A player sharing the scene
        // who isn't in the party stays anonymous (bar + name hidden). The party frame (top-left)
        // is the group's HP/name readout instead.
        bool IsLobbyMember()
        {
            var lm = SteamLobbyManager.Instance;
            return lm != null && lm.InLobby && !string.IsNullOrEmpty(SteamId) && lm.MemberSteamIds.Contains(SteamId);
        }

        void RefreshLobbyVisibility()
        {
            if (_hpBar == null) return;
            bool member = IsLobbyMember();
            if (_hpBar.gameObject.activeSelf != member) _hpBar.gameObject.SetActive(member);
            _hpBar.SetName(member ? PlayerName : null);
        }

        void RebindSprites()
        {
            var lp = Object.FindAnyObjectByType<PlayerController2D>();
            if (lp == null) return;

            // Match the local hero's ACTUAL (shrunk) world scale so the partner's avatar is the
            // same size and seats at the same height — Awake's fixed 2× made it bigger and, with
            // the sprite's bottom pivot, its body/HP-bar floated well above the local hero.
            transform.localScale = lp.transform.lossyScale;

            // Copy the hero's sorting layer + material so the partner renders in the same URP 2D
            // pass (a runtime SpriteRenderer's default material can be dropped by the 2D renderer).
            var lpSr0 = lp.GetComponent<SpriteRenderer>();
            if (_sr != null && lpSr0 != null) { _sr.sortingLayerID = lpSr0.sortingLayerID; _sr.sharedMaterial = lpSr0.sharedMaterial; }

            // Prefer the per-class ATTACK VARIANT frames (archerAttack1Frames, …) the local
            // hero actually swings with — the merged *AttackFrames arrays are often empty
            // once the variant system is used, which is why the partner's swing/cast wasn't
            // showing (the avatar fell back to idle).
            switch (ClassType)
            {
                case ClassType.Mage:
                    _idle = lp.wizardIdleFrames;
                    _walk = lp.wizardWalkFrames;
                    _atk  = Pick(lp.wizardAttack1Frames, lp.wizardAttackFrames);
                    break;
                case ClassType.Archer:
                    _idle = lp.archerIdleFrames;
                    _walk = lp.archerWalkFrames;
                    _atk  = Pick(lp.archerAttack1Frames, lp.archerAttackFrames);
                    break;
                default:
                    _idle = lp.soldierIdleFrames;
                    _walk = lp.soldierWalkFrames;
                    _atk  = Pick(lp.soldierAttack1Frames, lp.soldierAttackFrames);
                    break;
            }
            if (_idle == null || _idle.Length == 0) _idle = lp.soldierIdleFrames;
            if (_walk == null || _walk.Length == 0) _walk = _idle;
            if (_atk  == null || _atk.Length  == 0) _atk  = _idle;

            _hurt  = ClassType == ClassType.Mage   ? lp.wizardHurtFrames
                   : ClassType == ClassType.Archer ? lp.archerHurtFrames  : lp.soldierHurtFrames;
            _death = ClassType == ClassType.Mage   ? lp.wizardDeathFrames
                   : ClassType == ClassType.Archer ? lp.archerDeathFrames : lp.soldierDeathFrames;

            if (_sr != null && _idle != null && _idle.Length > 0)
                _sr.sprite = _idle[0];

            SwitchAnim(_animKey, force: true);
        }

        static Sprite[] Pick(Sprite[] preferred, Sprite[] fallback)
            => (preferred != null && preferred.Length > 0) ? preferred : fallback;

        public void ApplyState(float x, float y, bool flipX, string anim)
        {
            _targetPos = new Vector3(x, y, 0f);
            _flipX     = flipX;
            if (!_hasFirstState)
            {
                // Snap on first packet so the avatar doesn't drift in from the origin.
                _smoothedPos       = _targetPos;
                transform.position = _targetPos;
                _hasFirstState     = true;
            }
            if (!_dead && anim != _animKey) SwitchAnim(anim, force: false);   // dead holds its death pose
            _animKey = anim;
        }

        // Plays a one-shot clip (hurt / death). hold=true freezes on the last frame (death);
        // otherwise it resumes the looping state anim afterwards (hurt → idle/walk).
        void PlayOneShot(Sprite[] frames, float fps, bool hold)
        {
            if (frames == null || frames.Length == 0)
            {
                if (!hold && !_dead) SwitchAnim(_animKey, true);
                return;
            }
            if (_animCo != null) StopCoroutine(_animCo);
            _animCo = StartCoroutine(OneShot(frames, fps, hold));
        }

        IEnumerator OneShot(Sprite[] frames, float fps, bool hold)
        {
            float dt = 1f / Mathf.Max(1f, fps);
            foreach (var f in frames)
            {
                if (_sr != null) _sr.sprite = f;
                yield return new WaitForSeconds(dt);
            }
            if (!hold && !_dead) SwitchAnim(_animKey, true);
        }

        void SwitchAnim(string key, bool force)
        {
            Sprite[] frames = _idle; float fps = 8f;
            switch (key)
            {
                case "walk": frames = _walk; fps = 10f; break;
                case "atk":  frames = _atk;  fps = 12f; break;
            }
            if (frames == null || frames.Length == 0) return;
            if (_animCo != null) StopCoroutine(_animCo);
            _animCo = StartCoroutine(AnimLoop(frames, fps));
        }

        IEnumerator AnimLoop(Sprite[] frames, float fps)
        {
            float dt = 1f / fps;
            int i = 0;
            while (true)
            {
                if (_sr != null) _sr.sprite = frames[i % frames.Length];
                i++;
                yield return new WaitForSeconds(dt);
            }
        }

        void Update()
        {
            RefreshLobbyVisibility();
            if (!_hasFirstState) return;
            _smoothedPos       = Vector3.Lerp(_smoothedPos, _targetPos, Time.deltaTime * 12f);
            transform.position = _smoothedPos;
            if (_sr != null) _sr.flipX = _flipX;
        }
    }
}

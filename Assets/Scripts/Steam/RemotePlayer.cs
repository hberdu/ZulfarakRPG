using System.Collections;
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
        Sprite[]        _idle, _walk, _atk;
        string          _animKey = "idle";
        Coroutine       _animCo;

        Vector3 _targetPos;
        Vector3 _smoothedPos;
        bool    _flipX;
        bool    _hasFirstState;

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
                _hpBar.SetHealth(1, 1);
                _hpBar.SetName(PlayerName);
            }
        }

        void RebindSprites()
        {
            var lp = Object.FindAnyObjectByType<PlayerController2D>();
            if (lp == null) return;

            switch (ClassType)
            {
                case ClassType.Mage:
                    _idle = lp.wizardIdleFrames;
                    _walk = lp.wizardWalkFrames;
                    _atk  = lp.wizardAttackFrames;
                    break;
                case ClassType.Archer:
                    _idle = lp.archerIdleFrames;
                    _walk = lp.archerWalkFrames;
                    _atk  = lp.archerAttackFrames;
                    break;
                default:
                    _idle = lp.soldierIdleFrames;
                    _walk = lp.soldierWalkFrames;
                    _atk  = lp.soldierAttackFrames;
                    break;
            }
            if (_idle == null || _idle.Length == 0) _idle = lp.soldierIdleFrames;
            if (_walk == null || _walk.Length == 0) _walk = _idle;
            if (_atk  == null || _atk.Length  == 0) _atk  = _idle;

            if (_sr != null && _idle != null && _idle.Length > 0)
                _sr.sprite = _idle[0];

            SwitchAnim(_animKey, force: true);
        }

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
            if (anim != _animKey) SwitchAnim(anim, force: false);
            _animKey = anim;
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
            if (!_hasFirstState) return;
            _smoothedPos       = Vector3.Lerp(_smoothedPos, _targetPos, Time.deltaTime * 12f);
            transform.position = _smoothedPos;
            if (_sr != null) _sr.flipX = _flipX;
        }
    }
}

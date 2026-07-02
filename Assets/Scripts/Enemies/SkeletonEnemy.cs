using System.Collections;
using UnityEngine;

namespace ZulfarakRPG
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public class SkeletonEnemy : MonoBehaviour
    {
        [Header("Stats")]
        public float maxHealth     = 50f;
        public float moveSpeed     = 3.8f;
        public float gravityScale  = 3f;
        // Short melee reach — the skeleton must be right on top of the hero to hit.
        public float attackRange   = 0.7f;
        public float attackDamage  = 8f;
        public float attackCooldown = 1.8f;

        [Header("Scene Bounds (keeps the enemy on-screen)")]
        public float sceneBoundsMinX = 0.45f;
        public float sceneBoundsMaxX = 4.55f;

        [Header("Sprites")]
        public Sprite[] idleFrames;
        public Sprite[] walkFrames;
        public Sprite[] attackFrames;
        public Sprite[] deathFrames;

        // ── State ──────────────────────────────────────────────────────────
        private Rigidbody2D    _rb;
        private SpriteRenderer _sr;
        private WorldHealthBar _hpBar;
        private PlayerController2D _player;

        private float  _hp;
        private float  _atkTimer;
        private float  _attackLock;
        private bool   _dead;

        private Coroutine _animCoroutine;
        private string    _currentAnim;

        public bool IsAlive => !_dead;

        // ── Init ───────────────────────────────────────────────────────────
        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            _hpBar = GetComponentInChildren<WorldHealthBar>(true);
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = gravityScale;
            _rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _hp = maxHealth;
        }

        void Start()
        {
            _player = FindAnyObjectByType<PlayerController2D>();
            if (idleFrames != null && idleFrames.Length > 0 && _sr != null) _sr.sprite = idleFrames[0];
            // Ground on the same line as the player using the authored foot collider
            // (offset 0.5, size 0.3x0.2 — identical to the hero); gravity + the shared
            // GroundFloor then keep it pinned to the floor while it walks, so it can't fly.
            RestOnGroundAtSpawn();

            _hpBar?.AttachAbove(_sr);
            _hpBar?.SetHealth(_hp, maxHealth);
            PlayAnim(idleFrames, 8f);

            // Never physically shove anything: ignore collisions with other skeletons AND
            // the player. Enemies close to attackRange and strike via TakeDamage — contact
            // must not push the hero across the screen. (They still rest on the GroundFloor.)
            var myCol = GetComponent<Collider2D>();
            if (myCol != null)
            {
                foreach (var other in FindObjectsByType<SkeletonEnemy>(FindObjectsSortMode.None))
                {
                    if (other == this) continue;
                    var otherCol = other.GetComponent<Collider2D>();
                    if (otherCol != null) Physics2D.IgnoreCollision(myCol, otherCol, true);
                }
                var playerCol = _player ? _player.GetComponent<Collider2D>() : null;
                if (playerCol != null) Physics2D.IgnoreCollision(myCol, playerCol, true);
            }
        }

        // ── AI Loop ────────────────────────────────────────────────────────
        void Update()
        {
            if (_dead || _player == null) return;

            _atkTimer   -= Time.deltaTime;
            _attackLock -= Time.deltaTime;

            float horizontalDist = Mathf.Abs(_player.transform.position.x - transform.position.x);

            if (horizontalDist > attackRange)
            {
                float dir = Mathf.Sign(_player.transform.position.x - transform.position.x);
                _rb.linearVelocity = new Vector2(dir * moveSpeed, _rb.linearVelocity.y);
                _sr.flipX = dir < 0;
                if (_attackLock <= 0) PlayAnim(walkFrames, 10f);
            }
            else
            {
                _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
                float dir = Mathf.Sign(_player.transform.position.x - transform.position.x);
                if (Mathf.Abs(dir) > 0.001f) _sr.flipX = dir < 0;
                if (_atkTimer <= 0)
                {
                    _player.TakeDamage(attackDamage);
                    _atkTimer   = attackCooldown;
                    _attackLock = attackFrames != null && attackFrames.Length > 0
                                  ? attackFrames.Length / 12f : 0.5f;
                    // One swing per attack — loop:false lets us return to idle while
                    // the cooldown ticks instead of looping the swing repeatedly.
                    PlayAnim(attackFrames, 12f, forceRestart: true, loop: false);
                }
                else if (_attackLock <= 0)
                    PlayAnim(idleFrames, 8f);
            }

            ClampToSceneBounds();
            UpdateHpBarStagger();
        }

        // When skeletons stand close together (similar X), shift each HP bar up by a
        // small delta so all bars are visible instead of perfectly overlapping.
        // Rank = number of nearer-spawned (lower instance ID) skeletons within
        // ~0.6 world units → deterministic per frame, no flicker.
        void UpdateHpBarStagger()
        {
            if (_hpBar == null) return;
            int rank = 0;
            float myX = transform.position.x;
            EntityId myId = GetEntityId();
            foreach (var other in FindObjectsByType<SkeletonEnemy>(FindObjectsSortMode.None))
            {
                if (other == this || !other.IsAlive) continue;
                if (Mathf.Abs(other.transform.position.x - myX) > 0.6f) continue;
                if (other.GetEntityId() < myId) rank++;
            }
            _hpBar.SetStaggerOffset(rank * 0.045f);
        }

        void RestOnGroundAtSpawn()
        {
            var col = GetComponent<Collider2D>();
            if (col == null) return;
            Physics2D.SyncTransforms();
            float groundTop = GroundAlignUtil.FindGroundTopY();
            float shift = (groundTop + 0.002f) - col.bounds.min.y;
            transform.position += new Vector3(0f, shift, 0f);
            _rb.linearVelocity = Vector2.zero;
        }

        // Allow the spawner to walk the skeleton in from off-screen without snapping back.
        public void ReleaseFromSpawn() => _enteredScene = true;

        private bool _enteredScene;

        void ClampToSceneBounds()
        {
            float x = transform.position.x;
            if (!_enteredScene)
            {
                if (x <= sceneBoundsMaxX) _enteredScene = true;
                return;
            }
            float cx = Mathf.Clamp(x, sceneBoundsMinX, sceneBoundsMaxX);
            if (Mathf.Abs(cx - x) > 0.001f)
            {
                transform.position = new Vector3(cx, transform.position.y, transform.position.z);
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            }
        }

        // ── Combat ─────────────────────────────────────────────────────────
        public void TakeDamage(float dmg, bool isCrit = false)
        {
            if (_dead) return;
            // White popup (crits render yellow with a "*"). Only the player damages
            // enemies, so this is always "damage dealt by my character".
            DamagePopup.Spawn(transform, dmg, Color.white, isCrit);
            HurtFlash.Flash(_sr);
            _hp = Mathf.Max(0, _hp - dmg);
            _hpBar?.SetHealth(_hp, maxHealth);
            if (_hp <= 0) StartCoroutine(Die());
        }

        IEnumerator Die()
        {
            _dead = true;
            _rb.linearVelocity = Vector2.zero;
            GetComponent<BoxCollider2D>().enabled = false;
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            if (_hpBar != null) _hpBar.gameObject.SetActive(false);

            if (deathFrames != null && deathFrames.Length > 0)
            {
                float dt = 0.1f;
                foreach (var f in deathFrames)
                {
                    if (_sr) _sr.sprite = f;
                    yield return new WaitForSeconds(dt);
                }
            }

            WaveManager.Instance?.OnEnemyDied(this);
            Destroy(gameObject, 0.3f);
        }

        // ── Animation ──────────────────────────────────────────────────────
        void PlayAnim(Sprite[] frames, float fps, bool forceRestart = false, bool loop = true)
        {
            if (frames == null || frames.Length == 0) return;
            string key = frames[0]?.name;
            if (!forceRestart && key == _currentAnim) return;
            _currentAnim = key;
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimLoop(frames, fps, loop));
        }

        IEnumerator AnimLoop(Sprite[] frames, float fps, bool loop)
        {
            float dt = 1f / fps;
            int i = 0;
            while (!_dead && (loop || i < frames.Length))
            {
                if (_sr) _sr.sprite = frames[i % frames.Length];
                i++;
                yield return new WaitForSeconds(dt);
            }
            // Non-looping anim finished — clear so PlayAnim accepts the next switch.
            if (!loop) _currentAnim = null;
        }
    }
}

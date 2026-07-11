using System;
using System.Collections;
using System.Text;
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
        public Sprite[] hurtFrames;

        [Header("Server")]
        public string enemyId;

        // Per-instance ID used to route co-op damage packets. WaveManager assigns
        // wave{n}_{index} deterministically so both host and guest agree on which
        // skeleton a remote attack hit. Empty for enemies not spawned by WaveManager.
        [HideInInspector] public string netInstanceId;

        // ── State ──────────────────────────────────────────────────────────
        protected Rigidbody2D    _rb;
        protected SpriteRenderer _sr;
        protected WorldHealthBar _hpBar;
        protected PlayerController2D _player;

        protected float  _hp;
        protected float  _atkTimer;
        protected float  _attackLock;
        protected bool   _dead;

        private Coroutine _animCoroutine;
        private string    _currentAnim;
        private static EnemyDefinitionDto[] _enemyCatalogCache;
        private static bool _enemyCatalogLoaded;

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

            // Regular enemies read a touch smaller than the hero; the boss keeps its authored
            // size (SpawnScaleMultiplier override). Applied BEFORE seating so the ground snap
            // below re-aligns the resized collider/feet.
            float sizeMul = SpawnScaleMultiplier;
            if (!Mathf.Approximately(sizeMul, 1f))
            {
                var s = transform.localScale;
                transform.localScale = new Vector3(s.x * sizeMul, s.y * sizeMul, s.z);
            }

            // Ground on the same line as the player using the authored foot collider
            // (offset 0.5, size 0.3x0.2 — identical to the hero); gravity + the shared
            // GroundFloor then keep it pinned to the floor while it walks, so it can't fly.
            RestOnGroundAtSpawn();

            _hpBar?.AttachAbove(_sr);
            if (UsesBossHealthBar) _hpBar?.EnableBossFrame();
            _hpBar?.SetHealth(_hp, maxHealth);
            PlayAnim(idleFrames, 8f);
            StartCoroutine(ResolveEnemyFromServer());

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

            TickAI();
            TickPoison();

            ClampToSceneBounds();
            UpdateHpBarStagger();
        }

        // ── Poison damage-over-time (archer's Tiro de Serpe) ─────────────────
        float _poisonDps;
        float _poisonTimeLeft;
        float _poisonTick;

        // Applies (or refreshes) a poison DoT: `dps` damage per second for `duration`
        // seconds. Re-applying takes the stronger dps and the longer remaining time.
        public void ApplyPoison(float dps, float duration)
        {
            if (_dead || dps <= 0f || duration <= 0f) return;
            _poisonDps      = Mathf.Max(_poisonDps, dps);
            _poisonTimeLeft = Mathf.Max(_poisonTimeLeft, duration);
        }

        void TickPoison()
        {
            if (_dead || _poisonTimeLeft <= 0f) return;
            _poisonTimeLeft -= Time.deltaTime;
            _poisonTick     += Time.deltaTime;
            const float interval = 0.5f;
            if (_poisonTick >= interval)
            {
                _poisonTick -= interval;
                float dmg = _poisonDps * interval;
                ApplyPoisonDamage(dmg);
                // Sync the tick to co-op partners so the enemy's HP + green numbers match.
                MultiplayerSync.Instance?.BroadcastDamage(netInstanceId, dmg, false, poison: true);
            }
            if (_poisonTimeLeft <= 0f) _poisonDps = 0f;
        }

        // A poison tick replayed from a co-op partner (they own the DoT); shows the same
        // green number and HP loss here without a hurt flash / stun.
        public void ApplyRemotePoisonTick(float dmg) => ApplyPoisonDamage(dmg);

        void ApplyPoisonDamage(float dmg)
        {
            if (_dead || dmg <= 0f) return;
            // Direct HP loss (no hurt flash / stun) + a green poison number.
            _hp = Mathf.Max(0f, _hp - dmg);
            _hpBar?.SetHealth(_hp, maxHealth);
            DamagePopup.Spawn(transform, dmg, new Color(0.45f, 1f, 0.35f, 1f));
            if (_hp <= 0f) StartCoroutine(Die());
        }

        // All living skeletons whose position is within `radius` of `center` — used by the
        // warrior/mage area skills and the archer's arrow rain to pick targets.
        public static System.Collections.Generic.List<SkeletonEnemy> AliveInRadius(Vector3 center, float radius)
        {
            var list = new System.Collections.Generic.List<SkeletonEnemy>();
            float r2 = radius * radius;
            foreach (var e in FindObjectsByType<SkeletonEnemy>(FindObjectsSortMode.None))
                if (e != null && e.IsAlive &&
                    ((Vector2)(e.transform.position - center)).sqrMagnitude <= r2)
                    list.Add(e);
            return list;
        }

        // In co-op the enemy engages whichever lobby member is CLOSEST — the local
        // hero or a RemotePlayer avatar — so monsters stop at the first player they
        // reach instead of always chasing the local user. Real damage is only applied
        // when the target is the local player: the remote victim's own client runs
        // the same AI against its own local player, and the HP loss syncs back
        // through the state packets (popup + flash on RemotePlayer.SetHealth).
        protected Transform _targetTf;
        protected bool      _targetIsLocal = true;

        protected void AcquireNearestTarget()
        {
            _targetTf      = _player != null ? _player.transform : null;
            _targetIsLocal = true;
            float best = _targetTf != null
                       ? Mathf.Abs(_targetTf.position.x - transform.position.x)
                       : float.MaxValue;
            foreach (var rp in FindObjectsByType<RemotePlayer>(FindObjectsSortMode.None))
            {
                if (rp == null) continue;
                float d = Mathf.Abs(rp.transform.position.x - transform.position.x);
                if (d < best) { best = d; _targetTf = rp.transform; _targetIsLocal = false; }
            }
        }

        protected virtual void TickAI()
        {
            AcquireNearestTarget();
            if (_targetTf == null) return;

            float horizontalDist = Mathf.Abs(_targetTf.position.x - transform.position.x);

            if (horizontalDist > attackRange)
            {
                float dir = Mathf.Sign(_targetTf.position.x - transform.position.x);
                _rb.linearVelocity = new Vector2(dir * moveSpeed, _rb.linearVelocity.y);
                _sr.flipX = dir < 0;
                if (_attackLock <= 0) PlayAnim(walkFrames, 10f);
            }
            else
            {
                _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
                float dir = Mathf.Sign(_targetTf.position.x - transform.position.x);
                if (Mathf.Abs(dir) > 0.001f) _sr.flipX = dir < 0;
                if (_atkTimer <= 0)
                {
                    // Swing at whoever is in front; only the local hero loses HP here.
                    if (_targetIsLocal) _player.TakeDamage(attackDamage);
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
            GroundAlignUtil.SeatCharacterOnGround(transform, _sr);
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
        public virtual void TakeDamage(float dmg, bool isCrit = false)
        {
            if (_dead) return;
            // White popup (crits render yellow with a "*"). Only the player damages
            // enemies, so this is always "damage dealt by my character".
            DamagePopup.Spawn(transform, dmg, Color.white, isCrit);
            HurtFlash.Flash(_sr);
            _hp = Mathf.Max(0, _hp - dmg);
            _hpBar?.SetHealth(_hp, maxHealth);
            if (_hp <= 0)
            {
                StartCoroutine(Die());
            }
            else if (hurtFrames != null && hurtFrames.Length > 0)
            {
                _attackLock = Mathf.Max(_attackLock, hurtFrames.Length / 14f);
                PlayAnim(hurtFrames, 14f, forceRestart: true, loop: false);
            }
        }

        IEnumerator Die()
        {
            _dead = true;
            OnDeathStarted();
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

            yield return RegisterKillOnServer();

            WaveManager.Instance?.OnEnemyDied(this);
            Destroy(gameObject, 0.3f);
        }

        // Hook for subclasses (e.g. boss killing its remaining minions).
        protected virtual void OnDeathStarted() { }

        // Bosses override this to get the bigger dragon-framed HP bar.
        protected virtual bool UsesBossHealthBar => false;

        // Multiplier applied on top of server-catalog stats so bosses keep their
        // buffed HP/damage even when ResolveEnemyFromServer overwrites the fields.
        protected virtual float ServerStatMultiplier => 1f;

        // On-spawn size multiplier applied to the authored prefab scale. Regular skeletons
        // read a touch smaller than the hero; the boss overrides this to 1 to keep its size.
        // Applied in Start() before ground-seating, which re-aligns the enemy automatically.
        protected virtual float SpawnScaleMultiplier => 0.85f;

        IEnumerator RegisterKillOnServer()
        {
            if (ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady)
            {
                yield break;
            }

            var resolvedEnemyId = GetServerEnemyId();
            if (string.IsNullOrWhiteSpace(resolvedEnemyId))
            {
                Debug.LogWarning("[SkeletonEnemy] EnemyId vazio; kill não enviado ao servidor.");
                yield break;
            }

            var task = ServerApiClient.Instance.RegisterMonsterKillAsync(resolvedEnemyId, gameObject.name);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted || task.IsCanceled || task.Result == null)
            {
                var error = task.Exception?.GetBaseException();
                Debug.LogWarning($"[SkeletonEnemy] Kill remoto falhou para '{resolvedEnemyId}': {error?.Message}");
                yield break;
            }

            var result = task.Result;
            Debug.Log($"[SkeletonEnemy] Kill remoto OK enemy={result.enemyId} exp={result.expGained} gold={result.goldGained} drops={result.drops.Length}");
            // Floating "+N" gold reward rising from where the monster fell.
            if (result.goldGained > 0)
                GoldPopup.Spawn(transform.position, result.goldGained);
            if (result.character != null)
            {
                PlayerManager.Instance?.ApplyServerCharacter(result.character, saveLocal: false, preserveCurrentHp: true);
            }

            if (result.inventory != null)
            {
                Inventory.Instance?.ApplyServerKillResult(result.inventory, notify: true);
            }

            FindAnyObjectByType<CityUI>()?.RefreshPlayerInfo();
        }

        string GetServerEnemyId()
        {
            if (!string.IsNullOrWhiteSpace(enemyId))
            {
                return enemyId.Trim();
            }

            var source = gameObject.name.Replace("(Clone)", "").Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            var raw = source.ToLowerInvariant();
            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(ch);
                }
                else if (ch == ' ' || ch == '-' || ch == '_')
                {
                    if (sb.Length == 0 || sb[sb.Length - 1] == '_') continue;
                    sb.Append('_');
                }
            }

            return sb.ToString().Trim('_');
        }

        IEnumerator ResolveEnemyFromServer()
        {
            if (ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady)
            {
                yield break;
            }

            if (!_enemyCatalogLoaded)
            {
                var task = ServerApiClient.Instance.LoadEnemyDefinitionsAsync();
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (task.IsFaulted || task.IsCanceled)
                {
                    var error = task.Exception?.GetBaseException();
                    Debug.LogWarning($"[SkeletonEnemy] Falha ao carregar catálogo de inimigos: {error?.Message}");
                    yield break;
                }

                _enemyCatalogCache = task.Result ?? Array.Empty<EnemyDefinitionDto>();
                _enemyCatalogLoaded = true;
            }

            if (_enemyCatalogCache == null || _enemyCatalogCache.Length == 0)
            {
                yield break;
            }

            var currentId = GetServerEnemyId();
            var currentName = gameObject.name.Replace("(Clone)", "").Trim();
            var matched = ResolveCatalogEnemy(_enemyCatalogCache, currentId, currentName);
            if (matched == null)
            {
                Debug.LogWarning($"[SkeletonEnemy] Sem mapeamento de catálogo para '{currentName}' (id atual '{currentId}').");
                yield break;
            }

            enemyId = matched.enemyId;
            maxHealth = Mathf.Max(1f, matched.hp) * ServerStatMultiplier;
            attackDamage = Mathf.Max(0f, matched.attack) * ServerStatMultiplier;
            _hp = maxHealth;
            _hpBar?.SetHealth(_hp, maxHealth);
        }

        private static EnemyDefinitionDto ResolveCatalogEnemy(EnemyDefinitionDto[] catalog, string enemyIdRaw, string enemyNameRaw)
        {
            var idKey = NormalizeKey(enemyIdRaw);
            var nameKey = NormalizeKey(enemyNameRaw);

            EnemyDefinitionDto best = null;
            var bestScore = -1;

            foreach (var enemy in catalog)
            {
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.enemyId))
                {
                    continue;
                }

                var enemyIdKey = NormalizeKey(enemy.enemyId);
                var enemyNameKey = NormalizeKey(enemy.name);
                var score = 0;

                if (!string.IsNullOrWhiteSpace(idKey))
                {
                    if (enemyIdKey == idKey || enemyNameKey == idKey) score = score < 100 ? 100 : score;
                    else if (enemyIdKey.Contains(idKey) || enemyNameKey.Contains(idKey)) score = score < 80 ? 80 : score;
                    else if (idKey.Contains(enemyIdKey) || idKey.Contains(enemyNameKey)) score = score < 60 ? 60 : score;
                }

                if (!string.IsNullOrWhiteSpace(nameKey))
                {
                    if (enemyIdKey == nameKey || enemyNameKey == nameKey) score = score < 100 ? 100 : score;
                    else if (enemyIdKey.Contains(nameKey) || enemyNameKey.Contains(nameKey)) score = score < 80 ? 80 : score;
                    else if (nameKey.Contains(enemyIdKey) || nameKey.Contains(enemyNameKey)) score = score < 60 ? 60 : score;
                }

                if (score > bestScore)
                {
                    best = enemy;
                    bestScore = score;
                }
            }

            return bestScore >= 60 ? best : null;
        }

        private static string NormalizeKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var trimmed = raw.Replace("(Clone)", "").Trim().ToLowerInvariant();
            var sb = new StringBuilder(trimmed.Length);
            foreach (var ch in trimmed)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        // ── Animation ──────────────────────────────────────────────────────
        protected void PlayAnim(Sprite[] frames, float fps, bool forceRestart = false, bool loop = true)
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

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public class PlayerController2D : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed       = 1.6f;
        public float sceneBoundsMinX = 0.3f;
        public float sceneBoundsMaxX = 4.5f;

        [Header("Combat")]
        public float maxHealth      = 100f;
        public float attackDamage   = 25f;
        public float attackRange    = 2.5f;
        // Attacks per second. Drives BOTH the attack-animation speed AND the cadence:
        // the whole attack anim plays within one interval (1/attackSpeed), and the
        // archer's arrow is released partway through that same animation — so arrows
        // depend exclusively on the attack animation, which is governed by attackSpeed.
        public float attackSpeed    = 1.3f;
        [Range(0f, 1f)] public float critChance     = 0.05f;  // 5% initial crit chance
        public float                  critMultiplier = 2f;

        [Header("Mage Kiting")]
        // The mage never lets an enemy close in: once one is within kitePreferredDistance
        // it backpedals (facing the enemy) to keep firing fireballs from range. kiteSpeed
        // is deliberately brisk so it out-paces the approach and stays "sempre de longe".
        public float kitePreferredDistance = 4.0f;
        public float kiteSpeed             = 3.4f;

        float AttackInterval => 1f / Mathf.Max(0.1f, attackSpeed);

        [Header("Warrior Sprites")]
        public Sprite[] soldierIdleFrames;
        public Sprite[] soldierWalkFrames;
        public Sprite[] soldierAttackFrames;
        public Sprite[] soldierDeathFrames;
        public Sprite[] soldierHurtFrames;

        [Header("Mage Sprites")]
        public Sprite[] wizardIdleFrames;
        public Sprite[] wizardWalkFrames;
        public Sprite[] wizardAttackFrames;
        public Sprite[] wizardDeathFrames;
        public Sprite[] wizardHurtFrames;

        [Header("Archer Sprites")]
        public Sprite[] archerIdleFrames;
        public Sprite[] archerWalkFrames;
        public Sprite[] archerAttackFrames;
        public Sprite[] archerDeathFrames;
        public Sprite[] archerHurtFrames;

        // ── Alternate attacks ────────────────────────────────────────────────
        // Each class has several distinct attack animations in the Tiny RPG pack
        // (Soldier: Attack01/02/03; Wizard & Archer: Attack01/02). When these are
        // assigned the hero CYCLES through them one per swing/cast/shot instead of
        // replaying a single merged strip — so combat reads as a real combo. If a
        // class's variant fields are left empty it falls back to the merged
        // *AttackFrames array above (previous behaviour), so nothing breaks.
        [Header("Warrior Attack Variants (cycled per swing)")]
        public Sprite[] soldierAttack1Frames;
        public Sprite[] soldierAttack2Frames;
        public Sprite[] soldierAttack3Frames;

        [Header("Mage Attack Variants (cycled per cast)")]
        public Sprite[] wizardAttack1Frames;
        public Sprite[] wizardAttack2Frames;
        // Magic projectile sheets from the pack's Magic(Projectile) folder — one per
        // cast variant (Attack01 → magic1, Attack02 → magic2). Empty → procedural orb.
        public Sprite[] wizardMagic1Frames;
        public Sprite[] wizardMagic2Frames;

        [Header("Archer Attack Variants (cycled per shot)")]
        public Sprite[] archerAttack1Frames;
        public Sprite[] archerAttack2Frames;
        // Arrow sprites from the pack's Arrow(Projectile) folder — cycled per shot so
        // successive arrows differ. Empty → the procedural/Resources arrow.
        public Sprite[] arrowVariantSprites;

        // ── Runtime state ──────────────────────────────────────────────────
        private enum Phase { Playing, Celebrating, WalkingToPortal, Running, Dead }
        private Phase _phase = Phase.Playing;

        private Rigidbody2D    _rb;
        private SpriteRenderer _sr;
        private WorldHealthBar _hpBar;

        private float    _hp;
        // Exposed for the bottom-corner HUD (PlayerHud) so the green HP bar tracks health.
        public float Health    => _hp;
        public float MaxHealthValue => maxHealth;
        public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(_hp / maxHealth) : 0f;
        // True while the attack animation is playing — used by MultiplayerSync so
        // the remote avatar mirrors attack swings instead of only idle/walk.
        public bool IsAttacking => _attackLock > 0f;
        private float    _atkTimer;
        private float    _attackLock;
        private Sprite[] _idle, _walk, _atk, _death, _hurt;
        private ClassType _classType = ClassType.Warrior;

        // Alternate-attack rotation (built from the *Attack1/2/3 variant fields).
        private System.Collections.Generic.List<Sprite[]> _atkVariants;
        private System.Collections.Generic.List<Sprite[]> _magicVariants; // mage projectile frames, aligned with _atkVariants
        private int _atkVariantIndex;   // which attack animation plays next
        private int _arrowShotIndex;    // which arrow sprite the archer fires next

        // Click-to-move (city scene)
        private bool  _hasClickTarget;
        private float _clickTargetX;

        // Animation
        private Coroutine _animCoroutine;
        private string    _currentAnim;

        bool InCity    => SceneManager.GetActiveScene().name == "Zulfarak";
        bool InDungeon => SceneManager.GetActiveScene().name == "Dungeon";

        // ── Init ───────────────────────────────────────────────────────────
        void Awake()
        {
            _rb  = GetComponent<Rigidbody2D>();
            _sr  = GetComponent<SpriteRenderer>();
            _hpBar = GetComponentInChildren<WorldHealthBar>(true);

            _rb.gravityScale           = 3f;
            _rb.constraints            = RigidbodyConstraints2D.FreezeRotation;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // Slim collider that only covers the visible body (not wider than the art).
            var col = GetComponent<BoxCollider2D>();
            if (col != null) { col.size = new Vector2(0.15f, 0.2f); col.offset = new Vector2(0f, 0.5f); }
            _hp = maxHealth;

            // Snapshot the authored combat stats so equipment modifiers (which are additive
            // fractions) always compose against a stable baseline instead of compounding.
            _baseCritChance    = critChance;
            _baseCritMultiplier = critMultiplier;
            _baseAttackSpeed   = attackSpeed;
            _baseMoveSpeed     = moveSpeed;
        }

        // Authored bases for stats that equipment modifies multiplicatively/additively.
        float _baseCritChance, _baseCritMultiplier, _baseAttackSpeed, _baseMoveSpeed;

        void Start()
        {
            SyncRuntimeStatsFromPlayerData();
            SelectClassSprites();

            // If the Inspector sprite arrays weren't assigned (e.g. after a scene reset),
            // generate a minimal placeholder so the player is at least visible.
            if ((_idle == null || _idle.Length == 0) && _sr != null)
            {
                var ph = MakePlaceholderSprite();
                _idle  = new[] { ph };
                _walk  = _idle;
                _atk   = _idle;
                _death = _idle;
                Debug.LogWarning("[Player] No sprites assigned — using placeholder. Assign sprite arrays in the Inspector.");
            }

            PlayAnim(_idle, 8f);
            if (_idle != null && _idle.Length > 0 && _sr != null) _sr.sprite = _idle[0];

            // Diagnostic: log what SpriteAlphaBounds returns at runtime so we can tell
            // whether the texture is Read/Write enabled and where the visible feet are.
            if (_sr != null && _sr.sprite != null)
            {
                var ab = SpriteAlphaBounds.Get(_sr.sprite);
                Debug.Log($"[Player.Start] sprite={_sr.sprite.name} bottom={ab.bottomFromBottom:F3} top={ab.topFromBottom:F3} spriteH={_sr.sprite.bounds.size.y:F3} pos={transform.position} scale={transform.lossyScale}");
            }

            // Rest the player on the ground exactly like the city NPCs: settle its
            // authored foot collider (offset 0.5, size 0.3×0.2 — identical to the NPCs)
            // onto the ground line. Deterministic; no reliance on alpha-readable art.
            RestOnGroundAtSpawn();

            // Class-specific reach (overrides the authored value): the archer AND the mage
            // fight from range (the mage lobs a fireball), while the melee Warrior must
            // close right up to the enemy before it swings.
            attackRange = (_classType == ClassType.Archer || _classType == ClassType.Mage) ? 6f : 1.1f;

            // Tight GREEN HP bar: width and Y both auto-detected from the sprite alpha.
            _hpBar?.AttachAbove(_sr,
                                padding:         0.005f,
                                fillColor:       new Color(0.25f, 0.85f, 0.30f, 1f),
                                widthMultiplier: 0.67f);
            _hpBar?.SetHealth(_hp, maxHealth);
            // Show the player's character name above the bar (tiny bold label).
            string displayName = PlayerManager.Instance?.Data?.playerName;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = SteamIntegration.Instance?.SteamName ?? "Player";
            _hpBar?.SetName(displayName);

            // Learned skills fire on the attack cadence (one per tick, replacing the basic
            // attack) — see HandleAutoAttack.
            SkillManager.Ensure();
            _skillCaster = GetComponent<SkillAutoCaster>();
            if (_skillCaster == null) _skillCaster = gameObject.AddComponent<SkillAutoCaster>();
        }

        private SkillAutoCaster _skillCaster;

        void SyncRuntimeStatsFromPlayerData()
        {
            var data = PlayerManager.Instance != null ? PlayerManager.Instance.Data : null;
            if (data == null) return;

            if (data.maxHp > 0) maxHealth = data.maxHp;
            if (data.attack > 0) attackDamage = data.attack;

            _hp = Mathf.Clamp(data.hp, 0f, maxHealth);
            ApplyEquipmentModifiers(data);
        }

        // Folds the equipment-derived combat modifiers (crit, attack speed, movement speed)
        // into the live combat fields, always composing against the authored baselines so
        // equipping/unequipping is fully reversible. Called every frame so gear changes made
        // while playing take effect immediately.
        void ApplyEquipmentModifiers(PlayerData data)
        {
            if (data == null) return;
            critChance     = Mathf.Clamp01(_baseCritChance + data.critChanceBonus);
            critMultiplier = _baseCritMultiplier + data.critDamageBonus;
            attackSpeed    = _baseAttackSpeed * (1f + Mathf.Max(-0.9f, data.attackSpeedBonus));
            moveSpeed      = Mathf.Max(0.1f, _baseMoveSpeed + data.moveSpeedBonus);
        }

        void SyncPlayerDataHealthFromRuntime()
        {
            var data = PlayerManager.Instance != null ? PlayerManager.Instance.Data : null;
            if (data == null) return;

            var maxHpInt = Mathf.Max(1, Mathf.RoundToInt(maxHealth));
            var hpInt = Mathf.Clamp(Mathf.RoundToInt(_hp), 0, maxHpInt);
            data.maxHp = maxHpInt;
            data.hp = hpInt;
        }

        void SelectClassSprites()
        {
            _classType = PlayerManager.Instance?.Data?.classType ?? ClassType.Warrior;
            switch (_classType)
            {
                case ClassType.Mage:
                    _idle  = Pick(wizardIdleFrames,   soldierIdleFrames);
                    _walk  = Pick(wizardWalkFrames,   soldierWalkFrames);
                    _atk   = Pick(wizardAttackFrames, soldierAttackFrames);
                    _death = Pick(wizardDeathFrames,  soldierDeathFrames);
                    _hurt  = Pick(wizardHurtFrames,   soldierHurtFrames);
                    break;
                case ClassType.Archer:
                    _idle  = Pick(archerIdleFrames,   soldierIdleFrames);
                    _walk  = Pick(archerWalkFrames,   soldierWalkFrames);
                    _atk   = Pick(archerAttackFrames, soldierAttackFrames);
                    _death = Pick(archerDeathFrames,  soldierDeathFrames);
                    _hurt  = Pick(archerHurtFrames,   soldierHurtFrames);
                    break;
                default:
                    _idle  = soldierIdleFrames;
                    _walk  = soldierWalkFrames;
                    _atk   = soldierAttackFrames;
                    _death = soldierDeathFrames;
                    _hurt  = soldierHurtFrames;
                    break;
            }
            if (_idle == null || _idle.Length == 0) _idle = soldierIdleFrames;
            if (_walk == null || _walk.Length == 0) _walk = _idle;
            if (_atk  == null || _atk.Length  == 0) _atk  = _idle;

            BuildAttackVariants();
        }

        // Gathers this class's distinct attack animations (and, for the mage, the
        // matching projectile sheets) into parallel rotation lists. Falls back to the
        // single merged _atk strip when no variant fields are assigned.
        void BuildAttackVariants()
        {
            _atkVariants   = new System.Collections.Generic.List<Sprite[]>();
            _magicVariants = new System.Collections.Generic.List<Sprite[]>();
            _atkVariantIndex = 0;
            _arrowShotIndex  = 0;

            // Single basic attack per class (no combo alternation).
            switch (_classType)
            {
                case ClassType.Mage:
                    AddVariant(wizardAttack1Frames, wizardMagic1Frames);
                    break;
                case ClassType.Archer:
                    AddVariant(archerAttack1Frames, null);
                    break;
                default:
                    AddVariant(soldierAttack1Frames, null);
                    break;
            }

            // No variant art assigned → keep the legacy single merged swing.
            if (_atkVariants.Count == 0)
            {
                _atkVariants.Add(_atk);
                _magicVariants.Add(null);
            }
        }

        void AddVariant(Sprite[] frames, Sprite[] magic)
        {
            if (frames == null || frames.Length == 0) return;
            _atkVariants.Add(frames);
            _magicVariants.Add(magic != null && magic.Length > 0 ? magic : null);
        }

        static Sprite[] Pick(Sprite[] preferred, Sprite[] fallback)
            => (preferred != null && preferred.Length > 0) ? preferred : fallback;

        // Seats the hero's VISIBLE feet on the ground line (alpha-aware) and realigns
        // the foot collider bottom to match, so physics keeps holding the sprite at
        // the visible line. Falls back to collider-only when the alpha scan is
        // unreliable, so a non-readable texture can never strand the hero mid-air.
        void RestOnGroundAtSpawn()
        {
            GroundAlignUtil.SeatCharacterOnGround(transform, _sr);
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
        }

        // ── Update ─────────────────────────────────────────────────────────
        void Update()
        {
            if (_phase != Phase.Playing) return;

            _atkTimer   -= Time.deltaTime;
            _attackLock -= Time.deltaTime;

            if (InCity)  HandleCityInput();

            HandleMovement();
            HandleAutoAttack();
            HandleLifeRegeneration();
            ClampToSceneBounds();
        }

        void HandleLifeRegeneration()
        {
            var data = PlayerManager.Instance != null ? PlayerManager.Instance.Data : null;
            if (data == null) return;

            // Keep the equipment-derived combat fields (crit / attack & move speed) live so
            // gear equipped mid-play takes effect right away.
            ApplyEquipmentModifiers(data);

            if (data.attack > 0 && Mathf.Abs(attackDamage - data.attack) > 0.001f)
            {
                attackDamage = data.attack;
            }

            if (data.maxHp > 0 && Mathf.Abs(maxHealth - data.maxHp) > 0.001f)
            {
                maxHealth = data.maxHp;
                _hp = Mathf.Clamp(_hp, 0f, maxHealth);
            }

            if (Mathf.Abs(_hp - data.hp) > 0.5f)
            {
                _hp = Mathf.Clamp(data.hp, 0f, maxHealth);
                _hpBar?.SetHealth(_hp, maxHealth);
            }

            var healPerSecond = Mathf.Max(0f, data.healPower);
            if (healPerSecond <= 0f || _hp >= maxHealth) return;

            _hp = Mathf.Min(maxHealth, _hp + healPerSecond * Time.deltaTime);
            SyncPlayerDataHealthFromRuntime();
            _hpBar?.SetHealth(_hp, maxHealth);
        }

        // ── City click input ───────────────────────────────────────────────
        void HandleCityInput()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (WorldMapPanel.IsOpen) return;   // map overlay consumes the click

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = 0f;

            // Portal click?
            foreach (var col in Physics2D.OverlapPointAll((Vector2)worldPos))
            {
                var portal = col.GetComponent<Portal2D>();
                if (portal != null && portal.IsOpen)
                {
                    SetClickTarget(portal.transform.position.x);
                    ClickIndicator.SpawnAt(portal.transform.position);
                    return;
                }
            }

            // Ground click — walk to that X (clamped to scene bounds)
            float tx = Mathf.Clamp(worldPos.x, sceneBoundsMinX, sceneBoundsMaxX);
            SetClickTarget(tx);
            ClickIndicator.SpawnAt(new Vector3(tx, worldPos.y, 0f));
        }

        void SetClickTarget(float x)
        {
            _hasClickTarget = true;
            _clickTargetX   = x;
        }

        // Cancels a pending click-to-move — called when OverlayWindow detects the press was
        // actually a window drag, so the hero doesn't walk to where the drag began.
        public void CancelClickTarget()
        {
            _hasClickTarget = false;
        }

        // Public auto-walk (used by the city "repeat" toggle): walk to X exactly like a
        // ground/portal click, so reaching the dungeon portal re-triggers its transition.
        public void AutoWalkToX(float x)
        {
            if (_phase != Phase.Playing) return;
            SetClickTarget(x);
        }

        // ── Movement ──────────────────────────────────────────────────────
        void HandleMovement()
        {
            if (_hasClickTarget)
            {
                MoveTowardX(_clickTargetX);
                return;
            }

            if (InDungeon)
            {
                if (_classType == ClassType.Mage)
                {
                    // The mage NEVER fights up close: it retreats to keep enemies at staff
                    // range and blasts them with fireballs on the way in. Only when cornered
                    // against a scene bound does it stand and keep casting.
                    var near = FindNearest(kitePreferredDistance);
                    if (near != null && TryKiteAwayFrom(near)) return;
                }
                else
                {
                    // Walk FORWARD to meet the nearest enemy — but only once it has
                    // actually appeared on screen. During the wave transition the hero
                    // stands free (idle) until monsters walk in, then advances and holds
                    // at attack range instead of marching in place at them.
                    var target = FindNearest(999f);
                    if (target != null)
                    {
                        float dist = Vector2.Distance(transform.position, target.transform.position);
                        if (dist > attackRange && IsOnScreen(target.transform.position))
                        {
                            MoveTowardX(target.transform.position.x);
                            return;
                        }
                    }
                }
            }

            // Idle — also fires between auto-attacks while an enemy is in range.
            // (Removing the FindNearest gate is what stops the attack anim from
            // looping multiple times per arrow / per swing.)
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            if (_attackLock <= 0) PlayAnim(_idle, 8f);
        }

        void MoveTowardX(float targetX)
        {
            float diff = targetX - transform.position.x;
            if (Mathf.Abs(diff) < 0.12f)
            {
                _hasClickTarget    = false;
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                if (_attackLock <= 0) PlayAnim(_idle, 8f);
                return;
            }
            float dir = Mathf.Sign(diff);
            _rb.linearVelocity = new Vector2(dir * moveSpeed, _rb.linearVelocity.y);
            _sr.flipX = dir < 0;
            if (_attackLock <= 0) PlayAnim(_walk, 10f);
        }

        // Mage-only: steps away from `enemy` to preserve firing distance while keeping the
        // sprite facing the enemy (backpedal). Returns false when already pinned against a
        // scene bound — nowhere left to retreat, so the caller lets it stand and keep casting.
        bool TryKiteAwayFrom(SkeletonEnemy enemy)
        {
            float away  = transform.position.x < enemy.transform.position.x ? -1f : 1f;
            float nextX = transform.position.x + away * kiteSpeed * Time.deltaTime;
            if (nextX <= sceneBoundsMinX + 0.01f || nextX >= sceneBoundsMaxX - 0.01f)
                return false;   // cornered — hold ground and keep firing fireballs

            _rb.linearVelocity = new Vector2(away * kiteSpeed, _rb.linearVelocity.y);
            _sr.flipX = enemy.transform.position.x < transform.position.x;   // face the enemy while retreating
            if (_attackLock <= 0) PlayAnim(_walk, 10f);
            return true;
        }

        // ── Auto-attack ────────────────────────────────────────────────────
        void HandleAutoAttack()
        {
            if (_atkTimer > 0) return;

            var target = FindNearest(attackRange);
            if (target == null) return;

            // Skills ALWAYS replace the basic attack: on each attack tick, if an equipped
            // skill is off cooldown, cast exactly ONE (the caster picks one) and consume the
            // tick — so the two skills never fire at the same time and both respect the base
            // attack cadence. Only when no skill is ready does the basic attack happen.
            if (_skillCaster != null && _skillCaster.TryCastReady(target))
            {
                _atkTimer   = AttackInterval;
                _attackLock = Mathf.Max(_attackLock, Mathf.Min(AttackInterval * 0.6f, 0.6f));
                return;
            }

            _sr.flipX = target.transform.position.x < transform.position.x;

            // Roll crit once per attack — 2× damage, yellow "*" popup.
            bool  crit = Random.value < critChance;
            float dmg  = crit ? attackDamage * critMultiplier : attackDamage;

            // Pick the next attack animation in the class's rotation (a real combo:
            // Soldier cycles its 3 swings, Wizard/Archer alternate their 2). The chosen
            // variant also selects the mage's matching projectile sheet.
            if (_atkVariants == null || _atkVariants.Count == 0) BuildAttackVariants();
            int      variant   = _atkVariantIndex;
            Sprite[] atkFrames = _atkVariants[variant];
            _atkVariantIndex   = (_atkVariantIndex + 1) % _atkVariants.Count;

            // Exactly ONE complete swing/draw per attack, played over ~60% of the attack
            // interval (1/attackSpeed). Keeping it below the interval leaves a gap that the
            // player spends in Idle between attacks (see HandleMovement), and since animDur
            // is always < interval the animation can never be out-paced by the attack rate.
            // Capped so a very slow attackSpeed doesn't stretch the swing into slow-motion.
            // Arrow release is timed off THIS animation duration.
            float interval = AttackInterval;
            float animDur  = Mathf.Min(interval * 0.6f, 0.6f);
            float atkFps   = (atkFrames != null && atkFrames.Length > 0) ? atkFrames.Length / animDur : 12f;

            // Ranged classes release their projectile partway through the cast/draw; the
            // melee Warrior hits instantly.
            if (_classType == ClassType.Archer)
                StartCoroutine(FireArrowAfterDraw(target, dmg, crit, animDur));
            else if (_classType == ClassType.Mage)
                StartCoroutine(FireFireballAfterCast(target, dmg, crit, animDur, variant));
            else
            {
                target.TakeDamage(dmg, crit);
                MultiplayerSync.Instance?.BroadcastDamage(target.netInstanceId, dmg, crit);
            }

            _atkTimer   = interval;
            _attackLock = animDur;
            PlayAnim(atkFrames, atkFps, forceRestart: true, loop: false);
        }

        // Releases the arrow at ~halfway through the attack animation — the timing is
        // derived from animDur (which comes from attackSpeed), so the arrow is bound
        // exclusively to the attack animation.
        IEnumerator FireArrowAfterDraw(SkeletonEnemy target, float dmg, bool crit, float animDur)
        {
            yield return new WaitForSeconds(animDur * 0.5f);
            if (target == null || !target.IsAlive) yield break;
            FireArrow(target, dmg, crit);
        }

        void FireArrow(SkeletonEnemy target, float dmg, bool crit)
        {
            if (target == null) return;
            // Spawn from the archer's COLLIDER CENTER (visible body — collider matches art).
            var myCol = GetComponent<Collider2D>();
            Vector3 spawnPos = myCol != null
                ? myCol.bounds.center
                : transform.position + Vector3.up * 0.5f;

            float dir = Mathf.Sign(target.transform.position.x - transform.position.x);
            if (dir == 0f) dir = 1f;
            // Push slightly toward the target so the arrow doesn't start inside the archer
            spawnPos += new Vector3(dir * 0.35f, 0f, 0f);

            // Cycle through the pack's arrow sprites so successive shots differ.
            Sprite arrowSprite = null;
            if (arrowVariantSprites != null && arrowVariantSprites.Length > 0)
            {
                arrowSprite = arrowVariantSprites[_arrowShotIndex % arrowVariantSprites.Length];
                _arrowShotIndex++;
            }

            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.position = spawnPos;
            var arrow = arrowGO.AddComponent<Arrow>();
            arrow.Init(target, dmg, crit, arrowSprite);
        }

        // Mage counterpart of FireArrowAfterDraw: the fireball leaves the staff at the
        // apex of the cast ("ao levantar o cajado"), so the projectile is bound to the
        // attack animation exactly like the archer's arrow.
        IEnumerator FireFireballAfterCast(SkeletonEnemy target, float dmg, bool crit, float animDur, int variant)
        {
            yield return new WaitForSeconds(animDur * 0.5f);
            if (target == null || !target.IsAlive) yield break;
            FireFireball(target, dmg, crit, variant);

            // The wizard strip's trailing frames swing the arm back behind the body
            // AFTER the release — cut them and hold the cast-apex pose until the
            // attack lock expires and idle takes over.
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _currentAnim = null;
        }

        void FireFireball(SkeletonEnemy target, float dmg, bool crit, int variant)
        {
            if (target == null) return;
            // Spawn from the mage's COLLIDER CENTER (visible body — collider matches art).
            var myCol = GetComponent<Collider2D>();
            Vector3 spawnPos = myCol != null
                ? myCol.bounds.center
                : transform.position + Vector3.up * 0.5f;

            float dir = Mathf.Sign(target.transform.position.x - transform.position.x);
            if (dir == 0f) dir = 1f;
            // Launch from the raised staff tip: a little forward and up from the body center.
            spawnPos += new Vector3(dir * 0.35f, 0.15f, 0f);

            // Basic attack: no big cast flash (that's reserved for skills). Just the orb.

            // The magic sheet paired with this cast variant (null → procedural orb).
            Sprite[] magic = (_magicVariants != null && variant < _magicVariants.Count)
                ? _magicVariants[variant] : null;

            var fbGO = new GameObject("Fireball");
            fbGO.transform.position = spawnPos;
            var fb = fbGO.AddComponent<Fireball>();
            fb.Init(target, dmg, crit, magic);
        }

        // Public cast trigger used by SkillAutoCaster so learned skills visually cast
        // (flip + attack animation + brief movement lock) even though they don't go
        // through HandleAutoAttack. Cycles the class's attack rotation like the basic
        // attack does, so successive skill casts alternate variants when available.
        // Returns the cast animation duration so callers can align damage / VFX / the
        // projectile spawn to the cast APEX instead of the very first frame.
        public float PlayCastAnimation(Vector3 targetWorldPos)
        {
            if (_atkVariants == null || _atkVariants.Count == 0) BuildAttackVariants();
            if (_atkVariants == null || _atkVariants.Count == 0) return 0f;
            if (_sr != null) _sr.flipX = targetWorldPos.x < transform.position.x;

            int      variant   = _atkVariantIndex;
            Sprite[] atkFrames = _atkVariants[variant];
            _atkVariantIndex   = (_atkVariantIndex + 1) % _atkVariants.Count;

            float animDur = Mathf.Min(AttackInterval * 0.6f, 0.6f);
            float atkFps  = (atkFrames != null && atkFrames.Length > 0) ? atkFrames.Length / animDur : 12f;
            _attackLock   = Mathf.Max(_attackLock, animDur);
            PlayAnim(atkFrames, atkFps, forceRestart: true, loop: false);
            return animDur;
        }

        // True when a world point is within the visible camera view (used to gate the
        // hero's forward advance: he only steps toward enemies once they're on screen).
        bool IsOnScreen(Vector3 worldPos)
        {
            var cam = Camera.main;
            if (cam == null) return worldPos.x <= sceneBoundsMaxX + 3f;
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return vp.z > 0f && vp.x <= 1.02f && vp.x >= -0.30f;
        }

        SkeletonEnemy FindNearest(float range)
        {
            SkeletonEnemy best = null;
            float minD = range;
            foreach (var e in FindObjectsByType<SkeletonEnemy>(FindObjectsSortMode.None))
            {
                if (!e.IsAlive) continue;
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d < minD) { minD = d; best = e; }
            }
            return best;
        }

        // ── Boundary clamp ─────────────────────────────────────────────────
        void ClampToSceneBounds()
        {
            float cx = Mathf.Clamp(transform.position.x, sceneBoundsMinX, sceneBoundsMaxX);
            if (Mathf.Abs(cx - transform.position.x) > 0.001f)
            {
                transform.position = new Vector3(cx, transform.position.y, transform.position.z);
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                _hasClickTarget = false;
            }
        }

        // Restores HP (used by auto-cast support skills). Clamped to maxHealth.
        public void Heal(float amount)
        {
            if (_phase == Phase.Dead || amount <= 0f) return;
            _hp = Mathf.Min(maxHealth, _hp + amount);
            SyncPlayerDataHealthFromRuntime();
            _hpBar?.SetHealth(_hp, maxHealth);
            DamagePopup.Spawn(transform, amount, new Color(0.4f, 1f, 0.45f, 1f));
        }

        // Nearest living enemy to the hero within `range` (used by the skill auto-caster).
        public SkeletonEnemy NearestEnemy(float range) => FindNearest(range);

        // ── Combat ─────────────────────────────────────────────────────
        public void TakeDamage(float dmg, bool isMagic = false)
        {
            if (_phase == Phase.Dead) return;

            // Equipped gear reduces incoming damage by the matching resistance (physical for
            // melee, magic for spells). Resistances are capped in Inventory.RecalculateStats.
            var data = PlayerManager.Instance != null ? PlayerManager.Instance.Data : null;
            if (data != null)
            {
                float resist = Mathf.Clamp(isMagic ? data.magicResistPct : data.physicalResistPct, 0f, 0.9f);
                dmg *= (1f - resist);
            }

            // Red popup — damage received by the player.
            DamagePopup.Spawn(transform, dmg, new Color(1f, 0.25f, 0.25f, 1f));
            HurtFlash.Flash(_sr);
            _hp = Mathf.Max(0f, _hp - dmg);
            SyncPlayerDataHealthFromRuntime();
            _hpBar?.SetHealth(_hp, maxHealth);
            if (_hp <= 0f)
            {
                StartCoroutine(DieRoutine());
            }
            else if (_hurt != null && _hurt.Length > 0)
            {
                _attackLock = Mathf.Max(_attackLock, _hurt.Length / 14f);
                PlayAnim(_hurt, 14f, forceRestart: true, loop: false);
            }
        }

        // True during the inter-wave march (input/auto-attack suppressed, velocity
        // zero) — MultiplayerSync broadcasts "walk" from this so the partner sees
        // the march instead of an idle pose.
        public bool IsRunning => _phase == Phase.Running;

        // Inter-wave regroup: force-walk the hero back to the screen-start X while
        // input and auto-attack stay suppressed (Phase.Running); WaveManager releases
        // us via SetRunning(false) once the next wave's enemies are on screen.
        public IEnumerator WalkBackToStart(float x)
        {
            if (_phase == Phase.Dead) yield break;
            _phase          = Phase.Running;
            _hasClickTarget = false;
            PlayAnim(_walk, 10f, forceRestart: true);
            while (Mathf.Abs(transform.position.x - x) > 0.1f && _phase == Phase.Running)
            {
                float dir = Mathf.Sign(x - transform.position.x);
                _rb.linearVelocity = new Vector2(dir * moveSpeed, _rb.linearVelocity.y);
                if (_sr) _sr.flipX = dir < 0;
                yield return null;
            }
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            if (_sr) _sr.flipX = false;
        }

        // Called by WaveManager during the inter-wave run. The player stays put,
        // the BG parallax scrolls; we just play the walk anim facing right.
        public void SetRunning(bool on)
        {
            if (_phase == Phase.Dead) return;
            if (on)
            {
                _phase = Phase.Running;
                _rb.linearVelocity = Vector2.zero;
                if (_sr) _sr.flipX = false;
                PlayAnim(_walk, 12f, forceRestart: true);
            }
            else
            {
                _phase = Phase.Playing;
                PlayAnim(_idle, 8f, forceRestart: true);
            }
        }

        // Called by Portal2D when the player enters the portal trigger. Stops all
        // input/AI/physics, locks the player in idle, and renders a pulsing white
        // halo above the sprite for the portal's pre-load animation window.
        public void StartPortalAbsorb(float duration)
        {
            if (_phase == Phase.Dead) return;
            _phase             = Phase.WalkingToPortal;
            _rb.linearVelocity = Vector2.zero;
            _hasClickTarget    = false;
            if (_idle != null && _idle.Length > 0)
                PlayAnim(_idle, 8f, forceRestart: true);
            StartCoroutine(PortalAbsorbRoutine(Mathf.Max(0.05f, duration)));
        }

        IEnumerator PortalAbsorbRoutine(float duration)
        {
            // The player no longer trembles or dyes — it simply stands in place while a
            // dense smoke cloud swallows it ("apenas jogue a fumaça"), which then carries
            // over into the Dungeon load.
            Vector3 basePos  = transform.position;
            float t = 0f;
            float nextPuff = 0f;
            PortalSmoke.BurstAt(basePos, 5);   // small opening burst
            while (t < duration)
            {
                // Light, occasional puffs (reduced) — a wisp of pixel smoke, not a cloud.
                if (t >= nextPuff)
                {
                    PortalSmoke.BurstAt(basePos, 3);
                    nextPuff = t + 0.18f;
                }
                t += Time.deltaTime;
                yield return null;
            }
            PortalSmoke.BurstAt(basePos, 6);
            // Tell the next (Dungeon) scene to bloom purple smoke that dissipates as the wave begins.
            PortalSmoke.PendingAtWaveStart = true;
        }

        IEnumerator DieRoutine()
        {
            _phase = Phase.Dead;
            _rb.linearVelocity = Vector2.zero;
            _rb.constraints    = RigidbodyConstraints2D.FreezeAll;
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            WaveManager.Instance?.OnPlayerDied();

            if (_death != null && _death.Length > 0 && _sr != null)
            {
                float dt = 0.12f;
                foreach (var f in _death)
                {
                    _sr.sprite = f;
                    yield return new WaitForSeconds(dt);
                }
            }
            else
            {
                yield return new WaitForSeconds(0.4f);
            }

            yield return new WaitForSeconds(1.6f);
            PlayerManager.Instance?.RestoreFullHealthAndSave();
            // Death penalty: always respawn in the main city, not back into the dungeon.
            SceneManager.LoadScene("Zulfarak");
        }

        // ── Celebration + portal walk ───────────────────────────────────────
        public void Celebrate()
        {
            _phase = Phase.Celebrating;
            _rb.linearVelocity = Vector2.zero;
            StartCoroutine(CelebrationRoutine());
        }

        IEnumerator CelebrationRoutine()
        {
            // No victory jumps — the hero just holds idle for a beat before
            // walking to the exit portal.
            yield return new WaitForSeconds(1.2f);
            WaveManager.Instance?.OnCelebrationDone();
        }

        public void WalkToPortal(Vector3 portalPos)
        {
            _phase = Phase.WalkingToPortal;
            StartCoroutine(WalkToRoutine(portalPos));
        }

        IEnumerator WalkToRoutine(Vector3 target)
        {
            PlayAnim(_walk, 10f, forceRestart: true);
            while (Mathf.Abs(transform.position.x - target.x) > 0.4f)
            {
                float dir = Mathf.Sign(target.x - transform.position.x);
                _rb.linearVelocity = new Vector2(dir * moveSpeed * 0.8f, _rb.linearVelocity.y);
                _sr.flipX = dir < 0;
                yield return null;
            }
            _rb.linearVelocity = Vector2.zero;
            PlayAnim(_idle, 8f, forceRestart: true);
            // The return to the city lingers: the hero stands in the portal while a
            // dense smoke cloud slowly swallows him ("demora um pouco mais"), then the
            // screen fades to black before the city loads.
            yield return StartCoroutine(PortalAbsorbRoutine(2.4f));
            // Returning to the city — don't arm the Dungeon arrival bloom.
            PortalSmoke.PendingAtWaveStart = false;
            SceneFader.FadeToBlack(0.4f);
            yield return new WaitForSeconds(0.4f);
            SceneManager.LoadScene("Zulfarak");
        }

        // ── Grounded detection ─────────────────────────────────────────────
        void OnCollisionEnter2D(Collision2D col) => CheckGround(col);
        void OnCollisionStay2D(Collision2D col)  => CheckGround(col);
        void OnCollisionExit2D(Collision2D col)  { }

        void CheckGround(Collision2D col)
        {
            foreach (var cp in col.contacts)
                if (cp.normal.y > 0.5f) return; // grounded — nothing to do
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
            int   i  = 0;
            while (loop || i < frames.Length)
            {
                _sr.sprite = frames[i % frames.Length];
                i++;
                yield return new WaitForSeconds(dt);
            }
            // Non-looping anim finished — clear so PlayAnim accepts the next switch.
            _currentAnim = null;
        }

        // Generates a simple blue silhouette (16×32 px) used when Inspector sprite
        // arrays are empty, so the player is at least visible for debugging.
        static Sprite MakePlaceholderSprite()
        {
            const int W = 16, H = 32;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            var body = new Color(0.25f, 0.45f, 0.90f, 1f);
            var head = new Color(0.85f, 0.70f, 0.55f, 1f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool inBody = x >= 3 && x <= 12 && y >= 2 && y <= 20;
                    bool inHead = x >= 4 && x <= 11 && y >= 21 && y <= 29;
                    t.SetPixel(x, y, inHead ? head : inBody ? body : Color.clear);
                }
            t.Apply();
            // Pivot at bottom-center so the sprite's feet sit on the transform origin.
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 32f);
        }
    }
}

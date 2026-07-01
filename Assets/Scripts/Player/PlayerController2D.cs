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

        float AttackInterval => 1f / Mathf.Max(0.1f, attackSpeed);

        [Header("Warrior Sprites")]
        public Sprite[] soldierIdleFrames;
        public Sprite[] soldierWalkFrames;
        public Sprite[] soldierAttackFrames;
        public Sprite[] soldierDeathFrames;

        [Header("Mage Sprites")]
        public Sprite[] wizardIdleFrames;
        public Sprite[] wizardWalkFrames;
        public Sprite[] wizardAttackFrames;
        public Sprite[] wizardDeathFrames;

        [Header("Archer Sprites")]
        public Sprite[] archerIdleFrames;
        public Sprite[] archerWalkFrames;
        public Sprite[] archerAttackFrames;
        public Sprite[] archerDeathFrames;

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
        private float    _atkTimer;
        private float    _attackLock;
        private Sprite[] _idle, _walk, _atk, _death;
        private ClassType _classType = ClassType.Warrior;

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
            _hp = maxHealth;
        }

        void Start()
        {
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

            // Auto-fit collider to the visible body so physics rests the player's
            // visible feet on the ground top (instead of leaving a gap from the
            // sprite's transparent padding). FitColliderToVisibleBounds has its
            // own reliability check + early return for non-readable textures.
            FitColliderToVisibleBounds();

            // Rest the player ON the ground at spawn. The scene authored the player
            // BELOW the ground collider top, so the fitted collider overlaps the ground
            // and Box2D violently ejects it (→ the "falling forever" bug). Shift the
            // player up so its collider bottom touches the ground top with no overlap.
            RestOnGroundAtSpawn();

            // Archer is a ranged class — double the attack range vs Warrior/Mage melee.
            if (_classType == ClassType.Archer)
                attackRange *= 2f;

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
                    break;
                case ClassType.Archer:
                    _idle  = Pick(archerIdleFrames,   soldierIdleFrames);
                    _walk  = Pick(archerWalkFrames,   soldierWalkFrames);
                    _atk   = Pick(archerAttackFrames, soldierAttackFrames);
                    _death = Pick(archerDeathFrames,  soldierDeathFrames);
                    break;
                default:
                    _idle  = soldierIdleFrames;
                    _walk  = soldierWalkFrames;
                    _atk   = soldierAttackFrames;
                    _death = soldierDeathFrames;
                    break;
            }
            if (_idle == null || _idle.Length == 0) _idle = soldierIdleFrames;
            if (_walk == null || _walk.Length == 0) _walk = _idle;
            if (_atk  == null || _atk.Length  == 0) _atk  = _idle;
        }

        static Sprite[] Pick(Sprite[] preferred, Sprite[] fallback)
            => (preferred != null && preferred.Length > 0) ? preferred : fallback;

        // Resize the BoxCollider2D so its bottom edge sits on the visible feet pixels
        // and its top edge on the visible head pixels. Without this, transparent padding
        // in the 100×100 frame leaves the character floating above the ground.
        // Pivot-AGNOSTIC: col.offset is TRANSFORM-relative, but ab.feetFromBottom is
        // measured from the sprite's bottom edge. Center-pivoted sprites have their
        // bottom at sprite.bounds.min.y (≈ -0.5), so we must add that offset — otherwise
        // the collider ends up ~0.5 units too high (× scale in world space) and the
        // character floats above the ground after RestOnGroundAtSpawn compensates.
        void FitColliderToVisibleBounds()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null || _sr == null || _sr.sprite == null) return;
            var ab = SpriteAlphaBounds.Get(_sr.sprite);
            float spriteH = _sr.sprite.bounds.size.y;
            if (ab.bottomFromBottom <= 0.001f && ab.topFromBottom >= spriteH - 0.001f) return;

            float feet = ab.feetFromBottom;
            float top  = ab.topFromBottom;
            float h    = Mathf.Max(0.05f, top - feet);
            float localBottom = _sr.sprite.bounds.min.y + feet;   // sprite bottom → transform origin
            col.size   = new Vector2(Mathf.Max(0.10f, ab.width * 0.6f), h);
            col.offset = new Vector2(0f, localBottom + h * 0.5f);
        }

        // Aligns the visible sprite feet AND the collider bottom to the ground line.
        // Uses GroundAlignUtil.SnapToGround (sprite-bounds based → pivot-agnostic) to
        // land the character visually, then trims any residual gap between the fitted
        // collider bottom and the ground so physics rests on the visible standing line.
        void RestOnGroundAtSpawn()
        {
            if (_sr == null) return;
            GroundAlignUtil.SnapToGround(transform, _sr);

            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                float groundTop = GroundAlignUtil.FindGroundTopY();
                float shift = (groundTop + 0.002f) - col.bounds.min.y;
                if (Mathf.Abs(shift) > 0.002f) transform.position += new Vector3(0f, shift, 0f);
            }
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
            ClampToSceneBounds();
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
                // Auto-approach nearest enemy, advancing further right into the wave
                // (stop closer than before so the player pushes toward the enemies).
                var target = FindNearest(attackRange * 5f);
                if (target != null)
                {
                    float dist = Vector2.Distance(transform.position, target.transform.position);
                    if (dist > attackRange * 0.5f)
                    {
                        MoveTowardX(target.transform.position.x);
                        return;
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

        // ── Auto-attack ────────────────────────────────────────────────────
        void HandleAutoAttack()
        {
            if (_atkTimer > 0) return;

            var target = FindNearest(attackRange);
            if (target == null) return;

            _sr.flipX = target.transform.position.x < transform.position.x;

            // Roll crit once per attack — 2× damage, yellow "*" popup.
            bool  crit = Random.value < critChance;
            float dmg  = crit ? attackDamage * critMultiplier : attackDamage;

            // The attack animation plays within one attack interval (1/attackSpeed),
            // leaving a small gap. Arrow release is timed off THIS animation duration.
            float interval = AttackInterval;
            float animDur  = Mathf.Clamp(interval * 0.85f, 0.12f, 0.7f);
            float atkFps   = (_atk != null && _atk.Length > 0) ? _atk.Length / animDur : 12f;

            // Archer releases the arrow partway through the swing; melee hits instantly.
            if (_classType == ClassType.Archer) StartCoroutine(FireArrowAfterDraw(target, dmg, crit, animDur));
            else                                target.TakeDamage(dmg, crit);

            _atkTimer   = interval;
            _attackLock = animDur;
            PlayAnim(_atk, atkFps, forceRestart: true, loop: false);
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

            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.position = spawnPos;
            var arrow = arrowGO.AddComponent<Arrow>();
            arrow.Init(target, dmg, crit);
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

        // ── Combat ─────────────────────────────────────────────────────
        public void TakeDamage(float dmg)
        {
            if (_phase == Phase.Dead) return;
            // Red popup — damage received by the player.
            DamagePopup.Spawn(transform, dmg, new Color(1f, 0.25f, 0.25f, 1f));            HurtFlash.Flash(_sr);            _hp = Mathf.Max(0f, _hp - dmg);
            _hpBar?.SetHealth(_hp, maxHealth);
            if (_hp <= 0f) StartCoroutine(DieRoutine());
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

        // Portal's signature violet — the player is dyed this colour as it's pulled in.
        static readonly Color PortalColor = new Color(0.66f, 0.38f, 1f, 1f);

        IEnumerator PortalAbsorbRoutine(float duration)
        {
            var halo = BuildPortalAbsorbHalo();
            Color baseColor  = _sr != null ? _sr.color : Color.white;
            Vector3 basePos  = transform.position;
            Vector3 baseScale = transform.localScale;
            float t = 0f;
            while (t < duration)
            {
                float p = t / duration;                       // 0 → 1
                float ramp = p * p;                           // accelerate the "suck-in"

                // Tremble: high-frequency jitter that intensifies toward the end.
                float shakeAmp = 0.015f + ramp * 0.06f;
                Vector3 jitter = new Vector3(
                    (Mathf.PerlinNoise(t * 40f, 0.3f) - 0.5f),
                    (Mathf.PerlinNoise(0.7f, t * 40f) - 0.5f), 0f) * (shakeAmp * 2f);

                // Pulled toward the portal (rings sit up/right of the absorb point) while shrinking.
                transform.position   = basePos + jitter;
                transform.localScale = Vector3.Lerp(baseScale, baseScale * 0.12f, ramp);

                // Whole sprite dyes to the portal colour as it dissolves in.
                if (_sr != null)
                    _sr.color = Color.Lerp(baseColor, PortalColor, Mathf.Clamp01(p * 1.3f));

                // Halo pulses fast in the portal colour and inflates.
                if (halo != null)
                {
                    float pulse = (Mathf.Sin(t * 18f) + 1f) * 0.5f;
                    var sr = halo.GetComponent<SpriteRenderer>();
                    if (sr != null)
                        sr.color = new Color(PortalColor.r, PortalColor.g, PortalColor.b, 0.45f + pulse * 0.45f);
                    halo.transform.localScale = Vector3.one * (1.10f + p * 0.90f + pulse * 0.12f);
                }
                t += Time.deltaTime;
                yield return null;
            }
            // Tell the next (Dungeon) scene to bloom purple smoke that dissipates as the wave begins.
            PortalSmoke.PendingAtWaveStart = true;
        }

        GameObject BuildPortalAbsorbHalo()
        {
            var go = new GameObject("PortalAbsorbHalo");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.50f, -0.1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeAbsorbHaloSprite();
            sr.color        = new Color(PortalColor.r, PortalColor.g, PortalColor.b, 0.55f);
            sr.sortingOrder = 50;
            return go;
        }

        static Sprite _absorbHalo;
        static Sprite MakeAbsorbHaloSprite()
        {
            if (_absorbHalo != null) return _absorbHalo;
            const int N = 64;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            float cx = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - cx) / cx;
                    float dy = (y - cx) / cx;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    float a  = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * 0.85f));
                }
            t.Apply();
            _absorbHalo = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _absorbHalo;
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
            for (int i = 0; i < 3; i++)
            {
                _rb.linearVelocity = new Vector2(0f, 7f);
                yield return new WaitForSeconds(0.65f);
            }
            yield return new WaitForSeconds(0.4f);
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
            // Pivot at bottom-center so FitColliderToVisibleBounds works correctly.
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 32f);
        }
    }
}

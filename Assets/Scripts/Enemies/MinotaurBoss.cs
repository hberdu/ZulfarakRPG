using System.Collections;
using UnityEngine;

namespace ZulfarakRPG
{
    // RANK A bonus boss, summoned by the red portal in the final dungeon. An extremely hard
    // Minotaur: towering, huge HP, heavy hits, and a periodic GORE CHARGE — it paws the ground
    // (telegraph), then rushes horizontally across the arena goring whoever it runs through for
    // massive damage. Spawned purely at runtime (no prefab); its art is sliced from the
    // Resources/minotaur sheet (8×7 grid of 100px frames).
    public class MinotaurBoss : SkeletonEnemy
    {
        [Header("Gore charge")]
        public float chargeCooldown  = 6f;
        public float chargeTelegraph = 0.6f;
        public float chargeSpeed     = 9f;
        public float chargeDamageMul = 2.5f;
        public float chargeReach     = 0.9f;

        protected override float ServerStatMultiplier => 1f;
        protected override bool  UsesBossHealthBar    => true;
        protected override float SpawnScaleMultiplier => 1.5f;   // towering
        protected override float EntranceInvulnSeconds => 2f;

        bool  _charging;
        float _chargeTimer = 4f;

        protected override void TickAI()
        {
            if (_charging) return;
            _chargeTimer -= Time.deltaTime;
            AcquireNearestTarget();
            if (_targetTf != null && _chargeTimer <= 0f) { StartCoroutine(GoreCharge()); return; }
            base.TickAI();   // normal melee chase/attack between charges
        }

        IEnumerator GoreCharge()
        {
            _charging = true;
            _chargeTimer = chargeCooldown;
            _rb.linearVelocity = Vector2.zero;

            AcquireNearestTarget();
            float dir = _targetTf != null ? Mathf.Sign(_targetTf.position.x - transform.position.x)
                                          : (_sr != null && _sr.flipX ? -1f : 1f);
            if (dir == 0f) dir = 1f;
            if (_sr != null) _sr.flipX = dir < 0;

            // Telegraph: rear up (stretch) so the charge is readable.
            var baseScale = transform.localScale;
            float t = 0f;
            while (t < chargeTelegraph && !_dead)
            {
                t += Time.deltaTime;
                float s = t / chargeTelegraph;
                transform.localScale = new Vector3(baseScale.x * (1f - 0.10f * s), baseScale.y * (1f + 0.10f * s), baseScale.z);
                yield return null;
            }
            transform.localScale = baseScale;
            if (_dead) { _charging = false; yield break; }

            // Rush horizontally until the far wall (or ~1.4 s), goring the hero once on the way.
            PlayAnim(attackFrames != null && attackFrames.Length > 0 ? attackFrames : walkFrames, 16f, forceRestart: true);
            bool gored = false;
            t = 0f;
            while (t < 1.4f && !_dead)
            {
                t += Time.deltaTime;
                transform.position += new Vector3(dir * chargeSpeed * Time.deltaTime, 0f, 0f);
                if (!gored && _player != null &&
                    Mathf.Abs(_player.transform.position.x - transform.position.x) <= chargeReach)
                {
                    _player.TakeDamage(attackDamage * chargeDamageMul);
                    gored = true;
                    PortalSmoke.BurstAt(_player.transform.position + Vector3.up * 0.2f, 6);
                }
                float x = transform.position.x;
                if (x <= sceneBoundsMinX + 0.2f || x >= sceneBoundsMaxX - 0.2f) break;   // hit the wall
                yield return null;
            }
            transform.position = new Vector3(Mathf.Clamp(transform.position.x, sceneBoundsMinX, sceneBoundsMaxX),
                                             transform.position.y, transform.position.z);
            _rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.35f);   // brief recovery — a window to punish it
            _charging = false;
        }

        // ── Spawn (runtime; no prefab) ──────────────────────────────────────
        public static MinotaurBoss Spawn(Vector3 pos)
        {
            LoadFrames();

            var go = new GameObject("Minotaur");
            go.transform.position = pos;

            // The boss HP bar (dragon-framed) lives as a child; add it BEFORE the boss component
            // so SkeletonEnemy.Awake's GetComponentInChildren<WorldHealthBar> finds it.
            var barGO = new GameObject("HealthBar");
            barGO.transform.SetParent(go.transform, false);
            barGO.AddComponent<WorldHealthBar>();

            var m   = go.AddComponent<MinotaurBoss>();   // RequireComponent adds RB2D / Box / SR
            var sr  = go.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 1;                          // same layer as the hero
            var col = go.GetComponent<BoxCollider2D>();
            col.size   = new Vector2(0.45f, 0.60f);       // body box; bottom ≈ sprite feet so it grounds cleanly
            col.offset = new Vector2(0f, 0.32f);

            m.idleFrames = _idle; m.walkFrames = _walk; m.attackFrames = _attack;
            m.hurtFrames = _hurt; m.deathFrames = _death;
            m.enemyId = "minotaur";

            // Extremely hard. SkeletonEnemy.Start() then multiplies by the per-phase ramp — in
            // Dungeon_4_1 (phase 4) that's ~4× HP and ~2.2× damage ON TOP of these.
            // ponytail: tune the four numbers here if it's unbeatable / too soft.
            m.maxHealth      = 3000f;
            m.attackDamage   = 26f;
            m.moveSpeed      = 2.2f;
            m.attackRange    = 0.95f;
            m.attackCooldown = 1.4f;
            return m;
        }

        // ── Sprite sheet (Resources/minotaur = 8 cols × 7 rows of 100px frames) ──
        // Row order (Craftpix Tiny RPG Pack 02): 0 idle, 1 walk, 2 run, 3 attack1, 4 attack2,
        // 5 take-hit, 6 death. We use idle/walk/attack/hurt/death.
        static Sprite[] _idle, _walk, _attack, _hurt, _death;
        static bool _loaded;
        static void LoadFrames()
        {
            if (_loaded) return;
            _loaded = true;
            var tex = Resources.Load<Texture2D>("minotaur");
            if (tex == null) { Debug.LogWarning("[MinotaurBoss] Resources/minotaur não encontrado."); return; }
            const int F = 100;
            int rows = Mathf.Max(1, tex.height / F);

            // PNG rows are top-down; Unity texture Y is bottom-up → flip: pngRow r sits at y=(rows-1-r)*F.
            // Frames get unique names so PlayAnim treats each animation as distinct (Sprite.Create
            // leaves .name empty otherwise, and every anim would share the same "" key).
            Sprite[] Row(int pngRow, int count)
            {
                int y = (rows - 1 - pngRow) * F;
                var a = new Sprite[count];
                for (int i = 0; i < count; i++)
                {
                    a[i] = Sprite.Create(tex, new Rect(i * F, y, F, F), new Vector2(0.5f, 0f), 100f);
                    a[i].name = $"mino{pngRow}_{i}";
                }
                return a;
            }
            _idle   = Row(0, 6);
            _walk   = Row(1, 8);
            _attack = Row(3, 7);
            _hurt   = Row(5, 4);
            _death  = Row(6, 4);
        }
    }
}

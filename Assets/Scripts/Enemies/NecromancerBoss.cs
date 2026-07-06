using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Wave-10 boss: a Necromancer that fights at RANGE — it hurls magic bolts
    // (playing its Attack02 cast) and keeps its distance while its skeleton minions
    // do the rushing. On its first appearance it steps out of a green portal, TAUNTS
    // the player, and raises a batch of skeletons; it re-summons whenever none remain.
    public class NecromancerBoss : SkeletonEnemy
    {
        [Header("Boss / Summon")]
        public Sprite[]  summonFrames;
        public GameObject minionPrefab;
        public int   minionsPerSummon = 5;
        public float summonCooldown   = 14f;
        public float summonFps        = 10f;

        [Header("Ranged magic attack")]
        public Sprite[] magicBoltFrames;      // Magic(projectile) effect frames
        public float    castMinDistance = 2.4f;  // kite back if the player gets closer than this
        public float    castFps         = 12f;

        private readonly List<SkeletonEnemy> _minions = new();
        private bool  _summoning;
        private bool  _casting;
        private float _summonTimer = 1.5f;   // first re-summon shortly after engaging
        private int   _summonBatch;
        private bool  _entranceStarted;
        private bool  _entranceDone;

        protected override void TickAI()
        {
            // Entrance: rise from a green portal, taunt, open with a summon, and only
            // afterwards start the ranged fight.
            if (!_entranceDone)
            {
                if (!_entranceStarted)
                {
                    _entranceStarted = true;
                    StartCoroutine(EntranceRoutine());
                }
                return;
            }

            if (_summoning || _casting) return;

            _summonTimer -= Time.deltaTime;
            _minions.RemoveAll(m => m == null || !m.IsAlive);

            if (_summonTimer <= 0f && _minions.Count == 0 && minionPrefab != null)
            {
                StartCoroutine(SummonRoutine());
                return;
            }

            RangedCastAI();
        }

        // Stand at staff range and lob magic bolts; kite backwards if the player closes in.
        void RangedCastAI()
        {
            float dx   = _player.transform.position.x - transform.position.x;
            float dist = Mathf.Abs(dx);
            if (dist > 0.01f) _sr.flipX = dx < 0;   // face the player

            if (dist < castMinDistance)
            {
                float away  = -Mathf.Sign(dx);
                float nextX = transform.position.x + away * moveSpeed * Time.deltaTime;
                if (nextX > sceneBoundsMinX + 0.05f && nextX < sceneBoundsMaxX - 0.05f)
                {
                    _rb.linearVelocity = new Vector2(away * moveSpeed, _rb.linearVelocity.y);
                    if (_attackLock <= 0) PlayAnim(walkFrames, 10f);
                    return;
                }
            }

            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            if (_atkTimer <= 0f)
            {
                StartCoroutine(CastRoutine());
                return;
            }
            if (_attackLock <= 0) PlayAnim(idleFrames, 8f);
        }

        IEnumerator CastRoutine()
        {
            _casting  = true;
            _atkTimer = attackCooldown;
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            float dur = attackFrames != null && attackFrames.Length > 0 ? attackFrames.Length / castFps : 0.8f;
            _attackLock = dur;
            if (attackFrames != null && attackFrames.Length > 0)
                PlayAnim(attackFrames, castFps, forceRestart: true, loop: false);

            // Release the bolt partway through the cast (apex of the raised staff).
            yield return new WaitForSeconds(dur * 0.55f);
            if (!_dead && _player != null) FireBolt();
            yield return new WaitForSeconds(dur * 0.45f);
            _casting = false;
        }

        void FireBolt()
        {
            var myCol = GetComponent<Collider2D>();
            Vector3 spawn = myCol != null ? myCol.bounds.center : transform.position + Vector3.up * 0.5f;
            float dir = Mathf.Sign(_player.transform.position.x - transform.position.x);
            if (dir == 0f) dir = 1f;
            spawn += new Vector3(dir * 0.35f, 0.15f, 0f);

            var go = new GameObject("MagicBolt");
            go.transform.position = spawn;
            go.AddComponent<MagicBolt>().Init(_player, attackDamage, magicBoltFrames);
        }

        IEnumerator EntranceRoutine()
        {
            // Materialize on-screen out of a green portal instead of walking in.
            var pos = new Vector3(
                Mathf.Clamp(sceneBoundsMaxX - 0.9f, sceneBoundsMinX, sceneBoundsMaxX),
                transform.position.y, transform.position.z);
            transform.position = pos;
            _rb.linearVelocity = Vector2.zero;
            ReleaseFromSpawn();
            SetVisible(false);

            var portal = GreenPortalFX.Create(pos + Vector3.up * 0.55f);
            yield return new WaitForSeconds(0.9f);
            if (_dead) { if (portal) portal.Dismiss(); yield break; }

            PortalSmoke.BurstAt(pos + Vector3.up * 0.3f, 14);
            SetVisible(true);
            yield return new WaitForSeconds(0.35f);
            if (portal) portal.Dismiss();
            if (_dead) yield break;

            // Deboche: face the player and laugh (squash/stretch bounce) before raising the dead.
            yield return StartCoroutine(TauntRoutine());
            if (_dead) yield break;

            // Opening summon, then the ranged fight begins.
            yield return StartCoroutine(SummonRoutine());
            _entranceDone = true;
        }

        // A little mocking "laugh" — the necromancer faces the player and bobs with a
        // squash/stretch a few times, jeering before the skeletons rise.
        IEnumerator TauntRoutine()
        {
            if (_player != null)
                _sr.flipX = (_player.transform.position.x - transform.position.x) < 0;
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            if (idleFrames != null && idleFrames.Length > 0)
                PlayAnim(idleFrames, 8f, forceRestart: true);

            Vector3 baseScale = transform.localScale;
            const int laughs = 3;
            for (int i = 0; i < laughs; i++)
            {
                float t = 0f;
                while (t < 0.28f)
                {
                    t += Time.deltaTime;
                    float s = Mathf.Sin(t / 0.28f * Mathf.PI);          // 0→1→0
                    transform.localScale = new Vector3(
                        baseScale.x * (1f + 0.10f * s),
                        baseScale.y * (1f - 0.08f * s),                 // squash down as it "cackles"
                        baseScale.z);
                    yield return null;
                }
                transform.localScale = baseScale;
            }
            transform.localScale = baseScale;
        }

        void SetVisible(bool visible)
        {
            if (_sr) _sr.enabled = visible;
            if (_hpBar) _hpBar.gameObject.SetActive(visible);
            var col = GetComponent<Collider2D>();
            if (col) col.enabled = visible;
            _rb.simulated = visible;
        }

        IEnumerator SummonRoutine()
        {
            _summoning = true;
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);

            float dur = summonFrames != null && summonFrames.Length > 0
                        ? summonFrames.Length / summonFps : 1f;
            if (summonFrames != null && summonFrames.Length > 0)
                PlayAnim(summonFrames, summonFps, forceRestart: true, loop: false);
            _attackLock = dur;

            // Minions appear mid-cast, when the magic effect peaks.
            yield return new WaitForSeconds(dur * 0.6f);
            if (_dead) { _summoning = false; yield break; }

            _summonBatch++;
            for (int i = 0; i < minionsPerSummon; i++)
            {
                // Alternate sides around the boss so the pack fans out.
                float side   = (i % 2 == 0) ? -1f : 1f;
                float offset = side * (0.6f + (i / 2) * 0.55f);
                var pos = new Vector3(
                    Mathf.Clamp(transform.position.x + offset, sceneBoundsMinX, sceneBoundsMaxX),
                    transform.position.y, transform.position.z);

                var go = Instantiate(minionPrefab, pos, Quaternion.identity);
                var sk = go.GetComponent<SkeletonEnemy>();
                if (sk)
                {
                    sk.enemyId       = enemyId;   // fall back to boss mapping if unresolved
                    sk.netInstanceId = $"{netInstanceId}_s{_summonBatch}_{i}";
                    sk.ReleaseFromSpawn();
                    _minions.Add(sk);
                    WaveManager.Instance?.RegisterSummon(sk);
                }
                PortalSmoke.BurstAt(pos + Vector3.up * 0.2f, 6);
            }

            yield return new WaitForSeconds(dur * 0.4f);
            _summonTimer = summonCooldown;
            _summoning   = false;
        }

        protected override void OnDeathStarted()
        {
            // The necromancer's magic dies with him.
            foreach (var m in _minions)
                if (m != null && m.IsAlive) m.TakeDamage(999999f);
            _minions.Clear();
        }
    }
}

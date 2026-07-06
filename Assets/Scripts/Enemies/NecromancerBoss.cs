using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Wave-10 boss. Summons a batch of skeleton minions (playing its Summon
    // animation) whenever none of its minions remain alive, and otherwise
    // fights like a regular SkeletonEnemy (chase + melee).
    public class NecromancerBoss : SkeletonEnemy
    {
        [Header("Boss / Summon")]
        public Sprite[]  summonFrames;
        public GameObject minionPrefab;
        public int   minionsPerSummon = 5;
        public float summonCooldown   = 14f;
        public float summonFps        = 10f;

        private readonly List<SkeletonEnemy> _minions = new();
        private bool  _summoning;
        private float _summonTimer = 1.5f;   // first summon shortly after engaging
        private int   _summonBatch;
        private bool  _entranceStarted;
        private bool  _entranceDone;

        protected override void TickAI()
        {
            // Entrance: step out of a green portal, open with a summon, and only
            // afterwards start chasing the player (combat begins on contact).
            if (!_entranceDone)
            {
                if (!_entranceStarted)
                {
                    _entranceStarted = true;
                    StartCoroutine(EntranceRoutine());
                }
                return;
            }

            if (_summoning) return;

            _summonTimer -= Time.deltaTime;
            _minions.RemoveAll(m => m == null || !m.IsAlive);

            if (_summonTimer <= 0f && _minions.Count == 0 && minionPrefab != null)
            {
                StartCoroutine(SummonRoutine());
                return;
            }

            base.TickAI();
        }

        IEnumerator EntranceRoutine()
        {
            // Materialize on-screen out of a green portal instead of walking in
            // from the off-screen spawn point.
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
            yield return new WaitForSeconds(0.5f);
            if (portal) portal.Dismiss();
            if (_dead) yield break;

            // Opening act: summon the skeletons, then base.TickAI takes over —
            // the boss walks onto the player and only then the fight begins.
            yield return StartCoroutine(SummonRoutine());
            _entranceDone = true;
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

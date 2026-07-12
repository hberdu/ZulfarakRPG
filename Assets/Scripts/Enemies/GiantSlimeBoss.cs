using System.Collections;
using UnityEngine;

namespace ZulfarakRPG
{
    // Phase 3-1 boss: a Giant Slime. Slow melee like a big slime (reuses base chase/attack),
    // plus one signature ability — a SLAM: it squashes, leaps high, then crashes down dealing
    // heavy area damage to anyone near the landing.
    public class GiantSlimeBoss : SkeletonEnemy
    {
        [Header("Slam ability")]
        public float slamCooldown        = 7f;
        public float slamTelegraph       = 0.5f;   // squash before the leap
        public float slamRiseTime        = 0.35f;
        public float slamHang            = 0.15f;
        public float slamFallTime        = 0.22f;
        public float slamHeight          = 2.2f;
        public float slamRadius          = 1.4f;
        public float slamDamageMultiplier = 2.2f;

        protected override float ServerStatMultiplier => 1f;
        protected override bool  UsesBossHealthBar     => true;
        protected override float SpawnScaleMultiplier  => 1.3f;   // big blob

        private bool  _slamming;
        private float _slamTimer = 3f;

        protected override void TickAI()
        {
            if (_slamming) return;

            _slamTimer -= Time.deltaTime;
            AcquireNearestTarget();
            if (_targetTf != null && _slamTimer <= 0f)
            {
                StartCoroutine(SlamRoutine());
                return;
            }

            base.TickAI();   // normal slow melee between slams
        }

        IEnumerator SlamRoutine()
        {
            _slamming = true;
            _slamTimer = slamCooldown;
            _rb.linearVelocity = Vector2.zero;

            var tr = transform;
            Vector3 baseScale = tr.localScale;
            Vector3 groundPos = tr.position;

            // Telegraph: squash down.
            float t = 0f;
            while (t < slamTelegraph && !_dead)
            {
                t += Time.deltaTime;
                float s = t / slamTelegraph;
                tr.localScale = new Vector3(baseScale.x * (1f + 0.25f * s), baseScale.y * (1f - 0.20f * s), baseScale.z);
                yield return null;
            }
            if (_dead) { tr.localScale = baseScale; _slamming = false; yield break; }

            // Rise (stretch up).
            t = 0f;
            while (t < slamRiseTime && !_dead)
            {
                t += Time.deltaTime;
                float u = t / slamRiseTime;
                tr.position = new Vector3(groundPos.x, groundPos.y + slamHeight * u, groundPos.z);
                tr.localScale = new Vector3(baseScale.x * (1f - 0.10f * u), baseScale.y * (1f + 0.20f * u), baseScale.z);
                yield return null;
            }
            // Track the target horizontally at the apex, then hang briefly.
            AcquireNearestTarget();
            float landX = _targetTf != null
                ? Mathf.Clamp(_targetTf.position.x, sceneBoundsMinX + 0.3f, sceneBoundsMaxX - 0.3f)
                : groundPos.x;
            yield return new WaitForSeconds(slamHang);
            if (_dead) { tr.position = groundPos; tr.localScale = baseScale; _slamming = false; yield break; }

            // Crash down onto landX.
            Vector3 apex = tr.position;
            apex.x = landX;
            t = 0f;
            while (t < slamFallTime && !_dead)
            {
                t += Time.deltaTime;
                float u = t / slamFallTime;
                tr.position = new Vector3(landX, Mathf.Lerp(apex.y, groundPos.y, u), groundPos.z);
                yield return null;
            }
            tr.position = new Vector3(landX, groundPos.y, groundPos.z);
            tr.localScale = new Vector3(baseScale.x * 1.3f, baseScale.y * 0.7f, baseScale.z); // impact squash

            // Area damage to the local hero if near the landing.
            if (!_dead && _player != null && Mathf.Abs(_player.transform.position.x - landX) <= slamRadius)
                _player.TakeDamage(attackDamage * slamDamageMultiplier);
            PortalSmoke.BurstAt(new Vector3(landX, groundPos.y + 0.1f, 0f), 10);

            // Recover to normal shape.
            t = 0f;
            while (t < 0.25f && !_dead)
            {
                t += Time.deltaTime;
                tr.localScale = Vector3.Lerp(new Vector3(baseScale.x * 1.3f, baseScale.y * 0.7f, baseScale.z), baseScale, t / 0.25f);
                yield return null;
            }
            tr.localScale = baseScale;
            _slamming = false;
        }
    }
}

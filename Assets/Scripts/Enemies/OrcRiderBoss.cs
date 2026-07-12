using System.Collections;
using UnityEngine;

namespace ZulfarakRPG
{
    // Phase 2-1 boss: a mounted Orc Rider. Fights melee like a big orc (reuses the base
    // SkeletonEnemy chase/attack), plus one signature ability — a CHARGE: it rears up
    // (telegraph), then dashes across the arena trampling whoever it runs over for heavy damage.
    public class OrcRiderBoss : SkeletonEnemy
    {
        [Header("Charge ability")]
        public float chargeCooldown        = 8f;
        public float chargeTelegraph       = 0.6f;   // rear-up pause before dashing
        public float chargeDistance        = 5f;
        public float chargeSpeed           = 11f;
        public float chargeRecover         = 0.5f;
        public float chargeDamageMultiplier = 2f;    // trample hit = 2× the basic attack

        // Boss stats come straight from the server catalog (seeder sizes it well above orcs).
        protected override float ServerStatMultiplier => 1f;
        protected override bool  UsesBossHealthBar     => true;
        protected override float SpawnScaleMultiplier  => 1.1f;   // mounted = bigger

        private bool  _charging;
        private float _chargeTimer = 3f;   // first charge a few seconds in

        protected override void TickAI()
        {
            if (_charging) return;

            _chargeTimer -= Time.deltaTime;
            AcquireNearestTarget();
            if (_targetTf != null && _chargeTimer <= 0f)
            {
                StartCoroutine(ChargeRoutine());
                return;
            }

            base.TickAI();   // normal melee chase/attack between charges
        }

        IEnumerator ChargeRoutine()
        {
            _charging = true;
            _chargeTimer = chargeCooldown;
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            // Telegraph: face the target and pause so the player can react.
            AcquireNearestTarget();
            float dir = _targetTf != null
                        ? Mathf.Sign(_targetTf.position.x - transform.position.x)
                        : (_sr != null && _sr.flipX ? -1f : 1f);
            if (dir == 0f) dir = 1f;
            if (_sr != null) _sr.flipX = dir < 0;
            PlayAnim(idleFrames, 8f, forceRestart: true);
            yield return new WaitForSeconds(chargeTelegraph);
            if (_dead) { _charging = false; yield break; }

            // Dash across, trampling the local hero once for heavy damage.
            var runFrames = walkFrames != null && walkFrames.Length > 0 ? walkFrames : attackFrames;
            PlayAnim(runFrames, 14f, forceRestart: true);
            float trampleDmg = attackDamage * chargeDamageMultiplier;
            float traveled = 0f;
            bool  trampled = false;   // trample the local hero at most once per charge
            while (traveled < chargeDistance && !_dead)
            {
                float nextX = Mathf.Clamp(transform.position.x + dir * chargeSpeed * Time.deltaTime,
                                          sceneBoundsMinX, sceneBoundsMaxX);
                float moved = Mathf.Abs(nextX - transform.position.x);
                if (moved < 0.0001f) break;   // hit the arena edge
                traveled += moved;
                transform.position = new Vector3(nextX, transform.position.y, transform.position.z);

                if (!trampled && _player != null &&
                    Mathf.Abs(_player.transform.position.x - transform.position.x) < 0.5f)
                {
                    _player.TakeDamage(trampleDmg);
                    trampled = true;
                }
                yield return null;
            }

            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            PlayAnim(idleFrames, 8f, forceRestart: true);
            yield return new WaitForSeconds(chargeRecover);
            _charging = false;
        }
    }
}

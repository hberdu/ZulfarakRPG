using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Phase 4-1 boss: the Alpha Werewolf. Bigger and fiercer than the pack werewolves.
    // Fights melee like a big werewolf (reuses the base SkeletonEnemy chase/attack), plus one
    // signature ability — a POUNCE: it crouches (telegraph), leaps in an arc onto the target's
    // position raking whoever it lands on, then enters a brief FRENZY (faster move + swings).
    public class AlphaWerewolfBoss : SkeletonEnemy
    {
        [Header("Pounce ability")]
        public float pounceCooldown        = 7f;
        public float pounceTelegraph       = 0.45f;  // crouch before the leap
        public float pounceTime            = 0.4f;    // arc flight duration
        public float pounceHeight          = 2.4f;    // arc apex height
        public float pounceRadius          = 1.1f;    // claw rake reach on landing
        public float pounceDamageMultiplier = 2.3f;

        [Header("Frenzy (after landing)")]
        public float frenzyDuration        = 3f;
        public float frenzySpeedMul        = 1.6f;
        public float frenzyAttackSpeedMul  = 0.55f;   // multiplies attackCooldown (lower = faster)

        protected override float ServerStatMultiplier => 1f;   // server catalog sizes the alpha
        protected override bool  UsesBossHealthBar     => true;
        protected override float SpawnScaleMultiplier  => 1.35f; // biggest boss so far

        private bool  _pouncing;
        private float _pounceTimer = 3f;

        protected override void TickAI()
        {
            if (_pouncing) return;

            _pounceTimer -= Time.deltaTime;
            AcquireNearestTarget();
            if (_targetTf != null && _pounceTimer <= 0f)
            {
                StartCoroutine(PounceRoutine());
                return;
            }

            base.TickAI();   // normal melee chase/attack between pounces
        }

        IEnumerator PounceRoutine()
        {
            _pouncing = true;
            _pounceTimer = pounceCooldown;
            _rb.linearVelocity = Vector2.zero;

            var tr = transform;
            Vector3 baseScale = tr.localScale;
            float groundY = tr.position.y;

            // Telegraph: crouch (squash) and face the target.
            AcquireNearestTarget();
            float dir = _targetTf != null ? Mathf.Sign(_targetTf.position.x - tr.position.x)
                                          : (_sr != null && _sr.flipX ? -1f : 1f);
            if (dir == 0f) dir = 1f;
            if (_sr != null) _sr.flipX = dir < 0;
            float t = 0f;
            while (t < pounceTelegraph && !_dead)
            {
                t += Time.deltaTime;
                float s = t / pounceTelegraph;
                tr.localScale = new Vector3(baseScale.x * (1f + 0.15f * s), baseScale.y * (1f - 0.18f * s), baseScale.z);
                yield return null;
            }
            tr.localScale = baseScale;
            if (_dead) { _pouncing = false; yield break; }

            // Lock the landing spot to the target's current X (clamped in the arena).
            float startX = tr.position.x;
            float landX  = _targetTf != null
                ? Mathf.Clamp(_targetTf.position.x, sceneBoundsMinX + 0.3f, sceneBoundsMaxX - 0.3f)
                : startX + dir * 3f;
            if (_sr != null) _sr.flipX = (landX - startX) < 0;

            // Parabolic leap: X lerps start→land, Y follows a 4·h·u·(1-u) arch.
            var leapFrames = attackFrames != null && attackFrames.Length > 0 ? attackFrames : walkFrames;
            PlayAnim(leapFrames, 14f, forceRestart: true);
            t = 0f;
            while (t < pounceTime && !_dead)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / pounceTime);
                float x = Mathf.Lerp(startX, landX, u);
                float y = groundY + pounceHeight * 4f * u * (1f - u);
                tr.position = new Vector3(x, y, tr.position.z);
                yield return null;
            }
            tr.position = new Vector3(landX, groundY, tr.position.z);
            tr.localScale = new Vector3(baseScale.x * 1.2f, baseScale.y * 0.85f, baseScale.z); // landing squash
            PortalSmoke.BurstAt(new Vector3(landX, groundY + 0.1f, 0f), 8);

            // Claw rake: everyone within pounceRadius of the landing takes heavy damage
            // (only the local hero actually loses HP; remote victims resolve on their own client).
            if (!_dead && _player != null && Mathf.Abs(_player.transform.position.x - landX) <= pounceRadius)
                _player.TakeDamage(attackDamage * pounceDamageMultiplier);

            // Recover shape.
            t = 0f;
            var squashed = tr.localScale;
            while (t < 0.2f && !_dead)
            {
                t += Time.deltaTime;
                tr.localScale = Vector3.Lerp(squashed, baseScale, t / 0.2f);
                yield return null;
            }
            tr.localScale = baseScale;

            // FRENZY: temporarily faster and hits more often.
            float origSpeed = moveSpeed, origCd = attackCooldown;
            moveSpeed      *= frenzySpeedMul;
            attackCooldown *= frenzyAttackSpeedMul;
            _pouncing = false;                 // resume normal melee, now enraged
            yield return new WaitForSeconds(frenzyDuration);
            moveSpeed      = origSpeed;
            attackCooldown = origCd;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Auto-casts every LEARNED skill on its own cooldown while in the dungeon — the hero
    // has no mana, so learned skills simply fire when their timer elapses (Taskbar-Hero
    // style). Damage skills hit the nearest enemy; heal skills top the hero up. Attached
    // to the player by PlayerController2D.
    [RequireComponent(typeof(PlayerController2D))]
    public class SkillAutoCaster : MonoBehaviour
    {
        // Live cooldown state of an equipped skill, consumed by the on-character HUD.
        public struct ActiveSkill { public SkillDef def; public int level; public float remaining; public float total; }
        public readonly List<ActiveSkill> Active = new();

        PlayerController2D _player;
        readonly Dictionary<string, float> _cd = new();

        void Awake()
        {
            _player = GetComponent<PlayerController2D>();
            SkillCooldownHUD.Attach(this);
        }

        // Skills auto-cast in EVERY dungeon phase, not just the first one — the phase-2/3/4
        // scenes are named "Dungeon_2_1", "Dungeon_3_1", "Dungeon_4_1".
        bool InDungeon
        {
            get
            {
                var n = SceneManager.GetActiveScene().name;
                return n == "Dungeon" || n.StartsWith("Dungeon_");
            }
        }

        // Only TICKS the cooldowns (for the HUD). Casting is driven by the attack cadence
        // via TryCastReady(), so skills fire one at a time and replace the basic attack.
        void Update()
        {
            Active.Clear();
            if (_player == null || SkillManager.Instance == null) return;

            float cdr = Mathf.Clamp(PlayerManager.Instance?.Data?.cooldownReductionPct ?? 0f, 0f, 0.9f);
            foreach (var (def, level) in SkillManager.Instance.Equipped())
            {
                if (!_cd.TryGetValue(def.id, out var t)) { t = 0f; _cd[def.id] = 0f; }
                if (InDungeon) { t = Mathf.Max(0f, t - Time.deltaTime); _cd[def.id] = t; }
                Active.Add(new ActiveSkill { def = def, level = level, remaining = t, total = def.CooldownAt(level) * (1f - cdr) });
            }
        }

        // Called by PlayerController2D.HandleAutoAttack on each attack tick. Casts exactly
        // ONE ready equipped skill (in slot order) and returns true; the caller then skips
        // the basic attack for that tick. Returns false when no skill is ready/castable.
        public bool TryCastReady(SkeletonEnemy target)
        {
            if (_player == null || SkillManager.Instance == null || !InDungeon) return false;

            foreach (var (def, level) in SkillManager.Instance.Equipped())
            {
                if (!_cd.TryGetValue(def.id, out var t)) { t = 0f; _cd[def.id] = 0f; }
                if (t > 0f) continue;                  // still on cooldown
                if (TryCast(def, level))               // handles heal-needed / target checks
                {
                    // Equipped gear's cooldown reduction shortens the recharge (capped in
                    // Inventory.RecalculateStats).
                    float cdr = PlayerManager.Instance?.Data?.cooldownReductionPct ?? 0f;
                    _cd[def.id] = def.CooldownAt(level) * (1f - Mathf.Clamp(cdr, 0f, 0.9f));
                    return true;                       // one skill per tick
                }
            }
            return false;
        }

        // Radius (world units) of the warrior/mage area skills — "everything inside the
        // swing/blast animation".
        const float AreaRadius = 1.9f;

        bool TryCast(SkillDef def, int level)
        {
            if (def.effect == SkillEffect.Heal)
            {
                if (_player.Health >= _player.MaxHealthValue * 0.98f) return false;
                float healAnim = _player.PlayCastAnimation(_player.transform.position);
                StartCoroutine(ApplyHealAtCastApex(def, level, Mathf.Max(0.03f, healAnim * 0.2f)));
                return true;
            }

            // Damage: aim at the nearest living enemy on the battlefield.
            var e = _player.NearestEnemy(16f);
            if (e == null || !e.IsAlive) return false;

            float atk = _player.attackDamage;

            switch (def.shape)
            {
                case SkillShape.ArcherConcentrated:
                    // 2 s charge → single 200% white shot (handles its own animation).
                    StartCoroutine(ConcentratedShot(e, 2f * atk));
                    return true;

                case SkillShape.ArcherSerpent:
                {
                    // Bow glows green (charge) → green cast ring → venom arrow + poison DoT.
                    StartCoroutine(SerpentCast(e, atk));
                    return true;
                }

                case SkillShape.ArcherRain:
                {
                    // Use the hero's NORMAL cast animation (its real, correctly-coloured sprite). The
                    // old baked "aim up" sprite recoloured the whole hero and made it lean over; the
                    // sky-aim read now comes from the falling arrows instead of a full-body pose.
                    _player.PlayCastAnimation(e.transform.position);
                    SkillCastFX.Spawn(_player.transform.position, new Color(1f, 1f, 1f, 0.95f));
                    StartCoroutine(ArrowRainAtApex(0.75f * atk, 0.03f));
                    return true;
                }

                case SkillShape.AreaMelee:
                {
                    float anim = _player.PlayCastAnimation(e.transform.position);
                    float dmg  = def.PowerAt(level) + atk * 0.5f;
                    StartCoroutine(ApplyAreaAtApex(e.transform.position, def, dmg, Mathf.Max(0.03f, anim * 0.2f)));
                    return true;
                }

                default: // Single
                {
                    float anim = _player.PlayCastAnimation(e.transform.position);
                    float dmg  = def.PowerAt(level) + atk * 0.5f;
                    StartCoroutine(ApplyDamageAtCastApex(e, def, dmg, Mathf.Max(0.03f, anim * 0.2f)));
                    return true;
                }
            }
        }

        // World point in front of the hero (collider centre nudged toward the target) —
        // where projectiles/muzzle effects originate.
        Vector3 Muzzle(Vector3 targetPos)
        {
            var col = _player.GetComponent<Collider2D>();
            Vector3 c = col != null ? col.bounds.center : _player.transform.position + Vector3.up * 0.5f;
            float dir = Mathf.Sign(targetPos.x - _player.transform.position.x);
            if (dir == 0f) dir = 1f;
            return c + new Vector3(dir * 0.35f, 0.05f, 0f);
        }

        // One single arrow sprite for ALL skill projectiles (Serpe, Chuva) — the same art the
        // basic shot flies — so a volley reads as one consistent arrow instead of mixed variants.
        Sprite CurrentArrowSprite() => Arrow.SharedSprite;

        // Baked "aiming up" archer pose (Resources/archer_aim_up), used by Chuva de Flechas so the
        // hero visibly aims skyward without being repositioned. Loaded once.
        static Sprite _aimUp; static bool _aimUpTried;
        static Sprite AimUpSprite()
        {
            if (_aimUp == null && !_aimUpTried) { _aimUp = Resources.Load<Sprite>("archer_aim_up"); _aimUpTried = true; }
            return _aimUp;
        }

        // Animated neon-green serpent (Resources/serpent_green = 4×24px sheet), sliced at runtime.
        static Sprite[] _snakeFrames; static bool _snakeTried;
        static Sprite[] SnakeFrames()
        {
            if (_snakeFrames == null && !_snakeTried)
            {
                _snakeTried = true;
                var tex = Resources.Load<Texture2D>("serpent_green");
                if (tex != null)
                {
                    const int fw = 24, fh = 24; int n = Mathf.Max(1, tex.width / fw);
                    _snakeFrames = new Sprite[n];
                    for (int i = 0; i < n; i++)
                        _snakeFrames[i] = Sprite.Create(tex, new Rect(i*fw, 0, fw, fh), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            return _snakeFrames;
        }

        // The archer's VISIBLE on-screen height (alpha-trimmed, so the transparent 100px frame
        // padding doesn't inflate it) — used to size the serpent telegraph.
        float ArcherVisibleHeight()
        {
            var psr = _player != null ? _player.GetComponent<SpriteRenderer>() : null;
            if (psr == null || psr.sprite == null) return 0.8f;
            var ab = SpriteAlphaBounds.Get(psr.sprite);
            return Mathf.Max(0.1f, (ab.topFromBottom - ab.bottomFromBottom) * Mathf.Abs(_player.transform.lossyScale.y));
        }

        // ── Archer: Tiro de Serpe ────────────────────────────────────────────
        // A neon-green SERPENT slithers up at the bow (animated telegraph), a green cast ring pops,
        // then a neon-outlined venom arrow is loosed dealing attack damage + a poison DoT.
        System.Collections.IEnumerator SerpentCast(SkeletonEnemy target, float atk)
        {
            if (_player == null || target == null || !target.IsAlive) yield break;

            var frames = SnakeFrames();
            GameObject snake; SpriteRenderer ssr = null;
            if (frames != null && frames.Length > 0)
            {
                snake = new GameObject("SerpentTelegraph");
                ssr = snake.AddComponent<SpriteRenderer>();
                ssr.sprite = frames[0]; ssr.sortingOrder = 60;
                Arrow.ApplyWorldSize(snake.transform, frames[0], ArcherVisibleHeight() * 0.5f);  // half archer height
            }
            else
            {
                snake = SkillCastFXWhite(_player.transform.position);
                var g = snake.GetComponent<SpriteRenderer>();
                if (g != null) { g.color = new Color(0.35f, 1f, 0.4f, 0.85f); g.sortingOrder = 60; }
            }

            const float charge = 0.12f;   // very brief wind-up (snappy)
            float t = 0f;
            float baseSx = Mathf.Abs(snake.transform.localScale.x);
            while (t < charge)
            {
                if (_player == null || target == null || !target.IsAlive) { if (snake) Destroy(snake); yield break; }
                t += Time.deltaTime;
                Vector3 bow = Muzzle(target.transform.position);
                bool left = target.transform.position.x < _player.transform.position.x;
                if (snake)
                {
                    snake.transform.position = bow + Vector3.up * 0.1f;
                    var s = snake.transform.localScale; s.x = baseSx * (left ? -1f : 1f); snake.transform.localScale = s;
                    if (ssr != null && frames != null) ssr.sprite = frames[(int)(t / 0.08f) % frames.Length]; // animate
                }
                yield return null;
            }
            if (snake) Destroy(snake);
            if (target == null || !target.IsAlive) yield break;

            // Draw pose + green cast ring, then loose the neon-outlined arrow quickly.
            float anim = _player.PlayCastAnimation(target.transform.position);
            SkillCastFX.Spawn(_player.transform.position, new Color(0.35f, 1f, 0.4f, 0.95f));
            yield return new WaitForSeconds(Mathf.Max(0.02f, anim * 0.12f));
            if (target == null || !target.IsAlive) yield break;

            Vector3 origin = Muzzle(target.transform.position);
            var tcol = target.GetComponent<Collider2D>();
            Vector3 tpos = tcol != null ? tcol.bounds.center : target.transform.position + Vector3.up * 0.5f;
            SerpentArrow.Spawn(origin, target, atk, 0.30f * atk, 4f, CurrentArrowSprite());
            MultiplayerSync.Instance?.BroadcastSerpent(origin, tpos);
        }

        // ── Archer: Chuva de Flechas ─────────────────────────────────────────
        System.Collections.IEnumerator ArrowRainAtApex(float perArrowDamage, float delay)
        {
            yield return new WaitForSeconds(delay);
            var enemies = SkeletonEnemy.AliveInRadius(_player.transform.position, 22f);
            if (enemies.Count == 0) yield break;
            for (int i = enemies.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (enemies[i], enemies[j]) = (enemies[j], enemies[i]);
            }
            // One arrow per DISTINCT enemy (up to 3) so two arrows never share a target — at range
            // their arcs overlapped into one "duplicated" arrow. A lone target (e.g. a boss) still
            // gets the full 3-arrow fan, which reads fine up close.
            int arrows = enemies.Count == 1 ? 3 : Mathf.Min(3, enemies.Count);
            for (int i = 0; i < arrows; i++)
            {
                var tgt = enemies[i % enemies.Count];   // distinct targets when ≥2 enemies
                if (tgt == null || !tgt.IsAlive) continue;
                var col = tgt.GetComponent<Collider2D>();
                Vector3 center = col != null ? col.bounds.center : tgt.transform.position + Vector3.up * 0.5f;
                // Fan the volley: spread the launch point AND aim each arrow at a distinct spot
                // around the target, so arrows that share an enemy (few enemies alive) never fly
                // the same arc and overlap into one "duplicated" animation.
                Vector3 origin    = _player.transform.position + Vector3.up * 0.4f + new Vector3((i - 1) * 0.30f, 0f, 0f);
                Vector3 aimOffset = new Vector3((i - 1) * 0.28f, (i - 1) * 0.06f, 0f);
                FallingArrow.Spawn(tgt, origin, perArrowDamage, i * 0.16f, Arrow.FlatSprite, aimOffset);
                MultiplayerSync.Instance?.BroadcastArrowFall(center);
            }
        }

        // ── Archer: Tiro Concentrado ─────────────────────────────────────────
        System.Collections.IEnumerator ConcentratedShot(SkeletonEnemy target, float damage)
        {
            if (_player == null || target == null || !target.IsAlive) yield break;

            // A white EAGLE forms and beats its wings IN FRONT of the hero (mirrors the Serpe's
            // serpent telegraph), wrapped in a soft white aura, growing as the shot charges.
            var frames = SkillEagle.Frames();
            var eagle = new GameObject("EagleTelegraph");
            var esr   = eagle.AddComponent<SpriteRenderer>();
            esr.sprite = frames[0]; esr.sortingOrder = 61;

            var aura = new GameObject("EagleAura");
            var asr  = aura.AddComponent<SpriteRenderer>();
            asr.sprite = SkillEagle.Aura(); asr.sortingOrder = 60;

            float eagleH = ArcherVisibleHeight();
            float t = 0f;
            const float charge = 0.5f;
            // Show the wind-up on the partner's screen too (their avatar plays the same eagle charge).
            MultiplayerSync.Instance?.BroadcastEagleCharge(charge, eagleH);
            while (t < charge)
            {
                if (_player == null || target == null || !target.IsAlive)
                    { if (eagle) Destroy(eagle); if (aura) Destroy(aura); yield break; }
                t += Time.deltaTime;
                float k   = t / charge;   // 0..1 grow-in
                float dir = target.transform.position.x < _player.transform.position.x ? -1f : 1f;
                Vector3 pos = _player.transform.position + new Vector3(dir * 0.45f, 0.55f, 0f);

                // Eagle: grow in, flap, face the target.
                eagle.transform.position = pos;
                Arrow.ApplyWorldSize(eagle.transform, esr.sprite, eagleH * Mathf.Lerp(0.35f, 0.95f, k));
                var s = eagle.transform.localScale; s.x = Mathf.Abs(s.x) * dir; eagle.transform.localScale = s;
                esr.sprite = frames[(int)(t / 0.09f) % frames.Length];
                var ec = Color.white; ec.a = Mathf.Lerp(0.2f, 1f, k); esr.color = ec;

                // Aura: soft white halo, a touch larger than the eagle, pulsing.
                aura.transform.position = pos;
                Arrow.ApplyWorldSize(aura.transform, asr.sprite, eagleH * Mathf.Lerp(0.6f, 1.6f, k));
                asr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.1f, 0.5f, k) * (0.8f + 0.2f * Mathf.Sin(t * 18f)));
                yield return null;
            }
            if (eagle) Destroy(eagle);
            if (aura)  Destroy(aura);
            if (target == null || !target.IsAlive) yield break;

            // Release: draw pose, white burst, then the arrow — natural art with a white outline
            // (mirrors the Serpe arrow's neon-green outline).
            float anim = _player.PlayCastAnimation(target.transform.position);
            yield return new WaitForSeconds(Mathf.Max(0.03f, anim * 0.15f));
            if (target == null || !target.IsAlive) yield break;

            Vector3 origin = Muzzle(target.transform.position);
            SkillCastFX.Spawn(origin, Color.white);

            var go = new GameObject("ConcentratedArrow");
            go.transform.position = origin;
            var arrow = go.AddComponent<Arrow>();
            arrow.speed = 10f;   // slower concentrated shot
            arrow.Init(target, damage, false, null);
            var sr = go.GetComponent<SpriteRenderer>();
            // White outline: a larger white silhouette of the same arrow, just behind it.
            var outline = new GameObject("WhiteOutline");
            outline.transform.SetParent(go.transform, false);
            var osr = outline.AddComponent<SpriteRenderer>();
            osr.sprite       = sr != null ? sr.sprite : Arrow.SharedSprite;
            osr.color        = new Color(1f, 1f, 1f, 1f);
            osr.sortingOrder = (sr != null ? sr.sortingOrder : 300) - 1;
            outline.transform.localScale = Vector3.one * 1.4f;

            MultiplayerSync.Instance?.BroadcastConcentrated(origin, target.transform.position);
        }

        // A soft white glow sprite — the Serpe telegraph's fallback when serpent_green is missing.
        GameObject SkillCastFXWhite(Vector3 pos)
        {
            var go = new GameObject("ConcentratedCharge");
            go.transform.position = pos + Vector3.up * 0.5f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SkillDart.Get();       // any small sprite; scaled up as a glow
            sr.color = new Color(1f, 1f, 1f, 0.65f);
            sr.sortingOrder = 12;
            return go;
        }

        // ── Warrior / Mage: area damage ──────────────────────────────────────
        System.Collections.IEnumerator ApplyAreaAtApex(Vector3 center, SkillDef def, float dmg, float delay)
        {
            yield return new WaitForSeconds(delay);
            var hits = SkeletonEnemy.AliveInRadius(center, AreaRadius);
            foreach (var e in hits)
            {
                if (e == null || !e.IsAlive) continue;
                e.TakeDamage(dmg, false);
                MultiplayerSync.Instance?.BroadcastDamage(e.netInstanceId, dmg, false);
            }
            // One large effect over the blast so the area read is obvious.
            SkillEffectAnim.Spawn(center, def.fxSheet, def.fxCols, def.fxRows, def.color, 3.8f);
            MultiplayerSync.Instance?.BroadcastSkillBurst(center, def.fxSheet, def.fxCols, def.fxRows, def.color, 3.8f);
        }

        // Waits until the player's raised-staff apex before landing the effect + damage,
        // so the hit reads as "the animation caused it" instead of a silent teleport-hit.
        // Also fires the shared fireball cast-flash at the staff tip for continuity with
        // the basic attack.
        IEnumerator ApplyDamageAtCastApex(SkeletonEnemy target, SkillDef def, float dmg, float delay)
        {
            float dir = Mathf.Sign(target != null
                ? target.transform.position.x - _player.transform.position.x : 1f);
            if (dir == 0f) dir = 1f;
            Vector3 flashPos = _player.transform.position + new Vector3(dir * 0.35f, 0.15f, 0f);
            Fireball.SpawnCastFlash(flashPos, dir);

            yield return new WaitForSeconds(delay);
            if (target == null || !target.IsAlive) yield break;

            target.TakeDamage(dmg, false);
            MultiplayerSync.Instance?.BroadcastDamage(target.netInstanceId, dmg, false);
            // Skills read BIG and super visible (much larger than the basic-attack projectiles).
            SkillEffectAnim.Spawn(target.transform.position, def.fxSheet, def.fxCols, def.fxRows, def.color, 3.2f);
            MultiplayerSync.Instance?.BroadcastSkillBurst(target.transform.position, def.fxSheet, def.fxCols, def.fxRows, def.color, 3.2f);
        }

        IEnumerator ApplyHealAtCastApex(SkillDef def, int level, float delay)
        {
            yield return new WaitForSeconds(delay);
            _player.Heal(def.PowerAt(level));
            SkillEffectAnim.Spawn(_player.transform.position, def.fxSheet, def.fxCols, def.fxRows, def.color, 2.4f);
            MultiplayerSync.Instance?.BroadcastSkillBurst(_player.transform.position, def.fxSheet, def.fxCols, def.fxRows, def.color, 2.4f);
        }

        // Element colour for the HUD / fallback FX (carried on the skill).
        public static Color SkillFxColor(SkillDef def) => def.color;
    }

    // Tiny expanding ring so an auto-cast reads as a spell hit, coloured by the skill's
    // element (fire red, ice blue, …).
    public class SkillCastFX : MonoBehaviour
    {
        float _t;
        SpriteRenderer _sr;
        Color _color = new Color(0.75f, 0.55f, 1f, 0.95f);
        const float Life = 0.35f;

        public static void Spawn(Vector3 worldPos, Color color)
        {
            var go = new GameObject("SkillCastFX");
            go.transform.position   = worldPos + new Vector3(0f, 0.3f, -0.4f);
            go.transform.localScale = Vector3.one * 0.4f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = RingSprite();
            sr.color        = new Color(color.r, color.g, color.b, 0.95f);
            sr.sortingOrder = 45;
            var fx = go.AddComponent<SkillCastFX>();
            fx._sr = sr;
            fx._color = color;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float p = _t / Life;
            if (p >= 1f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.5f, p);
            if (_sr != null) { var c = _color; c.a = 0.95f * (1f - p); _sr.color = c; }
        }

        static Sprite _ring;
        static Sprite RingSprite()
        {
            if (_ring != null) return _ring;
            const int N = 48;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) * 4f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            _ring = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 64f);
            return _ring;
        }
    }
}

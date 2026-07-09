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

        bool InDungeon => SceneManager.GetActiveScene().name == "Dungeon";

        // Only TICKS the cooldowns (for the HUD). Casting is driven by the attack cadence
        // via TryCastReady(), so skills fire one at a time and replace the basic attack.
        void Update()
        {
            Active.Clear();
            if (_player == null || SkillManager.Instance == null) return;

            foreach (var (def, level) in SkillManager.Instance.Equipped())
            {
                if (!_cd.TryGetValue(def.id, out var t)) { t = 0f; _cd[def.id] = 0f; }
                if (InDungeon) { t = Mathf.Max(0f, t - Time.deltaTime); _cd[def.id] = t; }
                Active.Add(new ActiveSkill { def = def, level = level, remaining = t, total = def.CooldownAt(level) });
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
                    _cd[def.id] = def.CooldownAt(level);
                    return true;                       // one skill per tick
                }
            }
            return false;
        }

        bool TryCast(SkillDef def, int level)
        {
            if (def.effect == SkillEffect.Heal)
            {
                if (_player.Health >= _player.MaxHealthValue * 0.98f) return false;
                float healAnim = _player.PlayCastAnimation(_player.transform.position);
                StartCoroutine(ApplyHealAtCastApex(def, level, Mathf.Max(0.05f, healAnim * 0.5f)));
                return true;
            }

            // Damage: strike the nearest living enemy on the battlefield.
            var e = _player.NearestEnemy(14f);
            if (e == null || !e.IsAlive) return false;

            float dmgAnim = _player.PlayCastAnimation(e.transform.position);
            float dmg = def.PowerAt(level) + _player.attackDamage * 0.5f;
            StartCoroutine(ApplyDamageAtCastApex(e, def, dmg, Mathf.Max(0.05f, dmgAnim * 0.5f)));
            return true;
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
        }

        IEnumerator ApplyHealAtCastApex(SkillDef def, int level, float delay)
        {
            yield return new WaitForSeconds(delay);
            _player.Heal(def.PowerAt(level));
            SkillEffectAnim.Spawn(_player.transform.position, def.fxSheet, def.fxCols, def.fxRows, def.color, 2.4f);
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

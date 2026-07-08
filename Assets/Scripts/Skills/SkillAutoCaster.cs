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

        void Update()
        {
            Active.Clear();
            if (_player == null || SkillManager.Instance == null) return;

            foreach (var (def, level) in SkillManager.Instance.Equipped())
            {
                if (!_cd.TryGetValue(def.id, out var t))
                {
                    // Start ready (no frozen cooldown shown outside the dungeon).
                    t = 0f;
                    _cd[def.id] = t;
                }

                // Only tick / cast in the dungeon; elsewhere the timer holds so the HUD
                // simply shows the current (ready) state.
                if (InDungeon)
                {
                    t -= Time.deltaTime;
                    if (t <= 0f)
                    {
                        bool cast = TryCast(def, level);
                        t = cast ? def.CooldownAt(level) : 0f;   // stay ready until a target/need appears
                    }
                }
                _cd[def.id] = t;

                Active.Add(new ActiveSkill
                {
                    def = def, level = level,
                    remaining = Mathf.Max(0f, t), total = def.CooldownAt(level)
                });
            }
        }

        bool TryCast(SkillDef def, int level)
        {
            if (def.effect == SkillEffect.Heal)
            {
                if (_player.Health >= _player.MaxHealthValue * 0.98f) return false;
                _player.Heal(def.PowerAt(level));
                return true;
            }

            // Damage: strike the nearest living enemy on the battlefield.
            var e = _player.NearestEnemy(14f);
            if (e == null || !e.IsAlive) return false;

            float dmg = def.PowerAt(level) + _player.attackDamage * 0.5f;
            e.TakeDamage(dmg, false);
            MultiplayerSync.Instance?.BroadcastDamage(e.netInstanceId, dmg, false);
            SkillCastFX.Spawn(e.transform.position, SkillFxColor(def));
            return true;
        }

        // Element colour for a skill's cast VFX, inferred from its name/id
        // (fogo=vermelho, gelo=azul, raio=amarelo, arcano=roxo, veneno=verde, cura=verde…).
        public static Color SkillFxColor(SkillDef def)
        {
            if (def.effect == SkillEffect.Heal) return new Color(0.40f, 1f, 0.50f);
            string s = (def.id + " " + def.name).ToLowerInvariant();
            if (Has(s, "fogo", "bola", "chama", "meteoro", "faisca", "explos"))   return new Color(1.00f, 0.42f, 0.16f); // fogo
            if (Has(s, "gelo", "nevasca", "frag", "congel"))                       return new Color(0.42f, 0.72f, 1.00f); // gelo
            if (Has(s, "raio", "choque", "eletr"))                                 return new Color(1.00f, 0.90f, 0.30f); // raio
            if (Has(s, "arcan", "aniquil", "dardo", "catac"))                      return new Color(0.72f, 0.36f, 1.00f); // arcano
            if (Has(s, "veneno", "erva"))                                          return new Color(0.55f, 0.90f, 0.30f); // veneno
            return new Color(0.92f, 0.86f, 0.70f);                                                                        // físico (aço)
        }

        static bool Has(string s, params string[] keys)
        {
            foreach (var k in keys) if (s.Contains(k)) return true;
            return false;
        }
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

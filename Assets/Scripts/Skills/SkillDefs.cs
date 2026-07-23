using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    public enum SkillEffect { Damage, Heal }

    // How a damage skill lands:
    //  Single            – hits only the nearest enemy (default).
    //  AreaMelee         – hits EVERY enemy within the swing/blast radius (warrior + mage).
    //  ArcherSerpent     – zig-zag venom arrow: normal hit + poison DoT.
    //  ArcherConcentrated– 2 s charge, then a single 200%-damage white shot.
    //  ArcherRain        – 3 arrows rain down on random enemies for 75% each.
    public enum SkillShape { Single, AreaMelee, ArcherSerpent, ArcherConcentrated, ArcherRain }

    // One skill. Each class has exactly 3 skills (one node). Learning gives a cooldown-
    // based auto-cast in the dungeon (no mana). Carries its pack icon, an element colour,
    // and which PixelEffect animation sheet to play on cast.
    public class SkillDef
    {
        public string id;
        public string name;
        public string desc;
        public ClassType cls;
        public int node;
        public SkillEffect effect;
        public float baseCooldown;
        public float basePower;
        public int maxLevel;
        public string iconPath;         // skills-pack icon
        public Color color;             // element colour (icon tint + HUD + fallback FX)
        public int fxSheet;             // PixelEffect sheet number (1..10)
        public int fxCols, fxRows;      // sheet grid
        public SkillShape shape;        // how the damage lands (see SkillShape)

        // Hard floor: no skill ever casts faster than this, even maxed. The old formula
        // (baseCooldown * (1 - 0.06*(level-1))) went linearly to ZERO and then NEGATIVE —
        // Chuva de Flechas hit -0.1 s at level 18. Damage keeps scaling with level (PowerAt),
        // so flooring the cooldown doesn't remove the reward for leveling.
        public const float CooldownFloor = 4f;
        public float CooldownAt(int level) =>
            Mathf.Max(CooldownFloor, baseCooldown * (1f - 0.06f * (level - 1)));
        public float PowerAt(int level)    => basePower * level;
    }

    // 3 skills per class. Icon tiles are best-guesses from the 16-column skills sheet
    // (index = row*16 + col); element colours + PixelEffect sheets are chosen to match.
    public static class SkillDefs
    {
        public const int SkillsPerNode = 3;

        static readonly Color Fire   = new Color(1.00f, 0.40f, 0.16f);
        static readonly Color Ice    = new Color(0.36f, 0.66f, 1.00f);
        static readonly Color Arcane = new Color(0.72f, 0.36f, 1.00f);
        static readonly Color Nature = new Color(0.48f, 0.90f, 0.40f);
        static readonly Color Steel  = new Color(0.86f, 0.86f, 0.96f);

        static SkillDef S(string id, string name, string desc, ClassType cls, SkillEffect fx,
                          float cd, float pw, int iconTile, Color color, int fxSheet, int fxCols, int fxRows,
                          SkillShape shape = SkillShape.Single)
            => new SkillDef
            {
                // maxLevel high so a skill point (1 per character level) can always be
                // invested — you can keep leveling a skill every time you level up.
                id = id, name = name, desc = desc, cls = cls, node = 0, effect = fx,
                baseCooldown = cd, basePower = pw, maxLevel = 20,
                iconPath = IconPaths.Skill(iconTile), color = color,
                fxSheet = fxSheet, fxCols = fxCols, fxRows = fxRows, shape = shape
            };

        // Warrior + Mage damage skills all hit in AREA (every enemy in the blast).
        static readonly SkillDef[] Warrior =
        {
            S("w_golpe",     "Golpe Pesado",  "Golpe forte que atinge todos ao redor.", ClassType.Warrior, SkillEffect.Damage, 6.0f, 16f, 12,  Steel,  8, 3, 2, SkillShape.AreaMelee),
            S("w_investida", "Investida",     "Avanca girando a lamina, dano em area.",  ClassType.Warrior, SkillEffect.Damage, 8.0f, 44f, 107, Steel,  9, 4, 3, SkillShape.AreaMelee),
            S("w_vigor",     "Vigor",         "Recupera vida do guerreiro.",             ClassType.Warrior, SkillEffect.Heal,   9.0f, 24f, 337, Nature, 3, 2, 2),
        };

        static readonly SkillDef[] Mage =
        {
            S("m_fogo",   "Bola de Fogo",     "Explosao de fogo que atinge todos na area.", ClassType.Mage, SkillEffect.Damage, 6.0f, 22f, 4,  Fire,   4, 3, 3, SkillShape.AreaMelee),
            S("m_gelo",   "Fragmento de Gelo","Estilhacos de gelo em area.",                ClassType.Mage, SkillEffect.Damage, 7.5f, 36f, 64, Ice,    5, 3, 2, SkillShape.AreaMelee),
            S("m_arcano", "Explosao Arcana",  "Detona energia arcana em area.",             ClassType.Mage, SkillEffect.Damage, 9.0f, 58f, 38, Arcane, 6, 3, 2, SkillShape.AreaMelee),
        };

        // Archer skills — damage is a % of the archer's ATTACK (computed in SkillAutoCaster),
        // so basePower here is just the nominal number the HUD tooltip shows.
        static readonly SkillDef[] Archer =
        {
            S("a_serpe",       "Tiro de Serpe",    "Flecha em zigue-zague: dano normal + veneno (30% do ataque/s por 4s).", ClassType.Archer, SkillEffect.Damage, 7.0f, 30f, 16, Nature, 1, 4, 3, SkillShape.ArcherSerpent),
            S("a_concentrado", "Tiro Concentrado", "Concentra por 2s e dispara: 200% do dano de ataque.",                   ClassType.Archer, SkillEffect.Damage, 8.0f, 200f, 14, Steel, 8, 3, 2, SkillShape.ArcherConcentrated),
            S("a_chuva",       "Chuva de Flechas", "3 flechas caem sobre inimigos aleatorios: 75% do ataque cada.",         ClassType.Archer, SkillEffect.Damage, 9.0f, 75f, 1,  Steel, 8, 3, 2, SkillShape.ArcherRain),
        };

        public static ClassType CurrentClass => PlayerManager.Instance != null && PlayerManager.Instance.Data != null
            ? PlayerManager.Instance.Data.classType : ClassType.Warrior;

        public static SkillDef[] All => CurrentClass switch
        {
            ClassType.Mage   => Mage,
            ClassType.Archer => Archer,
            _                => Warrior
        };

        public static int NodeCount => 1;

        public static IEnumerable<SkillDef> InNode(int node)
        {
            if (node != 0) yield break;
            foreach (var s in All) yield return s;
        }

        public static SkillDef Get(string id)
        {
            foreach (var s in Warrior) if (s.id == id) return s;
            foreach (var s in Mage)    if (s.id == id) return s;
            foreach (var s in Archer)  if (s.id == id) return s;
            return null;
        }

        public static string NodeName(int node) => CurrentClass switch
        {
            ClassType.Mage   => "Magias",
            ClassType.Archer => "Habilidades",
            _                => "Habilidades"
        };
    }
}

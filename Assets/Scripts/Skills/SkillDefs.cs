using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    public enum SkillEffect { Damage, Heal }

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

        public float CooldownAt(int level) => baseCooldown * (1f - 0.06f * (level - 1));
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
                          float cd, float pw, int iconTile, Color color, int fxSheet, int fxCols, int fxRows)
            => new SkillDef
            {
                id = id, name = name, desc = desc, cls = cls, node = 0, effect = fx,
                baseCooldown = cd, basePower = pw, maxLevel = 5,
                iconPath = IconPaths.Skill(iconTile), color = color,
                fxSheet = fxSheet, fxCols = fxCols, fxRows = fxRows
            };

        static readonly SkillDef[] Warrior =
        {
            S("w_golpe",     "Golpe Pesado",  "Golpe forte no inimigo mais proximo.", ClassType.Warrior, SkillEffect.Damage, 3.0f, 16f, 12,  Steel,  8, 3, 2),
            S("w_investida", "Investida",     "Avanca girando a lamina em area.",     ClassType.Warrior, SkillEffect.Damage, 6.0f, 44f, 107, Steel,  9, 4, 3),
            S("w_vigor",     "Vigor",         "Recupera vida do guerreiro.",          ClassType.Warrior, SkillEffect.Heal,   7.0f, 24f, 337, Nature, 3, 2, 2),
        };

        static readonly SkillDef[] Mage =
        {
            S("m_fogo",   "Bola de Fogo",     "Arremessa uma bola de fogo.",          ClassType.Mage, SkillEffect.Damage, 3.5f, 22f, 4,  Fire,   4, 3, 3),
            S("m_gelo",   "Fragmento de Gelo","Estilhaco de gelo cortante.",          ClassType.Mage, SkillEffect.Damage, 5.0f, 36f, 64, Ice,    5, 3, 2),
            S("m_arcano", "Explosao Arcana",  "Detona energia arcana sobre o alvo.",  ClassType.Mage, SkillEffect.Damage, 7.0f, 58f, 38, Arcane, 6, 3, 2),
        };

        static readonly SkillDef[] Archer =
        {
            S("a_certeiro","Tiro Certeiro",   "Flecha certeira de alto dano.",        ClassType.Archer, SkillEffect.Damage, 3.0f, 18f, 14, Steel,  8, 3, 2),
            S("a_veneno",  "Flecha Venenosa", "Flecha que envenena o alvo.",          ClassType.Archer, SkillEffect.Damage, 5.0f, 32f, 16, Nature, 1, 4, 3),
            S("a_chuva",   "Chuva de Flechas","Torrente de flechas sobre o alvo.",    ClassType.Archer, SkillEffect.Damage, 7.0f, 52f, 1,  Steel,  8, 3, 2),
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

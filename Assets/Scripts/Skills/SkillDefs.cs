using System.Collections.Generic;

namespace ZulfarakRPG
{
    public enum SkillEffect { Damage, Heal }

    // One skill leaf. Skills are grouped into vertical NODES (4 per node) and belong to a
    // class. Only the current player's class tree is shown/learnable. Learning gives the
    // skill a cooldown-based auto-cast in the dungeon (no mana — Taskbar-Hero style).
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
        public string iconPath;      // pack icon PNG

        public float CooldownAt(int level) => baseCooldown * (1f - 0.06f * (level - 1));
        public float PowerAt(int level)    => basePower * level;
    }

    // Class-specific skill catalogues (4 nodes × 4 skills each). Icons come from the
    // Skills & Spells pack (tileNNN); tile indices are best-guess ranges per class —
    // adjust here to pick different art. Ids are class-prefixed so they stay globally
    // unique in the saved data.
    public static class SkillDefs
    {
        public const int SkillsPerNode = 4;

        static SkillDef S(string id, string name, string desc, ClassType cls, int node,
                          SkillEffect fx, float cd, float pw, int tile)
            => new SkillDef { id = id, name = name, desc = desc, cls = cls, node = node,
                              effect = fx, baseCooldown = cd, basePower = pw, maxLevel = 5,
                              iconPath = IconPaths.Skill(tile) };

        static readonly SkillDef[] Warrior =
        {
            S("w_corte",   "Corte Rapido",  "Golpe veloz no inimigo mais proximo.",   ClassType.Warrior, 0, SkillEffect.Damage, 2.5f, 10f, 0),
            S("w_vigor",   "Vigor",         "Recupera um pouco de vida.",             ClassType.Warrior, 0, SkillEffect.Heal,   5.0f, 12f, 1),
            S("w_duplo",   "Golpe Duplo",   "Dois cortes rapidos em sequencia.",      ClassType.Warrior, 0, SkillEffect.Damage, 3.5f, 16f, 2),
            S("w_pancada", "Pancada",       "Golpe pesado de curto alcance.",         ClassType.Warrior, 0, SkillEffect.Damage, 4.0f, 22f, 3),

            S("w_investida","Investida",    "Avanca causando dano ao alvo.",          ClassType.Warrior, 1, SkillEffect.Damage, 5.0f, 30f, 4),
            S("w_sede",    "Sede de Sangue","Cura ao ferir os inimigos.",             ClassType.Warrior, 1, SkillEffect.Heal,   6.0f, 18f, 5),
            S("w_furia",   "Golpe de Furia","Ataque furioso de alto dano.",           ClassType.Warrior, 1, SkillEffect.Damage, 6.0f, 40f, 6),
            S("w_rasgo",   "Rasgo",         "Corte sangrento continuo.",              ClassType.Warrior, 1, SkillEffect.Damage, 4.5f, 26f, 7),

            S("w_provocar","Provocar",      "Golpe que atordoa e fere.",              ClassType.Warrior, 2, SkillEffect.Damage, 7.0f, 45f, 8),
            S("w_forte",   "Fortitude",     "Cura potente do guerreiro.",             ClassType.Warrior, 2, SkillEffect.Heal,   8.0f, 30f, 9),
            S("w_esmagar", "Esmagar",       "Marreta esmagadora.",                    ClassType.Warrior, 2, SkillEffect.Damage, 6.5f, 50f, 10),
            S("w_terremoto","Terremoto",    "Impacto que abala o chao.",              ClassType.Warrior, 2, SkillEffect.Damage, 9.0f, 70f, 11),

            S("w_decap",   "Decapitar",     "Golpe final que dizima o alvo.",         ClassType.Warrior, 3, SkillEffect.Damage, 11f, 110f, 12),
            S("w_recup",   "Recuperar",     "Restaura muita vida de uma vez.",        ClassType.Warrior, 3, SkillEffect.Heal,   10f,  55f, 13),
            S("w_massacre","Massacre",      "Torrente de golpes brutais.",            ClassType.Warrior, 3, SkillEffect.Damage,  8f,  85f, 14),
            S("w_catac",   "Cataclismo",    "Cataclismo que destroi tudo.",           ClassType.Warrior, 3, SkillEffect.Damage, 13f, 150f, 15),
        };

        static readonly SkillDef[] Mage =
        {
            S("m_faisca",  "Faisca",        "Faisca arcana no alvo mais proximo.",    ClassType.Mage, 0, SkillEffect.Damage, 2.5f, 12f, 16),
            S("m_vigor",   "Vigor Arcano",  "Recupera um pouco de vida.",             ClassType.Mage, 0, SkillEffect.Heal,   5.0f, 12f, 17),
            S("m_dardo",   "Dardo Magico",  "Dardo de energia perfurante.",           ClassType.Mage, 0, SkillEffect.Damage, 3.5f, 18f, 18),
            S("m_choque",  "Choque",        "Descarga de curto alcance.",             ClassType.Mage, 0, SkillEffect.Damage, 4.0f, 22f, 19),

            S("m_bola",    "Bola de Fogo",  "Arremessa uma bola de fogo.",            ClassType.Mage, 1, SkillEffect.Damage, 5.0f, 32f, 20),
            S("m_chamas",  "Chamas",        "Labareda continua sobre o alvo.",        ClassType.Mage, 1, SkillEffect.Damage, 4.5f, 26f, 21),
            S("m_explosao","Explosao",      "Detona energia sobre o alvo.",           ClassType.Mage, 1, SkillEffect.Damage, 6.0f, 42f, 22),
            S("m_meteoro", "Meteoro",       "Impacto flamejante devastador.",         ClassType.Mage, 1, SkillEffect.Damage, 9.0f, 75f, 23),

            S("m_frag",    "Fragmento",     "Estilhaco de gelo cortante.",            ClassType.Mage, 2, SkillEffect.Damage, 5.5f, 45f, 24),
            S("m_nevasca", "Nevasca",       "Tempestade de gelo sobre o alvo.",       ClassType.Mage, 2, SkillEffect.Damage, 7.0f, 55f, 25),
            S("m_bencao",  "Bencao Gelida", "Cura potente do mago.",                  ClassType.Mage, 2, SkillEffect.Heal,   8.0f, 30f, 26),
            S("m_congelar","Congelar",      "Congela e fere o inimigo.",              ClassType.Mage, 2, SkillEffect.Damage, 6.5f, 48f, 27),

            S("m_raio",    "Raio Arcano",   "Torrente de dano arcano.",               ClassType.Mage, 3, SkillEffect.Damage,  8f,  85f, 28),
            S("m_restaur", "Restaurar",     "Restaura muita vida de uma vez.",        ClassType.Mage, 3, SkillEffect.Heal,   10f,  55f, 29),
            S("m_aniquil", "Aniquilacao",   "Aniquila o alvo com poder arcano.",      ClassType.Mage, 3, SkillEffect.Damage, 11f, 110f, 30),
            S("m_catac",   "Cataclismo",    "Cataclismo arcano que destroi tudo.",    ClassType.Mage, 3, SkillEffect.Damage, 13f, 150f, 31),
        };

        static readonly SkillDef[] Archer =
        {
            S("a_rapido",  "Tiro Rapido",   "Flecha veloz no alvo mais proximo.",     ClassType.Archer, 0, SkillEffect.Damage, 2.5f, 12f, 32),
            S("a_folego",  "Folego",        "Recupera um pouco de vida.",             ClassType.Archer, 0, SkillEffect.Heal,   5.0f, 12f, 33),
            S("a_duplo",   "Tiro Duplo",    "Dispara duas flechas.",                  ClassType.Archer, 0, SkillEffect.Damage, 3.5f, 18f, 34),
            S("a_pesado",  "Tiro Pesado",   "Flecha pesada de curto alcance.",        ClassType.Archer, 0, SkillEffect.Damage, 4.0f, 22f, 35),

            S("a_certeiro","Tiro Certeiro", "Flecha certeira de alto dano.",          ClassType.Archer, 1, SkillEffect.Damage, 5.0f, 34f, 36),
            S("a_segundo", "Segundo Folego","Cura ao acertar os inimigos.",           ClassType.Archer, 1, SkillEffect.Heal,   6.0f, 18f, 37),
            S("a_perfura", "Flecha Perfurante","Perfura a armadura do alvo.",         ClassType.Archer, 1, SkillEffect.Damage, 6.0f, 42f, 38),
            S("a_saraiva", "Saraivada",     "Rajada de flechas rapidas.",             ClassType.Archer, 1, SkillEffect.Damage, 4.5f, 26f, 39),

            S("a_armadilha","Armadilha",    "Arma uma armadilha explosiva.",          ClassType.Archer, 2, SkillEffect.Damage, 7.0f, 45f, 40),
            S("a_veneno",  "Flecha Venenosa","Flecha que envenena o alvo.",           ClassType.Archer, 2, SkillEffect.Damage, 6.5f, 48f, 41),
            S("a_ervas",   "Ervas Curativas","Cura potente do arqueiro.",             ClassType.Archer, 2, SkillEffect.Heal,   8.0f, 30f, 42),
            S("a_bomba",   "Bomba",         "Lanca uma bomba explosiva.",             ClassType.Archer, 2, SkillEffect.Damage, 9.0f, 70f, 43),

            S("a_chuva",   "Chuva de Flechas","Torrente de flechas do ceu.",          ClassType.Archer, 3, SkillEffect.Damage,  8f,  85f, 44),
            S("a_recup",   "Recuperar",     "Restaura muita vida de uma vez.",        ClassType.Archer, 3, SkillEffect.Heal,   10f,  55f, 45),
            S("a_mortal",  "Tiro Mortal",   "Flecha final que dizima o alvo.",        ClassType.Archer, 3, SkillEffect.Damage, 11f, 110f, 46),
            S("a_furacao", "Furacao",       "Furacao de flechas que destroi tudo.",   ClassType.Archer, 3, SkillEffect.Damage, 13f, 150f, 47),
        };

        // ── Active (current player class) queries ─────────────────────────
        public static ClassType CurrentClass => PlayerManager.Instance != null && PlayerManager.Instance.Data != null
            ? PlayerManager.Instance.Data.classType : ClassType.Warrior;

        public static SkillDef[] All => CurrentClass switch
        {
            ClassType.Mage   => Mage,
            ClassType.Archer => Archer,
            _                => Warrior
        };

        public static int NodeCount
        {
            get { int max = 0; foreach (var s in All) if (s.node > max) max = s.node; return max + 1; }
        }

        public static IEnumerable<SkillDef> InNode(int node)
        {
            foreach (var s in All) if (s.node == node) yield return s;
        }

        // Searches EVERY class (equipped ids may outlive a class view) — ids are unique.
        public static SkillDef Get(string id)
        {
            foreach (var s in Warrior) if (s.id == id) return s;
            foreach (var s in Mage)    if (s.id == id) return s;
            foreach (var s in Archer)  if (s.id == id) return s;
            return null;
        }

        public static string NodeName(int node) => CurrentClass switch
        {
            ClassType.Mage   => node switch { 0 => "Fundamentos", 1 => "Fogo",      2 => "Gelo",        _ => "Arcano" },
            ClassType.Archer => node switch { 0 => "Fundamentos", 1 => "Precisao",  2 => "Armadilhas",  _ => "Tempestade" },
            _                => node switch { 0 => "Fundamentos", 1 => "Furia",     2 => "Bastiao",     _ => "Carnificina" },
        };
    }
}

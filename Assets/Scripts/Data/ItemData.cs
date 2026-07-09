using UnityEngine;

namespace ZulfarakRPG
{
    // Cape is appended at the END so existing serialized ordinals (Weapon..Consumable)
    // are preserved for saves that store the enum by integer.
    public enum ItemType { Weapon, Helmet, Chest, Legs, Boots, Gloves, Ring, Amulet, Consumable, Cape }

    // The game exposes four qualities to the player: Comum, Raro, Mito, Lendário.
    // They map onto Common / Rare / Epic / Legendary respectively.
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    [CreateAssetMenu(fileName = "ItemData", menuName = "ZulfarakRPG/Item Data")]
    public class ItemData : ScriptableObject
    {
        public string itemId;
        public string itemName;
        [TextArea] public string description;
        public ItemType itemType;
        public ItemRarity rarity;
        public Sprite icon;
        // Absolute path to the icon PNG in a downloaded pack (used by the native windows
        // and the weapon-in-hand). Empty → fall back to a generated/placeholder look.
        public string iconPath;
        public int requiredLevel = 1;
        public int goldValue = 10;

        // Stat bonuses
        public int bonusHp;
        public int bonusAttack;
        public int bonusDefense;
        public float bonusSpeed;
        public float bonusHealPower;

        // ── Extended combat stats ────────────────────────────────────────────
        // Flat stats.
        public int flatDamage;        // weapons: added directly to the character's attack
        public int armor;             // defense pieces: raises Defesa AND feeds Resist. Física
        public float bonusMoveSpeed;  // boots: world movement speed

        // Percent stats (stored as fractions, e.g. 0.15f = +15%). Summed across equipped
        // gear by Inventory.RecalculateStats and applied to the live character.
        public float pctDamage;             // + % damage (multiplies attack)
        public float pctCritChance;         // + % critical chance
        public float pctCritDamage;         // + % critical damage
        public float pctAttackSpeed;        // + % attack speed
        public float pctLifeRegen;          // + % of max HP regenerated per second
        public float pctPhysicalResist;     // + % physical resistance
        public float pctMagicResist;        // + % magic resistance
        public float pctCooldownReduction;  // + % skill cooldown reduction

        // Class restrictions (empty = all classes can use)
        public ClassType[] allowedClasses;

        public bool CanBeUsedBy(ClassType classType)
        {
            if (allowedClasses == null || allowedClasses.Length == 0) return true;
            foreach (var c in allowedClasses)
                if (c == classType) return true;
            return false;
        }

        public Color RarityColor => QualityColor(rarity);

        // Player-facing quality colours (Diablo-like): Comum = marrom, Raro = azul,
        // Mito = roxo, Lendário = dourado. Used by the inventory UI AND the on-character
        // equipment tint so the two always match.
        public static Color QualityColor(ItemRarity r) => r switch
        {
            ItemRarity.Common    => new Color(0.58f, 0.38f, 0.20f),  // marrom terroso
            ItemRarity.Uncommon  => new Color(0.30f, 0.85f, 0.30f),  // (verde — não exposto)
            ItemRarity.Rare      => new Color(0.26f, 0.55f, 1.00f),  // azul vivo
            ItemRarity.Epic      => new Color(0.68f, 0.24f, 0.98f),  // roxo (mito)
            ItemRarity.Legendary => new Color(1.00f, 0.80f, 0.16f),  // dourado
            _ => Color.white
        };

        // Human-readable list of this item's non-zero stats (label, value), in a fixed
        // priority order — consumed by the inventory hover tooltip (TaskbarHero style).
        public System.Collections.Generic.List<(string label, string value)> StatLines()
        {
            var lines = new System.Collections.Generic.List<(string, string)>(10);
            if (flatDamage > 0)          lines.Add(("Dano",              "+" + flatDamage));
            if (armor > 0)               lines.Add(("Armadura",          "+" + armor));
            if (bonusHp > 0)             lines.Add(("Vida",              "+" + bonusHp));
            if (bonusDefense > 0)        lines.Add(("Defesa",            "+" + bonusDefense));
            if (bonusMoveSpeed > 0f)     lines.Add(("Vel. Movimento",    "+" + bonusMoveSpeed.ToString("0.00")));
            if (pctDamage > 0f)          lines.Add(("Dano",              Pct(pctDamage)));
            if (pctCritChance > 0f)      lines.Add(("Chance de Critico", Pct(pctCritChance)));
            if (pctCritDamage > 0f)      lines.Add(("Dano Critico",      Pct(pctCritDamage)));
            if (pctAttackSpeed > 0f)     lines.Add(("Vel. de Ataque",    Pct(pctAttackSpeed)));
            if (pctLifeRegen > 0f)       lines.Add(("Regen. de Vida",    Pct(pctLifeRegen) + "/s"));
            if (pctPhysicalResist > 0f)  lines.Add(("Resist. Fisica",    Pct(pctPhysicalResist)));
            if (pctMagicResist > 0f)     lines.Add(("Resist. Magica",    Pct(pctMagicResist)));
            if (pctCooldownReduction > 0f) lines.Add(("Red. de Recarga",  Pct(pctCooldownReduction)));
            return lines;
        }

        static string Pct(float fraction) => "+" + Mathf.RoundToInt(fraction * 100f) + "%";

        // Short Portuguese quality label for the inventory tags.
        public static string QualityLabel(ItemRarity r) => r switch
        {
            ItemRarity.Common    => "Comum",
            ItemRarity.Uncommon  => "Incomum",
            ItemRarity.Rare      => "Raro",
            ItemRarity.Epic      => "Mito",
            ItemRarity.Legendary => "Lendario",
            _ => ""
        };
    }
}

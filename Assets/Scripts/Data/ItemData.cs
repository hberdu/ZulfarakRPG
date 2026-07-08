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

using UnityEngine;

namespace ZulfarakRPG
{
    public enum ItemType { Weapon, Helmet, Chest, Legs, Boots, Gloves, Ring, Amulet, Consumable }
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

        public Color RarityColor => rarity switch
        {
            ItemRarity.Common    => new Color(0.80f, 0.80f, 0.80f),
            ItemRarity.Uncommon  => new Color(0.30f, 0.85f, 0.30f),
            ItemRarity.Rare      => new Color(0.25f, 0.50f, 1.00f),
            ItemRarity.Epic      => new Color(0.65f, 0.20f, 0.90f),
            ItemRarity.Legendary => new Color(1.00f, 0.65f, 0.10f),
            _ => Color.white
        };
    }
}

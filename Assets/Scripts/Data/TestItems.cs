using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Built-in, code-defined equipment used to test the inventory + on-character visuals
    // without needing the server catalog: one item of every quality (Comum / Raro / Mito /
    // Lendário) for each equippable slot (Arma, Capacete, Peito, Mãos, Pés, Capa).
    //
    // Ids are prefixed "tst_" so the rest of the game can recognise them as local test
    // items (they equip through the offline path, bypassing the server).
    public static class TestItems
    {
        public const string Prefix = "tst_";
        public static bool IsTestItem(string id) => !string.IsNullOrEmpty(id) && id.StartsWith(Prefix);

        // Slots that get a full quality set, with their Portuguese labels.
        static readonly (ItemType type, string label)[] Slots =
        {
            (ItemType.Weapon, "Arma"),
            (ItemType.Helmet, "Capacete"),
            (ItemType.Chest,  "Peito"),
            (ItemType.Gloves, "Maos"),
            (ItemType.Boots,  "Pes"),
            (ItemType.Cape,   "Capa"),
        };

        static readonly (ItemRarity rarity, int power)[] Qualities =
        {
            (ItemRarity.Common,    1),
            (ItemRarity.Rare,      2),
            (ItemRarity.Epic,      3),
            (ItemRarity.Legendary, 5),
        };

        static Dictionary<string, ItemData> _byId;

        static void EnsureBuilt()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, ItemData>();
            foreach (var (type, label) in Slots)
                foreach (var (rarity, power) in Qualities)
                {
                    var item = ScriptableObject.CreateInstance<ItemData>();
                    item.itemId      = Id(type, rarity);
                    item.itemName    = $"{label} {ItemData.QualityLabel(rarity)}";
                    item.description = $"Item de teste — {label} de qualidade {ItemData.QualityLabel(rarity)}.";
                    item.itemType    = type;
                    item.rarity      = rarity;
                    item.iconPath    = IconFor(type, rarity);
                    item.requiredLevel = 1;
                    item.goldValue   = 10 * power;
                    // Simple quality-scaled bonuses so equipping visibly changes stats.
                    item.bonusHp      = 5  * power;
                    item.bonusAttack  = 2  * power;
                    item.bonusDefense = 2  * power;
                    _byId[item.itemId] = item;
                }

            // Extra weapon variety: staves (cajados), one per quality.
            foreach (var (rarity, power) in Qualities)
            {
                var item = ScriptableObject.CreateInstance<ItemData>();
                item.itemId      = $"{Prefix}staff_{rarity}".ToLowerInvariant();
                item.itemName    = $"Cajado {ItemData.QualityLabel(rarity)}";
                item.description = $"Item de teste — Cajado de qualidade {ItemData.QualityLabel(rarity)}.";
                item.itemType    = ItemType.Weapon;
                item.rarity      = rarity;
                item.iconPath    = IconPaths.Weapon(40 + QualityIndex(rarity));   // staff-ish weapon icons
                item.requiredLevel = 1;
                item.goldValue   = 10 * power;
                item.bonusHp      = 5  * power;
                item.bonusAttack  = 3  * power;
                item.bonusDefense = 1  * power;
                _byId[item.itemId] = item;
            }
        }

        public static string Id(ItemType type, ItemRarity rarity)
            => $"{Prefix}{type}_{rarity}".ToLowerInvariant();

        static int QualityIndex(ItemRarity r) => r switch
        {
            ItemRarity.Common    => 0,
            ItemRarity.Rare      => 1,
            ItemRarity.Epic      => 2,
            ItemRarity.Legendary => 3,
            _ => 0
        };

        // Best-guess icon mapping per slot × quality. Weapons come from the Epic Weapons
        // pack (numbered), the rest from the Accessories/Armor pack (tileNNN). Adjust the
        // base indices here to pick different-looking icons.
        static string IconFor(ItemType type, ItemRarity rarity)
        {
            int qi = QualityIndex(rarity);   // 0..3
            switch (type)
            {
                case ItemType.Weapon: return IconPaths.Weapon(1 + qi);   // weapons 1..4
                case ItemType.Helmet: return IconPaths.Armor(0  + qi);
                case ItemType.Chest:  return IconPaths.Armor(8  + qi);
                case ItemType.Gloves: return IconPaths.Armor(16 + qi);
                case ItemType.Boots:  return IconPaths.Armor(24 + qi);
                case ItemType.Cape:   return IconPaths.Armor(32 + qi);
                default: return null;
            }
        }

        // Resolves a test item by id (or null). ItemDatabase.Get falls back to this.
        public static ItemData Get(string id)
        {
            if (!IsTestItem(id)) return null;
            EnsureBuilt();
            return _byId.TryGetValue(id, out var it) ? it : null;
        }

        public static IEnumerable<ItemData> All()
        {
            EnsureBuilt();
            return _byId.Values;
        }

        // Drops one of every test item into the bag (idempotent — skips ones already held).
        public static void AddAllToBag(Inventory inv)
        {
            if (inv == null) return;
            EnsureBuilt();
            foreach (var it in _byId.Values)
                if (!inv.HasItem(it.itemId))
                    inv.AddItem(it.itemId);
        }
    }
}

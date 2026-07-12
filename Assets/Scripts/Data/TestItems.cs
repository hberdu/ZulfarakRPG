using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Built-in, code-defined equipment used to test the inventory + on-character visuals
    // without needing the server catalog: one item of every quality (Comum / Raro / Mito /
    // Lendário) for each equippable slot (Arma, Capacete, Peito, Mãos, Pés, Capa) plus a
    // set of staves.
    //
    // Each item has a UNIQUE name and a hand-tuned set of attributes that scale with its
    // quality: weapons carry flat Dano, defense pieces carry Armadura (which feeds the
    // character's physical resistance), boots add Defesa + movement speed, and every piece
    // rolls a mix of the percent combat stats surfaced in the hover tooltip.
    //
    // Ids are prefixed "tst_" so the rest of the game can recognise them as local test
    // items (they equip through the offline path, bypassing the server).
    public static class TestItems
    {
        public const string Prefix = "tst_";
        public static bool IsTestItem(string id) => !string.IsNullOrEmpty(id) && id.StartsWith(Prefix);

        // Slots that get a full quality set.
        static readonly ItemType[] Slots =
        {
            ItemType.Weapon,
            ItemType.Helmet,
            ItemType.Chest,
            ItemType.Gloves,
            ItemType.Boots,
            ItemType.Cape,
        };

        static readonly ItemRarity[] Qualities =
        {
            ItemRarity.Common,
            ItemRarity.Rare,
            ItemRarity.Epic,
            ItemRarity.Legendary,
        };

        // Distinct, quality-ordered names per slot (index 0..3 = Comum/Raro/Mito/Lendário).
        static readonly Dictionary<ItemType, string[]> Names = new()
        {
            [ItemType.Weapon] = new[] { "Adaga Enferrujada", "Espada do Guardiao", "Lamina das Sombras", "Devoradora de Dragoes" },
            [ItemType.Helmet] = new[] { "Elmo de Couro",     "Elmo de Aco",        "Coroa Runica",        "Coroa do Rei Dragao" },
            [ItemType.Chest]  = new[] { "Tunica Puida",      "Peitoral de Aco",    "Armadura Runica",     "Egide do Dragao Negro" },
            [ItemType.Gloves] = new[] { "Manoplas Gastas",   "Manoplas de Placas", "Garras Runicas",      "Punhos do Cataclismo" },
            [ItemType.Boots]  = new[] { "Botas de Couro",    "Grevas de Aco",      "Passos do Vento",     "Botas do Andarilho Astral" },
            [ItemType.Cape]   = new[] { "Manto Esfarrapado", "Capa do Viajante",   "Manto Runico",        "Asa do Dragao Anciao" },
        };
        static readonly string[] StaffNames = { "Galho Rachado", "Cajado do Aprendiz", "Cajado Runico", "Cajado do Arquimago" };

        static Dictionary<string, ItemData> _byId;

        static void EnsureBuilt()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, ItemData>();
            foreach (var type in Slots)
                for (int qi = 0; qi < Qualities.Length; qi++)
                {
                    var rarity = Qualities[qi];
                    var item = ScriptableObject.CreateInstance<ItemData>();
                    item.itemId      = Id(type, rarity);
                    item.itemName    = Names[type][qi];
                    item.description = $"{Names[type][qi]} — qualidade {ItemData.QualityLabel(rarity)}.";
                    item.itemType    = type;
                    item.rarity      = rarity;
                    item.iconPath    = IconFor(type, rarity);
                    item.requiredLevel = 1;
                    item.goldValue   = 25 * (qi + 1) * (qi + 1);
                    ApplyStats(item, type, qi);
                    _byId[item.itemId] = item;
                }

            // Extra weapon variety: magic staves, one per quality.
            for (int qi = 0; qi < Qualities.Length; qi++)
            {
                var rarity = Qualities[qi];
                var item = ScriptableObject.CreateInstance<ItemData>();
                item.itemId      = $"{Prefix}staff_{rarity}".ToLowerInvariant();
                item.itemName    = StaffNames[qi];
                item.description = $"{StaffNames[qi]} — qualidade {ItemData.QualityLabel(rarity)}.";
                item.itemType    = ItemType.Weapon;
                item.rarity      = rarity;
                item.iconPath    = IconPaths.Weapon(40 + qi);   // staff-ish weapon icons
                item.requiredLevel = 1;
                item.goldValue   = 25 * (qi + 1) * (qi + 1);
                ApplyStaffStats(item, qi);
                _byId[item.itemId] = item;
            }

            // Real server catalog LEGENDARIES (no tst_ prefix → they persist to the server and
            // match the boss drop tables). Base numeric stats mirror ItemSeeder; the rarity
            // scaling in Inventory then multiplies them ~7× and layers on big crit/attack-speed
            // (weapon) or resist (armour) bonuses.
            AddCatalogItem("dragon_blade",    "Lâmina do Dragão", ItemType.Weapon, ItemRarity.Legendary, 60, 0,   0,  IconPaths.Weapon(4));
            AddCatalogItem("aegis_of_titans", "Égide dos Titãs",  ItemType.Chest,  ItemRarity.Legendary, 0,  220, 60, IconPaths.Armor(48 + 3));
        }

        static void AddCatalogItem(string id, string name, ItemType type, ItemRarity rarity,
                                   int atk, int hp, int def, string icon)
        {
            var it = ScriptableObject.CreateInstance<ItemData>();
            it.itemId        = id;
            it.itemName      = name;
            it.description   = $"{name} — qualidade {ItemData.QualityLabel(rarity)}.";
            it.itemType      = type;
            it.rarity        = rarity;
            it.iconPath      = icon;
            it.requiredLevel = 10;
            it.goldValue     = 2500;
            it.bonusAttack   = atk;
            it.bonusHp       = hp;
            it.bonusDefense  = def;
            _byId[id] = it;
        }

        // Per-slot attribute profiles, indexed by quality tier qi (0..3). Everything grows
        // with quality; each slot leans into a fantasy of its own so the tooltips differ.
        static void ApplyStats(ItemData item, ItemType type, int qi)
        {
            switch (type)
            {
                case ItemType.Weapon:   // sword: flat Dano + crit
                    item.flatDamage    = Pick(qi, 6, 12, 20, 32);
                    item.pctDamage     = Pick(qi, 0.03f, 0.06f, 0.10f, 0.16f);
                    item.pctCritChance = Pick(qi, 0.02f, 0.04f, 0.06f, 0.10f);
                    item.pctCritDamage = Pick(qi, 0.05f, 0.10f, 0.18f, 0.30f);
                    if (qi >= 3) item.pctAttackSpeed = 0.06f;
                    break;

                case ItemType.Helmet:   // armor + vitality, a touch of magic resist
                    item.armor             = Pick(qi, 5, 10, 16, 26);
                    item.bonusHp           = Pick(qi, 10, 20, 35, 60);
                    item.pctPhysicalResist = Pick(qi, 0.02f, 0.04f, 0.06f, 0.10f);
                    item.pctMagicResist    = Pick(qi, 0.00f, 0.01f, 0.03f, 0.05f);
                    break;

                case ItemType.Chest:    // the tank piece: most armor + HP
                    item.armor             = Pick(qi, 10, 18, 28, 44);
                    item.bonusHp           = Pick(qi, 20, 40, 70, 120);
                    item.pctPhysicalResist = Pick(qi, 0.03f, 0.06f, 0.10f, 0.16f);
                    break;

                case ItemType.Gloves:   // dexterity: attack speed + crit chance
                    item.armor             = Pick(qi, 3, 6, 10, 16);
                    item.pctAttackSpeed    = Pick(qi, 0.03f, 0.06f, 0.10f, 0.15f);
                    item.pctCritChance     = Pick(qi, 0.02f, 0.03f, 0.05f, 0.08f);
                    break;

                case ItemType.Boots:    // Defesa + movement speed (as requested)
                    item.armor             = Pick(qi, 3, 6, 10, 16);
                    item.bonusDefense      = Pick(qi, 3, 6, 10, 16);
                    item.bonusMoveSpeed    = Pick(qi, 0.15f, 0.25f, 0.40f, 0.60f);
                    item.pctPhysicalResist = Pick(qi, 0.01f, 0.02f, 0.03f, 0.05f);
                    break;

                case ItemType.Cape:     // arcane: magic resist + life regen + late CDR
                    item.armor              = Pick(qi, 2, 4, 7, 12);
                    item.pctMagicResist     = Pick(qi, 0.03f, 0.06f, 0.10f, 0.16f);
                    item.pctLifeRegen       = Pick(qi, 0.005f, 0.010f, 0.020f, 0.035f);
                    if (qi >= 3) item.pctCooldownReduction = 0.08f;
                    break;
            }
        }

        // Staves are the caster weapon: flat Dano, % Dano, cooldown reduction and crit damage.
        static void ApplyStaffStats(ItemData item, int qi)
        {
            item.flatDamage           = Pick(qi, 5, 10, 17, 28);
            item.pctDamage            = Pick(qi, 0.04f, 0.08f, 0.13f, 0.20f);
            item.pctCooldownReduction = Pick(qi, 0.02f, 0.04f, 0.07f, 0.12f);
            item.pctCritDamage        = Pick(qi, 0.04f, 0.08f, 0.14f, 0.22f);
        }

        static int Pick(int qi, int a, int b, int c, int d) => qi switch { 1 => b, 2 => c, 3 => d, _ => a };
        static float Pick(int qi, float a, float b, float c, float d) => qi switch { 1 => b, 2 => c, 3 => d, _ => a };

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
            // Armor sheet is 16 columns (index = row*16 + col): row0 helmets, row2 boots,
            // row3 chest, row1 hoods/capes. The armor pack has NO gloves, so gauntlets come
            // from the "400+ Accessories" pack (glove tiles picked to match each quality's
            // colour: marrom / azul / roxo / dourado).
            switch (type)
            {
                case ItemType.Weapon: return IconPaths.Weapon(1 + qi);   // weapons 1..4
                case ItemType.Helmet: return IconPaths.Armor(0  + qi);   // helmets
                case ItemType.Chest:  return IconPaths.Armor(48 + qi);   // chest
                case ItemType.Gloves: return IconPaths.Accessory(GloveTiles[qi]);   // gauntlets
                case ItemType.Boots:  return IconPaths.Armor(32 + qi);   // boots
                case ItemType.Cape:   return IconPaths.Armor(24 + qi);   // hoods/cloaks
                default: return null;
            }
        }

        // Glove tiles from the Accessories pack, one per quality (Comum/Raro/Mito/Lendario):
        // brown, blue, purple, gold — matching the quality colours.
        static readonly int[] GloveTiles = { 296, 299, 293, 301 };

        // Resolves a code-defined item by id (test items OR the real server-catalog legendaries).
        // ItemDatabase.Get falls back to this when the ScriptableObject catalog lacks the id.
        public static ItemData Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
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

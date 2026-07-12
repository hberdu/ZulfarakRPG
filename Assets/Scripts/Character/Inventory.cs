using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;

namespace ZulfarakRPG
{
    [Serializable]
    public class InventoryItem
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public class Equipment
    {
        public string weaponId;
        public string helmetId;
        public string chestId;
        public string legsId;
        public string bootsId;
        public string glovesId;
        public string ringId;
        public string amuletId;
        public string capeId;

        public string GetSlot(ItemType type) => type switch
        {
            ItemType.Weapon  => weaponId,
            ItemType.Helmet  => helmetId,
            ItemType.Chest   => chestId,
            ItemType.Legs    => legsId,
            ItemType.Boots   => bootsId,
            ItemType.Gloves  => glovesId,
            ItemType.Ring    => ringId,
            ItemType.Amulet  => amuletId,
            ItemType.Cape    => capeId,
            _ => null
        };

        public void SetSlot(ItemType type, string id)
        {
            switch (type)
            {
                case ItemType.Weapon:  weaponId  = id; break;
                case ItemType.Helmet:  helmetId  = id; break;
                case ItemType.Chest:   chestId   = id; break;
                case ItemType.Legs:    legsId    = id; break;
                case ItemType.Boots:   bootsId   = id; break;
                case ItemType.Gloves:  glovesId  = id; break;
                case ItemType.Ring:    ringId    = id; break;
                case ItemType.Amulet:  amuletId  = id; break;
                case ItemType.Cape:    capeId    = id; break;
            }
        }
    }

    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }

        public List<InventoryItem> Items  { get; private set; } = new();
        public Equipment Equipment        { get; private set; } = new();

        public event Action OnInventoryChanged;

        private string SavePath => Path.Combine(Application.persistentDataPath, "inventory.json");

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void AddItem(string itemId, int qty = 1)
        {
            var existing = Items.FirstOrDefault(i => i.itemId == itemId);
            if (existing != null) existing.quantity += qty;
            else Items.Add(new InventoryItem { itemId = itemId, quantity = qty });
            Save();
            OnInventoryChanged?.Invoke();
        }

        public bool RemoveItem(string itemId, int qty = 1)
        {
            var item = Items.FirstOrDefault(i => i.itemId == itemId);
            if (item == null || item.quantity < qty) return false;
            item.quantity -= qty;
            if (item.quantity <= 0) Items.Remove(item);
            Save();
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool HasItem(string itemId, int qty = 1) =>
            Items.Any(i => i.itemId == itemId && i.quantity >= qty);

        // Equip an item from the bag; unequips what was in that slot. Applied LOCALLY so the UI +
        // stats update immediately for ANY item, then persisted to the server via the inventory
        // PUT — which stores the loadout without needing the item in the server catalog, so it
        // reliably reaches the database (the old granular /equip endpoint silently failed when the
        // item wasn't in the server catalog, leaving nothing updated).
        public bool Equip(string itemId)
        {
            if (!HasItem(itemId)) return false;
            var data = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(itemId) : null;
            if (data == null || data.itemType == ItemType.Consumable) return false;

            var player = PlayerManager.Instance != null ? PlayerManager.Instance.Data : null;
            if (player == null) return false;
            if (player.level < data.requiredLevel) return false;
            if (!data.CanBeUsedBy(player.classType)) return false;

            // Move the currently-equipped item back to the bag, then equip the new one.
            string current = Equipment.GetSlot(data.itemType);
            if (!string.IsNullOrEmpty(current)) AddItem(current);
            RemoveItem(itemId);
            Equipment.SetSlot(data.itemType, itemId);
            RecalculateStats();
            Save();
            OnInventoryChanged?.Invoke();

            PersistToServer();
            return true;
        }

        public bool Unequip(ItemType slot)
        {
            string id = Equipment.GetSlot(slot);
            if (string.IsNullOrEmpty(id)) return false;
            Equipment.SetSlot(slot, null);
            AddItem(id);
            RecalculateStats();
            Save();
            OnInventoryChanged?.Invoke();

            PersistToServer();
            return true;
        }

        public bool UseConsumable(string itemId)
        {
            if (ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady)
            {
                try
                {
                    var state = ServerApiClient.Instance.UseConsumableAsync(itemId).GetAwaiter().GetResult();
                    ApplyServerState(state);
                    SaveLocal();
                    OnInventoryChanged?.Invoke();
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Inventory] Uso remoto de consumível falhou: {e.Message}");
                    return false;
                }
            }

            var data = ItemDatabase.Instance.Get(itemId);
            if (data == null || data.itemType != ItemType.Consumable) return false;
            var player = PlayerManager.Instance.Data;
            player.hp = Mathf.Min(player.hp + data.bonusHp, player.maxHp);
            if (!RemoveItem(itemId)) return false;
            PlayerManager.Instance.Save();
            return true;
        }

        // Recomputes the live character stats: server-scale base (the SAME formula the backend's
        // ProgressionRules uses, so the hero stays balanced against the server-scaled enemies and
        // matches what the server reports) PLUS every equipped item's bonuses (flat + %). Called
        // after any equip / unequip / server-state application, so gear always shows on the hero.
        private void RecalculateStats()
        {
            var player = PlayerManager.Instance != null ? PlayerManager.Instance.Data : null;
            if (player == null) return;

            // Base stats by level — mirrors ProgressionRules on the backend (100 +50/lvl HP,
            // 25 +25/lvl ATK & DEF, 0.01 +0.01/lvl speed, +1.25/lvl heal power).
            int lvl = Mathf.Max(1, player.level);
            player.maxHp     = 100 + 50 * (lvl - 1);
            int baseAttack   = 25  + 25 * (lvl - 1);
            player.defense   = 25  + 25 * (lvl - 1);
            player.speed     = 0.01f + 0.01f * (lvl - 1);
            player.healPower = 1.25f * (lvl - 1);

            // Reset the equipment-derived combat modifiers before re-summing them.
            player.armor                = 0;
            player.critChanceBonus      = 0f;
            player.critDamageBonus      = 0f;
            player.attackSpeedBonus     = 0f;
            player.lifeRegenPctBonus    = 0f;
            player.physicalResistPct    = 0f;
            player.magicResistPct       = 0f;
            player.cooldownReductionPct = 0f;
            player.moveSpeedBonus       = 0f;

            // Add equipment bonuses
            int   flatAttack = 0;   // bonusAttack + weapon flatDamage
            float dmgPct     = 0f;  // % damage, applied multiplicatively at the end
            foreach (ItemType slot in Enum.GetValues(typeof(ItemType)))
            {
                if (slot == ItemType.Consumable) continue;
                string id = Equipment.GetSlot(slot);
                if (string.IsNullOrEmpty(id)) continue;
                var item = ItemDatabase.Instance.Get(id);
                if (item == null) continue;

                // Rarity makes items DRASTICALLY stronger: the numeric bonuses are multiplied by
                // a steep per-tier factor (Common→Legendary), so a legendary piece dwarfs a common.
                float rm = RarityMult(item.rarity);
                player.maxHp    += Mathf.RoundToInt(item.bonusHp * rm);
                flatAttack      += Mathf.RoundToInt((item.bonusAttack + item.flatDamage) * rm);
                player.defense  += Mathf.RoundToInt((item.bonusDefense + item.armor) * rm);
                player.speed    += item.bonusSpeed * rm;
                player.healPower += item.bonusHealPower * rm;

                player.armor                += Mathf.RoundToInt(item.armor * rm);
                dmgPct                      += item.pctDamage;
                player.critChanceBonus      += item.pctCritChance;
                player.critDamageBonus      += item.pctCritDamage;
                player.attackSpeedBonus     += item.pctAttackSpeed;
                player.lifeRegenPctBonus    += item.pctLifeRegen;
                player.physicalResistPct    += item.pctPhysicalResist;
                player.magicResistPct       += item.pctMagicResist;
                player.cooldownReductionPct += item.pctCooldownReduction;
                player.moveSpeedBonus       += item.bonusMoveSpeed;

                // Rarity-derived combat stats so higher tiers carry the flashy % stats even when
                // the base item omits them: weapons → crit + attack speed + %dmg; armour → resist;
                // rings/amulets → a mix. Legendary weapons hit hard, fast, and crit a lot.
                AddRarityStats(player, item, ref dmgPct);
            }

            // Attack = (base + flat) boosted by % damage; resistances/CDR are capped so
            // stacked gear can never make the hero fully immune or zero-cooldown.
            player.attack = Mathf.RoundToInt((baseAttack + flatAttack) * (1f + dmgPct));
            player.physicalResistPct    = Mathf.Clamp(player.physicalResistPct, 0f, 0.75f);
            player.magicResistPct       = Mathf.Clamp(player.magicResistPct, 0f, 0.75f);
            player.cooldownReductionPct = Mathf.Clamp(player.cooldownReductionPct, 0f, 0.60f);
            // Percent life-regen resolves against the final max HP and adds to flat heal.
            player.healPower += player.lifeRegenPctBonus * player.maxHp;

            player.hp = Mathf.Min(player.hp, player.maxHp);
            PlayerManager.Instance.Save();
        }

        // ── Rarity scaling ───────────────────────────────────────────────────
        // Steep per-tier multiplier for an item's NUMERIC bonuses (atk/hp/def/speed/heal). Drastic
        // gaps so rarity really matters: a Legendary piece is ~7× a Common one.
        static float RarityMult(ItemRarity r) => r switch
        {
            ItemRarity.Common    => 1.0f,
            ItemRarity.Uncommon  => 1.6f,
            ItemRarity.Rare      => 2.6f,   // Raro
            ItemRarity.Epic      => 4.2f,   // Mito
            ItemRarity.Legendary => 7.0f,   // Lendário
            _                    => 1.0f,
        };

        // Per-rarity flashy % stats (index by rarity 0..4). Weapons roll crit + attack speed +
        // %damage + crit-damage; armour rolls resistances; rings/amulets take a blend — so a
        // Legendary weapon hits hard, fast and crits a lot, and Legendary armour is very tanky.
        static readonly float[] RCrit    = { 0f, 0.03f, 0.06f, 0.11f, 0.20f };
        static readonly float[] RAtkSpd  = { 0f, 0.05f, 0.10f, 0.18f, 0.30f };
        static readonly float[] RDmgPct  = { 0f, 0.05f, 0.10f, 0.18f, 0.30f };
        static readonly float[] RCritDmg = { 0f, 0.06f, 0.14f, 0.28f, 0.55f };
        static readonly float[] RResist  = { 0f, 0.04f, 0.08f, 0.14f, 0.22f };

        static void AddRarityStats(PlayerData p, ItemData item, ref float dmgPct)
        {
            int r = Mathf.Clamp((int)item.rarity, 0, 4);
            switch (item.itemType)
            {
                case ItemType.Weapon:
                    p.critChanceBonus  += RCrit[r];
                    p.attackSpeedBonus += RAtkSpd[r];
                    p.critDamageBonus  += RCritDmg[r];
                    dmgPct             += RDmgPct[r];
                    break;
                case ItemType.Ring:
                case ItemType.Amulet:
                    p.critChanceBonus   += RCrit[r] * 0.6f;
                    p.critDamageBonus   += RCritDmg[r] * 0.6f;
                    p.physicalResistPct += RResist[r] * 0.5f;
                    p.magicResistPct    += RResist[r] * 0.5f;
                    break;
                case ItemType.Consumable:
                    break;   // potions get no combat bonuses
                default:     // Helmet / Chest / Legs / Boots / Gloves / Cape → defensive
                    p.physicalResistPct += RResist[r];
                    p.magicResistPct    += RResist[r];
                    break;
            }
        }

        public void Save()
        {
            SaveLocal();
        }

        // Persists the current inventory + equipment loadout to the server (best-effort). Uses the
        // inventory PUT, which stores the loadout regardless of the server item catalog, so the
        // database updates reliably even for items the server hasn't catalogued.
        private void PersistToServer()
        {
            if (ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady) return;
            try
            {
                ServerApiClient.Instance.SaveInventoryAsync(BuildServerState()).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Inventory] Persistência do inventário no servidor falhou: {e.Message}");
            }
        }

        // Server-facing snapshot: strips local-only test items (bag + equipped slots), which the
        // server has no catalogue for.
        private InventoryStateDto BuildServerState()
        {
            string NonTest(string id) => TestItems.IsTestItem(id) ? null : id;
            return new InventoryStateDto
            {
                items = Items
                    .Where(i => i != null && i.quantity > 0 && !TestItems.IsTestItem(i.itemId))
                    .Select(i => new InventoryItemDto { itemId = i.itemId, quantity = i.quantity })
                    .ToArray(),
                equipment = new EquipmentDto
                {
                    weaponId = NonTest(Equipment.weaponId),
                    helmetId = NonTest(Equipment.helmetId),
                    chestId  = NonTest(Equipment.chestId),
                    legsId   = NonTest(Equipment.legsId),
                    bootsId  = NonTest(Equipment.bootsId),
                    glovesId = NonTest(Equipment.glovesId),
                    ringId   = NonTest(Equipment.ringId),
                    amuletId = NonTest(Equipment.amuletId)
                }
            };
        }

        public void Load()
        {
            if (ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady)
            {
                try
                {
                    SyncItemCatalog();
                    var remote = ServerApiClient.Instance.LoadInventoryAsync().GetAwaiter().GetResult();
                    if (remote != null)
                    {
                        ApplyServerState(remote);
                        SaveLocal();
                        return;
                    }
                    Items = new List<InventoryItem>();
                    Equipment = new Equipment();
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Inventory] Remote load failed: {e.Message}");
                    Items = new List<InventoryItem>();
                    Equipment = new Equipment();
                    return;
                }
            }

            if (!File.Exists(SavePath)) return;
            var data = JsonConvert.DeserializeAnonymousType(
                File.ReadAllText(SavePath),
                new { items = new List<InventoryItem>(), equipment = new Equipment() });
            Items     = data.items ?? new List<InventoryItem>();
            Equipment = data.equipment ?? new Equipment();
        }

        private void SyncItemCatalog()
        {
            var db = ItemDatabase.Instance;
            if (db == null || db.items == null || db.items.Length == 0) return;

            var payload = db.items
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.itemId))
                .Select(ItemDefinitionDto.FromItemData)
                .ToArray();

            if (payload.Length == 0) return;
            ServerApiClient.Instance.SyncItemDefinitionsAsync(payload).GetAwaiter().GetResult();
        }

        private void ApplyServerState(InventoryStateDto remote, bool preserveCurrentHp = false)
        {
            // Test items were removed — the server loot table is now the only source of gear, so a
            // server sync is authoritative and any lingering tst_* items are dropped here.
            if (remote == null)
            {
                Items = new List<InventoryItem>();
                Equipment = new Equipment();
            }
            else
            {
                Items = new List<InventoryItem>();
                if (remote.items != null)
                {
                    foreach (var it in remote.items)
                    {
                        if (string.IsNullOrWhiteSpace(it.itemId) || it.quantity <= 0) continue;
                        Items.Add(new InventoryItem { itemId = it.itemId, quantity = it.quantity });
                    }
                }

                Equipment = remote.equipment != null ? remote.equipment.ToEquipment() : new Equipment();

                if (remote.characterStats != null && PlayerManager.Instance != null && PlayerManager.Instance.Data != null)
                {
                    var stats = remote.characterStats;
                    var p = PlayerManager.Instance.Data;
                    var previousHp = p.hp;
                    p.maxHp = Mathf.Max(1, Mathf.Max(stats.maxHp, stats.hp));
                    p.hp = preserveCurrentHp
                        ? Mathf.Clamp(previousHp, 0, p.maxHp)
                        : Mathf.Clamp(stats.hp, 0, p.maxHp);
                    p.attack = Mathf.Max(0, stats.attack);
                    p.defense = Mathf.Max(0, stats.defense);
                    p.speed = Mathf.Max(0.01f, stats.speed);
                    p.healPower = Mathf.Max(0f, stats.healPower);
                    PlayerManager.Instance.NormalizeCurrentData();
                }
            }

            // Recompute the live stats from the server-scale base + every equipped item, so gear
            // bonuses always show and combat rewards / reloads never wipe them.
            RecalculateStats();
        }

        public void ApplyServerKillResult(InventoryStateDto remote, bool notify = true)
        {
            ApplyServerState(remote, preserveCurrentHp: true);
            if (notify)
            {
                OnInventoryChanged?.Invoke();
            }
        }

        private void SaveLocal()
        {
            var data = new { items = Items, equipment = Equipment };
            File.WriteAllText(SavePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

    }
}

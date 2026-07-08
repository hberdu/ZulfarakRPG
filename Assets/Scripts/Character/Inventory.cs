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

        // Equip an item from the bag; unequips what was in that slot
        public bool Equip(string itemId)
        {
            // Local test items (tst_*) never touch the server catalog — equip offline.
            if (!TestItems.IsTestItem(itemId) && ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady)
            {
                try
                {
                    var state = ServerApiClient.Instance.EquipItemAsync(itemId).GetAwaiter().GetResult();
                    ApplyServerState(state);
                    SaveLocal();
                    OnInventoryChanged?.Invoke();
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Inventory] Equip remoto falhou: {e.Message}");
                    return false;
                }
            }

            if (!HasItem(itemId)) return false;
            var data = ItemDatabase.Instance.Get(itemId);
            if (data == null || data.itemType == ItemType.Consumable) return false;

            var player = PlayerManager.Instance.Data;
            if (player.level < data.requiredLevel) return false;
            if (!data.CanBeUsedBy(player.classType)) return false;

            // Move current equipped item back to bag
            string current = Equipment.GetSlot(data.itemType);
            if (!string.IsNullOrEmpty(current)) AddItem(current);

            RemoveItem(itemId);
            Equipment.SetSlot(data.itemType, itemId);
            RecalculateStats();
            Save();
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool Unequip(ItemType slot)
        {
            // Local test items (tst_*) unequip offline, bypassing the server.
            if (!TestItems.IsTestItem(Equipment.GetSlot(slot))
                && ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady)
            {
                try
                {
                    var state = ServerApiClient.Instance.UnequipItemAsync(slot).GetAwaiter().GetResult();
                    ApplyServerState(state);
                    SaveLocal();
                    OnInventoryChanged?.Invoke();
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Inventory] Unequip remoto falhou: {e.Message}");
                    return false;
                }
            }

            string id = Equipment.GetSlot(slot);
            if (string.IsNullOrEmpty(id)) return false;
            Equipment.SetSlot(slot, null);
            AddItem(id);
            RecalculateStats();
            Save();
            OnInventoryChanged?.Invoke();
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

        private void RecalculateStats()
        {
            var player = PlayerManager.Instance.Data;
            var cls    = ClassDatabase.Instance.GetClass(player.classType);
            var sub    = ClassDatabase.Instance.GetSubclass(player.subclassType);
            if (cls == null) return;

            // Reset to base stats (scaled by level)
            float lvlScale = 1f + (player.level - 1) * 0.08f;
            player.maxHp   = Mathf.RoundToInt(cls.baseHp      * (sub?.hpMultiplier ?? 1f)  * lvlScale);
            player.attack  = Mathf.RoundToInt(cls.baseAttack  * (sub?.attackMultiplier ?? 1f) * lvlScale);
            player.defense = Mathf.RoundToInt(cls.baseDefense * (sub?.defenseMultiplier ?? 1f) * lvlScale);
            player.speed   = cls.baseSpeed * (sub?.speedMultiplier ?? 1f);

            // Add equipment bonuses
            foreach (ItemType slot in Enum.GetValues(typeof(ItemType)))
            {
                if (slot == ItemType.Consumable) continue;
                string id = Equipment.GetSlot(slot);
                if (string.IsNullOrEmpty(id)) continue;
                var item = ItemDatabase.Instance.Get(id);
                if (item == null) continue;
                player.maxHp    += item.bonusHp;
                player.attack   += item.bonusAttack;
                player.defense  += item.bonusDefense;
                player.speed    += item.bonusSpeed;
                player.healPower += item.bonusHealPower;
            }

            player.hp = Mathf.Min(player.hp, player.maxHp);
            PlayerManager.Instance.Save();
        }

        public void Save()
        {
            SaveLocal();
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
            // The server doesn't know the local test items (tst_*), so a server sync would
            // wipe them (and unequip them) — e.g. on every dungeon kill. Snapshot them here
            // and re-apply after so they survive across scenes / combat.
            var testBag = Items.Where(i => TestItems.IsTestItem(i.itemId)).ToList();
            var testEquip = new List<(ItemType slot, string id)>();
            foreach (ItemType slot in Enum.GetValues(typeof(ItemType)))
            {
                if (slot == ItemType.Consumable) continue;
                var eqId = Equipment.GetSlot(slot);
                if (TestItems.IsTestItem(eqId)) testEquip.Add((slot, eqId));
            }

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

            // Restore the local test items (bag + equipped) the server just discarded.
            foreach (var t in testBag)
                if (!Items.Any(i => i.itemId == t.itemId)) Items.Add(t);
            foreach (var (slot, id) in testEquip)
                Equipment.SetSlot(slot, id);
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

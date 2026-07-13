using UnityEngine;
using System.Linq;

namespace ZulfarakRPG
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "ZulfarakRPG/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        private static ItemDatabase _instance;
        public static ItemDatabase Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<ItemDatabase>("ItemDatabase");
                return _instance;
            }
        }

        public ItemData[] items;

        // Items pulled from the SERVER catalog at runtime (drops the client didn't author locally).
        // Keyed by itemId so a dropped item resolves for equipping / tooltips / stats.
        private System.Collections.Generic.Dictionary<string, ItemData> _runtime;

        public void RegisterRuntime(ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) return;
            (_runtime ??= new System.Collections.Generic.Dictionary<string, ItemData>())[item.itemId] = item;
        }

        public ItemData Get(string id)
        {
            var authored = items?.FirstOrDefault(i => i.itemId == id);
            if (authored != null) return authored;
            if (_runtime != null && _runtime.TryGetValue(id, out var rt)) return rt;
            return TestItems.Get(id);
        }
        public ItemData[] GetByType(ItemType type) => items?.Where(i => i.itemType == type).ToArray();
        public ItemData[] GetByRarity(ItemRarity rarity) => items?.Where(i => i.rarity == rarity).ToArray();
    }
}

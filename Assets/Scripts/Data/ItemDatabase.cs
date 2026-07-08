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

        public ItemData Get(string id)
            => items?.FirstOrDefault(i => i.itemId == id) ?? TestItems.Get(id);
        public ItemData[] GetByType(ItemType type) => items?.Where(i => i.itemType == type).ToArray();
        public ItemData[] GetByRarity(ItemRarity rarity) => items?.Where(i => i.rarity == rarity).ToArray();
    }
}

using UnityEngine;

namespace ZulfarakRPG
{
    [CreateAssetMenu(fileName = "MissionData", menuName = "ZulfarakRPG/Mission Data")]
    public class MissionData : ScriptableObject
    {
        public string missionId;
        public string missionName;
        [TextArea] public string description;
        public MissionType missionType;

        public int requiredLevel = 1;
        public float durationSeconds = 60f;

        // Rewards
        public int expReward;
        public int goldReward;
        public ItemReward[] itemRewards;

        // For guild missions
        public int requiredPlayers = 5;
        public EnemyData[] enemies;

        // Success chance modifiers per role (guild missions)
        public float tankSuccessBonus = 0.1f;
        public float healerSuccessBonus = 0.15f;
        public float dpsSuccessBonus = 0.05f;

        public Sprite missionIcon;
        public string locationName = "Zulfarak";
    }

    [System.Serializable]
    public class ItemReward
    {
        public string itemName;
        public float dropChance = 0.1f;
        public int quantity = 1;
    }
}

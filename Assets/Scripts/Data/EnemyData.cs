using UnityEngine;

namespace ZulfarakRPG
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "ZulfarakRPG/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        public string enemyName;
        [TextArea] public string description;
        public Sprite sprite;

        public int hp;
        public int attack;
        public int defense;
        public float attackSpeed = 1f;

        public int expReward;
        public int goldReward;
        public ItemReward[] possibleDrops;

        public bool isBoss;
    }
}

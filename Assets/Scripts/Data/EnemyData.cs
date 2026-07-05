using UnityEngine;
using System.Text;

namespace ZulfarakRPG
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "ZulfarakRPG/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        public string enemyId;
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

        public string GetServerEnemyId()
        {
            if (!string.IsNullOrWhiteSpace(enemyId))
            {
                return enemyId.Trim();
            }

            if (string.IsNullOrWhiteSpace(enemyName))
            {
                return string.Empty;
            }

            var raw = enemyName.Trim().ToLowerInvariant();
            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(ch);
                }
                else if (ch == ' ' || ch == '-' || ch == '_')
                {
                    if (sb.Length == 0 || sb[sb.Length - 1] == '_') continue;
                    sb.Append('_');
                }
            }

            return sb.ToString().Trim('_');
        }
    }
}

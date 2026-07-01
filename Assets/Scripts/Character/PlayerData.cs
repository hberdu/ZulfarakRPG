using System;
using UnityEngine;

namespace ZulfarakRPG
{
    [Serializable]
    public class PlayerData
    {
        public string steamId;
        public string playerName;

        // Appearance
        public CharacterSex sex;
        public SkinTone skinTone;
        public HairStyle hairStyle;
        public int hairColorIndex;
        public FaceStyle faceStyle;

        // Class
        public ClassType classType;
        public SubclassType subclassType;
        public bool subclassUnlocked;

        // Progression
        public int level = 1;
        public long currentExp;
        public long gold;

        // Stats
        public int hp;
        public int maxHp;
        public int attack;
        public int defense;
        public float speed;
        public float healPower;

        // Guild
        public string guildId;
        public bool isGuildLeader;

        // Location
        public string currentCity = "Zulfarak";

        public long expToNextLevel => (long)(100 * Math.Pow(level, 1.5f));

        public void AddExp(long amount)
        {
            currentExp += amount;
            while (currentExp >= expToNextLevel)
            {
                currentExp -= expToNextLevel;
                level++;
                OnLevelUp();
            }
        }

        private void OnLevelUp()
        {
            // Stats scale with level
            maxHp = Mathf.RoundToInt(maxHp * 1.1f);
            hp = maxHp;
            attack = Mathf.RoundToInt(attack * 1.08f);
            defense = Mathf.RoundToInt(defense * 1.06f);
        }
    }
}

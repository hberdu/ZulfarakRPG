using UnityEngine;

namespace ZulfarakRPG
{
    [CreateAssetMenu(fileName = "SubclassData", menuName = "ZulfarakRPG/Subclass Data")]
    public class SubclassData : ScriptableObject
    {
        public SubclassType subclassType;
        public ClassType parentClass;
        public Role role;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        // Base stats multipliers relative to parent class
        public float hpMultiplier = 1f;
        public float attackMultiplier = 1f;
        public float defenseMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float healPowerMultiplier = 1f;

        // Passive ability description
        public string passiveAbilityName;
        [TextArea] public string passiveAbilityDescription;
    }

    [CreateAssetMenu(fileName = "ClassData", menuName = "ZulfarakRPG/Class Data")]
    public class ClassData : ScriptableObject
    {
        public ClassType classType;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        // Base stats at level 1
        public int baseHp = 100;
        public int baseAttack = 10;
        public int baseDefense = 5;
        public float baseSpeed = 1f;
        public float baseHealPower = 0f;

        public SubclassData[] availableSubclasses;
    }
}

namespace ZulfarakRPG
{
    public enum ClassType { Mage, Warrior, Archer }

    public enum SubclassType
    {
        // Mage
        Cleric,
        FireMage,
        IceMage,
        LightningMage,
        // Warrior
        Shieldbearer,
        Lancer,
        Berserker,
        // Archer
        Survival,
        Hunter,
        Tracker
    }

    public enum Role { DPS, Healer, Tank }

    public enum MissionType { Individual, Guild }

    public enum MissionStatus { Available, InProgress, Completed, Failed }

    public enum GuildMissionState { WaitingForMembers, InProgress, Completed, Failed }

    public enum CharacterSex { Male, Female }

    public enum SkinTone { Light, Medium, Dark, VeryDark }

    public enum HairStyle { Short, Long, Braided, Bald, Curly }

    public enum FaceStyle { Default, Beard, Scar, Markings }
}

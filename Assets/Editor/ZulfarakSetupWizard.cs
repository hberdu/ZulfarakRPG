using UnityEngine;
using UnityEditor;
using System.IO;
using ZulfarakRPG;

// Run once: Tools > ZulfarakRPG > Setup All Assets
// Creates every ScriptableObject the game needs under Assets/ScriptableObjects/
public static class ZulfarakSetupWizard
{
    [MenuItem("Tools/ZulfarakRPG/Setup All Assets")]
    public static void SetupAll()
    {
        CreateSubclasses();
        CreateClasses();
        CreateClassDatabase();
        CreateEnemies();
        CreateMissions();
        CreateItems();
        CreateItemDatabase();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ZulfarakRPG] Setup completo! Todos os assets foram criados.");
    }

    // ──────────────────────────────────────────────────
    // SUBCLASSES
    // ──────────────────────────────────────────────────
    private static void CreateSubclasses()
    {
        // Mago
        CreateSubclass(SubclassType.Cleric,        ClassType.Mage,    Role.Healer, "Clérigo",
            "Mago da luz sagrada. Cura aliados e ressurge como guardião divino.",
            hpMult: 1.1f, atkMult: 0.7f, defMult: 1.0f, healMult: 1.8f);

        CreateSubclass(SubclassType.FireMage,      ClassType.Mage,    Role.DPS,    "Mago do Fogo",
            "Chamas devastadoras queimam inimigos e destroem defesas.",
            hpMult: 0.9f, atkMult: 1.5f, defMult: 0.8f);

        CreateSubclass(SubclassType.IceMage,       ClassType.Mage,    Role.DPS,    "Mago do Gelo",
            "Congela inimigos, reduzindo sua velocidade de ataque.",
            hpMult: 0.9f, atkMult: 1.3f, defMult: 0.9f, spdMult: 1.1f);

        CreateSubclass(SubclassType.LightningMage, ClassType.Mage,    Role.DPS,    "Mago do Raio",
            "Velocidade acima da média. Raios encadeiam dano entre alvos.",
            hpMult: 0.85f, atkMult: 1.4f, defMult: 0.75f, spdMult: 1.3f);

        // Guerreiro
        CreateSubclass(SubclassType.Shieldbearer,  ClassType.Warrior, Role.Tank,   "Escudeiro",
            "Absorve dano e mantém os inimigos focados nele.",
            hpMult: 1.5f, atkMult: 0.8f, defMult: 1.8f);

        CreateSubclass(SubclassType.Lancer,        ClassType.Warrior, Role.DPS,    "Lanceiro",
            "Lança ataques precisos que ignoram parte da defesa inimiga.",
            hpMult: 1.1f, atkMult: 1.3f, defMult: 1.0f);

        CreateSubclass(SubclassType.Berserker,     ClassType.Warrior, Role.DPS,    "Berserker",
            "Quanto menos HP, mais forte. Alto risco, alto retorno.",
            hpMult: 0.9f, atkMult: 1.6f, defMult: 0.7f, spdMult: 1.2f);

        // Arqueiro
        CreateSubclass(SubclassType.Survival,      ClassType.Archer,  Role.Healer, "Sobrevivência",
            "Usa ervas e habilidades de campo para curar aliados.",
            hpMult: 1.1f, atkMult: 0.8f, defMult: 1.0f, healMult: 1.5f);

        CreateSubclass(SubclassType.Hunter,        ClassType.Archer,  Role.DPS,    "Caçador",
            "Animais de estimação auxiliam o caçador em batalha.",
            hpMult: 1.0f, atkMult: 1.3f, defMult: 0.9f);

        CreateSubclass(SubclassType.Tracker,       ClassType.Archer,  Role.DPS,    "Rastreador",
            "Encontra pontos fracos. Ataques críticos frequentes.",
            hpMult: 0.9f, atkMult: 1.5f, defMult: 0.8f, spdMult: 1.15f);
    }

    private static SubclassData CreateSubclass(
        SubclassType type, ClassType parent, Role role, string name, string desc,
        float hpMult = 1f, float atkMult = 1f, float defMult = 1f,
        float spdMult = 1f, float healMult = 1f)
    {
        string path = $"Assets/ScriptableObjects/Classes/Sub_{type}.asset";
        SubclassData asset = LoadOrCreate<SubclassData>(path);
        asset.subclassType          = type;
        asset.parentClass           = parent;
        asset.role                  = role;
        asset.displayName           = name;
        asset.description           = desc;
        asset.hpMultiplier          = hpMult;
        asset.attackMultiplier      = atkMult;
        asset.defenseMultiplier     = defMult;
        asset.speedMultiplier       = spdMult;
        asset.healPowerMultiplier   = healMult;
        EditorUtility.SetDirty(asset);
        return asset;
    }

    // ──────────────────────────────────────────────────
    // CLASSES
    // ──────────────────────────────────────────────────
    private static void CreateClasses()
    {
        var mage = LoadOrCreate<ClassData>("Assets/ScriptableObjects/Classes/Class_Mage.asset");
        mage.classType   = ClassType.Mage;
        mage.displayName = "Mago";
        mage.description = "Mestres da magia arcana. Podem curar, destruir ou paralisar inimigos.";
        mage.baseHp      = 80;
        mage.baseAttack  = 14;
        mage.baseDefense = 4;
        mage.baseSpeed   = 0.9f;
        mage.baseHealPower = 5f;
        mage.availableSubclasses = LoadSubclassesForClass(ClassType.Mage);
        EditorUtility.SetDirty(mage);

        var warrior = LoadOrCreate<ClassData>("Assets/ScriptableObjects/Classes/Class_Warrior.asset");
        warrior.classType   = ClassType.Warrior;
        warrior.displayName = "Guerreiro";
        warrior.description = "Combatentes de frente que dominam o campo de batalha corpo a corpo.";
        warrior.baseHp      = 130;
        warrior.baseAttack  = 12;
        warrior.baseDefense = 8;
        warrior.baseSpeed   = 1.0f;
        warrior.availableSubclasses = LoadSubclassesForClass(ClassType.Warrior);
        EditorUtility.SetDirty(warrior);

        var archer = LoadOrCreate<ClassData>("Assets/ScriptableObjects/Classes/Class_Archer.asset");
        archer.classType   = ClassType.Archer;
        archer.displayName = "Arqueiro";
        archer.description = "Ágeis e versáteis, atacam à distância e vivem no deserto.";
        archer.baseHp      = 100;
        archer.baseAttack  = 13;
        archer.baseDefense = 5;
        archer.baseSpeed   = 1.2f;
        archer.baseHealPower = 2f;
        archer.availableSubclasses = LoadSubclassesForClass(ClassType.Archer);
        EditorUtility.SetDirty(archer);
    }

    private static SubclassData[] LoadSubclassesForClass(ClassType type)
    {
        var all = new System.Collections.Generic.List<SubclassData>();
        string[] guids = AssetDatabase.FindAssets("t:SubclassData", new[] { "Assets/ScriptableObjects/Classes" });
        foreach (var g in guids)
        {
            var s = AssetDatabase.LoadAssetAtPath<SubclassData>(AssetDatabase.GUIDToAssetPath(g));
            if (s != null && s.parentClass == type) all.Add(s);
        }
        return all.ToArray();
    }

    // ──────────────────────────────────────────────────
    // CLASS DATABASE
    // ──────────────────────────────────────────────────
    private static void CreateClassDatabase()
    {
        string[] classGuids = AssetDatabase.FindAssets("t:ClassData", new[] { "Assets/ScriptableObjects/Classes" });
        string[] subGuids   = AssetDatabase.FindAssets("t:SubclassData", new[] { "Assets/ScriptableObjects/Classes" });

        var db = LoadOrCreate<ClassDatabase>("Assets/Resources/ClassDatabase.asset");

        var classes = new ClassData[classGuids.Length];
        for (int i = 0; i < classGuids.Length; i++)
            classes[i] = AssetDatabase.LoadAssetAtPath<ClassData>(AssetDatabase.GUIDToAssetPath(classGuids[i]));
        db.classes = classes;

        var subs = new SubclassData[subGuids.Length];
        for (int i = 0; i < subGuids.Length; i++)
            subs[i] = AssetDatabase.LoadAssetAtPath<SubclassData>(AssetDatabase.GUIDToAssetPath(subGuids[i]));
        db.subclasses = subs;

        EditorUtility.SetDirty(db);
    }

    // ──────────────────────────────────────────────────
    // ENEMIES
    // ──────────────────────────────────────────────────
    private static void CreateEnemies()
    {
        // Solo mission enemies — Bandidos do Deserto
        CreateEnemy("Bandido Novato",    hp: 40,  atk: 6,  def: 2,  exp: 12,  gold: 5,  boss: false);
        CreateEnemy("Bandido Veterano",  hp: 70,  atk: 9,  def: 3,  exp: 20,  gold: 8,  boss: false);
        CreateEnemy("Líder dos Bandidos",hp: 120, atk: 14, def: 5,  exp: 40,  gold: 20, boss: true);

        // Guild mission enemies — Masmorra de Zulfarak
        CreateEnemy("Golem de Areia",    hp: 200, atk: 12, def: 10, exp: 50,  gold: 25, boss: false);
        CreateEnemy("Espectro das Dunas",hp: 150, atk: 18, def: 6,  exp: 60,  gold: 30, boss: false);
        CreateEnemy("Guardião de Zulfarak", hp: 500, atk: 22, def: 15, exp: 200, gold: 100, boss: true);
    }

    private static EnemyData CreateEnemy(string name, int hp, int atk, int def, int exp, int gold, bool boss)
    {
        string safeName = name.Replace(" ", "_");
        string path = $"Assets/ScriptableObjects/Enemies/Enemy_{safeName}.asset";
        EnemyData e = LoadOrCreate<EnemyData>(path);
        e.enemyName  = name;
        e.hp         = hp;
        e.attack     = atk;
        e.defense    = def;
        e.attackSpeed = 1f;
        e.expReward  = exp;
        e.goldReward = gold;
        e.isBoss     = boss;
        EditorUtility.SetDirty(e);
        return e;
    }

    private static EnemyData LoadEnemy(string name)
    {
        string safeName = name.Replace(" ", "_");
        return AssetDatabase.LoadAssetAtPath<EnemyData>(
            $"Assets/ScriptableObjects/Enemies/Enemy_{safeName}.asset");
    }

    // ──────────────────────────────────────────────────
    // MISSIONS
    // ──────────────────────────────────────────────────
    private static void CreateMissions()
    {
        // Solo
        var solo = LoadOrCreate<MissionData>("Assets/ScriptableObjects/Missions/Mission_PatrulhaDeserto.asset");
        solo.missionId      = "solo_patrulha_deserto";
        solo.missionName    = "Patrulha do Deserto";
        solo.description    = "Bandidos atacam as caravanas nos arredores de Zulfarak. Elimine-os antes que cheguem à cidade.";
        solo.missionType    = MissionType.Individual;
        solo.requiredLevel  = 1;
        solo.durationSeconds = 30f;
        solo.expReward      = 72;
        solo.goldReward     = 33;
        solo.enemies        = new EnemyData[]
        {
            LoadEnemy("Bandido Novato"),
            LoadEnemy("Bandido Novato"),
            LoadEnemy("Bandido Veterano"),
            LoadEnemy("Líder dos Bandidos")
        };
        EditorUtility.SetDirty(solo);

        // Guild
        var guild = LoadOrCreate<MissionData>("Assets/ScriptableObjects/Missions/Mission_MasmorraZulfarak.asset");
        guild.missionId      = "guild_masmorra_zulfarak";
        guild.missionName    = "A Masmorra de Zulfarak";
        guild.description    = "Criaturas antigas acordaram nas profundezas sob a cidade. Reúna sua guilda e entre na masmorra.";
        guild.missionType    = MissionType.Guild;
        guild.requiredLevel  = 1;
        guild.requiredPlayers = 5;
        guild.durationSeconds = 120f;
        guild.expReward      = 300;
        guild.goldReward     = 150;
        guild.tankSuccessBonus   = 0.15f;
        guild.healerSuccessBonus = 0.20f;
        guild.dpsSuccessBonus    = 0.08f;
        guild.enemies        = new EnemyData[]
        {
            LoadEnemy("Golem de Areia"),
            LoadEnemy("Espectro das Dunas"),
            LoadEnemy("Golem de Areia"),
            LoadEnemy("Guardião de Zulfarak")
        };
        EditorUtility.SetDirty(guild);
    }

    // ──────────────────────────────────────────────────
    // ITEMS
    // ──────────────────────────────────────────────────
    private static void CreateItems()
    {
        // Common starter gear (all classes)
        CreateItem("iron_sword",      "Espada de Ferro",       ItemType.Weapon,  ItemRarity.Common,    atk: 4,  lvl: 1,  gold: 15);
        CreateItem("wooden_staff",    "Cajado de Madeira",     ItemType.Weapon,  ItemRarity.Common,    atk: 3,  healPow: 2f, lvl: 1, gold: 15);
        CreateItem("short_bow",       "Arco Curto",            ItemType.Weapon,  ItemRarity.Common,    atk: 3,  spd: 0.1f, lvl: 1, gold: 15);
        CreateItem("leather_helmet",  "Elmo de Couro",         ItemType.Helmet,  ItemRarity.Common,    hp: 10, def: 2, lvl: 1, gold: 10);
        CreateItem("leather_chest",   "Peitoral de Couro",     ItemType.Chest,   ItemRarity.Common,    hp: 20, def: 3, lvl: 1, gold: 12);
        CreateItem("leather_legs",    "Calças de Couro",       ItemType.Legs,    ItemRarity.Common,    hp: 12, def: 2, lvl: 1, gold: 10);
        CreateItem("leather_boots",   "Botas de Couro",        ItemType.Boots,   ItemRarity.Common,    hp: 8,  def: 1, spd: 0.05f, lvl: 1, gold: 8);
        CreateItem("simple_gloves",   "Luvas Simples",         ItemType.Gloves,  ItemRarity.Common,    atk: 1, def: 1, lvl: 1, gold: 8);
        CreateItem("copper_ring",     "Anel de Cobre",         ItemType.Ring,    ItemRarity.Common,    hp: 5,  lvl: 1, gold: 5);
        CreateItem("bone_amulet",     "Amuleto de Osso",       ItemType.Amulet,  ItemRarity.Common,    atk: 2, lvl: 1, gold: 5);

        // Uncommon — drops from Veterano / rewards
        CreateItem("steel_sword",     "Espada de Aço",         ItemType.Weapon,  ItemRarity.Uncommon,  atk: 8,  lvl: 3, gold: 40);
        CreateItem("desert_robe",     "Manto do Deserto",      ItemType.Chest,   ItemRarity.Uncommon,  hp: 30, def: 4, healPow: 3f, lvl: 3, gold: 45);
        CreateItem("chain_helmet",    "Elmo de Malha",         ItemType.Helmet,  ItemRarity.Uncommon,  hp: 20, def: 5, lvl: 3, gold: 35);
        CreateItem("sand_boots",      "Botas da Areia",        ItemType.Boots,   ItemRarity.Uncommon,  hp: 15, def: 3, spd: 0.15f, lvl: 3, gold: 30);

        // Rare — boss drops
        CreateItem("zulfarak_blade",  "Lâmina de Zulfarak",    ItemType.Weapon,  ItemRarity.Rare,      atk: 16, def: 2,  lvl: 5, gold: 120);
        CreateItem("golem_shield",    "Escudo do Golem",       ItemType.Chest,   ItemRarity.Rare,      hp: 60,  def: 12, lvl: 5, gold: 150);
        CreateItem("specter_cloak",   "Manto do Espectro",     ItemType.Chest,   ItemRarity.Rare,      hp: 40,  def: 6,  spd: 0.3f, lvl: 5, gold: 130);

        // Consumable
        CreateItem("health_potion",   "Poção de Vida",         ItemType.Consumable, ItemRarity.Common,  hp: 50, lvl: 1, gold: 20);
        CreateItem("greater_potion",  "Poção de Vida Superior",ItemType.Consumable, ItemRarity.Uncommon, hp: 150, lvl: 3, gold: 60);
    }

    private static void CreateItem(
        string id, string name, ItemType type, ItemRarity rarity,
        int hp = 0, int atk = 0, int def = 0, float spd = 0f, float healPow = 0f,
        int lvl = 1, int gold = 10,
        string desc = null, ClassType[] classes = null)
    {
        string path = $"Assets/ScriptableObjects/Items/Item_{id}.asset";
        var item = LoadOrCreate<ItemData>(path);
        item.itemId          = id;
        item.itemName        = name;
        item.itemType        = type;
        item.rarity          = rarity;
        item.description     = desc ?? $"{rarity} {type.ToString().ToLower()} de nível {lvl}.";
        item.requiredLevel   = lvl;
        item.goldValue       = gold;
        item.bonusHp         = hp;
        item.bonusAttack     = atk;
        item.bonusDefense    = def;
        item.bonusSpeed      = spd;
        item.bonusHealPower  = healPow;
        item.allowedClasses  = classes ?? System.Array.Empty<ClassType>();
        EditorUtility.SetDirty(item);
    }

    private static void CreateItemDatabase()
    {
        var guids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/ScriptableObjects/Items" });
        var db    = LoadOrCreate<ItemDatabase>("Assets/Resources/ItemDatabase.asset");
        var items = new ItemData[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            items[i] = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guids[i]));
        db.items = items;
        EditorUtility.SetDirty(db);
    }

    // ──────────────────────────────────────────────────
    // UTIL
    // ──────────────────────────────────────────────────
    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Application.dataPath + "/../" + path));
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }
}

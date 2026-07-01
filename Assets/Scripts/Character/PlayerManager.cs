using UnityEngine;
using System.IO;
using Newtonsoft.Json;

namespace ZulfarakRPG
{
    public class PlayerManager : MonoBehaviour
    {
        public static PlayerManager Instance { get; private set; }

        public PlayerData Data { get; private set; }

        private string SavePath => Path.Combine(Application.persistentDataPath, "player.json");

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool HasSavedData() => File.Exists(SavePath);

        public void CreateNewCharacter(PlayerData data)
        {
            Data = data;
            ApplyClassStats();
            Save();
        }

        public void Load()
        {
            if (!HasSavedData()) return;
            string json = File.ReadAllText(SavePath);
            Data = JsonConvert.DeserializeObject<PlayerData>(json);
        }

        public void Save()
        {
            string json = JsonConvert.SerializeObject(Data, Formatting.Indented);
            File.WriteAllText(SavePath, json);
        }

        private void ApplyClassStats()
        {
            var db = ClassDatabase.Instance;
            ClassData cls = db.GetClass(Data.classType);
            if (cls == null) return;

            SubclassData sub = db.GetSubclass(Data.subclassType);
            float hpMult = sub != null ? sub.hpMultiplier : 1f;
            float atkMult = sub != null ? sub.attackMultiplier : 1f;
            float defMult = sub != null ? sub.defenseMultiplier : 1f;

            Data.maxHp = Mathf.RoundToInt(cls.baseHp * hpMult);
            Data.hp = Data.maxHp;
            Data.attack = Mathf.RoundToInt(cls.baseAttack * atkMult);
            Data.defense = Mathf.RoundToInt(cls.baseDefense * defMult);
            Data.speed = cls.baseSpeed * (sub != null ? sub.speedMultiplier : 1f);
            Data.healPower = cls.baseHealPower * (sub != null ? sub.healPowerMultiplier : 1f);
        }
    }
}

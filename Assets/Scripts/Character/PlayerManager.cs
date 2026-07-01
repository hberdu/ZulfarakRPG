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
            // Stats are best-effort — a missing/late ClassDatabase must NEVER stop the
            // character from being SAVED, or the game loops back to character creation
            // forever (HasSavedData stays false because player.json was never written).
            try { ApplyClassStats(); }
            catch (System.Exception e) { Debug.LogError($"[PlayerManager] ApplyClassStats failed: {e}"); }
            Save();
        }

        public void Load()
        {
            if (!HasSavedData()) return;
            try
            {
                string json = File.ReadAllText(SavePath);
                Data = JsonConvert.DeserializeObject<PlayerData>(json);
            }
            catch (System.Exception e) { Debug.LogError($"[PlayerManager] Load failed: {e}"); }
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Data, Formatting.Indented);
                File.WriteAllText(SavePath, json);
                Debug.Log($"[PlayerManager] Saved character to {SavePath}");
            }
            catch (System.Exception e) { Debug.LogError($"[PlayerManager] Save failed ({SavePath}): {e}"); }
        }

        private void ApplyClassStats()
        {
            var db = ClassDatabase.Instance;
            if (db == null) { Debug.LogWarning("[PlayerManager] ClassDatabase.Instance is null — skipping stats."); return; }
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

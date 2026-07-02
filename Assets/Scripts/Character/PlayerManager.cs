using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System;

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

        public bool HasSavedData()
        {
            if (ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady)
                return Data != null;
            return Data != null || File.Exists(SavePath);
        }

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
            if (ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady)
            {
                try
                {
                    var remote = ServerApiClient.Instance.LoadCharacterAsync().GetAwaiter().GetResult();
                    if (remote != null)
                    {
                        remote.steamId = SteamIntegration.Instance != null ? SteamIntegration.Instance.SteamId : remote.steamId;
                        Data = remote;
                        return;
                    }
                    Data = null;
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PlayerManager] Remote load failed: {e.Message}");
                    Data = null;
                    return;
                }
            }

            if (!File.Exists(SavePath)) return;
            try
            {
                string json = File.ReadAllText(SavePath);
                Data = JsonConvert.DeserializeObject<PlayerData>(json);
            }
            catch (System.Exception e) { Debug.LogError($"[PlayerManager] Load failed: {e}"); }
        }

        public void Save()
        {
            if (Data == null) return;

            if (string.IsNullOrWhiteSpace(Data.steamId) && SteamIntegration.Instance != null)
                Data.steamId = SteamIntegration.Instance.SteamId;

            if (ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady)
            {
                try
                {
                    ServerApiClient.Instance.SaveCharacterAsync(Data).GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PlayerManager] Remote save failed: {e.Message}");
                }
            }

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

using UnityEngine;
using System.Collections;
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

        // Async server-save state: Save() writes locally (instant) then queues a background
        // push so a slow/failed network never freezes the main thread (was a blocking call).
        private bool _serverSaveDirty;
        private bool _serverSaveRunning;

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
                        if (string.IsNullOrWhiteSpace(remote.steamId) && SteamIntegration.Instance != null)
                            remote.steamId = SteamIntegration.Instance.SteamId;
                        Data = remote;
                        NormalizeData();
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
                NormalizeData();
            }
            catch (System.Exception e) { Debug.LogError($"[PlayerManager] Load failed: {e}"); }
        }

        public void ApplyServerCharacter(CharacterDto dto, bool saveLocal = false, bool preserveCurrentHp = false)
        {
            if (dto == null) return;

            var previousHp = Data != null ? Data.hp : 0;
            var remote = dto.ToPlayerData();
            if (string.IsNullOrWhiteSpace(remote.steamId) && SteamIntegration.Instance != null)
                remote.steamId = SteamIntegration.Instance.SteamId;

            Data = remote;
            NormalizeData();

            if (preserveCurrentHp)
            {
                Data.hp = Mathf.Clamp(previousHp, 0, Data.maxHp);
            }

            if (saveLocal)
            {
                SaveLocalOnly();
            }
        }

        public void Save()
        {
            if (Data == null) return;

            if (string.IsNullOrWhiteSpace(Data.steamId) && SteamIntegration.Instance != null)
                Data.steamId = SteamIntegration.Instance.SteamId;

            // Local first — instant and never fails on network. Nothing is ever lost.
            try
            {
                SaveLocalOnly();
            }
            catch (System.Exception e) { Debug.LogError($"[PlayerManager] Save failed ({SavePath}): {e}"); }

            // Server push runs off the main thread with retry (offline queue).
            QueueServerSave();
        }

        private void QueueServerSave()
        {
            if (Data == null || ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady) return;
            _serverSaveDirty = true;
            if (!_serverSaveRunning) StartCoroutine(ServerSaveLoop());
        }

        private IEnumerator ServerSaveLoop()
        {
            _serverSaveRunning = true;
            float backoff = 1f;
            while (_serverSaveDirty)
            {
                _serverSaveDirty = false;
                if (Data == null || ServerApiClient.Instance == null || !ServerApiClient.Instance.IsReady) break;

                // FromPlayerData snapshots the current values synchronously before the await,
                // so later mutations to Data don't race this in-flight save.
                var task = ServerApiClient.Instance.SaveCharacterAsync(Data);
                while (!task.IsCompleted) yield return null;

                if (task.IsFaulted)
                {
                    var msg = task.Exception?.GetBaseException()?.Message ?? string.Empty;
                    if (msg.Contains("409"))
                    {
                        // Version conflict: another write won (other device / combat reward).
                        // Server is authoritative — stop clobbering; next Load picks it up.
                        Debug.LogWarning("[PlayerManager] Conflito de versão no save — servidor tem prioridade.");
                        break;
                    }

                    // Transient failure — requeue with backoff (stays pending until it lands).
                    _serverSaveDirty = true;
                    Debug.LogWarning($"[PlayerManager] Save remoto falhou (retry em {backoff:0}s): {msg}");
                    yield return new WaitForSeconds(backoff);
                    backoff = Mathf.Min(backoff * 2f, 30f);
                }
                else
                {
                    backoff = 1f;
                }
            }
            _serverSaveRunning = false;
        }

        public void NormalizeCurrentData()
        {
            NormalizeData();
        }

        public void RestoreFullHealthAndSave()
        {
            if (Data == null) return;
            Data.hp = Data.maxHp;
            NormalizeData();
            Save();
        }

        private void SaveLocalOnly()
        {
            if (Data == null) return;
            NormalizeData();
            string json = JsonConvert.SerializeObject(Data, Formatting.Indented);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[PlayerManager] Saved character to {SavePath}");
        }

        private void NormalizeData()
        {
            if (Data == null) return;

            Data.level = Mathf.Max(1, Data.level);
            Data.currentExp = Math.Max(0L, Data.currentExp);
            Data.expToNextLevelServer = Data.expToNextLevelServer > 0
                ? Data.expToNextLevelServer
                : PlayerData.CalculateExpToNextLevel(Data.level);
            EnsureCombatStatsFallback();
            Data.maxHp = Mathf.Max(1, Mathf.Max(Data.maxHp, Data.hp));
            Data.hp = Mathf.Clamp(Data.hp, 0, Data.maxHp);
            Data.attack = Mathf.Max(0, Data.attack);
            Data.defense = Mathf.Max(0, Data.defense);
            Data.speed = Mathf.Max(0.01f, Data.speed);
            Data.healPower = Mathf.Max(0f, Data.healPower);
            Data.gold = Math.Max(0L, Data.gold);
        }

        private void EnsureCombatStatsFallback()
        {
            if (Data.attack > 0 && Data.defense > 0 && Data.maxHp > 1)
            {
                return;
            }

            var db = ClassDatabase.Instance;
            if (db == null)
            {
                return;
            }

            var cls = db.GetClass(Data.classType);
            if (cls == null)
            {
                return;
            }

            var sub = db.GetSubclass(Data.subclassType);
            var lvlScale = 1f + (Mathf.Max(1, Data.level) - 1) * 0.08f;
            var baseMaxHp = Mathf.RoundToInt(cls.baseHp * (sub != null ? sub.hpMultiplier : 1f) * lvlScale);
            var baseAttack = Mathf.RoundToInt(cls.baseAttack * (sub != null ? sub.attackMultiplier : 1f) * lvlScale);
            var baseDefense = Mathf.RoundToInt(cls.baseDefense * (sub != null ? sub.defenseMultiplier : 1f) * lvlScale);
            var baseSpeed = cls.baseSpeed * (sub != null ? sub.speedMultiplier : 1f);
            var baseHeal = cls.baseHealPower * (sub != null ? sub.healPowerMultiplier : 1f);

            if (Data.maxHp <= 1) Data.maxHp = Mathf.Max(1, baseMaxHp);
            if (Data.attack <= 0) Data.attack = Mathf.Max(1, baseAttack);
            if (Data.defense <= 0) Data.defense = Mathf.Max(1, baseDefense);
            if (Data.speed <= 0.01f) Data.speed = Mathf.Max(0.01f, baseSpeed);
            if (Data.healPower <= 0f) Data.healPower = Mathf.Max(0f, baseHeal);
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

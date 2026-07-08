using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace ZulfarakRPG
{
    // Tracks which skills the player has learned/upgraded and how many points are free.
    // Points are earned 1 per character level (no mana in this game). Learned skills
    // auto-cast on their cooldown in the dungeon (see SkillAutoCaster). Persisted locally
    // in skills.json; independent of the server character schema.
    public class SkillManager : MonoBehaviour
    {
        public static SkillManager Instance { get; private set; }

        // How many points a node above must hold before the next node unlocks (Diablo-like gating).
        public const int PointsToUnlockNextNode = 2;

        // At most this many skills can be EQUIPPED (active) at once — only equipped
        // skills auto-cast and show a cooldown above the hero.
        public const int MaxEquipped = 2;

        public event Action OnSkillsChanged;

        [Serializable] class SaveData { public List<Entry> entries = new(); public List<string> equipped = new(); }
        [Serializable] class Entry { public string id; public int level; }

        readonly Dictionary<string, int> _levels = new();
        readonly List<string> _equipped = new();

        string SavePath => Path.Combine(Application.persistentDataPath, "skills.json");

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        // ── Queries ──────────────────────────────────────────────────────
        public int GetLevel(string id) => _levels.TryGetValue(id, out var l) ? l : 0;

        public int SpentPoints
        {
            get { int t = 0; foreach (var kv in _levels) t += kv.Value; return t; }
        }

        int PlayerLevel => PlayerManager.Instance?.Data?.level ?? 1;

        // One point per character level.
        public int TotalPoints     => Mathf.Max(0, PlayerLevel);
        public int AvailablePoints => Mathf.Max(0, TotalPoints - SpentPoints);

        public int PointsInNode(int node)
        {
            int t = 0;
            foreach (var s in SkillDefs.InNode(node)) t += GetLevel(s.id);
            return t;
        }

        public bool IsNodeUnlocked(int node)
            => node <= 0 || PointsInNode(node - 1) >= PointsToUnlockNextNode;

        public bool CanLearn(string id)
        {
            var def = SkillDefs.Get(id);
            if (def == null) return false;
            if (AvailablePoints <= 0) return false;
            if (GetLevel(id) >= def.maxLevel) return false;
            if (!IsNodeUnlocked(def.node)) return false;
            return true;
        }

        // ── Mutations ────────────────────────────────────────────────────
        public bool Learn(string id)
        {
            if (!CanLearn(id)) return false;
            _levels[id] = GetLevel(id) + 1;
            Save();
            OnSkillsChanged?.Invoke();
            return true;
        }

        // Enumerates every learned skill (level > 0) with its definition.
        public IEnumerable<(SkillDef def, int level)> Learned()
        {
            foreach (var s in SkillDefs.All)
            {
                int l = GetLevel(s.id);
                if (l > 0) yield return (s, l);
            }
        }

        // ── Equip (max 2) ────────────────────────────────────────────────
        public int EquippedCount => _equipped.Count;
        public bool IsEquipped(string id) => _equipped.Contains(id);

        // Equipping/unequipping an equipped skill toggles it; equipping a new one when
        // both slots are full drops the OLDEST so a click always does something.
        public void ToggleEquip(string id)
        {
            if (GetLevel(id) <= 0) return;                 // must be learned first
            if (_equipped.Contains(id)) _equipped.Remove(id);
            else
            {
                if (_equipped.Count >= MaxEquipped) _equipped.RemoveAt(0);
                _equipped.Add(id);
            }
            Save();
            OnSkillsChanged?.Invoke();
        }

        // Equipped skills that are still learned, in slot order.
        public IEnumerable<(SkillDef def, int level)> Equipped()
        {
            foreach (var id in _equipped)
            {
                int l = GetLevel(id);
                var def = SkillDefs.Get(id);
                if (def != null && l > 0) yield return (def, l);
            }
        }

        // ── Persistence ──────────────────────────────────────────────────
        void Load()
        {
            _levels.Clear();
            _equipped.Clear();
            try
            {
                if (!File.Exists(SavePath)) return;
                var data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(SavePath));
                if (data == null) return;
                if (data.entries != null)
                    foreach (var e in data.entries)
                        if (!string.IsNullOrEmpty(e.id) && e.level > 0) _levels[e.id] = e.level;
                if (data.equipped != null)
                    foreach (var id in data.equipped)
                        if (GetLevel(id) > 0 && !_equipped.Contains(id) && _equipped.Count < MaxEquipped)
                            _equipped.Add(id);
            }
            catch (Exception ex) { Debug.LogWarning($"[SkillManager] Load falhou: {ex.Message}"); }
        }

        void Save()
        {
            try
            {
                var data = new SaveData();
                foreach (var kv in _levels) data.entries.Add(new Entry { id = kv.Key, level = kv.Value });
                data.equipped.AddRange(_equipped);
                File.WriteAllText(SavePath, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception ex) { Debug.LogWarning($"[SkillManager] Save falhou: {ex.Message}"); }
        }

        // Bootstraps the singleton if the scene didn't place one.
        public static SkillManager Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("SkillManager");
            return go.AddComponent<SkillManager>();
        }
    }
}

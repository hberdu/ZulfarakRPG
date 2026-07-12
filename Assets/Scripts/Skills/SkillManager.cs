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

        // The class the learned/equipped lists were last validated against. When it changes
        // (e.g. the player switched mago→arqueiro) foreign-class skills are pruned. Null so
        // the first frame with a known class always runs the check.
        ClassType? _syncedClass;

        string SavePath => Path.Combine(Application.persistentDataPath, "skills.json");

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        bool _serverSynced;

        // Keeps the skill set locked to the active class every frame (cheap: only does work
        // when the class actually changes), and pulls the authoritative skill state from the
        // server once, right after authentication.
        void Update()
        {
            EnsureClassSynced();
            // Sync once, but only after the class is known (points/skills are class-scoped).
            if (!_serverSynced
                && ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady
                && PlayerManager.Instance != null && PlayerManager.Instance.Data != null)
            {
                _serverSynced = true;
                SyncWithServer();
            }
        }

        // ── Queries ──────────────────────────────────────────────────────
        public int GetLevel(string id) => _levels.TryGetValue(id, out var l) ? l : 0;

        public int SpentPoints
        {
            get
            {
                EnsureClassSynced();   // points spent on a former class must not count here
                int t = 0; foreach (var kv in _levels) t += kv.Value; return t;
            }
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
            if (def.cls != SkillDefs.CurrentClass) return false;   // only your own specialty
            if (AvailablePoints <= 0) return false;
            if (GetLevel(id) >= def.maxLevel) return false;
            if (!IsNodeUnlocked(def.node)) return false;
            return true;
        }

        // ── Mutations ────────────────────────────────────────────────────
        public bool Learn(string id)
        {
            if (!CanLearn(id)) return false;
            bool wasNew = GetLevel(id) == 0;
            _levels[id] = GetLevel(id) + 1;
            // Auto-equip a newly-learned skill while a slot is free, so its cooldown bar
            // shows immediately in the dungeon.
            if (wasNew && !_equipped.Contains(id) && _equipped.Count < MaxEquipped)
                _equipped.Add(id);
            Save();
            OnSkillsChanged?.Invoke();
            ConfirmLearnWithServer(id, wasNew);
            return true;
        }

        // Confirms the optimistic learn against the AUTHORITATIVE server. On rejection the local
        // level is rolled back so the server stays the source of truth for progression. Offline
        // (server not ready) the optimistic value stands until the next SyncWithServer.
        async void ConfirmLearnWithServer(string id, bool wasNew)
        {
            var api = ServerApiClient.Instance;
            if (api == null || !api.IsReady) return;
            try
            {
                var state = await api.LevelUpSkillAsync(id);
                if (state != null) ApplyServerState(state);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SkillManager] Servidor recusou upar '{id}': {ex.Message}. Revertendo.");
                int lvl = GetLevel(id);
                if (lvl <= 1) { _levels.Remove(id); if (wasNew) _equipped.Remove(id); }
                else _levels[id] = lvl - 1;
                Save();
                OnSkillsChanged?.Invoke();
            }
        }

        // Pushes local levels to the server (merge) and adopts the authoritative result. Never
        // wipes progress: the server only raises levels within the derived point budget, so this
        // both migrates a pre-existing local save and converges multiple devices.
        async void SyncWithServer()
        {
            var api = ServerApiClient.Instance;
            if (api == null || !api.IsReady) return;
            try
            {
                var local = new List<CharacterSkillDto>();
                foreach (var kv in _levels)
                    if (kv.Value > 0) local.Add(new CharacterSkillDto { skillId = kv.Key, level = kv.Value });
                var state = await api.MergeSkillsAsync(local.ToArray());
                if (state != null) ApplyServerState(state);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SkillManager] Sync de skills com o servidor falhou: {ex.Message}");
                _serverSynced = false;   // let it retry
            }
        }

        // Adopts the server's authoritative learned-skill levels as the source of truth. Keeps the
        // local equip loadout, dropping any equipped skill no longer learned and backfilling.
        void ApplyServerState(SkillsStateDto state)
        {
            if (state == null || state.skills == null) return;
            _levels.Clear();
            foreach (var s in state.skills)
            {
                if (string.IsNullOrEmpty(s.skillId) || s.level <= 0) continue;
                // Only keep this class's skills locally so spent-points match the server's
                // class-scoped budget. Other-class skills stay stored server-side, ignored here.
                var def = SkillDefs.Get(s.skillId);
                if (def == null || def.cls != SkillDefs.CurrentClass) continue;
                _levels[s.skillId] = s.level;
            }

            _equipped.RemoveAll(eid => GetLevel(eid) <= 0);
            if (_equipped.Count == 0)
                foreach (var kv in _levels)
                {
                    if (_equipped.Count >= MaxEquipped) break;
                    if (kv.Value > 0 && SkillDefs.Get(kv.Key) != null) _equipped.Add(kv.Key);
                }
            Save();
            OnSkillsChanged?.Invoke();
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
            EnsureClassSynced();   // never auto-cast a skill from a class the player left
            foreach (var id in _equipped)
            {
                int l = GetLevel(id);
                var def = SkillDefs.Get(id);
                if (def != null && l > 0) yield return (def, l);
            }
        }

        // ── Class specialty guard ────────────────────────────────────────
        // A character may only keep skills of its OWN class. Switching class (mago→arqueiro)
        // used to leave the old spells learned AND equipped — so mage spells kept auto-casting
        // and their spent points starved the archer's tree. Whenever the active class changes
        // we drop every skill that doesn't belong to it; the freed points return to the pool
        // automatically (AvailablePoints = level − points spent on THIS class's skills).
        void EnsureClassSynced()
        {
            var pm = PlayerManager.Instance;
            if (pm == null || pm.Data == null) return;   // class unknown yet — never prune blindly
            var cls = pm.Data.classType;
            if (_syncedClass == cls) return;
            _syncedClass = cls;
            if (PruneForeignSkills(cls))
            {
                Save();
                OnSkillsChanged?.Invoke();
            }
        }

        // Removes learned/equipped skills that aren't part of `cls`, then backfills any free
        // equip slot with the class's own learned skills. Returns true if anything changed.
        bool PruneForeignSkills(ClassType cls)
        {
            bool changed = false;

            var stale = new List<string>();
            foreach (var kv in _levels)
            {
                var def = SkillDefs.Get(kv.Key);
                if (def == null || def.cls != cls) stale.Add(kv.Key);
            }
            foreach (var id in stale) { _levels.Remove(id); changed = true; }

            for (int i = _equipped.Count - 1; i >= 0; i--)
            {
                var def = SkillDefs.Get(_equipped[i]);
                if (def == null || def.cls != cls || GetLevel(_equipped[i]) <= 0)
                { _equipped.RemoveAt(i); changed = true; }
            }

            // Fill any slot freed by pruning with this class's remaining learned skills.
            if (_equipped.Count < MaxEquipped)
                foreach (var kv in _levels)
                {
                    if (_equipped.Count >= MaxEquipped) break;
                    if (kv.Value > 0 && !_equipped.Contains(kv.Key)) { _equipped.Add(kv.Key); changed = true; }
                }

            return changed;
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

                // Migrate renamed archer skills so invested points aren't lost:
                //   a_veneno  → a_serpe        (Tiro de Serpe, the venom shot)
                //   a_certeiro→ a_concentrado  (Tiro Concentrado, the charged shot)
                MigrateSkill("a_veneno",   "a_serpe");
                MigrateSkill("a_certeiro", "a_concentrado");

                if (data.equipped != null)
                    foreach (var id in data.equipped)
                        if (GetLevel(id) > 0 && !_equipped.Contains(id) && _equipped.Count < MaxEquipped)
                            _equipped.Add(id);
            }
            catch (Exception ex) { Debug.LogWarning($"[SkillManager] Load falhou: {ex.Message}"); }

            // If skills were learned but none equipped (older saves), auto-equip up to the
            // cap so the cooldown bars appear.
            if (_equipped.Count == 0)
                foreach (var kv in _levels)
                {
                    if (_equipped.Count >= MaxEquipped) break;
                    if (kv.Value > 0 && SkillDefs.Get(kv.Key) != null) _equipped.Add(kv.Key);
                }
        }

        // Moves a learned level from an old (removed) skill id onto its replacement, so a
        // save made before the archer skills were renamed keeps its points.
        void MigrateSkill(string oldId, string newId)
        {
            if (!_levels.TryGetValue(oldId, out var lvl) || lvl <= 0) return;
            _levels[newId] = Mathf.Max(GetLevel(newId), lvl);
            _levels.Remove(oldId);
            for (int i = 0; i < _equipped.Count; i++)
                if (_equipped[i] == oldId) _equipped[i] = newId;
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

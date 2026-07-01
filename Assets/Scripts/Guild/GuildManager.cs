using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;

namespace ZulfarakRPG
{
    public class GuildManager : MonoBehaviour
    {
        public static GuildManager Instance { get; private set; }

        public Guild CurrentGuild { get; private set; }

        public event Action<Guild> OnGuildCreated;
        public event Action<Guild> OnGuildJoined;
        public event Action OnGuildLeft;

        private string SavePath => Path.Combine(Application.persistentDataPath, "guild.json");

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool CreateGuild(string guildName)
        {
            var player = PlayerManager.Instance.Data;
            if (!string.IsNullOrEmpty(player.guildId)) return false;

            CurrentGuild = new Guild
            {
                guildId = Guid.NewGuid().ToString(),
                guildName = guildName,
                leaderSteamId = player.steamId
            };
            CurrentGuild.AddMember(player.steamId);
            player.guildId = CurrentGuild.guildId;
            player.isGuildLeader = true;

            Save();
            PlayerManager.Instance.Save();
            OnGuildCreated?.Invoke(CurrentGuild);
            return true;
        }

        // Called by network layer when server sends guild data
        public void ReceiveGuildData(Guild guild)
        {
            CurrentGuild = guild;
            PlayerManager.Instance.Data.guildId = guild.guildId;
            PlayerManager.Instance.Save();
            OnGuildJoined?.Invoke(guild);
        }

        public void LeaveGuild()
        {
            if (CurrentGuild == null) return;
            var player = PlayerManager.Instance.Data;
            CurrentGuild.RemoveMember(player.steamId);
            player.guildId = null;
            player.isGuildLeader = false;
            CurrentGuild = null;
            PlayerManager.Instance.Save();
            OnGuildLeft?.Invoke();
        }

        private void Save()
        {
            File.WriteAllText(SavePath, JsonConvert.SerializeObject(CurrentGuild, Formatting.Indented));
        }

        public void Load()
        {
            if (!File.Exists(SavePath)) return;
            CurrentGuild = JsonConvert.DeserializeObject<Guild>(File.ReadAllText(SavePath));
        }
    }
}

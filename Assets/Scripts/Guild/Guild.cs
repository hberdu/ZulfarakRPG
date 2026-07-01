using System;
using System.Collections.Generic;

namespace ZulfarakRPG
{
    [Serializable]
    public class Guild
    {
        public string guildId;
        public string guildName;
        public string leaderSteamId;
        public List<string> memberSteamIds = new List<string>();
        public int maxMembers = 5;

        public bool IsFull => memberSteamIds.Count >= maxMembers;
        public bool IsLeader(string steamId) => leaderSteamId == steamId;

        public bool AddMember(string steamId)
        {
            if (IsFull || memberSteamIds.Contains(steamId)) return false;
            memberSteamIds.Add(steamId);
            return true;
        }

        public bool RemoveMember(string steamId)
        {
            return memberSteamIds.Remove(steamId);
        }
    }
}

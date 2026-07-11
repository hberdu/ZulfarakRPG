using System;
using System.Collections;
using UnityEngine;

namespace ZulfarakRPG
{
    // Guilds live on the server now. This manager is a thin REST client: it mirrors the
    // authoritative guild into CurrentGuild (for the UI) and never persists guilds locally.
    public class GuildManager : MonoBehaviour
    {
        public static GuildManager Instance { get; private set; }

        public Guild CurrentGuild { get; private set; }

        public event Action<Guild> OnGuildCreated;
        public event Action<Guild> OnGuildJoined;
        public event Action OnGuildLeft;
        public event Action<string> OnGuildError;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private static bool ServerReady =>
            ServerApiClient.Instance != null && ServerApiClient.Instance.IsReady;

        // Kicks off guild creation. Returns false only for a local pre-check failure; server
        // errors (e.g. name taken) arrive asynchronously via OnGuildError.
        public bool CreateGuild(string guildName)
        {
            var player = PlayerManager.Instance?.Data;
            if (player != null && !string.IsNullOrEmpty(player.guildId)) return false;
            if (!ServerReady) { OnGuildError?.Invoke("Servidor indisponível."); return false; }

            StartCoroutine(CreateRoutine(guildName));
            return true;
        }

        private IEnumerator CreateRoutine(string guildName)
        {
            yield return AwaitTask(
                ServerApiClient.Instance.CreateGuildAsync(guildName),
                dto =>
                {
                    ApplyGuild(dto);
                    if (CurrentGuild != null) OnGuildCreated?.Invoke(CurrentGuild);
                },
                err => OnGuildError?.Invoke(err));
        }

        public void JoinGuild(string guildId)
        {
            if (!ServerReady) { OnGuildError?.Invoke("Servidor indisponível."); return; }
            StartCoroutine(JoinRoutine(guildId));
        }

        private IEnumerator JoinRoutine(string guildId)
        {
            yield return AwaitTask(
                ServerApiClient.Instance.JoinGuildAsync(guildId),
                dto =>
                {
                    ApplyGuild(dto);
                    if (CurrentGuild != null) OnGuildJoined?.Invoke(CurrentGuild);
                },
                err => OnGuildError?.Invoke(err));
        }

        public void LeaveGuild()
        {
            if (!ServerReady)
            {
                // Best-effort local clear so the UI updates even offline.
                ClearGuild();
                OnGuildLeft?.Invoke();
                return;
            }
            StartCoroutine(LeaveRoutine());
        }

        private IEnumerator LeaveRoutine()
        {
            yield return AwaitTask(
                ServerApiClient.Instance.LeaveGuildAsync(),
                () => { ClearGuild(); OnGuildLeft?.Invoke(); },
                err => OnGuildError?.Invoke(err));
        }

        // Fetches the authoritative guild for the logged-in player.
        public void Load()
        {
            if (!ServerReady) return;
            StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            yield return AwaitTask(
                ServerApiClient.Instance.GetMyGuildAsync(),
                dto =>
                {
                    ApplyGuild(dto);
                    if (CurrentGuild != null) OnGuildJoined?.Invoke(CurrentGuild);
                    else OnGuildLeft?.Invoke();
                },
                err => Debug.LogWarning($"[GuildManager] Load falhou: {err}"));
        }

        // Kept for source compatibility with the (retired) WebSocket path.
        public void ReceiveGuildData(Guild guild)
        {
            CurrentGuild = guild;
            if (guild != null && PlayerManager.Instance?.Data != null)
                PlayerManager.Instance.Data.guildId = guild.guildId;
            OnGuildJoined?.Invoke(guild);
        }

        private void ApplyGuild(GuildDto dto)
        {
            CurrentGuild = FromDto(dto);
            var player = PlayerManager.Instance?.Data;
            if (player != null)
            {
                player.guildId = CurrentGuild?.guildId;
                player.isGuildLeader = CurrentGuild != null && CurrentGuild.IsLeader(player.steamId);
            }
        }

        private void ClearGuild()
        {
            CurrentGuild = null;
            var player = PlayerManager.Instance?.Data;
            if (player != null) { player.guildId = null; player.isGuildLeader = false; }
        }

        private static Guild FromDto(GuildDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.id)) return null;
            var g = new Guild
            {
                guildId = dto.id,
                guildName = dto.name,
                leaderSteamId = dto.leaderSteamId,
                maxMembers = dto.maxMembers > 0 ? dto.maxMembers : 5
            };
            if (dto.members != null)
                foreach (var m in dto.members)
                    if (m != null && !string.IsNullOrEmpty(m.steamId)) g.memberSteamIds.Add(m.steamId);
            return g;
        }

        // ── Task→coroutine helpers (never block the main thread) ─────────────
        private static IEnumerator AwaitTask<T>(System.Threading.Tasks.Task<T> task, Action<T> onOk, Action<string> onErr)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted || task.IsCanceled) { onErr?.Invoke(task.Exception?.GetBaseException()?.Message ?? "Falha na requisição."); yield break; }
            onOk?.Invoke(task.Result);
        }

        private static IEnumerator AwaitTask(System.Threading.Tasks.Task task, Action onOk, Action<string> onErr)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted || task.IsCanceled) { onErr?.Invoke(task.Exception?.GetBaseException()?.Message ?? "Falha na requisição."); yield break; }
            onOk?.Invoke();
        }
    }
}

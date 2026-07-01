using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

namespace ZulfarakRPG
{
    // WebSocket client using System.Net.WebSockets (built into .NET, no extra package needed).
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Server")]
        public string serverUrl = "ws://localhost:3000";

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<NetworkMessage> OnMessageReceived;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly Queue<string> _pending = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Connect() => _ = ConnectAsync();

        private async Task ConnectAsync()
        {
            _cts = new CancellationTokenSource();
            _ws  = new ClientWebSocket();
            try
            {
                await _ws.ConnectAsync(new Uri(serverUrl), _cts.Token);
                OnConnected?.Invoke();
                _ = ReceiveLoop(_cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Network] Não foi possível conectar: {e.Message}");
                OnDisconnected?.Invoke();
            }
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[4096];
            try
            {
                while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        OnDisconnected?.Invoke();
                        break;
                    }
                    string raw = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    lock (_pending) _pending.Enqueue(raw);
                }
            }
            catch { OnDisconnected?.Invoke(); }
        }

        private void Update()
        {
            lock (_pending)
            {
                while (_pending.Count > 0)
                {
                    string raw = _pending.Dequeue();
                    try
                    {
                        var msg = JsonConvert.DeserializeObject<NetworkMessage>(raw);
                        HandleMessage(msg);
                        OnMessageReceived?.Invoke(msg);
                    }
                    catch (Exception e) { Debug.LogWarning($"[Network] {e.Message}"); }
                }
            }
        }

        private void HandleMessage(NetworkMessage msg)
        {
            string payload = msg.payload ?? "{}";
            switch (msg.type)
            {
                case "guild_data":
                    GuildManager.Instance.ReceiveGuildData(JsonConvert.DeserializeObject<Guild>(payload));
                    break;
                case "guild_mission_start":
                    var gmp = JsonConvert.DeserializeObject<GuildMissionPayload>(payload);
                    var mission = Array.Find(MissionManager.Instance.allMissions, m => m.missionId == gmp.missionId);
                    if (mission != null) MissionManager.Instance.StartGuildMission(mission, gmp.partyMembers);
                    break;
                case "lobby_update":
                    var lp = JsonConvert.DeserializeObject<LobbyUpdatePayload>(payload);
                    LobbyManager.Instance.ReceiveLobbyUpdate(lp.readyIds);
                    break;
                case "error":
                    var ep = JsonConvert.DeserializeObject<ErrorPayload>(payload);
                    Debug.LogWarning($"[Server] {ep?.message}");
                    break;
            }
        }

        public void Send(string type, object payload) => _ = SendAsync(type, payload);

        private async Task SendAsync(string type, object payload)
        {
            if (!IsConnected) return;
            var msg  = new NetworkMessage { type = type, payload = JsonConvert.SerializeObject(payload) };
            var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(msg));
            await _ws.SendAsync(new ArraySegment<byte>(json), WebSocketMessageType.Text, true, _cts.Token);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _ws?.Dispose();
        }
    }

    [Serializable] public class NetworkMessage      { public string type; public string payload; }
    [Serializable] public class GuildMissionPayload { public string missionId; public List<PlayerData> partyMembers; }
    [Serializable] public class LobbyUpdatePayload  { public string missionId; public List<string> readyIds; }
    [Serializable] public class ErrorPayload        { public string message; }
}

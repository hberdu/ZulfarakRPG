using System;
using System.Runtime.InteropServices;
using UnityEngine;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace ZulfarakRPG
{
    // Thin wrapper over SteamNetworkingMessages — byte-array send + Update-tick
    // poll. The high-level multiplayer protocol (MultiplayerSync) sits on top.
    //
    // Sessions are auto-accepted from any peer that is currently in our
    // SteamLobbyManager lobby; everyone else is ignored. Channel 0 is used
    // for all game traffic.
    public class SteamP2P : MonoBehaviour
    {
        public static SteamP2P Instance { get; private set; }

        public const int  ChannelMain          = 0;
        public const int  SendFlagsReliable    = 8;   // k_nSteamNetworkingSend_Reliable
        public const int  SendFlagsUnreliable  = 0;   // k_nSteamNetworkingSend_Unreliable

#if STEAMWORKS_NET
        public event Action<CSteamID, byte[]> OnMessage;
        Callback<SteamNetworkingMessagesSessionRequest_t> _cbSessionReq;
        readonly IntPtr[] _recvBuf = new IntPtr[32];
#else
        public event Action<string, byte[]> OnMessage;   // (senderSteamId, payload)
#endif

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if STEAMWORKS_NET
            if (SteamIntegration.Instance != null && SteamIntegration.Instance.IsInitialized)
                _cbSessionReq = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
#endif
        }

        // Sends payload to one lobby member. Reliable by default — the protocol
        // is small + at 10 Hz, so the slight ordering cost is fine.
        public void SendTo(string steamIdStr, byte[] payload, int sendFlags = SendFlagsReliable)
        {
#if STEAMWORKS_NET
            if (string.IsNullOrEmpty(steamIdStr) || payload == null || payload.Length == 0) return;
            if (!ulong.TryParse(steamIdStr, out ulong raw)) return;

            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(raw));

            IntPtr buf = Marshal.AllocHGlobal(payload.Length);
            try
            {
                Marshal.Copy(payload, 0, buf, payload.Length);
                SteamNetworkingMessages.SendMessageToUser(
                    ref identity, buf, (uint)payload.Length, sendFlags, ChannelMain);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
#endif
        }

        void Update()
        {
#if STEAMWORKS_NET
            if (SteamIntegration.Instance == null || !SteamIntegration.Instance.IsInitialized) return;

            int n = SteamNetworkingMessages.ReceiveMessagesOnChannel(ChannelMain, _recvBuf, _recvBuf.Length);
            for (int i = 0; i < n; i++)
            {
                IntPtr p = _recvBuf[i];
                if (p == IntPtr.Zero) continue;
                try
                {
                    var msg = (SteamNetworkingMessage_t)Marshal.PtrToStructure(
                        p, typeof(SteamNetworkingMessage_t));
                    if (msg.m_pData != IntPtr.Zero && msg.m_cbSize > 0)
                    {
                        var data = new byte[msg.m_cbSize];
                        Marshal.Copy(msg.m_pData, data, 0, msg.m_cbSize);
                        var sender = msg.m_identityPeer.GetSteamID();
                        OnMessage?.Invoke(sender, data);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SteamP2P] recv failed: {e.Message}");
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(p);
                }
            }
#endif
        }

#if STEAMWORKS_NET
        void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t r)
        {
            // Only auto-accept sessions from members of our current lobby.
            var senderId = r.m_identityRemote.GetSteamID();
            var lobby    = SteamLobbyManager.Instance;
            if (lobby == null || !lobby.MemberSteamIds.Contains(senderId.ToString()))
                return;

            var id = r.m_identityRemote;
            SteamNetworkingMessages.AcceptSessionWithUser(ref id);
        }
#endif
    }
}

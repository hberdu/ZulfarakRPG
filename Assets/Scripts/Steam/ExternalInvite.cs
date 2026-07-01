using UnityEngine;

namespace ZulfarakRPG
{
    // Invite path that does NOT require Steam's $100 partner fee. Instead of a Steam
    // overlay invite, the host shares a plain text invite — a DOWNLOAD link plus a
    // LOBBY JOIN CODE — through any channel (WhatsApp, Discord, …). A friend who
    // doesn't own the game downloads it from the link, opens it, and joins the host's
    // lobby by entering the code (routed through the game server, not Steam).
    //
    // This keeps all the "outside Steam" wiring in one place so the UI just calls
    // ExternalInvite.CopyToClipboard() / ExternalInvite.JoinByCode(code).
    public static class ExternalInvite
    {
        // Where a friend without the game downloads it. Point this at your itch.io /
        // Google Drive / website build link.
        public static string DownloadUrl = "https://SEU-LINK-DE-DOWNLOAD-AQUI";

        // A short code the friend types in to join the host's lobby. Prefers the real
        // lobby id; otherwise a stable per-host code derived from the Steam id.
        public static string JoinCode
        {
            get
            {
                var lobby = SteamLobbyManager.Instance;
                if (lobby != null && !string.IsNullOrEmpty(lobby.LobbyIdString) &&
                    lobby.LobbyIdString != "stub_lobby")
                    return lobby.LobbyIdString;

                var id = SteamIntegration.Instance?.SteamId;
                return ShortCode(string.IsNullOrEmpty(id) ? System.Guid.NewGuid().ToString() : id);
            }
        }

        static string ShortCode(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s) { h ^= c; h *= 16777619u; }
            return h.ToString("X8").Substring(0, 6);   // 6-char hex code
        }

        public static string BuildInviteText() =>
            "Venha jogar Zulfarak RPG comigo!\n" +
            $"1) Baixe o jogo: {DownloadUrl}\n" +
            $"2) Abra o jogo e entre no lobby com o codigo: {JoinCode}";

        // Copies the shareable invite to the OS clipboard so the host can paste it
        // anywhere. Works in editor and build.
        public static void CopyToClipboard()
        {
            GUIUtility.systemCopyBuffer = BuildInviteText();
            Debug.Log("[ExternalInvite] convite copiado para a area de transferencia:\n" + BuildInviteText());
        }

        // A friend WHO HAS the game joins the host's lobby by code via the game server
        // (no Steam needed). Requires the server to handle the "lobby_join_code" op —
        // the client side is wired here so the UI/flow is ready.
        public static void JoinByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            NetworkManager.Instance?.Send("lobby_join_code", new { code = code.Trim() });
            Debug.Log($"[ExternalInvite] solicitando join no lobby por codigo: {code.Trim()}");
        }
    }
}

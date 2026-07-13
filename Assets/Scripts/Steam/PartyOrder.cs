using System.Collections.Generic;

namespace ZulfarakRPG
{
    // The party's AGGRO ORDER: enemies focus the FIRST living player in this list (the "tank"),
    // and the rest are safe until the ones ahead fall. Editable by dragging the party-frame
    // portraits (numbered 1..N), synced to every client so all agree on who monsters chase.
    public static class PartyOrder
    {
        static readonly List<string> _order = new List<string>();

        // The order reconciled with live lobby membership: keeps the drag-set sequence, drops anyone
        // who left, and appends newcomers at the back (in lobby order). Solo → just the local id.
        public static List<string> Get()
        {
            var lm = SteamLobbyManager.Instance;
            var members = lm != null ? lm.MemberSteamIds : null;
            var result = new List<string>();
            if (members == null || members.Count == 0)
            {
                var me = SteamIntegration.Instance?.SteamId;
                if (!string.IsNullOrEmpty(me)) result.Add(me);
                return result;
            }
            foreach (var id in _order)
                if (members.Contains(id) && !result.Contains(id)) result.Add(id);
            foreach (var id in members)
                if (!result.Contains(id)) result.Add(id);
            return result;
        }

        // Set locally + tell every other client (so enemy focus matches on all screens).
        public static void Set(List<string> ids)
        {
            Assign(ids);
            MultiplayerSync.Instance?.BroadcastPartyOrder(_order);
        }

        // Adopt an order received from a partner (no re-broadcast).
        public static void Receive(IEnumerable<string> ids) => Assign(ids);

        // Position of a member in the reconciled aggro order (0 = #1 tank / front), or -1.
        public static int IndexOf(string id) => Get().IndexOf(id);

        static void Assign(IEnumerable<string> ids)
        {
            _order.Clear();
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id) && !_order.Contains(id)) _order.Add(id);
        }
    }
}

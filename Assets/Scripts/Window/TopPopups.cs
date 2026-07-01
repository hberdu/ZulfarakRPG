namespace ZulfarakRPG
{
    // Central coordinator for the top-of-screen modal popups (the native windows
    // that float ABOVE the game strip). Only ONE may be open at a time, exactly
    // like the NPC dialog: opening any of them closes the others.
    //
    // Kinds:
    //   Npc    → MenuPopupWindow   (Kael, Ferreiro, class masters)
    //   Map    → WorldMapPopup     (world map)
    //   Invite → FriendsListPopup  (Steam invite / lobby)
    public static class TopPopups
    {
        public enum Kind { None, Npc, Map, Invite }

        // Closes every top popup except the one identified by `keep`. Call this at
        // the very start of each popup's Show() so it replaces whatever was open.
        public static void CloseAllExcept(Kind keep)
        {
            if (keep != Kind.Npc)    MenuPopupWindow.Hide();
            if (keep != Kind.Map)    WorldMapPopup.Hide();
            if (keep != Kind.Invite) FriendsListPopup.Hide();
        }
    }
}

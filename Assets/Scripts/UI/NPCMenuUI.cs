namespace ZulfarakRPG
{
    // Routes NPC dialog text into MenuPopupWindow, a separate native top-level
    // window that opens above the game strip — like a new tab. The main game
    // window keeps its size and position untouched (no expand/restore).
    public static class NPCMenuUI
    {
        public static void Show(string title, string body) => MenuPopupWindow.Show(title, body);
        public static void Hide()                          => MenuPopupWindow.Hide();
    }
}

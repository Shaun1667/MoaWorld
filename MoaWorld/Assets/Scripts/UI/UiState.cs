namespace MoaWorld
{
    // Local-only UI state. While a menu is open the cursor is free and gameplay input is ignored.
    public static class UiState
    {
        public static bool IsMenuOpen { get; set; }
    }
}

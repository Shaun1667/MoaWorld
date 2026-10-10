using UnityEngine;

namespace MoaWorld
{
    // Local-only UI state. While a menu is open the cursor is free and gameplay input is ignored.
    public static class UiState
    {
        private static bool isMenuOpen;

        public static bool IsMenuOpen
        {
            get => isMenuOpen;
            set
            {
                isMenuOpen = value;
                ChangedFrame = Time.frameCount;
            }
        }

        // Frame of the last open/close, so one key press (e.g. Esc) cannot close one menu and open another.
        public static int ChangedFrame { get; private set; } = -1;
    }
}

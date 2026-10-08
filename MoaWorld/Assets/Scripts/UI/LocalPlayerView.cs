using UnityEngine;

namespace MoaWorld
{
    // Base for camera/HUD pieces that follow the local player. Bind runs when the local player
    // spawns (or immediately if it already exists); Unbind runs when it goes away.
    public abstract class LocalPlayerView : MonoBehaviour
    {
        protected GameObject Player { get; private set; }
        protected bool HasPlayer => Player != null;

        protected virtual void OnEnable()
        {
            LocalPlayer.Changed += HandlePlayerChanged;
            HandlePlayerChanged(LocalPlayer.Current);
        }

        protected virtual void OnDisable()
        {
            LocalPlayer.Changed -= HandlePlayerChanged;
            HandlePlayerChanged(null);
        }

        private void HandlePlayerChanged(GameObject player)
        {
            if (Player == player)
            {
                return;
            }
            if (Player != null)
            {
                Unbind();
            }
            Player = player;
            if (player != null)
            {
                Bind(player);
            }
        }

        protected abstract void Bind(GameObject player);
        protected virtual void Unbind() { }
    }
}

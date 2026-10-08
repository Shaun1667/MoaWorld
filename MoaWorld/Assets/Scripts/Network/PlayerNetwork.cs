using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Networked player root. The owner's copy becomes the local player; other copies
    // turn off input-driven components and just mirror the owner's movement.
    public class PlayerNetwork : NetworkBehaviour
    {
        [SerializeField] private Behaviour[] ownerOnlyComponents;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                LocalPlayer.Set(gameObject);
                return;
            }
            foreach (Behaviour component in ownerOnlyComponents)
            {
                component.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                LocalPlayer.Clear(gameObject);
            }
        }
    }
}

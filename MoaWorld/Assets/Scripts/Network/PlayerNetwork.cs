using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Networked player root. The owner's copy becomes the local player; other copies
    // turn off input-driven components and just mirror the owner's movement.
    // On the server it also loads and stores the player's saved data by unique player ID.
    public class PlayerNetwork : NetworkBehaviour
    {
        private static readonly List<PlayerNetwork> spawned = new List<PlayerNetwork>();

        [SerializeField] private Behaviour[] ownerOnlyComponents;

        // Server only: players currently in the world.
        public static IReadOnlyList<PlayerNetwork> Spawned => spawned;

        // Server only.
        public string PlayerId { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            spawned.Clear();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                spawned.Add(this);
                PlayerId = GameSession.Instance != null ? GameSession.Instance.GetPlayerId(OwnerClientId) : null;
                LoadSaveData();
            }

            if (IsOwner)
            {
                // Movement is owner-authoritative: placing ourselves at the spawn point makes sure the
                // server's copy starts there too instead of waiting for our first move.
                if (MoaBox.Instance != null)
                {
                    GetComponent<PlayerMovement>().Teleport(MoaBox.Instance.SpawnPosition);
                }
                LocalPlayer.Set(gameObject);
                RequestFullSyncRpc();
                return;
            }
            foreach (Behaviour component in ownerOnlyComponents)
            {
                component.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                spawned.Remove(this);
                if (WorldSave.Instance != null)
                {
                    WorldSave.Instance.Store(this);
                    if (!NetworkManager.ShutdownInProgress)
                    {
                        WorldSave.Instance.SaveNow();
                    }
                }
            }
            if (IsOwner)
            {
                LocalPlayer.Clear(gameObject);
            }
        }

        // The owner asks once it is spawned, so the data never arrives before the client is ready.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestFullSyncRpc()
        {
            GetComponent<PlayerParty>().SendFull();
            GetComponent<PlayerMoaBox>().SendFull();
        }

        private void LoadSaveData()
        {
            PlayerSaveData data = WorldSave.Instance != null && PlayerId != null ? WorldSave.Instance.Find(PlayerId) : null;
            if (data == null)
            {
                Debug.Log($"[Save] New player {PlayerId}");
                return;
            }
            GetComponent<PlayerInventory>().ReadSave(data);
            GetComponent<PlayerHealth>().ReadSave(data);
            GetComponent<PlayerParty>().ReadSave(data);
            GetComponent<PlayerMoaBox>().ReadSave(data);
            Debug.Log($"[Save] Loaded player {PlayerId}");
        }

        // Server only.
        public PlayerSaveData CreateSaveData()
        {
            var data = new PlayerSaveData { playerId = PlayerId };
            GetComponent<PlayerInventory>().WriteSave(data);
            GetComponent<PlayerHealth>().WriteSave(data);
            GetComponent<PlayerParty>().WriteSave(data);
            GetComponent<PlayerMoaBox>().WriteSave(data);
            return data;
        }
    }
}

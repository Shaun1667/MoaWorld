using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Personal Moa Box storage. A moa is fully healed the moment it is stored.
    // The server owns the list and sends the owning client small add/remove messages,
    // since a full box (900 moa) is too large to resend on every change.
    [RequireComponent(typeof(PlayerParty), typeof(PlayerCombat), typeof(PlayerNotifications))]
    public class PlayerMoaBox : NetworkBehaviour
    {
        private const int SyncChunkSize = 15;

        [Serializable]
        public class StoredMoa
        {
            public MoaInstance moa;
        }

        [Serializable]
        private class Chunk
        {
            public List<StoredMoa> items = new List<StoredMoa>();
        }

        private readonly List<StoredMoa> stored = new List<StoredMoa>();

        private PlayerParty party;
        private PlayerCombat combat;
        private PlayerNotifications notifications;
        private bool ownerReady;

        public IReadOnlyList<StoredMoa> Stored => stored;
        public int Capacity => GameConfig.Instance.storagePageSize * GameConfig.Instance.storagePageCount;
        public bool IsFull => stored.Count >= Capacity;
        public bool IsNearBox => MoaBox.Instance != null && MoaBox.Instance.IsInRange(transform.position);

        public event Action Changed;

        private void Awake()
        {
            party = GetComponent<PlayerParty>();
            combat = GetComponent<PlayerCombat>();
            notifications = GetComponent<PlayerNotifications>();
        }

        // Client requests. The server re-checks everything.

        public void Deposit(int partySlot)
        {
            DepositRpc(partySlot);
        }

        public void Withdraw(int index)
        {
            WithdrawRpc(index);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void DepositRpc(int partySlot)
        {
            MoaInstance moa = party.Get(partySlot);
            if (moa == null || !IsNearBox)
            {
                return;
            }
            if (IsFull)
            {
                notifications.Notify("모아 박스가 가득 찼어요");
                return;
            }

            combat.RecallIfActive(moa);
            party.Remove(moa);
            TryAdd(moa);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void WithdrawRpc(int index)
        {
            if (!IsNearBox || index < 0 || index >= stored.Count)
            {
                return;
            }
            if (party.IsFull)
            {
                notifications.Notify("파티가 가득 찼어요");
                return;
            }

            MoaInstance moa = stored[index].moa;
            stored.RemoveAt(index);
            SendToOwner(() => RemovedRpc(index));
            party.TryAdd(moa);
            Changed?.Invoke();
        }

        // Server only.
        public bool TryAdd(MoaInstance moa)
        {
            if (IsFull)
            {
                return false;
            }
            moa.currentHp = moa.MaxHp;
            var entry = new StoredMoa { moa = moa };
            stored.Add(entry);
            SendToOwner(() => AddedRpc(JsonUtility.ToJson(entry)));
            Changed?.Invoke();
            return true;
        }

        // Server: the owner asks for the whole box once it is ready to receive.
        public void SendFull()
        {
            ownerReady = true;
            if (IsOwner)
            {
                return;
            }
            ClearedRpc();
            for (int start = 0; start < stored.Count; start += SyncChunkSize)
            {
                var chunk = new Chunk { items = stored.GetRange(start, Mathf.Min(SyncChunkSize, stored.Count - start)) };
                AppendedRpc(JsonUtility.ToJson(chunk));
            }
        }

        // The host's own player already holds the real list, so nothing is sent to it.
        private void SendToOwner(Action send)
        {
            if (ownerReady && !IsOwner)
            {
                send();
            }
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void ClearedRpc()
        {
            stored.Clear();
            Changed?.Invoke();
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void AppendedRpc(string json)
        {
            stored.AddRange(JsonUtility.FromJson<Chunk>(json).items);
            Changed?.Invoke();
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void AddedRpc(string json)
        {
            stored.Add(JsonUtility.FromJson<StoredMoa>(json));
            Changed?.Invoke();
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void RemovedRpc(int index)
        {
            if (index >= 0 && index < stored.Count)
            {
                stored.RemoveAt(index);
                Changed?.Invoke();
            }
        }

        public void WriteSave(PlayerSaveData data)
        {
            data.box = new List<StoredMoa>(stored);
        }

        public void ReadSave(PlayerSaveData data)
        {
            stored.Clear();
            if (data.box != null)
            {
                foreach (StoredMoa entry in data.box)
                {
                    if (stored.Count < Capacity && entry?.moa != null && entry.moa.Species != null)
                    {
                        // Older saves could hold moa that were still waiting to heal.
                        entry.moa.currentHp = entry.moa.MaxHp;
                        stored.Add(entry);
                    }
                }
            }
            Changed?.Invoke();
        }
    }
}

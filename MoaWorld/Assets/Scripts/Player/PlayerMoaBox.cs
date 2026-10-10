using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Personal Moa Box storage. A moa fully heals once it has been stored for the configured time.
    // The server owns the list and sends the owning client small add/remove/update messages,
    // since a full box (900 moa) is too large to resend on every change.
    [RequireComponent(typeof(PlayerParty), typeof(PlayerCombat), typeof(PlayerNotifications))]
    public class PlayerMoaBox : NetworkBehaviour
    {
        private const int SyncChunkSize = 15;

        [Serializable]
        public class StoredMoa
        {
            public MoaInstance moa;
            public double storedAt; // world time in seconds
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

        private static double Now => WorldClock.Instance != null ? WorldClock.Instance.WorldTime : Time.timeAsDouble;
        private static double HealSeconds => GameConfig.Instance.moaBoxHealMinutes * 60.0;

        public event Action Changed;

        private void Awake()
        {
            party = GetComponent<PlayerParty>();
            combat = GetComponent<PlayerCombat>();
            notifications = GetComponent<PlayerNotifications>();
        }

        private void Update()
        {
            if (!IsServer)
            {
                return;
            }
            bool healedAny = false;
            for (int i = 0; i < stored.Count; i++)
            {
                StoredMoa entry = stored[i];
                if (entry.moa.currentHp < entry.moa.MaxHp && HealRemainingSeconds(entry) <= 0f)
                {
                    entry.moa.currentHp = entry.moa.MaxHp;
                    healedAny = true;
                    SendToOwner(() => UpdatedRpc(i, JsonUtility.ToJson(entry)));
                }
            }
            if (healedAny)
            {
                Changed?.Invoke();
            }
        }

        public float HealRemainingSeconds(StoredMoa entry)
        {
            return Mathf.Max(0f, (float)(HealSeconds - (Now - entry.storedAt)));
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
            var entry = new StoredMoa { moa = moa, storedAt = Now };
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

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void UpdatedRpc(int index, string json)
        {
            if (index >= 0 && index < stored.Count)
            {
                stored[index] = JsonUtility.FromJson<StoredMoa>(json);
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
                        stored.Add(entry);
                    }
                }
            }
            Changed?.Invoke();
        }
    }
}

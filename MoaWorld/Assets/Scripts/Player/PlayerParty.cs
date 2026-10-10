using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Up to six moa that can be summoned. The server owns the list; the owning client receives a copy
    // whenever anything in it changes (members, HP, level).
    public class PlayerParty : NetworkBehaviour
    {
        private const float SyncInterval = 0.2f;

        [Serializable]
        private class Snapshot
        {
            public List<MoaInstance> members = new List<MoaInstance>();
        }

        private readonly List<MoaInstance> members = new List<MoaInstance>();
        private string lastSentJson;
        private float nextSyncTime;
        private bool ownerReady;

        public IReadOnlyList<MoaInstance> Members => members;
        public bool IsFull => members.Count >= GameConfig.Instance.maxPartySize;

        public event Action Changed;

        private void Update()
        {
            if (!IsServer || IsOwner || !ownerReady || Time.unscaledTime < nextSyncTime)
            {
                return;
            }
            nextSyncTime = Time.unscaledTime + SyncInterval;
            SendIfChanged();
        }

        // Server only.
        public bool TryAdd(MoaInstance moa)
        {
            if (IsFull)
            {
                return false;
            }
            members.Add(moa);
            Changed?.Invoke();
            return true;
        }

        // Server only.
        public bool Remove(MoaInstance moa)
        {
            bool removed = members.Remove(moa);
            if (removed)
            {
                Changed?.Invoke();
            }
            return removed;
        }

        public int IndexOf(MoaInstance moa)
        {
            return members.IndexOf(moa);
        }

        public MoaInstance Get(int slot)
        {
            return slot >= 0 && slot < members.Count ? members[slot] : null;
        }

        // Server: the owner asks for a fresh copy once it is ready to receive.
        public void SendFull()
        {
            ownerReady = true;
            lastSentJson = null;
            SendIfChanged();
        }

        private void SendIfChanged()
        {
            string json = JsonUtility.ToJson(new Snapshot { members = members });
            if (json == lastSentJson)
            {
                return;
            }
            lastSentJson = json;
            ReceiveSnapshotRpc(json);
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void ReceiveSnapshotRpc(string json)
        {
            // The host's own player already holds the real list.
            if (IsServer)
            {
                return;
            }
            members.Clear();
            members.AddRange(JsonUtility.FromJson<Snapshot>(json).members);
            Changed?.Invoke();
        }

        public void WriteSave(PlayerSaveData data)
        {
            data.party = new List<MoaInstance>(members);
        }

        public void ReadSave(PlayerSaveData data)
        {
            members.Clear();
            if (data.party != null)
            {
                foreach (MoaInstance moa in data.party)
                {
                    if (members.Count < GameConfig.Instance.maxPartySize && moa != null && moa.Species != null)
                    {
                        members.Add(moa);
                    }
                }
            }
            Changed?.Invoke();
        }
    }
}

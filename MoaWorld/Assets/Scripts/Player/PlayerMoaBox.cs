using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoaWorld
{
    // Personal Moa Box storage. A moa fully heals once it has been stored for the configured time.
    [RequireComponent(typeof(PlayerParty), typeof(PlayerCombat), typeof(PlayerNotifications))]
    public class PlayerMoaBox : MonoBehaviour
    {
        [Serializable]
        public class StoredMoa
        {
            public MoaInstance moa;
            public double storedAt; // world time in seconds
        }

        private readonly List<StoredMoa> stored = new List<StoredMoa>();

        private PlayerParty party;
        private PlayerCombat combat;
        private PlayerNotifications notifications;

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
            bool healedAny = false;
            foreach (StoredMoa entry in stored)
            {
                if (entry.moa.currentHp < entry.moa.MaxHp && HealRemainingSeconds(entry) <= 0f)
                {
                    entry.moa.currentHp = entry.moa.MaxHp;
                    healedAny = true;
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

        public bool TryAdd(MoaInstance moa)
        {
            if (IsFull)
            {
                return false;
            }
            stored.Add(new StoredMoa { moa = moa, storedAt = Now });
            Changed?.Invoke();
            return true;
        }

        public bool Deposit(MoaInstance moa)
        {
            if (!IsNearBox || party.IndexOf(moa) < 0)
            {
                return false;
            }
            if (IsFull)
            {
                notifications.Notify("모아 박스가 가득 찼어요");
                return false;
            }

            combat.RecallIfActive(moa);
            party.Remove(moa);
            return TryAdd(moa);
        }

        public bool Withdraw(int index)
        {
            if (!IsNearBox || index < 0 || index >= stored.Count)
            {
                return false;
            }
            if (party.IsFull)
            {
                notifications.Notify("파티가 가득 찼어요");
                return false;
            }

            MoaInstance moa = stored[index].moa;
            stored.RemoveAt(index);
            party.TryAdd(moa);
            Changed?.Invoke();
            return true;
        }
    }
}

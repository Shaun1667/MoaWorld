using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoaWorld
{
    public class PlayerParty : MonoBehaviour
    {
        [Serializable]
        private class DebugMoaEntry
        {
            public MoaSpecies species;
            public int level = 5;
        }

        // Prototype only: lets combat be tested before capture exists. Clear this once capture works.
        [SerializeField] private DebugMoaEntry[] debugStartingMoa;

        private readonly List<MoaInstance> members = new List<MoaInstance>();

        public IReadOnlyList<MoaInstance> Members => members;
        public bool IsFull => members.Count >= GameConfig.Instance.maxPartySize;

        public event Action Changed;

        private void Awake()
        {
            if (debugStartingMoa == null)
            {
                return;
            }
            foreach (DebugMoaEntry entry in debugStartingMoa)
            {
                if (entry.species != null)
                {
                    TryAdd(MoaInstance.Create(entry.species, entry.level));
                }
            }
        }

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
    }
}

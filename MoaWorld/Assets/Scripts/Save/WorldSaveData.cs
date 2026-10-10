using System;
using System.Collections.Generic;

namespace MoaWorld
{
    // Everything the host keeps about one player, keyed by the player's unique ID.
    [Serializable]
    public class PlayerSaveData
    {
        public string playerId;
        public int coins;
        public int moaBalls;
        public int[] potions;
        public bool hasEverCaptured;
        public int armorTier = -1;
        public List<MoaInstance> party = new List<MoaInstance>();
        public List<PlayerMoaBox.StoredMoa> box = new List<PlayerMoaBox.StoredMoa>();
    }

    // The world save file on the host PC.
    [Serializable]
    public class WorldSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public double worldTime = -1; // negative: a new world that has never been saved
        public List<PlayerSaveData> players = new List<PlayerSaveData>();

        public PlayerSaveData FindPlayer(string playerId)
        {
            return players.Find(p => p.playerId == playerId);
        }

        public void StorePlayer(PlayerSaveData data)
        {
            int index = players.FindIndex(p => p.playerId == data.playerId);
            if (index >= 0)
            {
                players[index] = data;
            }
            else
            {
                players.Add(data);
            }
        }
    }
}

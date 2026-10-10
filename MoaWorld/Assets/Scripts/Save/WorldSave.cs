using System;
using System.IO;
using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Host only: loads the world save when a room opens, keeps each player's data by unique ID,
    // and writes the file on autosave, when a player leaves and when the room closes.
    public class WorldSave : MonoBehaviour
    {
        private WorldSaveData data = new WorldSaveData();
        private float nextAutosaveTime;

        public static WorldSave Instance { get; private set; }

        public static string FilePath => Path.Combine(Application.persistentDataPath, GameConfig.Instance.saveFileName);

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Every player was stored as it despawned during shutdown, so only the file is left to write.
        // This also covers quitting the game, where the network may shut down before anything else runs.
        private void OnServerStopped(bool wasHost)
        {
            WriteFile();
        }

        private static bool IsHosting
        {
            get
            {
                NetworkManager network = NetworkManager.Singleton;
                return network != null && network.IsServer && network.IsListening;
            }
        }

        private void Update()
        {
            if (!IsHosting)
            {
                return;
            }
            if (Time.unscaledTime >= nextAutosaveTime)
            {
                if (nextAutosaveTime > 0f)
                {
                    SaveNow();
                }
                nextAutosaveTime = Time.unscaledTime + GameConfig.Instance.autosaveSeconds;
            }
        }

        private void OnApplicationQuit()
        {
            if (IsHosting)
            {
                SaveNow();
            }
        }

        // Called right before the host starts. A missing or unreadable file starts a new world.
        public void Load()
        {
            data = new WorldSaveData();
            nextAutosaveTime = 0f;
            string path = FilePath;
            if (File.Exists(path))
            {
                try
                {
                    data = JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(path)) ?? new WorldSaveData();
                    Debug.Log($"[Save] Loaded world with {data.players.Count} player(s) from {path}");
                }
                catch (Exception e)
                {
                    string broken = path + ".broken";
                    File.Copy(path, broken, true);
                    Debug.LogError($"[Save] Could not read {path}, starting a new world. The old file was kept as {broken}. {e.Message}");
                    data = new WorldSaveData();
                }
            }

            if (WorldClock.Instance != null)
            {
                if (data.worldTime >= 0)
                {
                    WorldClock.Instance.SetWorldTime(data.worldTime);
                }
                else
                {
                    WorldClock.Instance.ResetToNewWorld();
                }
            }
        }

        public PlayerSaveData Find(string playerId)
        {
            return data.FindPlayer(playerId);
        }

        // Keeps the player's current state in memory; written to disk by the next save.
        public void Store(PlayerNetwork player)
        {
            if (!string.IsNullOrEmpty(player.PlayerId))
            {
                data.StorePlayer(player.CreateSaveData());
            }
        }

        public void SaveNow()
        {
            foreach (PlayerNetwork player in PlayerNetwork.Spawned)
            {
                Store(player);
            }
            WriteFile();
        }

        private void WriteFile()
        {
            if (WorldClock.Instance != null)
            {
                data.worldTime = WorldClock.Instance.WorldTime;
            }
            data.version = WorldSaveData.CurrentVersion;

            string path = FilePath;
            string temp = path + ".tmp";
            try
            {
                // Write to a temp file first so a crash mid-write never leaves a half-written save.
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
                Debug.Log($"[Save] World saved to {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Could not write {path}: {e.Message}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Keeps a region populated with wild moa. Region difficulty is set by the level range.
    public class MoaSpawner : MonoBehaviour
    {
        private const float RaycastHeight = 100f;
        private const int MaxPlacementAttempts = 10;

        public enum SpawnTime
        {
            Any,
            DayOnly,
            NightOnly,
        }

        [Serializable]
        public class SpawnEntry
        {
            public MoaSpecies species;
            public float weight = 1f;
            public SpawnTime time = SpawnTime.Any; // TBD per species (design: details later)
        }

        [SerializeField] private WildMoa wildMoaPrefab;
        [SerializeField] private SpawnEntry[] entries;
        [SerializeField] private float radius = 25f;
        [SerializeField] private int maxAlive = 10;
        [SerializeField] private int minLevel = 1;
        [SerializeField] private int maxLevel = 5;

        private readonly List<WildMoa> alive = new List<WildMoa>();
        private readonly List<float> pendingRespawnTimes = new List<float>();
        private bool started;

        public IReadOnlyList<WildMoa> Alive => alive;

        private void Update()
        {
            // Only the server (host) runs wild moa.
            if (!started)
            {
                NetworkManager network = NetworkManager.Singleton;
                if (network == null || !network.IsServer)
                {
                    return;
                }
                started = true;
                for (int i = 0; i < maxAlive; i++)
                {
                    SpawnOne();
                }
            }

            for (int i = pendingRespawnTimes.Count - 1; i >= 0; i--)
            {
                if (Time.time >= pendingRespawnTimes[i])
                {
                    pendingRespawnTimes.RemoveAt(i);
                    SpawnOne();
                }
            }
        }

        private void SpawnOne()
        {
            MoaSpecies species = PickSpecies();
            if (species == null || !TryFindSpawnPoint(out Vector3 point))
            {
                ScheduleRespawn();
                return;
            }

            MoaInstance moa = MoaInstance.Create(species, UnityEngine.Random.Range(minLevel, maxLevel + 1));
            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            WildMoa wild = Instantiate(wildMoaPrefab, point, rotation, transform);
            wild.Initialize(moa);
            wild.Despawned += OnDespawned;
            alive.Add(wild);
        }

        private void OnDespawned(WildMoa wild)
        {
            alive.Remove(wild);
            ScheduleRespawn();
        }

        private void ScheduleRespawn()
        {
            pendingRespawnTimes.Add(Time.time + GameConfig.Instance.wildRespawnSeconds);
        }

        // Weighted pick among entries allowed at the current time of day; null if none are.
        private MoaSpecies PickSpecies()
        {
            bool isNight = WorldClock.Instance != null && WorldClock.Instance.IsNight;
            float total = 0f;
            foreach (SpawnEntry entry in entries)
            {
                if (IsAllowedNow(entry, isNight))
                {
                    total += entry.weight;
                }
            }
            if (total <= 0f)
            {
                return null;
            }

            float roll = UnityEngine.Random.Range(0f, total);
            MoaSpecies last = null;
            foreach (SpawnEntry entry in entries)
            {
                if (!IsAllowedNow(entry, isNight))
                {
                    continue;
                }
                last = entry.species;
                roll -= entry.weight;
                if (roll <= 0f)
                {
                    return entry.species;
                }
            }
            return last;
        }

        private static bool IsAllowedNow(SpawnEntry entry, bool isNight)
        {
            return entry.time == SpawnTime.Any
                || (entry.time == SpawnTime.DayOnly && !isNight)
                || (entry.time == SpawnTime.NightOnly && isNight);
        }

        private bool TryFindSpawnPoint(out Vector3 point)
        {
            for (int i = 0; i < MaxPlacementAttempts; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 origin = transform.position + new Vector3(offset.x, RaycastHeight, offset.y);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RaycastHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<WildMoa>() == null
                    && hit.collider.GetComponentInParent<CharacterController>() == null
                    && hit.collider.GetComponentInParent<MoaBox>() == null
                    && hit.collider.GetComponentInParent<Shop>() == null)
                {
                    point = hit.point;
                    return true;
                }
            }
            point = default;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

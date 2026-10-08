using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoaWorld
{
    // Keeps a region populated with wild moa. Region difficulty is set by the level range.
    public class MoaSpawner : MonoBehaviour
    {
        private const float RaycastHeight = 100f;
        private const int MaxPlacementAttempts = 10;

        [Serializable]
        public class SpawnEntry
        {
            public MoaSpecies species;
            public float weight = 1f;
        }

        [SerializeField] private WildMoa wildMoaPrefab;
        [SerializeField] private SpawnEntry[] entries;
        [SerializeField] private float radius = 25f;
        [SerializeField] private int maxAlive = 10;
        [SerializeField] private int minLevel = 1;
        [SerializeField] private int maxLevel = 5;

        private readonly List<WildMoa> alive = new List<WildMoa>();
        private readonly List<float> pendingRespawnTimes = new List<float>();

        public IReadOnlyList<WildMoa> Alive => alive;

        private void Start()
        {
            for (int i = 0; i < maxAlive; i++)
            {
                SpawnOne();
            }
        }

        private void Update()
        {
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
            if (!TryFindSpawnPoint(out Vector3 point))
            {
                ScheduleRespawn();
                return;
            }

            MoaInstance moa = MoaInstance.Create(PickSpecies(), UnityEngine.Random.Range(minLevel, maxLevel + 1));
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

        private MoaSpecies PickSpecies()
        {
            float total = 0f;
            foreach (SpawnEntry entry in entries)
            {
                total += entry.weight;
            }

            float roll = UnityEngine.Random.Range(0f, total);
            foreach (SpawnEntry entry in entries)
            {
                roll -= entry.weight;
                if (roll <= 0f)
                {
                    return entry.species;
                }
            }
            return entries[entries.Length - 1].species;
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
                    && hit.collider.GetComponentInParent<MoaBox>() == null)
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

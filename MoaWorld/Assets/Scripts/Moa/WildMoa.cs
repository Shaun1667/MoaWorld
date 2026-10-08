using System;
using UnityEngine;

namespace MoaWorld
{
    // Wild moa AI: fights back when hit, aggressive species attack nearby players,
    // and gives up once the fight drags it too far from its spawn point.
    [RequireComponent(typeof(MoaUnit))]
    public class WildMoa : MonoBehaviour
    {
        private const float HomeStopDistance = 0.5f;

        [SerializeField] private CoinPickup coinPrefab;

        private MoaUnit unit;
        private CharacterController body;
        private Renderer[] renderers;
        private Vector3 home;
        private Combatant target;
        private Transform lastAttackerOwner;
        private PlayerCapture captureHolder;

        public MoaUnit Unit => unit;
        public MoaInstance Moa => unit.Moa;
        public bool IsBeingCaptured => captureHolder != null;

        public event Action<WildMoa> Despawned;

        private void Awake()
        {
            unit = GetComponent<MoaUnit>();
            body = GetComponent<CharacterController>();
            renderers = GetComponentsInChildren<Renderer>();
            unit.Damaged += OnDamaged;
            unit.Fainted += OnFainted;
        }

        // The first ball to land owns the capture attempt; later balls are rejected until it resolves.
        public bool TryClaimCapture(PlayerCapture thrower)
        {
            if (IsBeingCaptured || !unit.IsAttackable)
            {
                return false;
            }
            captureHolder = thrower;
            SetInsideBall(true);
            return true;
        }

        public void ReleaseCapture()
        {
            captureHolder = null;
            SetInsideBall(false);
        }

        private void SetInsideBall(bool inside)
        {
            unit.IsSuspended = inside;
            unit.Stop();
            body.enabled = !inside;
            foreach (Renderer r in renderers)
            {
                r.enabled = !inside;
            }
        }

        public void Initialize(MoaInstance moa)
        {
            unit.Initialize(moa, null);
            home = transform.position;
            name = $"WildMoa_{moa.speciesId}_Lv{moa.level}";
        }

        private void Update()
        {
            if (!unit.IsAttackable)
            {
                return;
            }

            GameConfig config = GameConfig.Instance;

            if (target == null || !target.IsAttackable)
            {
                // A recalled or fainted attacker hands the fight over to its owner.
                target = lastAttackerOwner != null ? lastAttackerOwner.GetComponent<Combatant>() : null;
                lastAttackerOwner = null;
            }
            if (target == null && unit.Moa.Species.aggressive)
            {
                target = FindNearbyPlayer(config.wildAggroRange);
            }
            if (target != null && HorizontalDistance(home, target.transform.position) > config.wildChaseRange)
            {
                target = null;
                lastAttackerOwner = null;
            }

            if (target != null)
            {
                unit.SetDestination(target.transform.position, unit.SkillReach(target) * 0.9f);
                unit.TryUseSkill(target);
            }
            else if (HorizontalDistance(transform.position, home) > HomeStopDistance)
            {
                unit.SetDestination(home, HomeStopDistance);
            }
            else
            {
                unit.Stop();
            }
        }

        private void OnDamaged(Combatant attacker, float amount)
        {
            if (attacker == null)
            {
                return;
            }
            target = attacker;
            lastAttackerOwner = attacker.OwnerRoot;
        }

        private void OnFainted(Combatant attacker)
        {
            GameConfig config = GameConfig.Instance;
            CoinPickup coins = Instantiate(coinPrefab, transform.position, Quaternion.identity);
            coins.Initialize(UnityEngine.Random.Range(config.wildCoinDropMin, config.wildCoinDropMax + 1));
            Despawn();
        }

        private Combatant FindNearbyPlayer(float range)
        {
            PlayerHealth nearest = null;
            float nearestDistance = range;
            foreach (PlayerHealth player in PlayerHealth.Active)
            {
                float distance = HorizontalDistance(transform.position, player.transform.position);
                if (distance <= nearestDistance)
                {
                    nearest = player;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // Called when defeated or captured; the spawner schedules a replacement.
        public void Despawn()
        {
            Despawned?.Invoke(this);
            Destroy(gameObject);
        }
    }
}

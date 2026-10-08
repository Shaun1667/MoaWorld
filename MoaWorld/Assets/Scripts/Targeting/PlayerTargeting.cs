using System;
using UnityEngine;

namespace MoaWorld
{
    // Aims through the screen-center crosshair; right click selects what is aimed at, or clears on empty space.
    public class PlayerTargeting : MonoBehaviour
    {
        private const int MaxHits = 16;

        private readonly RaycastHit[] hits = new RaycastHit[MaxHits];
        private Camera aimCamera;

        public Targetable HoveredTarget { get; private set; }
        public Targetable CurrentTarget { get; private set; }

        // World point under the crosshair (first thing hit, or the end of the aim range).
        public Vector3 AimPoint { get; private set; }

        public event Action<Targetable> TargetChanged;

        private void Awake()
        {
            aimCamera = Camera.main;
        }

        private void Update()
        {
            // Target was destroyed (defeated, captured, despawned).
            if (CurrentTarget == null && !ReferenceEquals(CurrentTarget, null))
            {
                SetTarget(null);
            }

            bool aiming = Cursor.lockState == CursorLockMode.Locked;
            HoveredTarget = aiming ? FindAimedTarget() : null;

            if (aiming && Input.GetMouseButtonDown(1))
            {
                SetTarget(HoveredTarget);
            }
        }

        public void SetTarget(Targetable target)
        {
            if (ReferenceEquals(target, CurrentTarget))
            {
                return;
            }
            CurrentTarget = target;
            TargetChanged?.Invoke(target);
        }

        private Targetable FindAimedTarget()
        {
            GameConfig config = GameConfig.Instance;
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float maxRayDistance = Vector3.Distance(ray.origin, transform.position) + config.targetMaxDistance;
            int count = Physics.SphereCastNonAlloc(ray, config.targetAimRadius, hits, maxRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            // The nearest hit wins, so terrain or walls block targets behind them.
            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider.transform.IsChildOf(transform) || hits[i].distance >= nearestDistance)
                {
                    continue;
                }
                nearest = hitCollider;
                nearestDistance = hits[i].distance;
            }

            AimPoint = ray.GetPoint(nearest != null ? nearestDistance : maxRayDistance);

            if (nearest == null)
            {
                return null;
            }

            Targetable target = nearest.GetComponentInParent<Targetable>();
            if (target == null || Vector3.Distance(transform.position, target.transform.position) > config.targetMaxDistance)
            {
                return null;
            }

            Combatant combatant = target.GetComponent<Combatant>();
            if (combatant != null && combatant.OwnerRoot == transform)
            {
                return null;
            }
            return target;
        }
    }
}

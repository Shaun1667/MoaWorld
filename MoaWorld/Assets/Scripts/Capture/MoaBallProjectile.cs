using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // A thrown moa ball. Flies on a ballistic arc; a wild moa it hits is pulled inside for a capture attempt,
    // anything else just consumes the ball. Simulated on the server; clients follow via NetworkTransform.
    public class MoaBallProjectile : NetworkBehaviour
    {
        private const int MaxHits = 8;
        private const float ShakeAngle = 25f;
        private const float ShakeFrequency = 1.5f;

        private readonly RaycastHit[] hits = new RaycastHit[MaxHits];

        private PlayerCapture thrower;
        private Vector3 velocity;
        private float flightEndTime;

        private bool capturing;
        private WildMoa captured;
        private float captureEndTime;
        private Quaternion restRotation;

        public void Launch(PlayerCapture owner, Vector3 launchVelocity)
        {
            thrower = owner;
            velocity = launchVelocity;
            flightEndTime = Time.time + GameConfig.Instance.ballMaxFlightSeconds;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }
            if (capturing)
            {
                UpdateCapture();
            }
            else
            {
                UpdateFlight();
            }
        }

        private void UpdateFlight()
        {
            if (Time.time >= flightEndTime)
            {
                NetworkObject.Despawn(true);
                return;
            }

            Vector3 position = transform.position;
            Vector3 step = velocity * Time.deltaTime;
            velocity += Physics.gravity * Time.deltaTime;

            float distance = step.magnitude;
            if (distance > 0f && TryFindHit(position, step / distance, distance, out RaycastHit hit))
            {
                transform.position = position + step.normalized * hit.distance;
                OnHit(hit.collider);
                return;
            }
            transform.position = position + step;
        }

        private bool TryFindHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit result)
        {
            int count = Physics.SphereCastNonAlloc(origin, GameConfig.Instance.ballRadius, direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            result = default;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (IsThrowerSide(hits[i].collider) || hits[i].distance >= nearest)
                {
                    continue;
                }
                nearest = hits[i].distance;
                result = hits[i];
            }
            return nearest < float.PositiveInfinity;
        }

        // Balls pass through the thrower and the thrower's own summoned moa.
        private bool IsThrowerSide(Collider hitCollider)
        {
            if (thrower == null)
            {
                return false;
            }
            if (hitCollider.transform.IsChildOf(thrower.transform))
            {
                return true;
            }
            Combatant combatant = hitCollider.GetComponentInParent<Combatant>();
            return combatant != null && combatant.OwnerRoot == thrower.transform;
        }

        private void OnHit(Collider hitCollider)
        {
            WildMoa wild = hitCollider.GetComponentInParent<WildMoa>();
            if (wild != null && thrower != null && wild.TryClaimCapture(thrower))
            {
                capturing = true;
                captured = wild;
                captureEndTime = Time.time + GameConfig.Instance.captureShakeSeconds;
                transform.position = wild.transform.position + Vector3.up * GameConfig.Instance.ballRadius * 2f;
                restRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.position - thrower.transform.position, Vector3.up));
                return;
            }

            if (thrower != null && hitCollider.GetComponentInParent<SummonedMoa>() != null)
            {
                thrower.Notify("주인이 있는 모아는 잡을 수 없어요");
            }
            NetworkObject.Despawn(true);
        }

        private void UpdateCapture()
        {
            if (captured == null)
            {
                NetworkObject.Despawn(true);
                return;
            }

            float shake = Mathf.Sin(Time.time * ShakeFrequency * Mathf.PI * 2f) * ShakeAngle;
            transform.rotation = restRotation * Quaternion.Euler(0f, 0f, shake);

            if (Time.time < captureEndTime)
            {
                return;
            }

            bool success = Random.value < MoaRules.CaptureChance(captured.Moa);
            if (thrower == null)
            {
                captured.ReleaseCapture();
            }
            else if (success)
            {
                thrower.OnCaptureSucceeded(captured.Moa);
                captured.Despawn();
            }
            else
            {
                captured.ReleaseCapture();
                thrower.OnCaptureFailed(captured.Moa);
            }
            NetworkObject.Despawn(true);
        }

        public override void OnDestroy()
        {
            if (thrower != null)
            {
                thrower.OnBallFinished();
            }
            base.OnDestroy();
        }
    }
}

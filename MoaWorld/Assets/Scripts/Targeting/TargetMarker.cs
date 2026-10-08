using UnityEngine;

namespace MoaWorld
{
    // Floating marker above the local player's current target.
    public class TargetMarker : LocalPlayerView
    {
        private const float HeightAboveTarget = 0.6f;
        private const float BobAmplitude = 0.1f;
        private const float BobSpeed = 3f;
        private const float SpinSpeed = 120f;

        [SerializeField] private GameObject visual;

        private PlayerTargeting targeting;

        protected override void Bind(GameObject player)
        {
            targeting = player.GetComponent<PlayerTargeting>();
        }

        protected override void Unbind()
        {
            targeting = null;
        }

        private void LateUpdate()
        {
            Targetable target = targeting != null ? targeting.CurrentTarget : null;
            Combatant combatant = target != null ? target.GetComponent<Combatant>() : null;
            bool show = target != null && (combatant == null || !combatant.IsSuspended);
            if (visual.activeSelf != show)
            {
                visual.SetActive(show);
            }
            if (!show)
            {
                return;
            }

            float bob = Mathf.Sin(Time.time * BobSpeed) * BobAmplitude;
            transform.position = target.TopPoint + Vector3.up * (HeightAboveTarget + bob);
            transform.Rotate(0f, SpinSpeed * Time.deltaTime, 0f, Space.World);
        }
    }
}

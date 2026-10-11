using UnityEngine;

namespace MoaWorld
{
    // Shows the trainer chosen in the lobby in place of the placeholder capsule and drives its Animator on every
    // machine: speed and ground contact come from the synced movement, throws/summons/hits from server events.
    [RequireComponent(typeof(PlayerNetwork))]
    public class PlayerModel : MonoBehaviour
    {
        private const float SpeedSmoothing = 10f;
        private const float GroundProbe = 0.35f;     // meters below the feet that still count as standing
        private const float AirborneDelay = 0.15f;   // short drops (steps, slopes) do not play the fall pose
        private const float JumpRiseSpeed = 1.5f;    // m/s upward that marks a jump
        private const float HitTintSeconds = 0.12f;
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int JumpId = Animator.StringToHash("Jump");
        private static readonly int ThrowId = Animator.StringToHash("Throw");
        private static readonly int SummonId = Animator.StringToHash("Summon");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Built by MoaWorld > Build Trainer Models. Index = PlayerNetwork.Trainer.")]
        [SerializeField] private GameObject[] trainerPrefabs;
        [Tooltip("Placeholder visual hidden while a trainer model is shown.")]
        [SerializeField] private GameObject placeholder;

        private PlayerNetwork network;
        private GameObject model;
        private Animator animator;
        private Renderer[] modelRenderers;
        private MaterialPropertyBlock tintBlock;
        private int builtTrainer = -1;
        private Vector3 lastPosition;
        private float smoothedSpeed;
        private float airTime;
        private bool grounded = true;
        private float tintEndTime;
        private bool tinted;

        private void Awake()
        {
            network = GetComponent<PlayerNetwork>();
            tintBlock = new MaterialPropertyBlock();
            GetComponent<PlayerCapture>().Thrown += () => Trigger(ThrowId);
            GetComponent<PlayerCombat>().Summoned += () => Trigger(SummonId);
            GetComponent<Combatant>().HitShown += OnHitShown;
            lastPosition = transform.position;
        }

        private void Update()
        {
            int wanted = network.IsSpawned ? network.Trainer : PlayerNetwork.PreferredTrainer;
            if (wanted != builtTrainer)
            {
                Build(wanted);
            }
            if (animator == null)
            {
                return;
            }

            // Measured from the actual movement so remote players (moved by NetworkTransform) animate too.
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float verticalSpeed = delta.y / dt;
            delta.y = 0f;
            float speed = Mathf.Clamp01(delta.magnitude / dt / GameConfig.Instance.playerMoveSpeed);
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-SpeedSmoothing * dt));
            animator.SetFloat(SpeedId, smoothedSpeed);

            bool touching = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.2f + GroundProbe,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (touching)
            {
                airTime = 0f;
                if (!grounded)
                {
                    grounded = true;
                    animator.SetBool(GroundedId, true);
                }
            }
            else
            {
                airTime += dt;
                bool jumped = grounded && verticalSpeed > JumpRiseSpeed;
                if (jumped)
                {
                    animator.SetTrigger(JumpId);
                }
                if (grounded && (jumped || airTime > AirborneDelay))
                {
                    grounded = false;
                    animator.SetBool(GroundedId, false);
                }
            }

            if (tinted && Time.time >= tintEndTime)
            {
                SetTint(false);
            }
        }

        private void Build(int index)
        {
            builtTrainer = index;
            if (model != null)
            {
                Destroy(model);
            }
            model = null;
            animator = null;
            if (trainerPrefabs == null || trainerPrefabs.Length == 0)
            {
                return;
            }
            GameObject prefab = trainerPrefabs[Mathf.Clamp(index, 0, trainerPrefabs.Length - 1)];
            if (prefab == null)
            {
                return;
            }
            model = Instantiate(prefab, transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            animator = model.GetComponentInChildren<Animator>();
            modelRenderers = model.GetComponentsInChildren<Renderer>();
            animator.SetBool(GroundedId, grounded);
            if (placeholder != null)
            {
                placeholder.SetActive(false);
            }
        }

        private void Trigger(int id)
        {
            if (animator != null)
            {
                animator.SetTrigger(id);
            }
        }

        private void OnHitShown()
        {
            Trigger(HitId);
            if (modelRenderers != null)
            {
                SetTint(true);
                tintEndTime = Time.time + HitTintSeconds;
            }
        }

        private void SetTint(bool on)
        {
            tinted = on;
            foreach (Renderer r in modelRenderers)
            {
                if (on)
                {
                    tintBlock.SetColor(BaseColorId, new Color(1f, 0.55f, 0.55f));
                    r.SetPropertyBlock(tintBlock);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }
    }
}

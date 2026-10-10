using UnityEngine;

namespace MoaWorld
{
    // Shows the species' 3D model in place of the placeholder capsule and drives its Animator
    // (Speed / Attack / Hit). Runs on every machine from synced data, so nothing here is networked.
    [RequireComponent(typeof(MoaUnit))]
    public class MoaModel : MonoBehaviour
    {
        private const float SpeedSmoothing = 10f;
        private const float HitTintSeconds = 0.12f;
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color HitTint = new Color(1f, 0.55f, 0.55f);

        private MoaUnit unit;
        private MoaSpecies shownSpecies;
        private GameObject model;
        private Animator animator;
        private Renderer[] modelRenderers;
        private MaterialPropertyBlock tintBlock;
        private Vector3 lastPosition;
        private float smoothedSpeed;
        private float tintEndTime;
        private bool tinted;
        private bool hidden;

        private void Awake()
        {
            unit = GetComponent<MoaUnit>();
            tintBlock = new MaterialPropertyBlock();
            unit.AppearanceChanged += Refresh;
            unit.SkillShown += OnSkillShown;
            unit.HitShown += OnHitShown;
            lastPosition = transform.position;
        }

        // Used while the moa is inside a moa ball.
        public void SetHidden(bool value)
        {
            hidden = value;
            ApplyVisibility();
        }

        private void Refresh()
        {
            MoaSpecies species = unit.Moa != null ? unit.Moa.Species : null;
            if (species == shownSpecies)
            {
                return;
            }
            shownSpecies = species;

            if (model != null)
            {
                Destroy(model);
                model = null;
                animator = null;
                modelRenderers = null;
            }
            if (species != null && species.modelPrefab != null)
            {
                model = Instantiate(species.modelPrefab, transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                animator = model.GetComponentInChildren<Animator>();
                modelRenderers = model.GetComponentsInChildren<Renderer>();
            }
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            // The whole placeholder object, so its face marker hides with the capsule.
            if (unit.PlaceholderRenderer != null)
            {
                unit.PlaceholderRenderer.gameObject.SetActive(!hidden && model == null);
            }
            if (modelRenderers != null)
            {
                foreach (Renderer r in modelRenderers)
                {
                    r.enabled = !hidden;
                }
            }
        }

        private void Update()
        {
            // Measured from the actual movement so it also works on clients, where NetworkTransform moves the moa.
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime / GameConfig.Instance.moaMoveSpeed : 0f;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, Mathf.Clamp01(speed), 1f - Mathf.Exp(-SpeedSmoothing * Time.deltaTime));

            if (animator != null)
            {
                animator.SetFloat(SpeedId, smoothedSpeed);
            }
            if (tinted && Time.time >= tintEndTime)
            {
                SetTint(false);
            }
        }

        private void OnSkillShown()
        {
            if (animator != null)
            {
                animator.SetTrigger(AttackId);
            }
        }

        private void OnHitShown()
        {
            if (animator == null)
            {
                return;
            }
            animator.SetTrigger(HitId);
            SetTint(true);
            tintEndTime = Time.time + HitTintSeconds;
        }

        private void SetTint(bool on)
        {
            tinted = on;
            foreach (Renderer r in modelRenderers)
            {
                if (on)
                {
                    tintBlock.SetColor(BaseColorId, HitTint);
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

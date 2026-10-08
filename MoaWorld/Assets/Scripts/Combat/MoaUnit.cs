using System;
using UnityEngine;

namespace MoaWorld
{
    // A moa in the world (wild or summoned): movement, its single skill, HP and exp.
    // WildMoa / SummonedMoa decide where to go and whom to attack.
    public class MoaUnit : Combatant
    {
        private const float GroundedStickVelocity = -2f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer bodyRenderer;

        private CharacterController controller;
        private float verticalVelocity;
        private bool hasDestination;
        private Vector3 destination;
        private float stopDistance;
        private float nextSkillTime;

        public MoaInstance Moa { get; private set; }

        public override bool IsAlive => Moa != null && !Moa.IsFainted;
        public override int Defense => Moa.Defense;
        public override MoaElement? Element => Moa.Species.element;
        protected override bool IsDepleted => Moa.IsFainted;

        public event Action<Combatant> Fainted;

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
        }

        public void Initialize(MoaInstance moa, Transform ownerRoot)
        {
            Moa = moa;
            OwnerRoot = ownerRoot;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, GameConfig.Instance.GetElementColor(moa.Species.element));
            bodyRenderer.SetPropertyBlock(block);
        }

        public void SetDestination(Vector3 point, float stopAt)
        {
            hasDestination = true;
            destination = point;
            stopDistance = stopAt;
        }

        public void Stop()
        {
            hasDestination = false;
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalVelocity = 0f;
        }

        // Distance at which the skill reaches the target's surface.
        public float SkillReach(Combatant target)
        {
            return Moa.Species.skillRange + Radius + target.Radius;
        }

        public bool TryUseSkill(Combatant target)
        {
            if (!IsAttackable || target == null || !target.IsAttackable || Time.time < nextSkillTime
                || HorizontalDistanceTo(target) > SkillReach(target))
            {
                return false;
            }

            nextSkillTime = Time.time + Moa.Species.skillCooldown;
            FaceTowards(target.transform.position);

            float damage = MoaRules.CalculateDamage(Moa, target);
            Vector3 origin = transform.position + Vector3.up * controller.center.y;
            Vector3 impact = target.transform.position + Vector3.up * target.Radius * 2f;
            SkillEffect.Spawn(origin, impact, GameConfig.Instance.GetElementColor(Moa.Species.element));

            target.ReceiveDamage(damage, this);
            Debug.Log($"[Combat] {name} -> {target.name}: {damage:F1} dmg");

            if (!target.IsAlive && target is MoaUnit defeated)
            {
                int levels = Moa.AddExp(MoaRules.ExpReward(defeated.Moa.level));
                if (levels > 0)
                {
                    Debug.Log($"[Combat] {name} leveled up to Lv{Moa.level}");
                }
            }
            return true;
        }

        protected override void ApplyDamage(float amount)
        {
            Moa.currentHp = Mathf.Max(0f, Moa.currentHp - amount);
        }

        protected override void OnDepleted(Combatant attacker)
        {
            Debug.Log($"[Combat] {name} fainted");
            Fainted?.Invoke(attacker);
        }

        private void Update()
        {
            if (!controller.enabled)
            {
                return;
            }

            GameConfig config = GameConfig.Instance;
            Vector3 move = Vector3.zero;

            if (hasDestination)
            {
                Vector3 toDestination = destination - transform.position;
                toDestination.y = 0f;
                if (toDestination.magnitude > stopDistance)
                {
                    move = toDestination.normalized * config.moaMoveSpeed;
                    FaceTowards(destination);
                }
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = GroundedStickVelocity;
            }
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            move.y = verticalVelocity;
            controller.Move(move * Time.deltaTime);
        }

        private void FaceTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, GameConfig.Instance.moaTurnSpeed * Time.deltaTime);
        }
    }
}

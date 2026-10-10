using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // A moa in the world (wild or summoned): movement, its single skill, HP and exp.
    // WildMoa / SummonedMoa decide where to go and whom to attack. Everything runs on the server;
    // clients rebuild a read-only copy of the moa from the synced identity and HP.
    public class MoaUnit : Combatant
    {
        private const float GroundedStickVelocity = -2f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Everything clients need to rebuild the moa's stats (HP is synced separately since it changes often).
        public struct Identity : INetworkSerializable, IEquatable<Identity>
        {
            public FixedString64Bytes speciesId;
            public int level;
            public int ivHp;
            public int ivAttack;
            public int ivDefense;

            public static Identity From(MoaInstance moa)
            {
                return new Identity
                {
                    speciesId = moa.speciesId,
                    level = moa.level,
                    ivHp = moa.ivHp,
                    ivAttack = moa.ivAttack,
                    ivDefense = moa.ivDefense,
                };
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref speciesId);
                serializer.SerializeValue(ref level);
                serializer.SerializeValue(ref ivHp);
                serializer.SerializeValue(ref ivAttack);
                serializer.SerializeValue(ref ivDefense);
            }

            public bool Equals(Identity other)
            {
                return speciesId.Equals(other.speciesId) && level == other.level
                    && ivHp == other.ivHp && ivAttack == other.ivAttack && ivDefense == other.ivDefense;
            }
        }

        [SerializeField] private Renderer bodyRenderer;

        private readonly NetworkVariable<Identity> identity = new NetworkVariable<Identity>();
        private readonly NetworkVariable<float> hp = new NetworkVariable<float>();
        private readonly NetworkVariable<NetworkObjectReference> ownerPlayer = new NetworkVariable<NetworkObjectReference>();

        private CharacterController controller;
        private float verticalVelocity;
        private bool hasDestination;
        private Vector3 destination;
        private float stopDistance;
        private float nextSkillTime;

        // Server: the real moa (shared with the owner's party). Client: a copy rebuilt from synced data.
        public MoaInstance Moa { get; private set; }

        public override bool IsAlive => Moa != null && !Moa.IsFainted;
        public override int Defense => Moa.Defense;
        public override MoaElement? Element => Moa.Species.element;
        protected override bool IsDepleted => Moa.IsFainted;

        // Server only.
        public event Action<Combatant> Fainted;

        // Every machine: the moa (species/level) is known or changed, so visuals can update.
        public event Action AppearanceChanged;

        // Every machine: the moa used its skill (for the attack animation).
        public event Action SkillShown;

        public Renderer PlaceholderRenderer => bodyRenderer;

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                return;
            }
            identity.OnValueChanged += OnIdentityChanged;
            hp.OnValueChanged += OnHpChanged;
            ownerPlayer.OnValueChanged += OnOwnerPlayerChanged;
            RebuildFromIdentity();
            ResolveOwner();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            identity.OnValueChanged -= OnIdentityChanged;
            hp.OnValueChanged -= OnHpChanged;
            ownerPlayer.OnValueChanged -= OnOwnerPlayerChanged;
        }

        // Server, after spawning.
        public void Initialize(MoaInstance moa, Transform ownerRoot)
        {
            Moa = moa;
            OwnerRoot = ownerRoot;
            NetworkObject ownerObject = ownerRoot != null ? ownerRoot.GetComponent<NetworkObject>() : null;
            ownerPlayer.Value = ownerObject != null ? new NetworkObjectReference(ownerObject) : default;
            SyncMoa();
            ApplyColor();
        }

        // Server: push the moa's current level and HP to clients.
        public void SyncMoa()
        {
            identity.Value = Identity.From(Moa);
            hp.Value = Moa.currentHp;
        }

        private void OnIdentityChanged(Identity previous, Identity current)
        {
            RebuildFromIdentity();
        }

        private void OnHpChanged(float previous, float current)
        {
            if (Moa != null)
            {
                Moa.currentHp = current;
            }
        }

        private void OnOwnerPlayerChanged(NetworkObjectReference previous, NetworkObjectReference current)
        {
            ResolveOwner();
        }

        private void RebuildFromIdentity()
        {
            Identity id = identity.Value;
            string speciesId = id.speciesId.ToString();
            if (string.IsNullOrEmpty(speciesId) || MoaDatabase.Instance.Get(speciesId) == null)
            {
                return;
            }
            Moa = new MoaInstance
            {
                speciesId = speciesId,
                level = id.level,
                ivHp = id.ivHp,
                ivAttack = id.ivAttack,
                ivDefense = id.ivDefense,
                currentHp = hp.Value,
            };
            ApplyColor();
        }

        private void ResolveOwner()
        {
            OwnerRoot = ownerPlayer.Value.TryGet(out NetworkObject ownerObject) ? ownerObject.transform : null;
        }

        private void ApplyColor()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, GameConfig.Instance.GetElementColor(Moa.Species.element));
            bodyRenderer.SetPropertyBlock(block);
            AppearanceChanged?.Invoke();
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

        // Server only.
        public bool TryUseSkill(Combatant target)
        {
            if (!IsServer || !IsAttackable || target == null || !target.IsAttackable || Time.time < nextSkillTime
                || HorizontalDistanceTo(target) > SkillReach(target))
            {
                return false;
            }

            nextSkillTime = Time.time + Moa.Species.skillCooldown;
            FaceTowards(target.transform.position);

            float damage = MoaRules.CalculateDamage(Moa, target);
            Vector3 origin = transform.position + Vector3.up * controller.center.y;
            Vector3 impact = target.transform.position + Vector3.up * target.Radius * 2f;
            ShowSkillRpc(origin, impact);

            target.ReceiveDamage(damage, this);
            Debug.Log($"[Combat] {name} -> {target.name}: {damage:F1} dmg");

            if (!target.IsAlive && target is MoaUnit defeated)
            {
                int levels = Moa.AddExp(MoaRules.ExpReward(defeated.Moa.level));
                if (levels > 0)
                {
                    SyncMoa();
                    Debug.Log($"[Combat] {name} leveled up to Lv{Moa.level}");
                }
            }
            return true;
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        private void ShowSkillRpc(Vector3 origin, Vector3 impact)
        {
            if (Moa != null)
            {
                SkillEffect.Spawn(origin, impact, GameConfig.Instance.GetElementColor(Moa.Species.element));
            }
            SkillShown?.Invoke();
        }

        protected override void ApplyDamage(float amount)
        {
            Moa.currentHp = Mathf.Max(0f, Moa.currentHp - amount);
            hp.Value = Moa.currentHp;
        }

        protected override void OnDepleted(Combatant attacker)
        {
            Debug.Log($"[Combat] {name} fainted");
            Fainted?.Invoke(attacker);
        }

        private void Update()
        {
            // Clients only mirror the server's position through NetworkTransform.
            if (!IsServer || !controller.enabled)
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

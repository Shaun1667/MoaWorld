using System;
using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Anything that can be hit: players and moa (wild or owned).
    // Damage is decided on the server only; clients just see the synced results.
    [RequireComponent(typeof(CharacterController))]
    public abstract class Combatant : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> suspended = new NetworkVariable<bool>();

        private CharacterController body;

        // Root transform of the owning player; null for wild moa. A player owns itself.
        public Transform OwnerRoot { get; protected set; }

        public abstract bool IsAlive { get; }

        // Temporarily out of combat (e.g. inside a moa ball during a capture attempt). Set by the server.
        public bool IsSuspended
        {
            get => suspended.Value;
            set => suspended.Value = value;
        }

        public bool IsAttackable => IsAlive && !IsSuspended;

        public abstract int Defense { get; }
        public abstract MoaElement? Element { get; }
        protected abstract bool IsDepleted { get; }

        public float Radius => body.radius;

        // Server only. (attacker, amount). Attacker may be null for environmental damage.
        public event Action<Combatant, float> Damaged;

        // Every machine: a hit landed on this combatant (for visual feedback).
        public event Action HitShown;

        // Every machine: IsSuspended changed.
        public event Action<bool> SuspendedChanged;

        protected virtual void Awake()
        {
            body = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            suspended.OnValueChanged += OnSuspendedChanged;
        }

        public override void OnNetworkDespawn()
        {
            suspended.OnValueChanged -= OnSuspendedChanged;
        }

        private void OnSuspendedChanged(bool previous, bool current)
        {
            SuspendedChanged?.Invoke(current);
        }

        public bool IsFriendlyTo(Combatant other)
        {
            return other != null && OwnerRoot != null && OwnerRoot == other.OwnerRoot;
        }

        public float HorizontalDistanceTo(Combatant other)
        {
            Vector3 delta = other.transform.position - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        public void ReceiveDamage(float amount, Combatant attacker)
        {
            if (!IsServer || !IsAttackable || amount <= 0f)
            {
                return;
            }

            ApplyDamage(amount);
            Damaged?.Invoke(attacker, amount);
            ShowHitRpc();
            if (IsDepleted)
            {
                OnDepleted(attacker);
            }
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        private void ShowHitRpc()
        {
            HitShown?.Invoke();
        }

        protected abstract void ApplyDamage(float amount);
        protected abstract void OnDepleted(Combatant attacker);
    }
}

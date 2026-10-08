using System;
using UnityEngine;

namespace MoaWorld
{
    // Anything that can be hit: players and moa (wild or owned).
    [RequireComponent(typeof(CharacterController))]
    public abstract class Combatant : MonoBehaviour
    {
        private CharacterController body;

        // Root transform of the owning player; null for wild moa. A player owns itself.
        public Transform OwnerRoot { get; protected set; }

        public abstract bool IsAlive { get; }

        // Temporarily out of combat (e.g. inside a moa ball during a capture attempt).
        public bool IsSuspended { get; set; }
        public bool IsAttackable => IsAlive && !IsSuspended;

        public abstract int Defense { get; }
        public abstract MoaElement? Element { get; }
        protected abstract bool IsDepleted { get; }

        public float Radius => body.radius;

        // (attacker, amount). Attacker may be null for environmental damage.
        public event Action<Combatant, float> Damaged;

        protected virtual void Awake()
        {
            body = GetComponent<CharacterController>();
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

        // Must only run on the server once networking is added.
        public void ReceiveDamage(float amount, Combatant attacker)
        {
            if (!IsAttackable || amount <= 0f)
            {
                return;
            }

            ApplyDamage(amount);
            Damaged?.Invoke(attacker, amount);
            if (IsDepleted)
            {
                OnDepleted(attacker);
            }
        }

        protected abstract void ApplyDamage(float amount);
        protected abstract void OnDepleted(Combatant attacker);
    }
}

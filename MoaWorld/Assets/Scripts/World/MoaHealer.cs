using UnityEngine;

namespace MoaWorld
{
    // Healing machine next to the Moa Box: talking to it fully heals the player's party.
    public class MoaHealer : MonoBehaviour
    {
        public static MoaHealer Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool IsInRange(Vector3 position)
        {
            Vector3 delta = position - transform.position;
            delta.y = 0f;
            return delta.magnitude <= GameConfig.Instance.healerInteractRange;
        }
    }
}

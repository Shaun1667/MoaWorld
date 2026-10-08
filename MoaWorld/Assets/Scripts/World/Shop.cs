using UnityEngine;

namespace MoaWorld
{
    // The shop stall next to the Moa Box.
    public class Shop : MonoBehaviour
    {
        public static Shop Instance { get; private set; }

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
            return delta.magnitude <= GameConfig.Instance.shopInteractRange;
        }
    }
}

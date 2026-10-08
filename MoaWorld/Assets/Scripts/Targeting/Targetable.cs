using UnityEngine;

namespace MoaWorld
{
    // Marks something the player can select as a target (wild moa now, other players later).
    public class Targetable : MonoBehaviour
    {
        private Collider bodyCollider;

        public Vector3 TopPoint
        {
            get
            {
                Vector3 position = transform.position;
                if (bodyCollider != null)
                {
                    position.y = bodyCollider.bounds.max.y;
                }
                return position;
            }
        }

        private void Awake()
        {
            bodyCollider = GetComponentInChildren<Collider>();
        }
    }
}

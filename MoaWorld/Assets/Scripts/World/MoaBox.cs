using UnityEngine;

namespace MoaWorld
{
    // The single indestructible Moa Box at the map center. Also the start and respawn point.
    public class MoaBox : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;

        public static MoaBox Instance { get; private set; }

        public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

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
            return delta.magnitude <= GameConfig.Instance.moaBoxInteractRange;
        }
    }
}

using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Moa coins dropped where a wild moa fainted. The first player to walk over them takes them all.
    // Pickup and expiry are decided by the server; every machine animates its own copy.
    public class CoinPickup : NetworkBehaviour
    {
        private const float SpinSpeed = 180f;
        private const float BobAmplitude = 0.15f;
        private const float BobSpeed = 2f;
        private const float HoverHeight = 0.5f;

        private int amount;
        private float despawnTime;
        private Vector3 basePosition;

        // Server, before spawning.
        public void Initialize(int coinAmount)
        {
            amount = coinAmount;
            despawnTime = Time.time + GameConfig.Instance.coinDespawnMinutes * 60f;
            transform.position += Vector3.up * HoverHeight;
        }

        public override void OnNetworkSpawn()
        {
            basePosition = transform.position;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * BobSpeed) * BobAmplitude);
            transform.Rotate(0f, SpinSpeed * Time.deltaTime, 0f, Space.World);

            if (!IsServer)
            {
                return;
            }

            if (Time.time >= despawnTime)
            {
                NetworkObject.Despawn(true);
                return;
            }

            float radius = GameConfig.Instance.coinPickupRadius;
            foreach (PlayerHealth player in PlayerHealth.Active)
            {
                Vector3 delta = player.transform.position - basePosition;
                delta.y = 0f;
                if (delta.magnitude <= radius)
                {
                    player.GetComponent<PlayerInventory>().AddCoins(amount);
                    player.GetComponent<PlayerNotifications>().Notify($"모아 코인 +{amount}");
                    NetworkObject.Despawn(true);
                    return;
                }
            }
        }
    }
}

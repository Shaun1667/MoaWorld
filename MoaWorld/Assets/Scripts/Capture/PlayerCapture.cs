using Unity.Netcode;
using UnityEngine;

namespace MoaWorld
{
    // Left click throws a moa ball at the crosshair. Captured moa go to the party, or the Moa Box when the party is full.
    // The owner aims; the server spends the ball, flies it and decides the capture.
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerParty), typeof(PlayerMoaBox))]
    [RequireComponent(typeof(PlayerTargeting), typeof(PlayerNotifications))]
    public class PlayerCapture : NetworkBehaviour
    {
        private const float HandHeight = 1.5f;
        private const float HandSideOffset = 0.4f;
        private const float MaxThrowOriginOffset = 3f; // server tolerance for the client's hand position

        [SerializeField] private MoaBallProjectile ballPrefab;

        private PlayerInventory inventory;
        private PlayerParty party;
        private PlayerMoaBox box;
        private PlayerTargeting targeting;
        private PlayerNotifications notifications;
        private int ballsInFlight;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            party = GetComponent<PlayerParty>();
            box = GetComponent<PlayerMoaBox>();
            targeting = GetComponent<PlayerTargeting>();
            notifications = GetComponent<PlayerNotifications>();
        }

        private void Update()
        {
            if (IsServer)
            {
                GrantStartingBallsIfNeeded();
            }

            if (IsOwner && Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            {
                Vector3 origin = transform.position + Vector3.up * HandHeight + transform.right * HandSideOffset;
                Vector3 velocity = BallisticVelocity(origin, targeting.AimPoint, GameConfig.Instance.ballThrowSpeed);
                ThrowRpc(origin, velocity);
            }
        }

        // Design: a player who has never caught a moa and has no balls receives the starting balls.
        private void GrantStartingBallsIfNeeded()
        {
            if (IsSpawned && !inventory.HasEverCaptured && inventory.MoaBalls == 0 && ballsInFlight == 0)
            {
                inventory.AddMoaBalls(GameConfig.Instance.startingMoaBalls);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ThrowRpc(Vector3 origin, Vector3 velocity)
        {
            if (party.IsFull && box.IsFull)
            {
                Notify("모아 박스가 가득 찼어요");
                return;
            }
            if (Vector3.Distance(origin, transform.position) > MaxThrowOriginOffset)
            {
                return;
            }
            if (!inventory.TryUseMoaBall())
            {
                Notify("모아볼이 없어요");
                return;
            }

            velocity = Vector3.ClampMagnitude(velocity, GameConfig.Instance.ballThrowSpeed);
            MoaBallProjectile ball = Instantiate(ballPrefab, origin, Quaternion.LookRotation(velocity));
            ball.NetworkObject.Spawn(true);
            ballsInFlight++;
            ball.Launch(this, velocity);
        }

        // Server only.
        public void OnCaptureSucceeded(MoaInstance moa)
        {
            inventory.MarkCaptured();
            string label = $"{moa.Species.displayName} Lv.{moa.level}";
            if (party.TryAdd(moa))
            {
                Notify($"{label} 포획 성공!");
            }
            else if (box.TryAdd(moa))
            {
                Notify($"{label} 포획 성공! 파티가 가득 차서 모아 박스로 보냈어요");
            }
        }

        public void OnCaptureFailed(MoaInstance moa)
        {
            Notify($"{moa.Species.displayName}이(가) 모아볼에서 빠져나왔어요");
        }

        public void OnBallFinished()
        {
            ballsInFlight = Mathf.Max(0, ballsInFlight - 1);
        }

        public void Notify(string message)
        {
            notifications.Notify(message);
        }

        // Low-arc launch velocity that lands on the target at the given speed; 45 degrees if out of reach.
        private static Vector3 BallisticVelocity(Vector3 from, Vector3 to, float speed)
        {
            Vector3 delta = to - from;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float horizontal = flat.magnitude;
            if (horizontal < 0.01f)
            {
                return delta.normalized * speed;
            }

            float gravity = -Physics.gravity.y;
            float speedSq = speed * speed;
            float discriminant = speedSq * speedSq - gravity * (gravity * horizontal * horizontal + 2f * delta.y * speedSq);
            float angle = discriminant >= 0f
                ? Mathf.Atan((speedSq - Mathf.Sqrt(discriminant)) / (gravity * horizontal))
                : Mathf.PI / 4f;

            return flat / horizontal * (speed * Mathf.Cos(angle)) + Vector3.up * (speed * Mathf.Sin(angle));
        }
    }
}

using Unity.Netcode.Components;
using UnityEngine;

namespace MoaWorld
{
    // Owner-only: reads input and moves the local player. Remote copies are moved by NetworkTransform.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        private const float GroundedStickVelocity = -2f;

        private CharacterController controller;
        private NetworkTransform networkTransform;
        private Transform cameraTransform;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            networkTransform = GetComponent<NetworkTransform>();
        }

        private void Update()
        {
            if (cameraTransform == null)
            {
                if (Camera.main == null)
                {
                    return;
                }
                cameraTransform = Camera.main.transform;
            }

            GameConfig config = GameConfig.Instance;

            Vector3 input = UiState.IsMenuOpen
                ? Vector3.zero
                : new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            input = Vector3.ClampMagnitude(input, 1f);

            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            Vector3 move = (forward * input.z + right * input.x) * config.playerMoveSpeed;

            if (move.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, config.playerTurnSpeed * Time.deltaTime);
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = GroundedStickVelocity;
            }
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);
        }

        public void Teleport(Vector3 position)
        {
            // CharacterController overrides transform changes while enabled.
            controller.enabled = false;
            if (networkTransform != null && networkTransform.IsSpawned && networkTransform.CanCommitToTransform)
            {
                // Teleport instead of a plain move so other players do not see the player slide across the map.
                networkTransform.Teleport(position, transform.rotation, transform.localScale);
            }
            else
            {
                transform.position = position;
            }
            controller.enabled = true;
            verticalVelocity = 0f;
        }
    }
}

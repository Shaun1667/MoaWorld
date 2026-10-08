using UnityEngine;

namespace MoaWorld
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        private const float GroundedStickVelocity = -2f;

        [SerializeField] private Transform cameraTransform;

        private CharacterController controller;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            GameConfig config = GameConfig.Instance;

            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
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
            transform.position = position;
            controller.enabled = true;
            verticalVelocity = 0f;
        }
    }
}

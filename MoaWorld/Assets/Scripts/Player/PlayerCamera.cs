using UnityEngine;

namespace MoaWorld
{
    // Third-person orbit camera. Cursor is hidden and locked by default and mouse movement orbits;
    // holding Alt frees the cursor and pauses orbiting. Scroll to zoom.
    public class PlayerCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        private float yaw;
        private float pitch = 20f;
        private float distance;
        private bool cursorFree;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void Start()
        {
            distance = GameConfig.Instance.cameraDistance;
            if (target != null)
            {
                yaw = target.eulerAngles.y;
            }
            ApplyCursorState();
        }

        private void Update()
        {
            bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (altHeld != cursorFree)
            {
                cursorFree = altHeld;
                ApplyCursorState();
            }
        }

        // Only re-applied on change or focus so Esc can still release the cursor in the editor.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyCursorState();
            }
        }

        private void ApplyCursorState()
        {
            Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = cursorFree;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            GameConfig config = GameConfig.Instance;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += Input.GetAxis("Mouse X") * config.cameraOrbitSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * config.cameraOrbitSensitivity;
                pitch = Mathf.Clamp(pitch, config.cameraMinPitch, config.cameraMaxPitch);
            }

            distance -= Input.GetAxis("Mouse ScrollWheel") * config.cameraZoomSpeed;
            distance = Mathf.Clamp(distance, config.cameraMinDistance, config.cameraMaxDistance);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + Vector3.up * config.cameraTargetHeight;
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}

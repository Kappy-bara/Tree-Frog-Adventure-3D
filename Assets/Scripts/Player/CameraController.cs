using TreeFrogAdventure.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// Simple 3rd Person Follow Camera.
    /// Orbits around the target using mouse input.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The transform the camera should follow (usually the Player).")]
        public Transform Target;

        [Header("Settings")]
        public float distance = 5f;
        public float height = 2f;
        public float rotationSpeed = 1f;
        public float minPitch = -20f;
        public float maxPitch = 60f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            if (Target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) Target = player.transform;
            }

            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;

            // Lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (Target == null || PauseMenuController.IsPaused || PlayerHealth.IsDead) return;

            // Read mouse input directly (New Input System)
            Vector2 lookInput = Vector2.zero;
            if (Mouse.current != null)
            {
                lookInput = Mouse.current.delta.ReadValue();
            }

            _yaw += lookInput.x * rotationSpeed * 0.1f;
            _pitch -= lookInput.y * rotationSpeed * 0.1f;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            // Calculate rotation and position
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 position = Target.position + Vector3.up * height - rotation * Vector3.forward * distance;

            // Apply to camera
            transform.position = position;
            transform.rotation = rotation;
        }
    }
}

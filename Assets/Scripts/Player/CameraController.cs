using TreeFrogAdventure.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// Third-person orbit camera.
    ///
    /// Two things this fixes over the original:
    ///
    /// 1. It reads the bound "Look" action instead of Mouse.current directly, so the
    ///    gamepad right stick works. The action was fully bound (gamepad stick, pointer
    ///    delta, joystick hat) and read by nothing.
    ///
    /// 2. It sweeps for geometry between the player and the camera and pulls in on a
    ///    hit. Previously the position was snapped with no occlusion test at all, so the
    ///    camera sat inside walls — which wall climbing makes constant, since climbing
    ///    presses the player flat against geometry by definition.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Tooltip("Usually the Player. Found by tag if left empty.")]
        public Transform Target;

        public float distance = 5f;
        public float height = 2f;
        public float rotationSpeed = 1f;
        public float minPitch = -20f;
        public float maxPitch = 60f;

        [Header("Sensitivity")]
        [Tooltip("Ignore the player's saved sensitivity and use the values below. " +
                 "For testing a specific feel; leave off in shipping levels.")]
        [SerializeField] private bool overrideSensitivity;

        [Tooltip("Mouse delta is measured in pixels per frame, gamepad sticks in -1..1 " +
                 "per second, so the two need separate scales or one is unusable.")]
        [SerializeField] private float mouseSensitivity = GameSettings.DefaultMouseSensitivity;
        [SerializeField] private float gamepadSensitivity = GameSettings.DefaultGamepadSensitivity;

        [Header("Collision")]
        [SerializeField] private bool avoidGeometry = true;

        [Tooltip("Layers the camera will not sit inside.")]
        [SerializeField] private LayerMask occluderLayers = (1 << 0) | (1 << 15);

        [Tooltip("Keeps the near plane out of the surface it pulls up against.")]
        [SerializeField] private float collisionPadding = 0.3f;

        [Tooltip("How fast the camera returns to full distance once unblocked. " +
                 "Pulling in is instant so the view never clips.")]
        [SerializeField] private float returnSpeed = 6f;

        [Header("Smoothing")]
        [Tooltip("0 disables position smoothing.")]
        [SerializeField] private float positionSmoothing = 12f;

        private float _yaw;
        private float _pitch;
        private float _currentDistance;
        private InputAction _lookAction;

        private void Start()
        {
            if (Target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) Target = player.transform;
            }

            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
            _currentDistance = distance;

            // PlayerInput lives on the Player, not on the camera.
            if (Target != null)
            {
                var input = Target.GetComponentInParent<PlayerInput>();
                if (input != null) _lookAction = input.actions.FindAction("Look");
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (Target == null || PauseMenuController.IsPaused || PlayerHealth.IsDead) return;

            ApplyLook();

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = Target.position + Vector3.up * height;

            float desired = ResolveDistance(pivot, rotation);
            Vector3 position = pivot - rotation * Vector3.forward * desired;

            transform.position = positionSmoothing > 0f
                ? Vector3.Lerp(transform.position, position, 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime))
                : position;

            transform.rotation = rotation;
        }

        private void ApplyLook()
        {
            Vector2 look = Vector2.zero;

            // Read the player's saved setting rather than the inspector value, so the
            // options screen actually does something. The serialized fields remain as
            // an explicit override for testing.
            float scale = overrideSensitivity ? mouseSensitivity : GameSettings.MouseSensitivity;

            if (_lookAction != null)
            {
                look = _lookAction.ReadValue<Vector2>();

                // A stick reports a held position, not a per-frame delta, so it has to
                // be integrated over time or it barely moves at high framerates.
                if (_lookAction.activeControl?.device is Gamepad)
                {
                    float gamepad = overrideSensitivity
                        ? gamepadSensitivity
                        : GameSettings.GamepadSensitivity;

                    scale = gamepad * Time.deltaTime;
                }
            }
            else if (Mouse.current != null)
            {
                look = Mouse.current.delta.ReadValue();
            }

            _yaw += look.x * rotationSpeed * scale;

            // Screen-space up normally means look up, which is pitching the camera
            // down; inverting flips that back for players who prefer it.
            float pitchDelta = look.y * rotationSpeed * scale;
            _pitch += GameSettings.InvertY ? pitchDelta : -pitchDelta;

            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        /// <summary>
        /// Sweeps from the player out to the camera; on a hit, sits just in front of it.
        /// Snaps in immediately but eases back out, because a slow pull-in would let the
        /// camera spend a moment inside the wall.
        /// </summary>
        private float ResolveDistance(Vector3 pivot, Quaternion rotation)
        {
            if (!avoidGeometry)
            {
                _currentDistance = distance;
                return distance;
            }

            Vector3 direction = -(rotation * Vector3.forward);
            float target = distance;

            if (Physics.SphereCast(pivot, collisionPadding, direction, out RaycastHit hit,
                                   distance, occluderLayers, QueryTriggerInteraction.Ignore))
            {
                target = Mathf.Max(0.2f, hit.distance - collisionPadding * 0.5f);
            }

            _currentDistance = target < _currentDistance
                ? target
                : Mathf.MoveTowards(_currentDistance, target, returnSpeed * Time.deltaTime);

            return _currentDistance;
        }
    }
}

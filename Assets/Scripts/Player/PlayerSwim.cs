using TreeFrogAdventure.Hazards;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// Swimming. Takes ownership of movement while the frog is in water, the same way
    /// TongueGrapple and VineClimb do — PlayerController early-returns on
    /// <see cref="IsSwimming"/> and this drives the CharacterController itself.
    ///
    /// Water is found by overlap against the Water layer rather than trigger callbacks:
    /// the player needs submersion DEPTH (for buoyancy and for knowing when the head
    /// breaks the surface), which a one-shot OnTriggerEnter cannot give. It also avoids
    /// adding 192 more trigger callbacks per scene to PlayerHealth's already-busy
    /// OnTriggerStay.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerSwim : MonoBehaviour
    {
        private static readonly Collider[] Overlaps = new Collider[8];

        private CharacterController _cc;
        private PlayerController _controller;
        private PlayerHealth _health;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private Transform _cameraTransform;

        private Vector3 _velocity;
        private WaterVolume _water;
        private float _surfaceY;

        /// <summary>True while the frog is swimming. PlayerController yields to this.</summary>
        public bool IsSwimming { get; private set; }

        /// <summary>How far the frog's centre is below the surface. 0 at or above it.</summary>
        public float SubmersionDepth { get; private set; }

        private void Start()
        {
            _cc = GetComponent<CharacterController>();
            _controller = GetComponent<PlayerController>();
            _health = GetComponent<PlayerHealth>();

            var input = GetComponent<PlayerInput>();
            if (input != null)
            {
                _moveAction = input.actions.FindAction("Move");
                _jumpAction = input.actions.FindAction("Jump");
            }

            if (Camera.main != null) _cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (Time.timeScale == 0f) return;

            UpdateWaterState();

            if (!IsSwimming) return;

            HandleSwimMovement();
        }

        // ------------------------------------------------------------ detection --

        private void UpdateWaterState()
        {
            PlayerData data = _controller.Data;
            if (data == null) return;

            Vector3 centre = transform.position + _cc.center;

            int count = Physics.OverlapSphereNonAlloc(
                centre, Mathf.Max(0.1f, _cc.radius), Overlaps,
                data.waterLayers, QueryTriggerInteraction.Collide);

            WaterVolume found = null;
            float bestSurface = float.MinValue;

            for (int i = 0; i < count; i++)
            {
                if (Overlaps[i] == null) continue;

                var volume = Overlaps[i].GetComponentInParent<WaterVolume>();
                if (volume == null) continue;

                // Overlapping pools can share edges; the highest surface wins so the
                // frog floats at the top of the stack rather than inside a lower one.
                if (volume.SurfaceHeight > bestSurface)
                {
                    bestSurface = volume.SurfaceHeight;
                    found = volume;
                }
            }

            if (found == null)
            {
                if (IsSwimming) ExitWater();
                return;
            }

            // Lava and acid reuse the same volume type; touching them is fatal.
            if (found.IsDeadly)
            {
                if (_health != null) _health.Kill();
                return;
            }

            _water = found;
            _surfaceY = bestSurface;
            SubmersionDepth = Mathf.Max(0f, _surfaceY - centre.y);

            if (!IsSwimming) EnterWater();
        }

        private void EnterWater()
        {
            IsSwimming = true;

            // Carry momentum in, but bleed most of the fall off — entering from a great
            // height should splash, not fire the frog to the bottom.
            _velocity = _controller.Velocity;
            _velocity.y = Mathf.Max(_velocity.y * 0.25f, -6f);

            // Water is a fresh footing: give the air jump back.
            _controller.HasUsedDoubleJump = false;
        }

        private void ExitWater()
        {
            IsSwimming = false;
            SubmersionDepth = 0f;
            _water = null;

            // Hand momentum back so leaving water is continuous rather than a dead stop.
            _controller.ApplyExternalVelocity(_velocity);
            _controller.HasUsedDoubleJump = false;
        }

        // ------------------------------------------------------------- movement --

        private void HandleSwimMovement()
        {
            PlayerData data = _controller.Data;
            float dt = Time.deltaTime;

            // --- horizontal ---------------------------------------------------
            Vector3 moveDir = GetCameraRelativeInput();

            if (moveDir.sqrMagnitude >= 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(moveDir),
                    data.rotationSpeed * dt);
            }

            _velocity.x = moveDir.x * data.swimSpeed;
            _velocity.z = moveDir.z * data.swimSpeed;

            // --- vertical -----------------------------------------------------
            bool jumpHeld = _jumpAction != null && _jumpAction.IsPressed();
            bool nearSurface = SubmersionDepth <= data.swimFloatDepth + 0.15f;

            if (jumpHeld && nearSurface)
            {
                // At the surface with Jump held: leap out. This is the only way to
                // leave water upward, so it must not require also finding a ledge.
                _velocity.y = data.waterExitJumpForce;
                _cc.Move(_velocity * dt);
                ExitWater();
                return;
            }

            if (jumpHeld)
            {
                _velocity.y = data.swimUpSpeed;
            }
            else
            {
                _velocity.y += data.waterGravity * dt;

                // Buoyancy scales with depth, so the frog settles at swimFloatDepth
                // instead of oscillating around the surface.
                float depthBelowFloat = SubmersionDepth - data.swimFloatDepth;
                if (depthBelowFloat > 0f)
                {
                    float scale = _water != null ? _water.BuoyancyScale : 1f;
                    _velocity.y += data.buoyancy * scale * depthBelowFloat * dt;
                }
            }

            // Vertical drag only. Horizontal is already set directly from input each
            // frame, so damping it too would just fight that assignment.
            float damping = Mathf.Clamp01(data.waterDrag * dt);
            _velocity.y = Mathf.Lerp(_velocity.y, 0f, damping);

            _cc.Move(_velocity * dt);
        }

        private Vector3 GetCameraRelativeInput()
        {
            Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;
            if (direction.magnitude < 0.1f) return Vector3.zero;

            float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg +
                          (_cameraTransform != null ? _cameraTransform.eulerAngles.y : 0f);

            return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace TreeFrogAdventure.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerController : MonoBehaviour
    {
        public PureMathFrog frogVisuals;

        [Header("Configuration")]
        [SerializeField] private PlayerData _playerData;

        private CharacterController _cc;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private Transform _cameraTransform;
        private TongueGrapple _tongueGrapple;
        private VineClimb _vineClimb;

        private Vector3 _velocity;
        private bool _hasUsedDoubleJump;

        private bool _isWallSliding;
        private Vector3 _wallNormal;
        private float _wallJumpLockTimer;

        public PlayerData Data => _playerData;
        public bool HasUsedDoubleJump { get => _hasUsedDoubleJump; set => _hasUsedDoubleJump = value; }

        private void Start()
        {
            _cc = GetComponent<CharacterController>();
            _tongueGrapple = GetComponent<TongueGrapple>();
            _vineClimb = GetComponent<VineClimb>();

            var playerInput = GetComponent<PlayerInput>();
            _moveAction = playerInput.actions.FindAction("Move");
            _jumpAction = playerInput.actions.FindAction("Jump");

            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (Time.timeScale == 0f) return;

            // If the tongue is currently pulling, skip regular movement and gravity
            if (_tongueGrapple != null && _tongueGrapple.IsPulling)
            {
                return;
            }

            // VineClimb fully owns movement while climbing
            if (_vineClimb != null && _vineClimb.IsClimbing)
            {
                return;
            }

            if (_wallJumpLockTimer > 0f) _wallJumpLockTimer -= Time.deltaTime;

            HandleWallDetection();

            if (_isWallSliding)
            {
                HandleWallSlideMovement();
                _cc.Move(_velocity * Time.deltaTime);
                return;
            }

            frogVisuals.SetWallSliding(false, Vector3.zero);

            // Skip normal horizontal control briefly after a wall jump so the push-off velocity
            // isn't instantly overwritten by input still held toward the wall — otherwise the
            // player could just hold "into wall" and spam jump to climb it for free.
            if (_wallJumpLockTimer <= 0f)
            {
                HandleMovement();
            }

            HandleJump();
            ApplyGravity();

            _cc.Move(_velocity * Time.deltaTime);
        }

        public void ApplyExternalVelocity(Vector3 newVelocity)
        {
            _velocity = newVelocity;
        }

        private Vector3 GetCameraRelativeInputDirection()
        {
            Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector3 inputDirection = new Vector3(input.x, 0f, input.y).normalized;
            if (inputDirection.magnitude < 0.1f) return Vector3.zero;

            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                (_cameraTransform != null ? _cameraTransform.eulerAngles.y : 0f);

            return Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }

        private void HandleMovement()
        {
            Vector3 moveDir = GetCameraRelativeInputDirection();

            if (moveDir.sqrMagnitude >= 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _playerData.rotationSpeed * Time.deltaTime);
            }

            _velocity.x = moveDir.x * _playerData.runSpeed;
            _velocity.z = moveDir.z * _playerData.runSpeed;
        }

        private void HandleWallDetection()
        {
            if (_cc.isGrounded || _wallJumpLockTimer > 0f)
            {
                _isWallSliding = false;
                return;
            }

            Vector3 origin = transform.TransformPoint(_cc.center + _playerData.wallCheckOffset);
            Vector3[] directions = { transform.forward, -transform.forward, transform.right, -transform.right };

            bool foundWall = false;
            Vector3 bestNormal = Vector3.zero;
            float bestDistance = float.MaxValue;
            float maxTiltFromVertical = Mathf.Sin(_playerData.wallMaxSurfaceAngle * Mathf.Deg2Rad);

            foreach (Vector3 dir in directions)
            {
                if (!Physics.Raycast(origin, dir, out RaycastHit hit, _playerData.wallCheckDistance, _playerData.wallLayers))
                {
                    continue;
                }

                // Reject shallow slopes/ramps — only near-vertical surfaces count as walls
                if (Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) > maxTiltFromVertical)
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestNormal = hit.normal;
                    foundWall = true;
                }
            }

            if (!foundWall)
            {
                _isWallSliding = false;
                return;
            }

            Vector3 inputDir = GetCameraRelativeInputDirection();
            bool pushingTowardWall = inputDir.sqrMagnitude > 0.01f && Vector3.Dot(inputDir, -bestNormal) > 0.3f;

            _isWallSliding = pushingTowardWall;
            if (pushingTowardWall) _wallNormal = bestNormal;
        }

        private void HandleWallSlideMovement()
        {
            // Nudge into the wall for stable contact; CharacterController collision keeps it pinned at the surface
            Vector3 intoWall = -_wallNormal * 2f;
            _velocity.x = intoWall.x;
            _velocity.z = intoWall.z;
            _velocity.y = Mathf.Max(_velocity.y + _playerData.gravity * Time.deltaTime, _playerData.wallSlideSpeed);

            frogVisuals.SetWallSliding(true, _wallNormal);

            if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
            {
                TriggerWallJump();
            }
        }

        private void TriggerWallJump()
        {
            _velocity = _wallNormal * _playerData.wallJumpAwayForce + Vector3.up * _playerData.wallJumpUpForce;
            transform.rotation = Quaternion.LookRotation(_wallNormal);

            frogVisuals.TriggerProceduralWallJump(_wallNormal);
            frogVisuals.SetWallSliding(false, Vector3.zero);

            _hasUsedDoubleJump = false;
            _isWallSliding = false;
            _wallJumpLockTimer = _playerData.wallJumpLockDuration;
        }

        private void HandleJump()
        {
            if (_cc.isGrounded) _hasUsedDoubleJump = false;

            if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
            {
                if (_cc.isGrounded)
                {
                    _velocity.y = _playerData.jumpForce;
                }
                else if (!_hasUsedDoubleJump)
                {
                    frogVisuals.TriggerProceduralDoubleJump();
                    _hasUsedDoubleJump = true;
                    _velocity.y = _playerData.doubleJumpForce;
                }
            }
        }

        private void ApplyGravity()
        {
            if (_cc.isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }
            else
            {
                _velocity.y += _playerData.gravity * Time.deltaTime;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_playerData == null) return;

            CharacterController cc = _cc != null ? _cc : GetComponent<CharacterController>();
            if (cc == null) return;

            Vector3 origin = transform.TransformPoint(cc.center + _playerData.wallCheckOffset);
            Vector3[] directions = { transform.forward, -transform.forward, transform.right, -transform.right };
            float maxTiltFromVertical = Mathf.Sin(_playerData.wallMaxSurfaceAngle * Mathf.Deg2Rad);

            foreach (Vector3 dir in directions)
            {
                if (Physics.Raycast(origin, dir, out RaycastHit hit, _playerData.wallCheckDistance, _playerData.wallLayers))
                {
                    bool tooSteep = Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) > maxTiltFromVertical;
                    Gizmos.color = tooSteep ? Color.yellow : Color.green;
                    Gizmos.DrawLine(origin, hit.point);
                    Gizmos.DrawSphere(hit.point, 0.03f);
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(origin, origin + dir * _playerData.wallCheckDistance);
                }
            }
        }
    }
}
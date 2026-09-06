using UnityEngine;
using UnityEngine.InputSystem;

namespace TreeFrogAdventure.Player
{
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    public class VineClimb : MonoBehaviour
    {
        private PlayerController _playerController;
        private CharacterController _cc;
        private InputAction _moveAction;
        private InputAction _jumpAction;

        private bool _isClimbing;
        private VineClimbable _activeVine;
        private float _regrabLockTimer;
        private float _atExtentTimer;

        public bool IsClimbing => _isClimbing;

        private void Start()
        {
            _playerController = GetComponent<PlayerController>();
            _cc = GetComponent<CharacterController>();

            var playerInput = GetComponent<PlayerInput>();
            _moveAction = playerInput.actions.FindAction("Move");
            _jumpAction = playerInput.actions.FindAction("Jump");
        }

        private void Update()
        {
            if (Time.timeScale == 0f) return;

            if (_regrabLockTimer > 0f) _regrabLockTimer -= Time.deltaTime;

            if (!_isClimbing) return;

            HandleClimbMovement();

            if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
            {
                ReleaseVine(true);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isClimbing || _regrabLockTimer > 0f) return;

            VineClimbable vine = other.GetComponent<VineClimbable>();
            if (vine != null) StartClimbing(vine);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_isClimbing) return;

            VineClimbable vine = other.GetComponent<VineClimbable>();
            if (vine == _activeVine) ReleaseVine(false);
        }

        private void StartClimbing(VineClimbable vine)
        {
            _isClimbing = true;
            _activeVine = vine;
            _atExtentTimer = 0f;

            _playerController.ApplyExternalVelocity(Vector3.zero);
            _playerController.HasUsedDoubleJump = false;

            Vector3 toVine = vine.transform.position - transform.position;
            toVine.y = 0f;
            if (toVine.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(toVine.normalized);
            }
        }

        private void HandleClimbMovement()
        {
            Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            PlayerData data = _playerController.Data;

            Vector3 climbMove = _activeVine.VineUp * (input.y * data.climbSpeed);
            Vector3 shimmyMove = transform.right * (input.x * data.climbShimmySpeed);

            _cc.Move((climbMove + shimmyMove) * Time.deltaTime);

            // Clamp only along the vine's own axis so shimmy movement isn't undone
            Vector3 bottom = _activeVine.BottomWorld;
            Vector3 vineDir = _activeVine.VineUp;
            float vineLength = Vector3.Distance(bottom, _activeVine.TopWorld);
            float currentT = Vector3.Dot(transform.position - bottom, vineDir);
            float clampedT = Mathf.Clamp(currentT, 0f, vineLength);
            float overshoot = clampedT - currentT;
            if (Mathf.Abs(overshoot) > 0.0001f) _cc.Move(vineDir * overshoot);

            if (_activeVine.IsAtExtent(transform.position))
            {
                _atExtentTimer += Time.deltaTime;
                if (_atExtentTimer > 0.2f) ReleaseVine(false);
            }
            else
            {
                _atExtentTimer = 0f;
            }
        }

        private void ReleaseVine(bool jumpedOff)
        {
            _isClimbing = false;
            _regrabLockTimer = 0.3f;

            if (jumpedOff)
            {
                PlayerData data = _playerController.Data;
                Vector3 awayDir = transform.position - _activeVine.transform.position;
                awayDir.y = 0f;
                awayDir = awayDir.sqrMagnitude > 0.01f ? awayDir.normalized : -transform.forward;

                Vector3 releaseVelocity = Vector3.up * data.climbExitJumpUpForce + awayDir * data.climbExitJumpAwayForce;
                _playerController.ApplyExternalVelocity(releaseVelocity);
                _playerController.HasUsedDoubleJump = false;
            }
            else
            {
                _playerController.ApplyExternalVelocity(Vector3.zero);
            }

            _activeVine = null;
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace TreeFrogAdventure.Player
{
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    public class TongueGrapple : MonoBehaviour
    {
        [Header("Visuals & Timing")]
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private Transform _mouthPosition;

        [Header("Tongue Dynamics")]
        [SerializeField] private float _tongueShootDuration = 0.35f; // Total time to reach target
        [SerializeField] private float _wobbleIntensity = 0.5f;      // How wide the wobble is
        [SerializeField] private float _wobbleSpeed = 50f;           // How fast it wobbles

        private PlayerController _playerController;
        private CharacterController _cc;
        private InputAction _tongueAction;
        private InputAction _jumpAction;
        private Transform _cameraTransform;

        private enum GrappleState { None, Shooting, Pulling, Retracting }
        private GrappleState _currentState = GrappleState.None;

        private Vector3 _tongueTarget;
        private Vector3 _currentTongueTip;
        private float _shootTimer; // Tracks how long the tongue has been shooting
        private bool _didHitTarget; // False when the shot missed and should just whip back

        public bool IsPulling => _currentState == GrappleState.Pulling;

        private void Start()
        {
            _playerController = GetComponent<PlayerController>();
            _cc = GetComponent<CharacterController>();

            var playerInput = GetComponent<PlayerInput>();
            _tongueAction = playerInput.actions.FindAction("Tongue");
            _jumpAction = playerInput.actions.FindAction("Jump");

            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = false;
            }
        }

        private void Update()
        {
            if (Time.timeScale == 0f) return;

            if (_currentState == GrappleState.Shooting)
            {
                HandleTongueShooting();
                UpdateTongueVisuals();
            }
            else if (_currentState == GrappleState.Pulling)
            {
                HandlePulling();
                UpdateTongueVisuals();
            }
            else if (_currentState == GrappleState.Retracting)
            {
                HandleRetracting();
                UpdateTongueVisuals();
            }
            else
            {
                HandleShootInput();
            }
        }

        private void HandleShootInput()
        {
            if (_tongueAction != null && _tongueAction.WasPressedThisFrame() && _cameraTransform != null)
            {
                // 1. Calculate exactly how far the camera is behind the player
                float distanceToPlayer = Vector3.Distance(_cameraTransform.position, transform.position);

                // 2. Push the raycast's starting position forward by that distance
                Vector3 rayStartPos = _cameraTransform.position + (_cameraTransform.forward * distanceToPlayer);

                // 3. Shoot the ray from this new, safer position
                Ray ray = new Ray(rayStartPos, _cameraTransform.forward);

                // Fire regardless of whether the ray connects — a miss just whips out and back
                _didHitTarget = Physics.Raycast(ray, out RaycastHit hit, _playerController.Data.tongueRange, _playerController.Data.grappleLayer);
                _tongueTarget = _didHitTarget ? hit.point : ray.GetPoint(_playerController.Data.tongueRange);
                _currentTongueTip = _mouthPosition.position;
                _currentState = GrappleState.Shooting;

                _shootTimer = 0f;

                Vector3 lookDir = (_tongueTarget - transform.position).normalized;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }

                if (_lineRenderer != null)
                {
                    UpdateTongueVisuals();
                    _lineRenderer.enabled = true;
                }
            }
        }

        private void HandleTongueShooting()
        {
            bool releasedButton = _tongueAction != null && !_tongueAction.IsPressed();
            if (releasedButton)
            {
                StopGrapple();
                return;
            }

            // 1. Update our timer (normalized from 0.0 to 1.0)
            _shootTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_shootTimer / _tongueShootDuration);

            // 2. The Easing Curve: Multiplying 't' by itself makes it start slow and end incredibly fast
            float easeT = t * t * t * t;

            // 3. Find the base position along the straight line
            Vector3 startPos = _mouthPosition.position;
            Vector3 basePos = Vector3.Lerp(startPos, _tongueTarget, easeT);

            // 4. Calculate a sideways direction to wobble along
            Vector3 directionToTarget = (_tongueTarget - startPos).normalized;
            Vector3 right = Vector3.Cross(directionToTarget, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right; // Fallback if shooting straight up

            // 5. Apply the Sine Wave Wobble (We multiply by (1 - t) so the wobble fades out right before it hits!)
            float currentWobble = Mathf.Sin(_shootTimer * _wobbleSpeed) * _wobbleIntensity * (1f - t);

            _currentTongueTip = basePos + (right * currentWobble);

            // 6. Once fully extended, either start pulling (hit) or whip back (missed)
            if (t >= 1f)
            {
                _currentTongueTip = _tongueTarget; // Snap perfectly to target
                if (_didHitTarget)
                {
                    _currentState = GrappleState.Pulling;
                }
                else
                {
                    _currentState = GrappleState.Retracting;
                    _shootTimer = 0f;
                }
            }
        }

        private void HandleRetracting()
        {
            _shootTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_shootTimer / _tongueShootDuration);
            float easeT = t * t; // ease-in as it snaps back toward the mouth

            _currentTongueTip = Vector3.Lerp(_tongueTarget, _mouthPosition.position, easeT);

            if (t >= 1f)
            {
                StopGrapple();
            }
        }

        private void HandlePulling()
        {
            Vector3 pullDirection = (_tongueTarget - transform.position).normalized;
            Vector3 pullVelocity = pullDirection * _playerController.Data.tonguePullSpeed;

            _cc.Move(pullVelocity * Time.deltaTime);

            float distanceToTarget = Vector3.Distance(transform.position, _tongueTarget);
            bool cancelJump = _jumpAction != null && _jumpAction.WasPressedThisFrame();
            bool releasedButton = _tongueAction != null && !_tongueAction.IsPressed();

            if (distanceToTarget < 1.5f || cancelJump || releasedButton)
            {
                StopGrapple();

                if (cancelJump)
                {
                    Vector3 jumpVelocity = new Vector3(0f, _playerController.Data.jumpForce, 0f);
                    _playerController.ApplyExternalVelocity(jumpVelocity);
                    _playerController.HasUsedDoubleJump = false;
                }
                else
                {
                    _playerController.ApplyExternalVelocity(Vector3.zero);
                }
            }
        }

        private void UpdateTongueVisuals()
        {
            if (_lineRenderer != null && _mouthPosition != null)
            {
                _lineRenderer.SetPosition(0, _mouthPosition.position);
                _lineRenderer.SetPosition(1, _currentTongueTip);
            }
        }

        private void StopGrapple()
        {
            _currentState = GrappleState.None;

            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = false;
            }
        }
    }
}
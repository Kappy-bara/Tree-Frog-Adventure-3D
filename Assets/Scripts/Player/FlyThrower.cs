using TreeFrogAdventure.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// Throws flies on the Attack input.
    ///
    /// The Attack action was already fully bound in InputSystem_Actions (mouse left,
    /// gamepad west, touch tap) but nothing read it — this is the first consumer.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    public class FlyThrower : MonoBehaviour
    {
        [SerializeField] private GameObject flyPrefab;

        [Tooltip("Spawn point. The player rig already has a GrapplePoint at the mouth — " +
                 "reuse it so flies and the tongue come from the same place.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Camera used to aim. Falls back to Camera.main.")]
        [SerializeField] private Camera aimCamera;

        [Tooltip("How far out the aim ray looks for a target before aiming at empty air.")]
        [SerializeField] private float aimDistance = 60f;

        private PlayerController _controller;
        private InputAction _attackAction;
        private float _cooldown;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();

            var input = GetComponent<PlayerInput>();
            _attackAction = input != null ? input.actions.FindAction("Attack") : null;

            if (aimCamera == null) aimCamera = Camera.main;
            if (muzzle == null) muzzle = transform;
        }

        private void Update()
        {
            _cooldown -= Time.deltaTime;

            if (_attackAction == null || flyPrefab == null) return;
            if (PlayerHealth.IsDead) return;
            if (!_attackAction.WasPressedThisFrame()) return;
            if (_cooldown > 0f) return;

            PlayerData data = _controller.Data;
            if (data == null) return;

            if (!GameSession.ConsumeFly()) return;   // out of ammo

            _cooldown = data.flyCooldown;
            Throw(data);
        }

        private void Throw(PlayerData data)
        {
            Vector3 origin = muzzle.position;
            Vector3 direction = GetAimDirection(origin);

            GameObject go = Instantiate(flyPrefab, origin, Quaternion.LookRotation(direction, Vector3.up));

            var projectile = go.GetComponent<FlyProjectile>();
            if (projectile != null)
            {
                projectile.Launch(direction, data.flyThrowForce, data.flyDamage,
                                  data.flyLifetime, data.flyHitLayers);
            }
        }

        /// <summary>
        /// Aims at whatever the crosshair is over, not straight along camera forward.
        /// The muzzle sits at the frog's mouth, off to the side of the camera, so
        /// copying camera forward would make throws drift wide at close range.
        /// </summary>
        private Vector3 GetAimDirection(Vector3 origin)
        {
            if (aimCamera == null) return transform.forward;

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            Vector3 target = Physics.Raycast(ray, out RaycastHit hit, aimDistance,
                                             ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.origin + ray.direction * aimDistance;

            Vector3 direction = target - origin;
            return direction.sqrMagnitude < 0.01f ? aimCamera.transform.forward : direction.normalized;
        }
    }
}

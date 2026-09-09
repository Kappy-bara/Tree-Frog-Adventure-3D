using TreeFrogAdventure.Enemies;
using UnityEngine;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// A thrown fly. The player's ranged answer to Bats and Bees, which spend most of
    /// their time out of stomp reach.
    ///
    /// Moves by sweeping a sphere from last position to next rather than relying on
    /// physics triggers: at ~22 m/s a small projectile can pass straight through a thin
    /// collider between two frames.
    /// </summary>
    public class FlyProjectile : MonoBehaviour
    {
        [SerializeField] private float radius = 0.18f;
        [SerializeField] private float gravity = -9f;
        [SerializeField] private GameObject hitEffectPrefab;

        private Vector3 _velocity;
        private int _damage = 1;
        private float _life;
        private LayerMask _hitLayers;
        private bool _launched;

        public void Launch(Vector3 direction, float speed, int damage, float lifetime, LayerMask hitLayers)
        {
            _velocity = direction.normalized * speed;
            _damage = Mathf.Max(1, damage);
            _life = Mathf.Max(0.1f, lifetime);
            _hitLayers = hitLayers;
            _launched = true;
        }

        private void Update()
        {
            if (!_launched) return;

            float dt = Time.deltaTime;

            _life -= dt;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _velocity.y += gravity * dt;

            Vector3 start = transform.position;
            Vector3 step = _velocity * dt;
            float distance = step.magnitude;

            if (distance > 0f &&
                Physics.SphereCast(start, radius, step.normalized, out RaycastHit hit, distance,
                                   _hitLayers, QueryTriggerInteraction.Collide))
            {
                Impact(hit.collider, hit.point);
                return;
            }

            transform.position = start + step;

            // Point along travel so the placeholder reads as thrown, not floating.
            if (_velocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(_velocity.normalized, Vector3.up);
            }
        }

        private void Impact(Collider other, Vector3 point)
        {
            var enemy = other.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(_damage, DamageKind.Projectile);
            }

            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, point, Quaternion.identity);
            }

            Destroy(gameObject);
        }
    }
}

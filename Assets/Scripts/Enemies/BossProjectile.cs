using TreeFrogAdventure.Fx;
using TreeFrogAdventure.Player;
using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Something a guardian spits at the frog.
    ///
    /// Deliberately the mirror of <see cref="FlyProjectile"/> — same sweep-a-sphere
    /// movement rather than a trigger collider, for the same reason: a small fast
    /// projectile passes straight through thin geometry between two frames. Sweeping
    /// also sidesteps needing a Rigidbody to make trigger callbacks fire against the
    /// player's CharacterController.
    /// </summary>
    public class BossProjectile : MonoBehaviour
    {
        [SerializeField] private float radius = 0.35f;
        [SerializeField] private float gravity = -14f;
        [SerializeField] private float lifetime = 6f;
        [SerializeField] private int damage = 1;

        [Tooltip("What stops the projectile. The player is found through this too.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [SerializeField] private float spinSpeed = 240f;

        private Vector3 _velocity;
        private float _life;
        private bool _launched;

        public void Launch(Vector3 velocity, int damageAmount, LayerMask layers)
        {
            _velocity = velocity;
            damage = Mathf.Max(1, damageAmount);
            hitLayers = layers;
            _life = lifetime;
            _launched = true;
        }

        /// <summary>Aims a ballistic arc that lands on the target after the given time.</summary>
        public static Vector3 ArcVelocity(Vector3 from, Vector3 to, float travelTime, float gravityY)
        {
            travelTime = Mathf.Max(0.1f, travelTime);

            Vector3 delta = to - from;
            Vector3 horizontal = new Vector3(delta.x, 0f, delta.z) / travelTime;

            // Solve the vertical component so the arc peaks and lands on target.
            float vy = delta.y / travelTime - 0.5f * gravityY * travelTime;

            return new Vector3(horizontal.x, vy, horizontal.z);
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
                                   hitLayers, QueryTriggerInteraction.Ignore))
            {
                Impact(hit.collider, hit.point);
                return;
            }

            transform.position = start + step;

            if (spinSpeed != 0f)
            {
                transform.Rotate(Vector3.one, spinSpeed * dt, Space.Self);
            }
        }

        private void Impact(Collider other, Vector3 point)
        {
            var player = other.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }

            FxPool.Play(FxKind.EnemyHit, point);
            Destroy(gameObject);
        }
    }
}

using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Shared plumbing for every enemy: finding the player, facing, home territory and
    /// shutting down on death. Subclasses implement <see cref="Tick"/> only.
    ///
    /// The player is found by tag rather than a reference because enemies live in the
    /// gameplay scene while the player rig may come from elsewhere; the tag lookup
    /// retries on an interval instead of once in Start(), which is what the old
    /// DirectWaypointPatrol did and why it silently did nothing if the enemy woke first.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Perception")]
        [Tooltip("Distance at which this enemy notices the player.")]
        [SerializeField] protected float detectionRadius = 8f;

        [Tooltip("Distance at which it gives up and goes home. Should exceed detection " +
                 "radius, or the enemy flickers between chasing and returning.")]
        [SerializeField] protected float loseInterestRadius = 14f;

        [Tooltip("How far from its spawn point it will roam.")]
        [SerializeField] protected float territoryRadius = 10f;

        [Header("Facing")]
        [SerializeField] protected float turnSpeed = 10f;

        protected Transform Player { get; private set; }
        protected EnemyHealth Health { get; private set; }
        protected EnemyVisual Visual { get; private set; }

        /// <summary>Where the enemy was placed. Roaming is measured from here.</summary>
        public Vector3 HomePosition { get; protected set; }

        private float _playerSearchTimer;
        private const float PlayerSearchInterval = 0.5f;

        protected virtual void Awake()
        {
            Health = GetComponent<EnemyHealth>();
            Visual = GetComponentInChildren<EnemyVisual>();
            HomePosition = transform.position;
        }

        protected virtual void Update()
        {
            if (Health.IsDead) return;

            if (Player == null)
            {
                _playerSearchTimer -= Time.deltaTime;
                if (_playerSearchTimer <= 0f)
                {
                    _playerSearchTimer = PlayerSearchInterval;
                    GameObject found = GameObject.FindWithTag("Player");
                    if (found != null) Player = found.transform;
                }
            }

            Tick(Time.deltaTime);
        }

        /// <summary>Per-frame AI. Only called while alive.</summary>
        protected abstract void Tick(float deltaTime);

        // ------------------------------------------------------------- helpers --

        protected bool HasPlayer => Player != null;

        protected float PlayerDistance =>
            Player == null ? float.MaxValue : Vector3.Distance(transform.position, Player.position);

        /// <summary>Horizontal distance only — vertical separation should not stop a
        /// ground enemy noticing someone standing right above it.</summary>
        protected float PlayerFlatDistance
        {
            get
            {
                if (Player == null) return float.MaxValue;
                Vector3 a = transform.position;
                Vector3 b = Player.position;
                a.y = 0f;
                b.y = 0f;
                return Vector3.Distance(a, b);
            }
        }

        protected Vector3 FlatDirectionTo(Vector3 target)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            return d.sqrMagnitude < 1e-6f ? transform.forward : d.normalized;
        }

        protected void FaceDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                1f - Mathf.Exp(-turnSpeed * deltaTime));
        }

        protected bool IsOutsideTerritory =>
            Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(HomePosition.x, 0f, HomePosition.z)) > territoryRadius;

        protected Vector3 RandomPointInTerritory()
        {
            Vector2 offset = Random.insideUnitCircle * territoryRadius;
            return HomePosition + new Vector3(offset.x, 0f, offset.y);
        }

        protected void ReportMotion(float intensity, bool grounded = true)
        {
            if (Visual != null) Visual.SetMotion(intensity, grounded);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Vector3 origin = Application.isPlaying ? HomePosition : transform.position;

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(origin, territoryRadius);

            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}

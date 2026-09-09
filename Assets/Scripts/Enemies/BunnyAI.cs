using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Bunny — hops around platforms.
    ///
    /// Moves in discrete hops rather than gliding: it plants, pauses, then launches.
    /// Horizontal speed is only applied while airborne, which is what makes it read as
    /// hopping instead of walking. Turns back at cliff edges so it stays on its platform.
    /// </summary>
    [RequireComponent(typeof(GroundEnemyMotor))]
    public class BunnyAI : EnemyBase
    {
        [Header("Hop")]
        [SerializeField] private float hopUpForce = 7f;
        [SerializeField] private float hopSpeed = 4.5f;
        [SerializeField] private float chaseHopSpeed = 6f;

        [Tooltip("Seconds planted between hops while wandering.")]
        [SerializeField] private float restInterval = 0.9f;

        [Tooltip("Seconds planted between hops while chasing the player.")]
        [SerializeField] private float chaseRestInterval = 0.45f;

        private GroundEnemyMotor _motor;
        private Vector3 _wanderTarget;
        private Vector3 _hopDirection;
        private float _restTimer;
        private bool _airborne;

        protected override void Awake()
        {
            base.Awake();
            _motor = GetComponent<GroundEnemyMotor>();
            _wanderTarget = RandomPointInTerritory();
        }

        protected override void Tick(float deltaTime)
        {
            bool chasing = HasPlayer
                           && PlayerFlatDistance <= detectionRadius
                           && !IsOutsideTerritory;

            if (_motor.IsGrounded)
            {
                if (_airborne)
                {
                    // Just landed.
                    _airborne = false;
                    _restTimer = chasing ? chaseRestInterval : restInterval;
                }

                _restTimer -= deltaTime;
                _motor.Idle(deltaTime);
                ReportMotion(0f, true);

                if (_restTimer <= 0f)
                {
                    LaunchHop(chasing);
                }
            }
            else
            {
                // Committed to the arc — direction is locked at launch so the hop has
                // weight instead of steering in mid-air.
                _motor.Move(_hopDirection * (chasing ? chaseHopSpeed : hopSpeed), deltaTime);
                ReportMotion(1f, false);
            }

            if (_hopDirection.sqrMagnitude > 0f)
            {
                FaceDirection(_hopDirection, deltaTime);
            }
        }

        private void LaunchHop(bool chasing)
        {
            Vector3 desired;

            if (chasing)
            {
                desired = FlatDirectionTo(Player.position);
            }
            else
            {
                if (Vector3.Distance(transform.position, _wanderTarget) < 1.5f || IsOutsideTerritory)
                {
                    _wanderTarget = IsOutsideTerritory ? HomePosition : RandomPointInTerritory();
                }
                desired = FlatDirectionTo(_wanderTarget);
            }

            // Do not hop off a ledge or into a wall — pick a new heading instead.
            if (!_motor.HasGroundAhead(desired) || _motor.IsBlocked(desired))
            {
                desired = Quaternion.Euler(0f, Random.Range(120f, 240f), 0f) * desired;
                _wanderTarget = RandomPointInTerritory();
            }

            _hopDirection = desired;
            _airborne = true;
            _motor.Jump(hopUpForce);
        }
    }
}

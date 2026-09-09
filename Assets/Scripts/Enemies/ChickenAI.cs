using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Chicken — charges, then runs away.
    ///
    /// Commits to a short aggressive dash, then panics and flees for a while before it
    /// can be provoked again. The flee window is when it is safe to approach, so the
    /// player learns to bait the charge and punish the retreat.
    /// </summary>
    [RequireComponent(typeof(GroundEnemyMotor))]
    public class ChickenAI : EnemyBase
    {
        private enum State { Idle, Charge, Flee }

        [Header("Speeds")]
        [SerializeField] private float idleSpeed = 1.2f;
        [SerializeField] private float chargeSpeed = 7.5f;
        [SerializeField] private float fleeSpeed = 9f;

        [Header("Timing")]
        [SerializeField] private float chargeDuration = 1.3f;
        [SerializeField] private float fleeDuration = 2.2f;

        [Tooltip("Distance at which the charge turns into a panic.")]
        [SerializeField] private float panicDistance = 2.2f;

        private GroundEnemyMotor _motor;
        private State _state = State.Idle;
        private float _stateTimer;
        private Vector3 _fleeDirection;
        private Vector3 _wanderTarget;

        protected override void Awake()
        {
            base.Awake();
            _motor = GetComponent<GroundEnemyMotor>();
            _wanderTarget = RandomPointInTerritory();
        }

        protected override void Tick(float deltaTime)
        {
            _stateTimer -= deltaTime;

            switch (_state)
            {
                case State.Idle:   TickIdle(deltaTime);   break;
                case State.Charge: TickCharge(deltaTime); break;
                case State.Flee:   TickFlee(deltaTime);   break;
            }
        }

        private void TickIdle(float deltaTime)
        {
            if (HasPlayer && PlayerFlatDistance <= detectionRadius && !IsOutsideTerritory)
            {
                _state = State.Charge;
                _stateTimer = chargeDuration;
                return;
            }

            if (Vector3.Distance(transform.position, _wanderTarget) < 1.2f || IsOutsideTerritory)
            {
                _wanderTarget = IsOutsideTerritory ? HomePosition : RandomPointInTerritory();
            }

            Vector3 dir = FlatDirectionTo(_wanderTarget);
            if (!_motor.HasGroundAhead(dir) || _motor.IsBlocked(dir))
            {
                _wanderTarget = RandomPointInTerritory();
                dir = FlatDirectionTo(_wanderTarget);
            }

            FaceDirection(dir, deltaTime);
            _motor.Move(dir * idleSpeed, deltaTime);
            ReportMotion(0.25f, _motor.IsGrounded);
        }

        private void TickCharge(float deltaTime)
        {
            if (!HasPlayer)
            {
                _state = State.Idle;
                return;
            }

            Vector3 dir = FlatDirectionTo(Player.position);
            FaceDirection(dir, deltaTime);
            _motor.Move(dir * chargeSpeed, deltaTime);
            ReportMotion(1f, _motor.IsGrounded);

            // Lost its nerve: got too close, ran out of time, or hit something.
            if (PlayerFlatDistance <= panicDistance || _stateTimer <= 0f || _motor.IsBlocked(dir, 0.8f))
            {
                _fleeDirection = -dir;
                _state = State.Flee;
                _stateTimer = fleeDuration;
            }
        }

        private void TickFlee(float deltaTime)
        {
            // Re-aim away from the player each frame, but bounce off walls and ledges
            // so it does not pin itself into a corner.
            if (HasPlayer) _fleeDirection = -FlatDirectionTo(Player.position);

            if (!_motor.HasGroundAhead(_fleeDirection) || _motor.IsBlocked(_fleeDirection))
            {
                _fleeDirection = Quaternion.Euler(0f, Random.Range(90f, 270f), 0f) * _fleeDirection;
            }

            FaceDirection(_fleeDirection, deltaTime);
            _motor.Move(_fleeDirection * fleeSpeed, deltaTime);
            ReportMotion(1f, _motor.IsGrounded);

            if (_stateTimer <= 0f)
            {
                _state = State.Idle;
                _wanderTarget = RandomPointInTerritory();
            }
        }
    }
}

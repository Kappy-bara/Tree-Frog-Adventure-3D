using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Rhino — charges at the player.
    ///
    /// Telegraphs before charging (scrapes in place) so the charge is dodgeable rather
    /// than unfair, locks its heading at the moment it commits, and is stunned after
    /// hitting a wall or overshooting. The stun window is the intended opening for the
    /// player to stomp it.
    /// </summary>
    [RequireComponent(typeof(GroundEnemyMotor))]
    public class RhinoAI : EnemyBase
    {
        private enum State { Patrol, Telegraph, Charge, Recover }

        [Header("Patrol")]
        [SerializeField] private float patrolSpeed = 1.8f;

        [Header("Charge")]
        [SerializeField] private float telegraphDuration = 0.8f;
        [SerializeField] private float chargeSpeed = 12f;
        [SerializeField] private float maxChargeDuration = 2.2f;
        [SerializeField] private float recoverDuration = 1.4f;

        [Tooltip("How far past the player it keeps going before giving up the charge.")]
        [SerializeField] private float chargeOvershoot = 3f;

        private GroundEnemyMotor _motor;
        private State _state = State.Patrol;
        private float _stateTimer;
        private Vector3 _chargeDirection;
        private Vector3 _patrolTarget;

        protected override void Awake()
        {
            base.Awake();
            _motor = GetComponent<GroundEnemyMotor>();
            _patrolTarget = RandomPointInTerritory();
        }

        protected override void Tick(float deltaTime)
        {
            _stateTimer -= deltaTime;

            switch (_state)
            {
                case State.Patrol:   TickPatrol(deltaTime);   break;
                case State.Telegraph: TickTelegraph(deltaTime); break;
                case State.Charge:   TickCharge(deltaTime);   break;
                case State.Recover:  TickRecover(deltaTime);  break;
            }
        }

        private void TickPatrol(float deltaTime)
        {
            if (HasPlayer && PlayerFlatDistance <= detectionRadius && !IsOutsideTerritory)
            {
                _state = State.Telegraph;
                _stateTimer = telegraphDuration;
                return;
            }

            if (Vector3.Distance(transform.position, _patrolTarget) < 1.5f || IsOutsideTerritory)
            {
                _patrolTarget = IsOutsideTerritory ? HomePosition : RandomPointInTerritory();
            }

            Vector3 dir = FlatDirectionTo(_patrolTarget);
            if (!_motor.HasGroundAhead(dir) || _motor.IsBlocked(dir))
            {
                _patrolTarget = RandomPointInTerritory();
                dir = FlatDirectionTo(_patrolTarget);
            }

            FaceDirection(dir, deltaTime);
            _motor.Move(dir * patrolSpeed, deltaTime);
            ReportMotion(0.35f, _motor.IsGrounded);
        }

        private void TickTelegraph(float deltaTime)
        {
            // Keep tracking during the wind-up, so the player has to actually move
            // rather than just sidestep once.
            if (HasPlayer) FaceDirection(FlatDirectionTo(Player.position), deltaTime);

            _motor.Idle(deltaTime);
            ReportMotion(0.15f, _motor.IsGrounded);

            if (_stateTimer <= 0f)
            {
                _chargeDirection = HasPlayer ? FlatDirectionTo(Player.position) : transform.forward;
                _state = State.Charge;
                _stateTimer = maxChargeDuration;
            }
        }

        private void TickCharge(float deltaTime)
        {
            _motor.Move(_chargeDirection * chargeSpeed, deltaTime);
            ReportMotion(1f, _motor.IsGrounded);

            bool hitWall = _motor.IsBlocked(_chargeDirection, 0.9f);
            bool noGround = !_motor.HasGroundAhead(_chargeDirection, 1.6f);

            bool pastPlayer = HasPlayer &&
                Vector3.Dot(FlatDirectionTo(Player.position), _chargeDirection) < 0f &&
                PlayerFlatDistance > chargeOvershoot;

            if (hitWall || noGround || pastPlayer || _stateTimer <= 0f)
            {
                _state = State.Recover;
                _stateTimer = recoverDuration;
            }
        }

        private void TickRecover(float deltaTime)
        {
            _motor.Idle(deltaTime);
            ReportMotion(0f, _motor.IsGrounded);

            if (_stateTimer <= 0f)
            {
                _state = State.Patrol;
                _patrolTarget = RandomPointInTerritory();
            }
        }
    }
}

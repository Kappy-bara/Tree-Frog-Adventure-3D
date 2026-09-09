using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Pig — runs while jumping.
    ///
    /// Unlike the Bunny, which only moves while airborne, the Pig runs continuously and
    /// jumps on top of that. The constant bouncing is what makes it awkward to stomp:
    /// its head keeps moving vertically, so the player has to time the landing.
    /// </summary>
    [RequireComponent(typeof(GroundEnemyMotor))]
    public class PigAI : EnemyBase
    {
        [Header("Movement")]
        [SerializeField] private float wanderSpeed = 2.5f;
        [SerializeField] private float chaseSpeed = 6.5f;

        [Header("Jumping")]
        [SerializeField] private float jumpForce = 6f;
        [SerializeField] private float jumpInterval = 0.85f;
        [SerializeField] private float chaseJumpInterval = 0.55f;

        private GroundEnemyMotor _motor;
        private Vector3 _wanderTarget;
        private float _jumpTimer;

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

            Vector3 direction;

            if (chasing)
            {
                direction = FlatDirectionTo(Player.position);
            }
            else
            {
                if (Vector3.Distance(transform.position, _wanderTarget) < 1.4f || IsOutsideTerritory)
                {
                    _wanderTarget = IsOutsideTerritory ? HomePosition : RandomPointInTerritory();
                }
                direction = FlatDirectionTo(_wanderTarget);
            }

            // Only steer away from hazards while grounded — mid-air the arc is committed.
            if (_motor.IsGrounded && (!_motor.HasGroundAhead(direction) || _motor.IsBlocked(direction)))
            {
                _wanderTarget = RandomPointInTerritory();
                direction = FlatDirectionTo(_wanderTarget);
            }

            FaceDirection(direction, deltaTime);
            _motor.Move(direction * (chasing ? chaseSpeed : wanderSpeed), deltaTime);

            _jumpTimer -= deltaTime;
            if (_motor.IsGrounded && _jumpTimer <= 0f)
            {
                _motor.Jump(jumpForce);
                _jumpTimer = chasing ? chaseJumpInterval : jumpInterval;
            }

            ReportMotion(chasing ? 1f : 0.5f, _motor.IsGrounded);
        }
    }
}

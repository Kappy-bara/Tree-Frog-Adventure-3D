using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Bat — flies a circuit and swoops at the player.
    ///
    /// Flyers deliberately do not use GroundEnemyMotor: they want no gravity and no
    /// ground collision, so raw transform movement is correct here rather than a
    /// workaround.
    ///
    /// The swoop target is locked when the dive begins, so it can be dodged. Between
    /// swoops it returns to its circuit, which puts it out of stomp reach — flies are
    /// the intended answer.
    /// </summary>
    public class BatAI : EnemyBase
    {
        private enum State { Circle, Swoop, Return }

        [Header("Flight")]
        [Tooltip("Cruise height above the spawn point.")]
        [SerializeField] private float flightHeight = 5f;
        [SerializeField] private float circleRadius = 4f;
        [SerializeField] private float circleAngularSpeed = 1.1f;
        [SerializeField] private float cruiseSpeed = 4.5f;

        [Header("Swoop")]
        [SerializeField] private float swoopSpeed = 12f;
        [SerializeField] private float returnSpeed = 6f;
        [SerializeField] private float swoopCooldown = 2.5f;

        [Tooltip("How close to the locked target counts as the swoop being finished.")]
        [SerializeField] private float swoopArriveDistance = 1.2f;

        private State _state = State.Circle;
        private float _angle;
        private float _cooldown;
        private Vector3 _swoopTarget;

        protected override void Awake()
        {
            base.Awake();
            _angle = Random.Range(0f, Mathf.PI * 2f);
        }

        protected override void Tick(float deltaTime)
        {
            _cooldown -= deltaTime;

            switch (_state)
            {
                case State.Circle: TickCircle(deltaTime); break;
                case State.Swoop:  TickSwoop(deltaTime);  break;
                case State.Return: TickReturn(deltaTime); break;
            }

            ReportMotion(_state == State.Swoop ? 1f : 0.6f, false);
        }

        private Vector3 CirclePoint()
        {
            return HomePosition
                   + Vector3.up * flightHeight
                   + new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * circleRadius;
        }

        private void TickCircle(float deltaTime)
        {
            _angle += circleAngularSpeed * deltaTime;

            Vector3 target = CirclePoint();
            MoveToward(target, cruiseSpeed, deltaTime);

            // Face along the circuit rather than at the centre of it.
            Vector3 tangent = new Vector3(-Mathf.Sin(_angle), 0f, Mathf.Cos(_angle));
            FaceDirection(tangent, deltaTime);

            if (_cooldown <= 0f && HasPlayer && PlayerDistance <= detectionRadius)
            {
                _swoopTarget = Player.position;
                _state = State.Swoop;
            }
        }

        private void TickSwoop(float deltaTime)
        {
            MoveToward(_swoopTarget, swoopSpeed, deltaTime);
            FaceDirection(_swoopTarget - transform.position, deltaTime);

            if (Vector3.Distance(transform.position, _swoopTarget) <= swoopArriveDistance)
            {
                _state = State.Return;
                _cooldown = swoopCooldown;
            }
        }

        private void TickReturn(float deltaTime)
        {
            Vector3 target = CirclePoint();
            MoveToward(target, returnSpeed, deltaTime);
            FaceDirection(target - transform.position, deltaTime);

            if (Vector3.Distance(transform.position, target) < 1f)
            {
                _state = State.Circle;
            }
        }

        private void MoveToward(Vector3 target, float speed, float deltaTime)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, speed * deltaTime);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Vector3 centre = (Application.isPlaying ? HomePosition : transform.position)
                             + Vector3.up * flightHeight;
            Gizmos.color = new Color(0.5f, 0.4f, 1f, 0.8f);
            Gizmos.DrawWireSphere(centre, circleRadius);
        }
    }
}

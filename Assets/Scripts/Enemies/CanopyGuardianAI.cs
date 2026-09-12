using TreeFrogAdventure.Fx;
using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Guardian of Canopy Heights — level 10.
    ///
    /// A flyer, so it spends most of the fight out of stomp reach: this is the boss
    /// that makes thrown flies mandatory rather than optional. Like BatAI it moves by
    /// transform rather than through GroundEnemyMotor, because it wants neither gravity
    /// nor ground collision.
    ///
    /// The dive is the only time it is reachable on foot, and the dive target locks
    /// when the dive begins, so it can be sidestepped.
    /// </summary>
    public class CanopyGuardianAI : BossBase
    {
        private enum State { Hover, Aim, Dive, Rise }

        [Header("Flight")]
        [SerializeField] private float hoverHeight = 8f;
        [SerializeField] private float hoverRadius = 6f;
        [SerializeField] private float hoverAngularSpeed = 0.8f;
        [SerializeField] private float cruiseSpeed = 6f;

        [Header("Dive")]
        [SerializeField] private float diveSpeed = 18f;
        [SerializeField] private float riseSpeed = 9f;
        [SerializeField] private float aimDuration = 0.9f;
        [SerializeField] private float diveArriveDistance = 1.5f;

        [Tooltip("Seconds it is stompable at the bottom of a dive. This is the only " +
                 "melee opening against a flyer, so it must not be tiny.")]
        [SerializeField] private float diveRecoveryWindow = 1.8f;

        [Tooltip("Seconds between dives at phase 1. Later phases shorten it.")]
        [SerializeField] private float diveIntervalAtFirstPhase = 5f;
        [SerializeField] private float diveIntervalAtLastPhase = 2.2f;

        [Header("Spit")]
        [SerializeField] private BossProjectile projectilePrefab;
        [SerializeField] private float spitIntervalAtFirstPhase = 2.4f;
        [SerializeField] private float spitIntervalAtLastPhase = 0.9f;
        [SerializeField] private float projectileTravelTime = 1.1f;
        [SerializeField] private int projectileDamage = 1;
        [SerializeField] private LayerMask projectileHitLayers = ~0;

        private State _state = State.Hover;
        private float _angle;
        private float _stateTimer;
        private float _diveTimer;
        private float _spitTimer;
        private Vector3 _diveTarget;

        protected override void Awake()
        {
            base.Awake();
            _angle = Random.Range(0f, Mathf.PI * 2f);
        }

        protected override void TickIdle(float deltaTime)
        {
            // Hold the circuit even while asleep, so the guardian is visibly circling
            // above the arena before the fight starts rather than frozen in the air.
            Orbit(deltaTime);
        }

        protected override void TickVulnerable(float deltaTime)
        {
            // Drops to stomp height while stunned. Without this the vulnerable window
            // would be meaningless on a flyer that never comes down.
            Vector3 target = HomePosition + Vector3.up * 1.2f;
            MoveToward(target, riseSpeed, deltaTime);
            ReportMotion(0.2f, false);

            _state = State.Hover;
        }

        protected override void TickPhase(int phase, float deltaTime)
        {
            _stateTimer -= deltaTime;
            _diveTimer -= deltaTime;
            _spitTimer -= deltaTime;

            switch (_state)
            {
                case State.Hover: TickHover(deltaTime); break;
                case State.Aim:   TickAim(deltaTime);   break;
                case State.Dive:  TickDive(deltaTime);  break;
                case State.Rise:  TickRise(deltaTime);  break;
            }
        }

        // --------------------------------------------------------------- states --

        private void TickHover(float deltaTime)
        {
            Orbit(deltaTime);

            if (_spitTimer <= 0f) Spit();

            if (_diveTimer <= 0f && HasPlayer)
            {
                _state = State.Aim;
                _stateTimer = aimDuration;
            }
        }

        private void TickAim(float deltaTime)
        {
            // Hangs still and stares: the tell that a dive is coming.
            if (HasPlayer) FaceDirection(FlatDirectionTo(Player.position), deltaTime);
            ReportMotion(0.3f, false);

            if (_stateTimer > 0f) return;

            _diveTarget = HasPlayer ? Player.position : HomePosition;
            _state = State.Dive;
        }

        private void TickDive(float deltaTime)
        {
            MoveToward(_diveTarget, diveSpeed, deltaTime);
            FaceDirection(_diveTarget - transform.position, deltaTime);
            ReportMotion(1f, false);

            if (Vector3.Distance(transform.position, _diveTarget) <= diveArriveDistance)
            {
                _state = State.Rise;
                _diveTimer = PhaseScalar(diveIntervalAtFirstPhase, diveIntervalAtLastPhase);

                // Bottom of the dive is the one moment this guardian is within stomp
                // reach. Dodge the dive, then punish it before it climbs away.
                Stun(diveRecoveryWindow);
            }
        }

        private void TickRise(float deltaTime)
        {
            Vector3 target = OrbitPoint();
            MoveToward(target, riseSpeed, deltaTime);
            ReportMotion(0.7f, false);

            if (Vector3.Distance(transform.position, target) < 1.5f) _state = State.Hover;
        }

        // -------------------------------------------------------------- helpers --

        private void Orbit(float deltaTime)
        {
            _angle += hoverAngularSpeed * deltaTime;

            MoveToward(OrbitPoint(), cruiseSpeed, deltaTime);

            Vector3 tangent = new Vector3(-Mathf.Sin(_angle), 0f, Mathf.Cos(_angle));
            FaceDirection(tangent, deltaTime);

            ReportMotion(0.5f, false);
        }

        private Vector3 OrbitPoint()
        {
            return HomePosition
                   + Vector3.up * hoverHeight
                   + new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * hoverRadius;
        }

        private void MoveToward(Vector3 target, float speed, float deltaTime)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, speed * deltaTime);
        }

        private void Spit()
        {
            _spitTimer = PhaseScalar(spitIntervalAtFirstPhase, spitIntervalAtLastPhase);

            if (projectilePrefab == null || !HasPlayer) return;

            Vector3 origin = transform.position + Vector3.down * 0.5f;

            // Lead the arc at the player's feet rather than their centre, so a moving
            // target is missed and a standing one is not.
            Vector3 velocity = BossProjectile.ArcVelocity(
                origin, Player.position, projectileTravelTime, -14f);

            BossProjectile shot = Instantiate(projectilePrefab, origin, Quaternion.identity);
            shot.Launch(velocity, projectileDamage, projectileHitLayers);

            FxPool.Play(FxKind.EnemyHit, origin);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Vector3 centre = (Application.isPlaying ? HomePosition : transform.position)
                             + Vector3.up * hoverHeight;

            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(centre, hoverRadius);
        }
    }
}

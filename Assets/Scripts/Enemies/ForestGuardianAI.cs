using TreeFrogAdventure.Fx;
using TreeFrogAdventure.Player;
using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Guardian of the Emerald Forest — level 5.
    ///
    /// The teaching boss. It does one thing, telegraphs it clearly, and misses if you
    /// move: it plants, rears up, then charges in a locked straight line. Charging into
    /// a wall stuns it, which is the fight's second lesson — the arena is a weapon.
    ///
    /// Between charges it slams the ground when the player keeps their distance, so
    /// running away is not a strategy.
    /// </summary>
    [RequireComponent(typeof(GroundEnemyMotor))]
    public class ForestGuardianAI : BossBase
    {
        private enum State { Stalk, Telegraph, Charge, Recover, Slam }

        [Header("Stalk")]
        [SerializeField] private float stalkSpeed = 3f;
        [SerializeField] private float chargeRange = 14f;

        [Header("Charge")]
        [Tooltip("Wind-up before the charge at phase 1. Later phases shorten it.")]
        [SerializeField] private float telegraphAtFirstPhase = 1.2f;
        [SerializeField] private float telegraphAtLastPhase = 0.55f;
        [SerializeField] private float chargeSpeed = 13f;
        [SerializeField] private float chargeDuration = 1.6f;
        [SerializeField] private float recoverDuration = 1.1f;

        [Tooltip("Seconds of the recovery spent stunned after hitting a wall — the " +
                 "window the fight is built around.")]
        [SerializeField] private float wallStunDuration = 2.2f;

        [Header("Slam")]
        [Tooltip("Used when the player stays outside charge range.")]
        [SerializeField] private float slamCooldown = 4f;
        [SerializeField] private float slamRadius = 7f;
        [SerializeField] private float slamUpForce = 9f;
        [SerializeField] private int slamDamage = 1;

        private GroundEnemyMotor _motor;
        private State _state = State.Stalk;
        private float _stateTimer;
        private float _slamTimer;
        private Vector3 _chargeDirection;
        private bool _slamPending;

        protected override void Awake()
        {
            base.Awake();
            _motor = GetComponent<GroundEnemyMotor>();
        }

        protected override void TickIdle(float deltaTime)
        {
            _motor.Idle(deltaTime);
            ReportMotion(0f, _motor.IsGrounded);
        }

        protected override void TickVulnerable(float deltaTime)
        {
            _motor.Idle(deltaTime);
            ReportMotion(0f, _motor.IsGrounded);

            // Come out of the stun into a clean stalk rather than resuming a charge
            // that the player has already dodged.
            _state = State.Stalk;
        }

        protected override void TickPhase(int phase, float deltaTime)
        {
            _stateTimer -= deltaTime;
            _slamTimer -= deltaTime;

            switch (_state)
            {
                case State.Stalk:     TickStalk(deltaTime);     break;
                case State.Telegraph: TickTelegraph(deltaTime); break;
                case State.Charge:    TickCharge(deltaTime);    break;
                case State.Recover:   TickRecover(deltaTime);   break;
                case State.Slam:      TickSlam(deltaTime);      break;
            }
        }

        // --------------------------------------------------------------- states --

        private void TickStalk(float deltaTime)
        {
            if (!HasPlayer)
            {
                _motor.Idle(deltaTime);
                ReportMotion(0f, _motor.IsGrounded);
                return;
            }

            Vector3 toPlayer = FlatDirectionTo(Player.position);
            FaceDirection(toPlayer, deltaTime);

            float distance = PlayerFlatDistance;

            if (distance <= chargeRange)
            {
                Enter(State.Telegraph, PhaseScalar(telegraphAtFirstPhase, telegraphAtLastPhase));
                _motor.Idle(deltaTime);
                return;
            }

            if (_slamTimer <= 0f)
            {
                Enter(State.Slam, 0.8f);
                _slamPending = true;
                _motor.Jump(slamUpForce);
                return;
            }

            _motor.Move(toPlayer * stalkSpeed, deltaTime);
            ReportMotion(0.6f, _motor.IsGrounded);
        }

        private void TickTelegraph(float deltaTime)
        {
            _motor.Idle(deltaTime);
            ReportMotion(0f, _motor.IsGrounded);

            if (HasPlayer) FaceDirection(FlatDirectionTo(Player.position), deltaTime);

            if (_stateTimer > 0f) return;

            // Direction locks here, not during the charge. That is what makes the
            // charge dodgeable and the telegraph worth reading.
            _chargeDirection = HasPlayer ? FlatDirectionTo(Player.position) : transform.forward;
            Enter(State.Charge, chargeDuration);
        }

        private void TickCharge(float deltaTime)
        {
            _motor.Move(_chargeDirection * chargeSpeed, deltaTime);
            ReportMotion(1f, _motor.IsGrounded);

            if (_motor.IsBlocked(_chargeDirection, 1.2f))
            {
                // Rammed a wall: a long, obvious opening, and the only way in for a
                // player who has run out of flies. Stun() is what makes it stompable —
                // without it this was just a pause in the pattern.
                Enter(State.Recover, wallStunDuration);
                Stun(wallStunDuration);
                return;
            }

            if (_stateTimer <= 0f) Enter(State.Recover, recoverDuration);
        }

        private void TickRecover(float deltaTime)
        {
            _motor.Idle(deltaTime);
            ReportMotion(0f, _motor.IsGrounded);

            if (_stateTimer <= 0f) Enter(State.Stalk, 0f);
        }

        private void TickSlam(float deltaTime)
        {
            _motor.Idle(deltaTime);
            ReportMotion(1f, _motor.IsGrounded);

            // Fire on the way down, the frame it lands.
            if (_slamPending && _motor.IsGrounded && _stateTimer < 0.6f)
            {
                _slamPending = false;
                _slamTimer = slamCooldown;
                DoSlam();
            }

            if (_stateTimer <= 0f)
            {
                _slamPending = false;
                Enter(State.Stalk, 0f);
            }
        }

        private void DoSlam()
        {
            FxPool.Play(FxKind.Land, transform.position);

            // Only the player is affected — a shockwave that also killed the arena's
            // own adds would make the summoning phases fight themselves.
            Collider[] hits = Physics.OverlapSphere(transform.position, slamRadius);

            foreach (Collider hit in hits)
            {
                var player = hit.GetComponentInParent<PlayerHealth>();
                if (player == null) continue;

                player.TakeDamage(slamDamage);
                break;
            }
        }

        private void Enter(State next, float duration)
        {
            _state = next;
            _stateTimer = duration;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, slamRadius);
        }
    }
}

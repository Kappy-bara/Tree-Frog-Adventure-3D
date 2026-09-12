using System.Collections.Generic;
using TreeFrogAdventure.Fx;
using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Guardian of the Volcanic Highlands — level 20. The final fight.
    ///
    /// Deliberately built from parts the player has already been taught: the Forest
    /// Guardian's telegraphed charge, plus the swamp's arena denial. What makes it the
    /// last boss is that it denies the ground itself — every charge leaves a trail of
    /// lava behind it, so the arena shrinks as the fight goes on and the final phase is
    /// fought on whatever floor is left.
    ///
    /// Pools expire, or the fight would become unwinnable rather than merely hard.
    /// </summary>
    [RequireComponent(typeof(GroundEnemyMotor))]
    public class VolcanoGuardianAI : BossBase
    {
        private enum State { Stalk, Telegraph, Charge, Recover }

        [Header("Stalk")]
        [SerializeField] private float stalkSpeed = 3.6f;
        [SerializeField] private float chargeRange = 16f;

        [Header("Charge")]
        [SerializeField] private float telegraphAtFirstPhase = 1f;
        [SerializeField] private float telegraphAtLastPhase = 0.4f;
        [SerializeField] private float chargeSpeedAtFirstPhase = 13f;
        [SerializeField] private float chargeSpeedAtLastPhase = 18f;
        [SerializeField] private float chargeDuration = 1.8f;
        [SerializeField] private float recoverDuration = 1f;
        [SerializeField] private float wallStunDuration = 2f;

        [Header("Lava trail")]
        [Tooltip("Dropped behind the guardian while charging. Hazard_LavaPool works as-is.")]
        [SerializeField] private GameObject lavaPoolPrefab;

        [Tooltip("Seconds between pools while charging.")]
        [SerializeField] private float lavaDropInterval = 0.28f;

        [Tooltip("Seconds a pool lasts. Without expiry the arena eventually has no " +
                 "floor left and the fight cannot be won.")]
        [SerializeField] private float lavaLifetime = 9f;

        [Tooltip("Hard cap on simultaneous pools, so a long fight cannot bury the arena.")]
        [SerializeField] private int maxLavaPools = 14;

        private readonly Queue<GameObject> _pools = new();
        private GroundEnemyMotor _motor;
        private State _state = State.Stalk;
        private float _stateTimer;
        private float _lavaTimer;
        private Vector3 _chargeDirection;

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

            _state = State.Stalk;
        }

        protected override void TickPhase(int phase, float deltaTime)
        {
            _stateTimer -= deltaTime;

            switch (_state)
            {
                case State.Stalk:     TickStalk(deltaTime);     break;
                case State.Telegraph: TickTelegraph(deltaTime); break;
                case State.Charge:    TickCharge(deltaTime);    break;
                case State.Recover:   TickRecover(deltaTime);   break;
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

            if (PlayerFlatDistance <= chargeRange)
            {
                _state = State.Telegraph;
                _stateTimer = PhaseScalar(telegraphAtFirstPhase, telegraphAtLastPhase);
                _motor.Idle(deltaTime);
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

            _chargeDirection = HasPlayer ? FlatDirectionTo(Player.position) : transform.forward;
            _state = State.Charge;
            _stateTimer = chargeDuration;
            _lavaTimer = 0f;
        }

        private void TickCharge(float deltaTime)
        {
            float speed = PhaseScalar(chargeSpeedAtFirstPhase, chargeSpeedAtLastPhase);

            _motor.Move(_chargeDirection * speed, deltaTime);
            ReportMotion(1f, _motor.IsGrounded);

            _lavaTimer -= deltaTime;
            if (_lavaTimer <= 0f)
            {
                _lavaTimer = lavaDropInterval;
                DropLava();
            }

            if (_motor.IsBlocked(_chargeDirection, 1.4f))
            {
                // Baiting the charge into the arena wall is the melee route in.
                _state = State.Recover;
                _stateTimer = wallStunDuration;
                Stun(wallStunDuration);
                return;
            }

            if (_stateTimer <= 0f)
            {
                _state = State.Recover;
                _stateTimer = recoverDuration;
            }
        }

        private void TickRecover(float deltaTime)
        {
            _motor.Idle(deltaTime);
            ReportMotion(0f, _motor.IsGrounded);

            if (_stateTimer <= 0f) _state = State.Stalk;
        }

        // ----------------------------------------------------------------- lava --

        private void DropLava()
        {
            if (lavaPoolPrefab == null) return;

            Vector3 position = transform.position;

            GameObject pool = Instantiate(lavaPoolPrefab, position, Quaternion.identity);
            _pools.Enqueue(pool);

            if (lavaLifetime > 0f) Destroy(pool, lavaLifetime);

            // Oldest-first eviction, so the trail reads as cooling behind the guardian.
            while (_pools.Count > maxLavaPools)
            {
                GameObject oldest = _pools.Dequeue();
                if (oldest != null) Destroy(oldest);
            }
        }
    }
}

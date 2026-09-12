using System.Collections.Generic;
using TreeFrogAdventure.Fx;
using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Guardian of the Misty Swamp — level 15.
    ///
    /// An artillery fight rather than a dodge-the-charge fight. It holds ground, lobs
    /// arcing spit that lands where the player is standing, and calls in adds at every
    /// phase — so the pressure comes from the arena filling up rather than from the
    /// guardian itself moving quickly.
    ///
    /// It repositions only when the player closes to melee, which keeps the fight from
    /// collapsing into standing underneath it.
    /// </summary>
    [RequireComponent(typeof(GroundEnemyMotor))]
    public class SwampGuardianAI : BossBase
    {
        [Header("Spit")]
        [SerializeField] private BossProjectile projectilePrefab;
        [SerializeField] private float spitIntervalAtFirstPhase = 2.2f;
        [SerializeField] private float spitIntervalAtLastPhase = 0.8f;
        [SerializeField] private float projectileTravelTime = 1.3f;
        [SerializeField] private int projectileDamage = 1;
        [SerializeField] private LayerMask projectileHitLayers = ~0;

        [Tooltip("Shots fired per volley in the final phase. Phase 1 always fires one.")]
        [SerializeField, Range(1, 5)] private int volleyAtLastPhase = 3;

        [Tooltip("How far apart a volley's shots land.")]
        [SerializeField] private float volleySpread = 3f;

        [Tooltip("Seconds it is stompable after firing. This guardian holds ground, " +
                 "so the recovery after a volley is the melee route in.")]
        [SerializeField] private float volleyRecoveryWindow = 1.5f;

        [Header("Reposition")]
        [Tooltip("Backs away when the player gets closer than this.")]
        [SerializeField] private float personalSpace = 5f;
        [SerializeField] private float retreatSpeed = 4f;

        [Header("Adds")]
        [Tooltip("Summoned at the start of each phase after the first.")]
        [SerializeField] private GameObject addPrefab;
        [SerializeField] private int addsPerPhase = 2;
        [SerializeField] private float addSpawnRadius = 6f;

        [Tooltip("Adds are despawned when the guardian dies, so the arena clears.")]
        [SerializeField] private bool despawnAddsOnDefeat = true;

        private readonly List<GameObject> _adds = new();
        private GroundEnemyMotor _motor;
        private float _spitTimer;

        protected override void Awake()
        {
            base.Awake();
            _motor = GetComponent<GroundEnemyMotor>();

            OnDefeated += HandleDefeated;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            OnDefeated -= HandleDefeated;
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
        }

        protected override void TickPhase(int phase, float deltaTime)
        {
            _spitTimer -= deltaTime;

            if (!HasPlayer)
            {
                _motor.Idle(deltaTime);
                ReportMotion(0f, _motor.IsGrounded);
                return;
            }

            Vector3 toPlayer = FlatDirectionTo(Player.position);
            FaceDirection(toPlayer, deltaTime);

            if (PlayerFlatDistance < personalSpace)
            {
                // Back off, but not off a ledge — the arena floor is finite.
                Vector3 away = -toPlayer;

                if (_motor.HasGroundAhead(away) && !_motor.IsBlocked(away))
                {
                    _motor.Move(away * retreatSpeed, deltaTime);
                    ReportMotion(0.8f, _motor.IsGrounded);
                }
                else
                {
                    _motor.Idle(deltaTime);
                    ReportMotion(0f, _motor.IsGrounded);
                }
            }
            else
            {
                _motor.Idle(deltaTime);
                ReportMotion(0.2f, _motor.IsGrounded);
            }

            if (_spitTimer <= 0f) Volley();
        }

        protected override void OnPhaseBegan(int phase)
        {
            SummonAdds();
        }

        // -------------------------------------------------------------- attacks --

        private void Volley()
        {
            _spitTimer = PhaseScalar(spitIntervalAtFirstPhase, spitIntervalAtLastPhase);

            if (projectilePrefab == null || !HasPlayer) return;

            int shots = Mathf.Max(1, Mathf.RoundToInt(PhaseScalar(1f, volleyAtLastPhase)));
            Vector3 origin = transform.position + Vector3.up * 1.5f;

            for (int i = 0; i < shots; i++)
            {
                // Spread the volley around the player so one shot is on target and the
                // rest deny the ground beside it, rather than all three being dodgeable
                // with a single sidestep.
                Vector3 target = Player.position;

                if (shots > 1)
                {
                    float offset = (i - (shots - 1) * 0.5f) * volleySpread;
                    target += transform.right * offset;
                }

                Vector3 velocity = BossProjectile.ArcVelocity(origin, target, projectileTravelTime, -14f);

                BossProjectile shot = Instantiate(projectilePrefab, origin, Quaternion.identity);
                shot.Launch(velocity, projectileDamage, projectileHitLayers);
            }

            FxPool.Play(FxKind.EnemyHit, origin);

            // Winded after firing. Closing the distance during this window is how the
            // fight is won without spending every fly on it.
            Stun(volleyRecoveryWindow);
        }

        private void SummonAdds()
        {
            if (addPrefab == null || addsPerPhase <= 0) return;

            for (int i = 0; i < addsPerPhase; i++)
            {
                float angle = (i / (float)addsPerPhase) * Mathf.PI * 2f + Random.value;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * addSpawnRadius;

                Vector3 position = HomePosition + offset + Vector3.up * 0.5f;

                GameObject add = Instantiate(addPrefab, position, Quaternion.identity);
                _adds.Add(add);

                FxPool.Play(FxKind.BossPhase, position);
            }
        }

        private void HandleDefeated(BossBase boss)
        {
            if (!despawnAddsOnDefeat) return;

            foreach (GameObject add in _adds)
            {
                if (add == null) continue;

                // Kill rather than Destroy, so the adds play their own death effect and
                // the arena clears with a flourish instead of things blinking out.
                var health = add.GetComponent<EnemyHealth>();

                if (health != null) health.Kill();
                else Destroy(add);
            }

            _adds.Clear();
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(Application.isPlaying ? HomePosition : transform.position, addSpawnRadius);
        }
    }
}

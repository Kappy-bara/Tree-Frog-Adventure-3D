using System;
using TreeFrogAdventure.Fx;
using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// A world guardian: the boss at the end of levels 5, 10, 15 and 20.
    ///
    /// Built on EnemyBase so guardians get player-finding, facing and territory for
    /// free and stay one kind of thing with the ordinary enemies — a stomp, a thrown
    /// fly and a lava pool all damage a boss through the same EnemyHealth they damage
    /// a bunny through.
    ///
    /// What a boss adds is a phase structure. Health is divided into equal bands, and
    /// crossing a band boundary opens a fixed vulnerable window: the guardian stops,
    /// becomes stompable, and can be punished. Outside that window it is not stompable
    /// at all, so the fight is a rhythm of waiting for the opening rather than a race
    /// to jump on its head. Subclasses implement <see cref="TickPhase"/> and decide
    /// what each phase does.
    /// </summary>
    public abstract class BossBase : EnemyBase
    {
        [Header("Identity")]
        [SerializeField] private string bossName = "Guardian";

        [Tooltip("Shown under the name when the fight starts.")]
        [SerializeField] private string bossTitle = "Guardian of the Grove";

        [Header("Phases")]
        [Tooltip("Health is split into this many equal bands. Each boundary crossed " +
                 "advances the phase and opens a vulnerable window.")]
        [SerializeField, Range(1, 5)] private int phaseCount = 3;

        [Tooltip("Seconds the guardian is stunned and stompable after losing a band.")]
        [SerializeField] private float vulnerableDuration = 3f;

        [Header("Activation")]
        [Tooltip("Start asleep and wait for a BossArena to wake it. Off makes the " +
                 "guardian live the moment the level loads.")]
        [SerializeField] private bool startInactive = true;

        /// <summary>1-based. Rises as health falls.</summary>
        public int Phase { get; private set; } = 1;

        public int PhaseCount => Mathf.Max(1, phaseCount);
        public string BossName => bossName;
        public string BossTitle => bossTitle;

        /// <summary>True while stunned between phases — the window to punish it.</summary>
        public bool IsVulnerable { get; private set; }

        /// <summary>False until a BossArena wakes it, if startInactive is set.</summary>
        public bool IsActive { get; private set; }

        public event Action<BossBase, int> OnPhaseChanged;
        public event Action<BossBase> OnDefeated;

        private float _vulnerableUntil;
        private bool _authoredStompable;

        protected override void Awake()
        {
            base.Awake();

            _authoredStompable = Health.Stompable;
            IsActive = !startInactive;

            // Outside a vulnerable window the guardian shrugs stomps off. Doing this
            // in Awake rather than trusting the prefab means a guardian authored as
            // stompable still behaves like a boss.
            Health.Stompable = false;

            Health.OnDamaged += HandleDamaged;
            Health.OnDeath += HandleDeath;
        }

        protected virtual void OnDestroy()
        {
            Health.OnDamaged -= HandleDamaged;
            Health.OnDeath -= HandleDeath;
        }

        /// <summary>Called by BossArena when the player enters and the doors shut.</summary>
        public void Activate()
        {
            if (IsActive) return;

            IsActive = true;
            OnPhaseChanged?.Invoke(this, Phase);
        }

        // ------------------------------------------------------------- driving --

        /// <summary>
        /// Sealed: phases are the whole point of this class, so subclasses override
        /// TickPhase and cannot accidentally bypass the phase and stun handling.
        /// </summary>
        protected sealed override void Tick(float deltaTime)
        {
            if (!IsActive)
            {
                TickIdle(deltaTime);
                return;
            }

            if (IsVulnerable)
            {
                if (Time.time >= _vulnerableUntil) EndVulnerable();
                else
                {
                    TickVulnerable(deltaTime);
                    return;
                }
            }

            TickPhase(Phase, deltaTime);
        }

        /// <summary>Per-frame behaviour for the current phase. Phase is 1-based.</summary>
        protected abstract void TickPhase(int phase, float deltaTime);

        /// <summary>
        /// Behaviour while stunned. The default stands still and reports no motion;
        /// a subclass that needs to settle out of a charge can override it.
        /// </summary>
        protected virtual void TickVulnerable(float deltaTime)
        {
            ReportMotion(0f);
        }

        /// <summary>Behaviour before the arena wakes the guardian. Idle by default.</summary>
        protected virtual void TickIdle(float deltaTime)
        {
            ReportMotion(0f);
        }

        /// <summary>Hook for subclasses that spawn adds or change arenas per phase.</summary>
        protected virtual void OnPhaseBegan(int phase) { }

        /// <summary>
        /// Opens the stomp window from a subclass — a guardian that has rammed a wall,
        /// overreached on a dive, or is recovering from a volley.
        ///
        /// This is what makes a boss killable at all. Phase changes need damage to
        /// happen, and damage without this needs thrown flies, so a player arriving
        /// with the default five flies could not take a six-hit-point guardian down.
        /// Every guardian must expose at least one of these openings.
        /// </summary>
        protected void Stun(float duration)
        {
            if (duration <= 0f || Health.IsDead) return;

            // Only ever extends, so a second opening during a stun cannot cut it short.
            float until = Time.time + duration;
            if (IsVulnerable && until <= _vulnerableUntil) return;

            IsVulnerable = true;
            _vulnerableUntil = until;
            Health.Stompable = true;

            FxPool.Play(FxKind.BossPhase, transform.position + Vector3.up);
        }

        // -------------------------------------------------------------- phases --

        private void HandleDamaged(EnemyHealth health, int amount, DamageKind kind)
        {
            if (health.CurrentHealth <= 0) return;

            int target = PhaseForHealth(health);
            if (target <= Phase) return;

            Phase = target;

            FxPool.Play(FxKind.BossPhase, transform.position + Vector3.up);
            OnPhaseChanged?.Invoke(this, Phase);
            OnPhaseBegan(Phase);

            BeginVulnerable();
        }

        /// <summary>
        /// Which band the remaining health falls in. Uses ceiling on the remaining
        /// fraction so a boss is only in its final phase once the last band has begun,
        /// not the instant it takes a scratch.
        /// </summary>
        private int PhaseForHealth(EnemyHealth health)
        {
            float remaining = (float)health.CurrentHealth / health.MaxHealth;
            int bandsLeft = Mathf.CeilToInt(remaining * PhaseCount);

            return Mathf.Clamp(PhaseCount - bandsLeft + 1, 1, PhaseCount);
        }

        private void BeginVulnerable()
        {
            if (vulnerableDuration <= 0f) return;

            IsVulnerable = true;
            _vulnerableUntil = Time.time + vulnerableDuration;
            Health.Stompable = true;
        }

        private void EndVulnerable()
        {
            IsVulnerable = false;
            Health.Stompable = false;
        }

        private void HandleDeath(EnemyHealth health, DamageKind kind)
        {
            // Restore the authored value so a re-used prefab instance is not left
            // permanently un-stompable by this component.
            Health.Stompable = _authoredStompable;

            IsVulnerable = false;
            OnDefeated?.Invoke(this);
        }

        // ------------------------------------------------------------- helpers --

        /// <summary>0 at full health, 1 at death. What the health bar draws.</summary>
        public float HealthFraction =>
            Health == null ? 1f : Mathf.Clamp01((float)Health.CurrentHealth / Health.MaxHealth);

        /// <summary>Scales with phase, so later phases hit harder without new code.</summary>
        protected float PhaseScalar(float atFirst, float atLast)
        {
            if (PhaseCount <= 1) return atLast;
            return Mathf.Lerp(atFirst, atLast, (Phase - 1f) / (PhaseCount - 1f));
        }
    }
}

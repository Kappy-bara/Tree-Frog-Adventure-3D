using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Bee — hovers above the player, then dives straight down.
    ///
    /// The dive is purely vertical with X/Z locked at the moment it commits, which is
    /// what distinguishes it from the Bat's angled swoop: you dodge a Bee by stepping
    /// sideways, and a Bat by moving out of its path.
    /// </summary>
    public class BeeAI : EnemyBase
    {
        private enum State { Track, Dive, Climb }

        [Header("Hover")]
        [Tooltip("Height held above the player while lining up.")]
        [SerializeField] private float hoverHeight = 6f;
        [SerializeField] private float trackSpeed = 4f;

        [Tooltip("How closely it must be lined up overhead before committing to a dive.")]
        [SerializeField] private float alignTolerance = 1.2f;

        [Header("Dive")]
        [SerializeField] private float diveSpeed = 14f;
        [SerializeField] private float climbSpeed = 6f;
        [SerializeField] private float diveCooldown = 2.2f;

        [Tooltip("Stops the dive this far above whatever is below, so it does not bury " +
                 "itself in the terrain.")]
        [SerializeField] private float groundClearance = 0.6f;

        [SerializeField] private LayerMask groundLayers = ~0;

        private State _state = State.Track;
        private float _cooldown;
        private float _diveFloorY;

        protected override void Tick(float deltaTime)
        {
            _cooldown -= deltaTime;

            switch (_state)
            {
                case State.Track: TickTrack(deltaTime); break;
                case State.Dive:  TickDive(deltaTime);  break;
                case State.Climb: TickClimb(deltaTime); break;
            }

            ReportMotion(_state == State.Dive ? 1f : 0.5f, false);
        }

        private void TickTrack(float deltaTime)
        {
            Vector3 target = HasPlayer && PlayerFlatDistance <= detectionRadius
                ? new Vector3(Player.position.x, Player.position.y + hoverHeight, Player.position.z)
                : HomePosition + Vector3.up * hoverHeight;

            transform.position = Vector3.MoveTowards(transform.position, target, trackSpeed * deltaTime);

            if (HasPlayer) FaceDirection(FlatDirectionTo(Player.position), deltaTime);

            bool linedUp = HasPlayer
                           && PlayerFlatDistance <= alignTolerance
                           && PlayerFlatDistance <= detectionRadius;

            if (linedUp && _cooldown <= 0f)
            {
                _diveFloorY = FindFloorBelow();
                _state = State.Dive;
            }
        }

        private void TickDive(float deltaTime)
        {
            // X and Z are frozen for the whole dive — this is the tell that makes it
            // dodgeable sideways.
            Vector3 p = transform.position;
            p.y -= diveSpeed * deltaTime;

            if (p.y <= _diveFloorY)
            {
                p.y = _diveFloorY;
                _state = State.Climb;
                _cooldown = diveCooldown;
            }

            transform.position = p;
        }

        private void TickClimb(float deltaTime)
        {
            float targetY = (HasPlayer ? Player.position.y : HomePosition.y) + hoverHeight;

            Vector3 p = transform.position;
            p.y = Mathf.MoveTowards(p.y, targetY, climbSpeed * deltaTime);
            transform.position = p;

            if (Mathf.Abs(p.y - targetY) < 0.2f)
            {
                _state = State.Track;
            }
        }

        private float FindFloorBelow()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 100f,
                                groundLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y + groundClearance;
            }

            // Nothing below (over a pit) — cap the dive so it does not fall forever.
            return transform.position.y - hoverHeight * 1.5f;
        }
    }
}

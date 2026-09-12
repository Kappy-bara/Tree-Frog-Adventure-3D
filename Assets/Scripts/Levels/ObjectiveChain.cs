using System;
using UnityEngine;

namespace TreeFrogAdventure.Levels
{
    /// <summary>
    /// Turns a level's checkpoints into a locked sequence.
    ///
    /// Only one objective exists at a time: checkpoint 2 is not merely un-reached, it is
    /// not in the world at all until checkpoint 1 is claimed, and the exit does not exist
    /// until the last checkpoint is. On a shared 100x100 map that is what stops a level
    /// from being "walk to whichever marker happens to be nearest" and makes it a route
    /// with a direction.
    ///
    /// Gating by <c>SetActive</c> rather than by a flag is deliberate: a disabled
    /// checkpoint has no renderer to see and no trigger to walk into, so the ordering
    /// cannot be skipped by approaching from an unexpected angle.
    ///
    /// State lives in the scene, so dying and respawning keeps your progress through the
    /// chain, while Retry — which reloads the level — correctly starts it again.
    /// </summary>
    public class ObjectiveChain : MonoBehaviour
    {
        [Tooltip("Checkpoints in route order. Index 0 is live at level start; the rest " +
                 "are revealed one at a time.")]
        [SerializeField] private Checkpoint[] checkpoints = Array.Empty<Checkpoint>();

        [Tooltip("Revealed once the last checkpoint is claimed. The LevelGoal on an " +
                 "ordinary level; the BossArena on a guardian level.")]
        [SerializeField] private GameObject finalObjective;

        [Tooltip("Name shown on the compass for the final objective.")]
        [SerializeField] private string finalObjectiveLabel = "Level Exit";

        [Tooltip("Switched off at level start and never re-enabled here. Used for the " +
                 "exit on a guardian level, where the BossArena opens it on defeat.")]
        [SerializeField] private GameObject[] lockedAtStart = Array.Empty<GameObject>();

        [Tooltip("Announce each newly revealed objective with a banner.")]
        [SerializeField] private bool announce = true;

        /// <summary>The chain in the loaded gameplay scene, for the compass to read.</summary>
        public static ObjectiveChain Current { get; private set; }

        /// <summary>How many checkpoints have been claimed.</summary>
        public int Claimed { get; private set; }

        public int Total => checkpoints != null ? checkpoints.Length : 0;

        /// <summary>True once the exit (or the arena) has been revealed.</summary>
        public bool FinalObjectiveRevealed { get; private set; }

        /// <summary>What the player should be heading for, or null when nothing is left.</summary>
        public Transform CurrentTarget
        {
            get
            {
                if (!FinalObjectiveRevealed)
                {
                    Checkpoint next = ActiveCheckpoint;
                    if (next != null) return next.Marker;
                }

                return finalObjective != null ? finalObjective.transform : null;
            }
        }

        public string CurrentLabel => FinalObjectiveRevealed
            ? finalObjectiveLabel
            : $"Checkpoint {Mathf.Min(Claimed + 1, Total)} of {Total}";

        private Checkpoint ActiveCheckpoint =>
            checkpoints != null && Claimed < checkpoints.Length ? checkpoints[Claimed] : null;

        // ---------------------------------------------------------------------

        private void Awake()
        {
            Current = this;

            // Everything off, then the first one on. Doing it in this order means a
            // chain rebuilt by the level tool cannot leave a stale object enabled.
            for (int i = 0; i < Total; i++)
            {
                Checkpoint checkpoint = checkpoints[i];
                if (checkpoint == null) continue;

                checkpoint.OnActivated -= HandleCheckpointClaimed;
                checkpoint.OnActivated += HandleCheckpointClaimed;

                checkpoint.gameObject.SetActive(i == 0);
            }

            if (finalObjective != null) finalObjective.SetActive(false);

            // On a guardian level the exit is not the chain's to reveal — the arena
            // opens it once the fight is won — but it must still start closed, or the
            // player could walk straight past the arena to it.
            foreach (GameObject locked in lockedAtStart)
            {
                if (locked != null) locked.SetActive(false);
            }

            if (Total == 0)
            {
                // A level with no checkpoints would otherwise have no way to ever open
                // its exit. Better an unlocked exit than an unfinishable level.
                Debug.LogWarning("[ObjectiveChain] No checkpoints wired; revealing the " +
                                 "final objective immediately.", this);
                RevealFinal();
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < Total; i++)
            {
                if (checkpoints[i] != null) checkpoints[i].OnActivated -= HandleCheckpointClaimed;
            }

            if (Current == this) Current = null;
        }

        private void HandleCheckpointClaimed(Checkpoint checkpoint)
        {
            int index = Array.IndexOf(checkpoints, checkpoint);

            // Only the one we are waiting on advances the chain. Anything else is a
            // checkpoint re-firing, which must not skip a step.
            if (index != Claimed) return;

            Claimed++;

            if (Claimed < Total)
            {
                Checkpoint next = checkpoints[Claimed];

                if (next != null)
                {
                    next.gameObject.SetActive(true);
                    Announce($"Checkpoint {Claimed} of {Total}", "The way ahead is open");
                }
                return;
            }

            RevealFinal();
        }

        private void RevealFinal()
        {
            if (FinalObjectiveRevealed) return;
            FinalObjectiveRevealed = true;

            if (finalObjective != null) finalObjective.SetActive(true);

            Announce(finalObjectiveLabel + " Open", "Head for the marker");
        }

        private void Announce(string title, string subtitle)
        {
            if (!announce) return;

            var banner = FindAnyObjectByType<LevelBanner>();
            if (banner != null) banner.ShowMessage(title, subtitle, 1.6f);
        }

        private void OnDrawGizmosSelected()
        {
            if (checkpoints == null) return;

            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            Vector3? previous = null;

            foreach (Checkpoint checkpoint in checkpoints)
            {
                if (checkpoint == null) continue;

                Vector3 at = checkpoint.transform.position;
                Gizmos.DrawWireSphere(at, 1.5f);

                if (previous.HasValue) Gizmos.DrawLine(previous.Value, at);
                previous = at;
            }

            if (previous.HasValue && finalObjective != null)
            {
                Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
                Gizmos.DrawLine(previous.Value, finalObjective.transform.position);
            }
        }
    }
}

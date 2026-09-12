using System.Collections;
using TreeFrogAdventure.Core;
using TreeFrogAdventure.Menu;
using TreeFrogAdventure.Player;
using UnityEngine;

namespace TreeFrogAdventure.Levels
{
    /// <summary>
    /// The end of a level. Walk the frog into it and the next level unlocks.
    ///
    /// This is what closes the progression gap: LevelProgress.CompleteLevel had
    /// no callers anywhere in the project, so nothing ever unlocked level 2 - you
    /// could build all 20 levels and players would still be stuck on the first.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LevelGoal : MonoBehaviour
    {
        [Tooltip("Seconds to wait before moving on, so the pickup/landing reads.")]
        [SerializeField] private float delaySeconds = 1f;

        [Tooltip("Skip the results screen and go straight to the next level. Off (the " +
                 "default) shows the results and lets the player choose.")]
        [SerializeField] private bool loadNextAutomatically;

        private bool _triggered;
        private float _levelStartTime;

        /// <summary>
        /// Fallback only. The real start time comes from LevelContext, because this
        /// object is disabled until the last checkpoint is claimed and its Start would
        /// otherwise fire long after the level actually began.
        /// </summary>
        private void Start() => _levelStartTime = Time.time;

        private float LevelStartTime => LevelContext.Current != null
            ? LevelContext.Current.LevelStartTime
            : _levelStartTime;

        private void Reset()
        {
            // Convenience when added by hand: a goal is always a trigger.
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || !IsPlayer(other)) return;

            _triggered = true;

            int level = LevelContext.Current != null && LevelContext.Current.LevelNumber > 0
                ? LevelContext.Current.LevelNumber
                : LevelLoader.CurrentLevel;

            if (level <= 0)
            {
                Debug.LogWarning("[LevelGoal] Could not determine the current level number; " +
                                 "progress not saved.", this);
                LevelLoader.LoadMainMenu();
                return;
            }

            float elapsed = Time.time - LevelStartTime;

            // Read the record BEFORE recording this run, or the "previous best" the
            // results screen compares against is the time that was just set.
            LevelRecord before = SaveSystem.GetLevel(level);
            float previousBest = before != null ? before.bestTimeSeconds : 0f;

            LevelProgress.CompleteLevel(level);

            SaveSystem.RecordCompletion(level, elapsed, GameSession.Coins);
            SaveSystem.RecordSecrets(level, GameSession.SecretsThisLevel);

            if (loadNextAutomatically)
            {
                // Escape hatch for testing a run of levels back to back.
                var banner = FindAnyObjectByType<LevelBanner>();
                if (banner != null) banner.ShowComplete(delaySeconds);

                StartCoroutine(Finish(level));
                return;
            }

            LevelRecord after = SaveSystem.GetLevel(level);

            LevelCompleteScreen.Show(new LevelCompleteScreen.Result
            {
                Level = level,
                TimeSeconds = elapsed,
                PreviousBestSeconds = previousBest,
                Coins = GameSession.Coins,
                SecretsFound = GameSession.SecretsThisLevel,
                SecretsInLevel = CountSecretsInLevel(),
                CrystalFound = after != null && after.crystalFound,
            });
        }

        /// <summary>
        /// How many secrets this level holds, so the results screen can show "1 / 3"
        /// rather than a bare count the player cannot judge. Inactive ones are included:
        /// a SecretArea may legitimately be disabled until something else reveals it.
        /// </summary>
        private static int CountSecretsInLevel()
        {
            return FindObjectsByType<SecretArea>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        private static bool IsPlayer(Collider other)
        {
            // Check the parent chain too - the collider may sit on a child of the
            // object that actually carries PlayerHealth.
            return other.GetComponentInParent<PlayerHealth>() != null
                   || other.CompareTag("Player");
        }

        private IEnumerator Finish(int level)
        {
            yield return new WaitForSeconds(delaySeconds);

            if (loadNextAutomatically && level < LevelProgress.TotalLevels)
            {
                LevelLoader.LoadLevel(level + 1);
            }
            else
            {
                LevelLoader.LoadMainMenu();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.35f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}

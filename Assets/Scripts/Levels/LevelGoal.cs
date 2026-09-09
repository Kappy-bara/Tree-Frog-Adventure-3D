using System.Collections;
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

        [Tooltip("On completion go straight to the next level. Off returns to the main menu.")]
        [SerializeField] private bool loadNextAutomatically = true;

        private bool _triggered;

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

            LevelProgress.CompleteLevel(level);
            StartCoroutine(Finish(level));
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

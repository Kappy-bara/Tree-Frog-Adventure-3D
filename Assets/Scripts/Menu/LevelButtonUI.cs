using TMPro;
using TreeFrogAdventure.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TreeFrogAdventure.Menu
{
    /// <summary>
    /// One square in the level-select grid.
    ///
    /// Until now this showed a number and a padlock and nothing else, while the save
    /// file quietly accumulated a crystal flag, a secret count and a best time for every
    /// level that nothing ever read — SaveSystem.GetLevel had no callers anywhere in the
    /// game. This is that reader.
    ///
    /// The extra widgets are optional: a button built before this change still works and
    /// simply shows less.
    /// </summary>
    public class LevelButtonUI : MonoBehaviour
    {
        public Button button;
        public TMP_Text label;
        public GameObject lockedOverlay;

        [Header("Progress (optional)")]
        [Tooltip("Shown once the level's Nature Crystal has been recovered.")]
        public GameObject crystalPip;

        [Tooltip("Secrets found in this level, e.g. \"2\". Hidden when none are found.")]
        public TMP_Text secretsLabel;

        [Tooltip("Best completion time. Hidden until the level is completed.")]
        public TMP_Text bestTimeLabel;

        [Tooltip("Tints the button once the level has been completed.")]
        public Image completedTint;

        public void Setup(int levelNumber, bool unlocked, UnityAction onClick)
        {
            label.text = levelNumber.ToString();
            button.interactable = unlocked;
            lockedOverlay.SetActive(!unlocked);

            button.onClick.RemoveAllListeners();
            if (unlocked)
            {
                button.onClick.AddListener(onClick);
            }

            ApplyProgress(unlocked ? SaveSystem.GetLevel(levelNumber) : null);
        }

        /// <summary>
        /// A locked level is passed a null record deliberately: showing that level 12
        /// holds a crystal you have not reached yet would spoil it.
        /// </summary>
        private void ApplyProgress(LevelRecord record)
        {
            bool completed = record != null && record.completed;

            if (crystalPip != null)
            {
                crystalPip.SetActive(record != null && record.crystalFound);
            }

            if (secretsLabel != null)
            {
                bool any = record != null && record.secretsFound > 0;
                secretsLabel.gameObject.SetActive(any);
                if (any) secretsLabel.text = record.secretsFound.ToString();
            }

            if (bestTimeLabel != null)
            {
                bool timed = record != null && record.bestTimeSeconds > 0f;
                bestTimeLabel.gameObject.SetActive(timed);

                if (timed)
                {
                    bestTimeLabel.text = Levels.LevelCompleteScreen.FormatTime(record.bestTimeSeconds);
                }
            }

            if (completedTint != null)
            {
                completedTint.gameObject.SetActive(completed);
            }
        }
    }
}

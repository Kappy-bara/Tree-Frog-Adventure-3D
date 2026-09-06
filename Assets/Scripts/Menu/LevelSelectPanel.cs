using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeFrogAdventure.Menu
{
    public class LevelSelectPanel : MonoBehaviour
    {
        public LevelButtonUI[] levelButtons = new LevelButtonUI[LevelProgress.TotalLevels];

        private void OnEnable()
        {
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            for (int i = 0; i < levelButtons.Length; i++)
            {
                int levelNumber = i + 1;
                bool unlocked = LevelProgress.IsLevelUnlocked(levelNumber);
                levelButtons[i].Setup(levelNumber, unlocked, () => LoadLevel(levelNumber));
            }
        }

        private void LoadLevel(int levelNumber)
        {
            if (!LevelProgress.IsLevelUnlocked(levelNumber)) return;
            SceneManager.LoadScene(LevelProgress.GetSceneName(levelNumber));
        }
    }
}

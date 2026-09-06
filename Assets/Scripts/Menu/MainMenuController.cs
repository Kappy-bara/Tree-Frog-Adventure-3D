using UnityEngine;

namespace TreeFrogAdventure.Menu
{
    public class MainMenuController : MonoBehaviour
    {
        public GameObject mainPanel;
        public GameObject playPanel;
        public GameObject optionsPanel;

        private void Start()
        {
            // Own our cursor state on entry rather than relying on whoever navigated here
            // to leave it correct (e.g. the pause menu locks the cursor for gameplay before
            // it can hand off, since that's the right state for resuming a level).
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            ShowMain();
        }

        public void OnPlayButton()
        {
            mainPanel.SetActive(false);
            playPanel.SetActive(true);
            optionsPanel.SetActive(false);
        }

        public void OnOptionsButton()
        {
            mainPanel.SetActive(false);
            playPanel.SetActive(false);
            optionsPanel.SetActive(true);
        }

        public void OnBackToMain()
        {
            ShowMain();
        }

        public void OnQuitButton()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowMain()
        {
            mainPanel.SetActive(true);
            playPanel.SetActive(false);
            optionsPanel.SetActive(false);
        }
    }
}

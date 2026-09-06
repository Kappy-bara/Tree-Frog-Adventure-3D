using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TreeFrogAdventure.Menu
{
    public class LevelButtonUI : MonoBehaviour
    {
        public Button button;
        public TMP_Text label;
        public GameObject lockedOverlay;

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
        }
    }
}

using System.Collections;
using TMPro;
using TreeFrogAdventure.Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TreeFrogAdventure.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerHealth : MonoBehaviour
    {
        /// <summary>True while the death screen is up — gates pause and camera input.</summary>
        public static bool IsDead { get; private set; }

        [Header("Health")]
        [Tooltip("No max HP cap — this is just the starting value. Pickups add HP, hits remove it.")]
        public int startingHealth = 3;
        public float invincibilityDuration = 1.5f;
        public float blinkInterval = 0.1f;

        [Header("References")]
        [Tooltip("The character's mesh renderer — a child of this object, so it can't be found via GetComponent on the player itself.")]
        [SerializeField] private Renderer characterRenderer;
        [SerializeField] private TextMeshProUGUI healthText;

        private int _currentHealth;
        private bool _isInvincible;
        private TMP_FontAsset _font;

        public int CurrentHealth => _currentHealth;

        private void Awake()
        {
            _currentHealth = startingHealth;
            IsDead = false;
            UpdateHealthDisplay();
        }

        public void TakeDamage(int amount = 1)
        {
            if (_isInvincible || IsDead) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            UpdateHealthDisplay();

            if (_currentHealth <= 0)
            {
                Die();
            }
            else
            {
                StartCoroutine(InvincibilityRoutine());
            }
        }

        public void Heal(int amount)
        {
            _currentHealth += amount;
            UpdateHealthDisplay();
        }

        private void UpdateHealthDisplay()
        {
            if (healthText != null) healthText.text = _currentHealth.ToString();
        }

        private void OnTriggerEnter(Collider other)
        {
            var source = other.GetComponent<DamageSource>();
            if (source != null) TakeDamage(source.damageAmount);
        }

        private void OnTriggerStay(Collider other)
        {
            // A trigger only fires Enter once per continuous overlap — Stay lets contact
            // keep damaging (gated by the invincibility window) if the source doesn't leave.
            var source = other.GetComponent<DamageSource>();
            if (source != null) TakeDamage(source.damageAmount);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var source = hit.collider.GetComponent<DamageSource>();
            if (source != null) TakeDamage(source.damageAmount);
        }

        private IEnumerator InvincibilityRoutine()
        {
            _isInvincible = true;
            float elapsed = 0f;

            while (elapsed < invincibilityDuration)
            {
                if (characterRenderer != null) characterRenderer.enabled = !characterRenderer.enabled;
                yield return new WaitForSeconds(blinkInterval);
                elapsed += blinkInterval;
            }

            if (characterRenderer != null) characterRenderer.enabled = true;
            _isInvincible = false;
        }

        private void Die()
        {
            IsDead = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (characterRenderer != null) characterRenderer.enabled = false;

            ShowDeathScreen();
        }

        // ---------------------------------------------------------------
        // Death screen UI (built at runtime, same approach as PauseMenuController)
        // ---------------------------------------------------------------

        private void ShowDeathScreen()
        {
            EnsureEventSystem();
            _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            var canvasGO = new GameObject("DeathCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            canvasGO.AddComponent<GraphicRaycaster>();

            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(canvasGO.transform, false);
            var dimRect = (RectTransform)dim.transform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImage = dim.AddComponent<Image>();
            dimImage.color = new Color(0.3f, 0f, 0f, 0.7f);

            var title = CreateText("You Died", canvasGO.transform, 80);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 150);
            titleRect.sizeDelta = new Vector2(800, 120);

            var buttonContainer = new GameObject("Buttons", typeof(RectTransform));
            buttonContainer.transform.SetParent(canvasGO.transform, false);
            var containerRect = (RectTransform)buttonContainer.transform;
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(400, 180);
            containerRect.anchoredPosition = new Vector2(0, -60);

            var vlg = buttonContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var restartButton = CreateButton("RestartButton", "Restart", buttonContainer.transform, 360, 70);
            restartButton.onClick.AddListener(OnRestartButton);

            var mainMenuButton = CreateButton("MainMenuButton", "Main Menu", buttonContainer.transform, 360, 70);
            mainMenuButton.onClick.AddListener(OnMainMenuButton);
        }

        private void OnRestartButton()
        {
            IsDead = false;
            Time.timeScale = 1f;
            // Not GetActiveScene(): under additive level loading the active
            // scene is the shared biome environment, so reloading it alone
            // would drop the player and the rest of the gameplay layer.
            LevelLoader.ReloadCurrentLevel();
        }

        private void OnMainMenuButton()
        {
            IsDead = false;
            Time.timeScale = 1f;
            LevelLoader.LoadMainMenu();
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            var uiModule = esGO.AddComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();
        }

        private TextMeshProUGUI CreateText(string content, Transform parent, int fontSize)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            if (_font != null) text.font = _font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string name, string label, Transform parent, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;

            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.12f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGO.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = label;
            if (_font != null) text.font = _font;
            text.fontSize = Mathf.Min(26f, height * 0.45f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }
    }
}

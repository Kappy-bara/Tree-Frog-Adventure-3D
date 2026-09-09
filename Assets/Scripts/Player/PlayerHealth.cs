using System.Collections;
using TMPro;
using TreeFrogAdventure.Core;
using TreeFrogAdventure.Levels;
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

        [Header("Respawn")]
        [Tooltip("Pause after dying before the player reappears at the checkpoint.")]
        [SerializeField] private float respawnDelay = 0.8f;

        private int _currentHealth;
        // An expiry timestamp rather than a bool. With a bool, two overlapping
        // invincibility windows each clear the flag when THEY finish, so a short
        // post-hit window could silently cancel a 10s power-up. Comparing against a
        // timestamp makes overlapping applications extend instead of truncate.
        private float _invincibleUntil;
        private Coroutine _blinkRoutine;
        private bool _isRespawning;
        private TMP_FontAsset _font;

        private PlayerStomp _stomp;
        private PlayerRespawn _respawn;
        private PlayerController _controller;

        public int CurrentHealth => _currentHealth;
        public bool IsInvincible => Time.time < _invincibleUntil;

        private void Awake()
        {
            _currentHealth = startingHealth;
            IsDead = false;

            _stomp = GetComponent<PlayerStomp>();
            _respawn = GetComponent<PlayerRespawn>();
            _controller = GetComponent<PlayerController>();

            UpdateHealthDisplay();
        }

        public void TakeDamage(int amount = 1)
        {
            if (IsInvincible || IsDead || _isRespawning) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            UpdateHealthDisplay();

            if (_currentHealth <= 0)
            {
                Die();
            }
            else
            {
                SetInvincible(invincibilityDuration, blink: true);
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

        private void OnTriggerEnter(Collider other) => ResolveContact(other, Vector3.zero, false);

        private void OnTriggerStay(Collider other)
        {
            // A trigger only fires Enter once per continuous overlap — Stay lets contact
            // keep damaging (gated by the invincibility window) if the source doesn't leave.
            ResolveContact(other, Vector3.zero, false);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
            => ResolveContact(hit.collider, hit.normal, true);

        /// <summary>
        /// Single entry point for every kind of contact. Stomping is tested first: a
        /// successful stomp must NOT also damage the player, and routing both through
        /// here avoids the two components racing on the same collision.
        /// </summary>
        private void ResolveContact(Collider other, Vector3 normal, bool hasNormal)
        {
            if (IsDead || other == null) return;

            if (_stomp != null && _stomp.TryStomp(other, normal, hasNormal)) return;

            var source = other.GetComponentInParent<DamageSource>();

            // EnemyHealth disables DamageSource the moment an enemy dies, so a corpse
            // lingering through its despawn delay stops hurting.
            if (source == null || !source.enabled) return;

            if (source.knockbackForce > 0f && _controller != null)
            {
                Vector3 away = transform.position - source.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 1e-4f) away = -transform.forward;

                _controller.ApplyExternalVelocity(
                    away.normalized * source.knockbackForce + Vector3.up * source.knockbackUpForce);
            }

            if (source.instantKill) Kill();
            else TakeDamage(source.damageAmount);
        }

        /// <summary>
        /// Grants invincibility for at least <paramref name="duration"/> seconds. Used
        /// both by the short post-hit window and by the Invincibility power-up.
        ///
        /// Only ever extends: a shorter window applied on top of a longer one is
        /// ignored, so getting hit during a power-up cannot cut it short.
        /// </summary>
        public void SetInvincible(float duration, bool blink)
        {
            float until = Time.time + Mathf.Max(0f, duration);
            if (until <= _invincibleUntil) return;

            _invincibleUntil = until;

            if (!blink) return;

            // Tracked by reference. The old code called
            // StopCoroutine(nameof(InvincibilityRoutine)), which only stops coroutines
            // STARTED by string name — it was a silent no-op, leaving an orphaned
            // routine still toggling the renderer.
            if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
            _blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        public void ClearInvincibility()
        {
            _invincibleUntil = 0f;
            StopBlinking();
        }

        private IEnumerator BlinkRoutine()
        {
            while (IsInvincible)
            {
                if (characterRenderer != null) characterRenderer.enabled = !characterRenderer.enabled;
                yield return new WaitForSeconds(blinkInterval);
            }

            if (characterRenderer != null) characterRenderer.enabled = true;
            _blinkRoutine = null;
        }

        private void StopBlinking()
        {
            if (_blinkRoutine != null)
            {
                StopCoroutine(_blinkRoutine);
                _blinkRoutine = null;
            }
            if (characterRenderer != null) characterRenderer.enabled = true;
        }

        /// <summary>
        /// Bypasses hit points — lava, bottomless pits, crushers. Still costs a life
        /// and still respawns at the checkpoint.
        /// </summary>
        public void Kill()
        {
            if (IsDead || _isRespawning) return;

            _currentHealth = 0;
            UpdateHealthDisplay();
            Die();
        }

        /// <summary>
        /// Health has hit zero. Spends a life; only a run with no lives left is
        /// actually game over. Previously this reloaded the whole level on any death,
        /// which is why checkpoints could not exist.
        /// </summary>
        private void Die()
        {
            if (IsDead || _isRespawning) return;

            if (GameSession.LoseLife())
            {
                StartCoroutine(RespawnRoutine());
                return;
            }

            IsDead = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (characterRenderer != null) characterRenderer.enabled = false;

            ShowDeathScreen();
        }

        private IEnumerator RespawnRoutine()
        {
            _isRespawning = true;

            ClearInvincibility();

            // Death is a clean slate — power-ups do not survive it.
            var buffs = GetComponent<PlayerBuffs>();
            if (buffs != null) buffs.ClearAll();

            if (characterRenderer != null) characterRenderer.enabled = false;
            if (_controller != null) _controller.enabled = false;

            yield return new WaitForSeconds(respawnDelay);

            if (_respawn != null) _respawn.RespawnNow();

            // Clear inherited momentum, or the player keeps the fatal fall's velocity
            // and immediately dies again at the checkpoint.
            if (_controller != null)
            {
                _controller.enabled = true;
                _controller.ApplyExternalVelocity(Vector3.zero);
            }

            _currentHealth = startingHealth;
            UpdateHealthDisplay();

            if (characterRenderer != null) characterRenderer.enabled = true;

            _isRespawning = false;

            // Brief grace period so you are not instantly re-killed by whatever
            // wandered onto the checkpoint.
            SetInvincible(invincibilityDuration, blink: true);
        }

        // ---------------------------------------------------------------
        // Game over UI (built at runtime, same approach as PauseMenuController).
        // Only reached when the last life is spent — ordinary deaths respawn.
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

            var title = CreateText("Game Over", canvasGO.transform, 80);
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

            // The run ended, so restarting is a fresh run: lives and coins reset.
            GameSession.ResetForNewGame();

            // Not GetActiveScene(): under additive level loading the active
            // scene is the shared biome environment, so reloading it alone
            // would drop the player and the rest of the gameplay layer.
            LevelLoader.ReloadCurrentLevel();
        }

        private void OnMainMenuButton()
        {
            IsDead = false;
            Time.timeScale = 1f;
            GameSession.ResetForNewGame();
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

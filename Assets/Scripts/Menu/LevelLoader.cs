using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeFrogAdventure.Menu
{
    /// <summary>
    /// Loads a level as two scenes: the biome environment plus a small gameplay
    /// scene on top of it.
    ///
    /// The environment loads as the PRIMARY scene, not the additive one. That
    /// ordering matters: fog, ambient light, skybox and the baked occlusion data
    /// all come from the *active* scene, so the biome look and its culling work
    /// with no extra code. The gameplay scene (player spawn, enemies, puzzle
    /// geometry) is layered on additively.
    ///
    /// The payoff is that all 5 levels of a world share one environment scene -
    /// fix a floating rock once instead of five times.
    /// </summary>
    public static class LevelLoader
    {
        /// <summary>0 when no level was entered through this loader (e.g. you
        /// pressed Play directly on a scene in the editor).</summary>
        public static int CurrentLevel { get; private set; }

        private static string _pendingGameplayScene;

        public static void LoadLevel(int levelNumber)
        {
            if (!LevelProgress.IsLevelUnlocked(levelNumber))
            {
                return;
            }

            string environmentScene = LevelProgress.GetEnvironmentScene(levelNumber);
            string gameplayScene = LevelProgress.GetLevelScene(levelNumber);

            if (!Application.CanStreamedLevelBeLoaded(environmentScene))
            {
                Debug.LogError($"[LevelLoader] Environment scene '{environmentScene}' is not in Build Settings. " +
                               "Run Tools > Tree Frog > Build Level Structure.");
                return;
            }

            CurrentLevel = levelNumber;

            // Only queue the additive load if the scene actually exists, so a
            // half-built level still drops you into a walkable world.
            _pendingGameplayScene = Application.CanStreamedLevelBeLoaded(gameplayScene)
                ? gameplayScene
                : null;

            if (_pendingGameplayScene == null)
            {
                Debug.LogWarning($"[LevelLoader] Gameplay scene '{gameplayScene}' not found in Build Settings. " +
                                 "Loading the environment only.");
            }

            SceneManager.sceneLoaded -= OnEnvironmentLoaded;
            SceneManager.sceneLoaded += OnEnvironmentLoaded;

            SceneManager.LoadScene(environmentScene, LoadSceneMode.Single);
        }

        /// <summary>
        /// Restart the current level. Use this instead of reloading the active
        /// scene - the active scene is the environment, so reloading it alone
        /// would throw away the player and everything else in the gameplay layer.
        /// </summary>
        public static void ReloadCurrentLevel()
        {
            if (CurrentLevel >= 1)
            {
                LoadLevel(CurrentLevel);
                return;
            }

            // Entered play mode directly on a scene: best effort.
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public static void LoadMainMenu()
        {
            CurrentLevel = 0;
            _pendingGameplayScene = null;
            SceneManager.sceneLoaded -= OnEnvironmentLoaded;
            SceneManager.LoadScene("MainMenu");
        }

        private static void OnEnvironmentLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnEnvironmentLoaded;

            if (string.IsNullOrEmpty(_pendingGameplayScene))
            {
                return;
            }

            string toLoad = _pendingGameplayScene;
            _pendingGameplayScene = null;

            // Additive, so the environment stays the active scene and keeps
            // providing the lighting settings.
            SceneManager.LoadSceneAsync(toLoad, LoadSceneMode.Additive);
        }
    }
}

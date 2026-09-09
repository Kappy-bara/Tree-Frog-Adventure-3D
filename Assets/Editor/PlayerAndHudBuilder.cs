using System.Text;
using TMPro;
using TreeFrogAdventure.Player;
using TreeFrogAdventure.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Adds the new combat components to the player prefab and builds the HUD.
    ///
    /// Run: Tools > Tree Frog > Setup Player And HUD
    /// Idempotent — re-running only fills in what is missing.
    /// </summary>
    public static class PlayerAndHudBuilder
    {
        private const string PlayerPrefab = "Assets/Prefabs/Player/Player_Camera.prefab";
        private const string FlyPrefab = "Assets/Prefabs/Combat/Fly_Projectile.prefab";
        private const string HudPrefab = "Assets/Prefabs/UI/GameHUD.prefab";

        [MenuItem("Tools/Tree Frog/Setup Player And HUD")]
        public static void Build()
        {
            var report = new StringBuilder();
            report.AppendLine("[PlayerAndHudBuilder] Result:");

            report.AppendLine(SetupPlayer());
            report.AppendLine("  " + BuildHud());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        // ---------------------------------------------------------------- player --

        private static string SetupPlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            if (root == null) return "  FAILED to load " + PlayerPrefab;

            var log = new StringBuilder();

            try
            {
                var controller = root.GetComponentInChildren<PlayerController>(true);
                if (controller == null)
                {
                    return "  FAILED — no PlayerController in " + PlayerPrefab;
                }

                GameObject player = controller.gameObject;

                // Not ??: UnityEngine.Object overloads ==, so a missing component is a
                // "fake null" that the null-coalescing operator does not catch.
                var stomp = player.GetComponent<PlayerStomp>();
                if (stomp == null)
                {
                    player.AddComponent<PlayerStomp>();
                    log.AppendLine("  + PlayerStomp");
                }

                var respawn = player.GetComponent<PlayerRespawn>();
                if (respawn == null)
                {
                    player.AddComponent<PlayerRespawn>();
                    log.AppendLine("  + PlayerRespawn");
                }

                var thrower = player.GetComponent<FlyThrower>();
                if (thrower == null)
                {
                    thrower = player.AddComponent<FlyThrower>();
                    log.AppendLine("  + FlyThrower");
                }

                // Reuse the tongue's existing mouth transform so flies and the tongue
                // originate from the same place.
                Transform muzzle = FindDeep(root.transform, "GrapplePoint");
                Camera cam = root.GetComponentInChildren<Camera>(true);
                var fly = AssetDatabase.LoadAssetAtPath<GameObject>(FlyPrefab);

                var so = new SerializedObject(thrower);
                so.FindProperty("flyPrefab").objectReferenceValue = fly;
                if (muzzle != null) so.FindProperty("muzzle").objectReferenceValue = muzzle;
                if (cam != null) so.FindProperty("aimCamera").objectReferenceValue = cam;
                so.ApplyModifiedPropertiesWithoutUndo();

                log.AppendLine($"  FlyThrower wired: prefab={(fly != null)}, " +
                               $"muzzle={(muzzle != null ? muzzle.name : "MISSING")}, " +
                               $"camera={(cam != null)}");

                // characterRenderer was never assigned, so the invincibility blink did
                // nothing. Point it at the frog's skinned mesh.
                var health = player.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    var hso = new SerializedObject(health);
                    SerializedProperty rendererProp = hso.FindProperty("characterRenderer");
                    if (rendererProp.objectReferenceValue == null)
                    {
                        var skinned = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                        if (skinned != null)
                        {
                            rendererProp.objectReferenceValue = skinned;
                            hso.ApplyModifiedPropertiesWithoutUndo();
                            log.AppendLine("  PlayerHealth.characterRenderer -> " + skinned.name);
                        }
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return log.Length > 0 ? log.ToString().TrimEnd() : "  player already up to date";
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------------- HUD --

        private static string BuildHud()
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            var root = new GameObject("GameHUD");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            TextMeshProUGUI lives = MakeStat(root.transform, "LivesText", "Lives", "x3",
                                             new Vector2(40f, -40f), new Color(1f, 0.45f, 0.5f), font);
            TextMeshProUGUI coins = MakeStat(root.transform, "CoinsText", "Coins", "0",
                                             new Vector2(40f, -110f), new Color(1f, 0.85f, 0.25f), font);
            TextMeshProUGUI flies = MakeStat(root.transform, "FliesText", "Flies", "0",
                                             new Vector2(40f, -180f), new Color(0.4f, 0.9f, 0.6f), font);
            TextMeshProUGUI health = MakeStat(root.transform, "HealthText", "HP", "3",
                                              new Vector2(40f, -250f), new Color(0.45f, 0.85f, 1f), font);

            var hud = root.AddComponent<HudController>();
            var so = new SerializedObject(hud);
            so.FindProperty("livesText").objectReferenceValue = lives;
            so.FindProperty("coinsText").objectReferenceValue = coins;
            so.FindProperty("fliesText").objectReferenceValue = flies;
            so.FindProperty("healthText").objectReferenceValue = health;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlaceholderFactory.SavePrefab(root, HudPrefab);
            return "GameHUD.prefab built (lives / coins / flies / health)";
        }

        /// <summary>A label pair: a small caption and the value beside it.</summary>
        private static TextMeshProUGUI MakeStat(Transform parent, string name, string caption,
                                                string initial, Vector2 anchoredPosition,
                                                Color color, TMP_FontAsset font)
        {
            var row = new GameObject(name + "Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var rect = (RectTransform)row.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(320f, 56f);

            MakeLabel(row.transform, "Caption", caption, 30, color * 0.85f, font,
                      new Vector2(0f, 0f), new Vector2(130f, 56f), TextAlignmentOptions.Left);

            return MakeLabel(row.transform, name, initial, 40, color, font,
                             new Vector2(135f, 0f), new Vector2(185f, 56f), TextAlignmentOptions.Left);
        }

        private static TextMeshProUGUI MakeLabel(Transform parent, string name, string text, int size,
                                                 Color color, TMP_FontAsset font, Vector2 position,
                                                 Vector2 size2, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size2;

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color32(0, 0, 0, 210);

            return label;
        }
    }
}

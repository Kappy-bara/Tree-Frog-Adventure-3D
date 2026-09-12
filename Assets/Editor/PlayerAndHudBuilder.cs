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

                if (player.GetComponent<PlayerSwim>() == null)
                {
                    player.AddComponent<PlayerSwim>();
                    log.AppendLine("  + PlayerSwim");
                }

                if (player.GetComponent<PlayerBuffs>() == null)
                {
                    player.AddComponent<PlayerBuffs>();
                    log.AppendLine("  + PlayerBuffs");
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
            var root = new GameObject("GameHUD");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // ---- one panel instead of five floating labels --------------------
            // The stats used to be five bare text pairs scattered down the left edge
            // with nothing tying them together or separating them from the world.
            var panel = new GameObject("StatsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(28f, -28f);
            panelRect.sizeDelta = new Vector2(330f, 268f);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = UiTheme.Panel;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = UiTheme.Surface;
            panelImage.raycastTarget = false;

            UiTheme.Outline(panelRect);

            var column = new GameObject("Rows", typeof(RectTransform));
            column.transform.SetParent(panel.transform, false);
            UiTheme.Stretch(column);

            var vlg = UiTheme.Column(column, spacing: 8f, align: TextAnchor.UpperLeft);
            vlg.padding = new RectOffset(20, 20, 18, 18);
            vlg.childForceExpandWidth = true;

            // ---- health, as a meter -------------------------------------------
            TextMeshProUGUI health = StatRow(column.transform, "Health", "HEALTH", "3",
                                             new Color(0.45f, 0.85f, 1f));

            RectTransform healthFill = MeterRow(column.transform, "HealthMeter",
                                                new Color(0.45f, 0.85f, 1f));

            TextMeshProUGUI lives = StatRow(column.transform, "Lives", "LIVES", "x3",
                                            new Color(1f, 0.45f, 0.5f));
            TextMeshProUGUI coins = StatRow(column.transform, "Coins", "COINS", "0", UiTheme.Gold);
            TextMeshProUGUI flies = StatRow(column.transform, "Flies", "FLIES", "0", UiTheme.Accent);
            TextMeshProUGUI crystals = StatRow(column.transform, "Crystals", "CRYSTALS", "0",
                                               UiTheme.Crystal);

            // ---- buffs, below the panel ---------------------------------------
            // Outside the panel because the line is empty most of the time, and an
            // empty strip of surface inside the panel reads as a bug.
            TextMeshProUGUI buffs = UiTheme.Label(root.transform, "BuffsText", string.Empty,
                                                  UiTheme.BodySize, UiTheme.Gold,
                                                  TextAlignmentOptions.Left);

            var buffRect = (RectTransform)buffs.transform;
            buffRect.anchorMin = new Vector2(0f, 1f);
            buffRect.anchorMax = new Vector2(0f, 1f);
            buffRect.pivot = new Vector2(0f, 1f);
            buffRect.anchoredPosition = new Vector2(32f, -308f);
            buffRect.sizeDelta = new Vector2(700f, 44f);

            root.AddComponent<ObjectiveCompass>();

            var hud = root.AddComponent<HudController>();
            var so = new SerializedObject(hud);
            so.FindProperty("livesText").objectReferenceValue = lives;
            so.FindProperty("coinsText").objectReferenceValue = coins;
            so.FindProperty("fliesText").objectReferenceValue = flies;
            so.FindProperty("healthText").objectReferenceValue = health;
            so.FindProperty("crystalsText").objectReferenceValue = crystals;
            so.FindProperty("buffsText").objectReferenceValue = buffs;
            so.FindProperty("healthFill").objectReferenceValue = healthFill;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlaceholderFactory.SavePrefab(root, HudPrefab);
            return "GameHUD.prefab built (panel: health meter / lives / coins / flies / crystals, " +
                   "buffs line, objective compass)";
        }

        /// <summary>
        /// A coloured dot, a muted caption and the value, right-aligned so the numbers
        /// line up in a column and can be read at a glance mid-jump.
        /// </summary>
        private static TextMeshProUGUI StatRow(Transform parent, string name, string caption,
                                               string initial, Color color)
        {
            var row = new GameObject(name + "Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 34f;

            var hlg = UiTheme.Row(row, spacing: 10f, align: TextAnchor.MiddleLeft);
            hlg.childForceExpandWidth = false;

            var dot = new GameObject("Dot", typeof(RectTransform));
            dot.transform.SetParent(row.transform, false);

            var dotLayout = dot.AddComponent<LayoutElement>();
            dotLayout.preferredWidth = 14f;
            dotLayout.preferredHeight = 14f;

            var dotImage = dot.AddComponent<Image>();
            dotImage.sprite = UiTheme.Circle;
            dotImage.color = color;
            dotImage.raycastTarget = false;

            TextMeshProUGUI captionLabel = UiTheme.Label(row.transform, "Caption", caption,
                                                         UiTheme.CaptionSize, UiTheme.TextMuted,
                                                         TextAlignmentOptions.Left);
            captionLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;

            TextMeshProUGUI value = UiTheme.Label(row.transform, name + "Text", initial,
                                                  UiTheme.SubheadingSize, color,
                                                  TextAlignmentOptions.Right);
            value.gameObject.AddComponent<LayoutElement>().preferredWidth = 96f;

            return value;
        }

        private static RectTransform MeterRow(Transform parent, string name, Color color)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 14f;

            var track = row.AddComponent<Image>();
            track.sprite = UiTheme.Pill;
            track.type = Image.Type.Sliced;
            track.color = new Color(0f, 0f, 0f, 0.5f);
            track.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(row.transform, false);

            var rect = (RectTransform)fill.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);

            var image = fill.AddComponent<Image>();
            image.sprite = UiTheme.Pill;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;

            return rect;
        }
    }
}

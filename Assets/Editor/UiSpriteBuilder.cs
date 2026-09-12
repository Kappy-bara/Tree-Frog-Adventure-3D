using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Generates the handful of sprites the UI needs.
    ///
    /// The project ships no UI sprites at all, so every panel, button and bar was a
    /// hard-edged untextured quad. That is also why the boss health bar could not use
    /// Image.fillAmount: a Filled image with no sprite silently falls back to a plain
    /// quad. Generating them keeps the "assets are built by tools" idiom the rest of the
    /// project follows, and a real art pass can overwrite the PNGs without touching code.
    ///
    /// Written into Resources so runtime-built screens can load them without a
    /// serialized reference — the pause menu, results screen and HUD are all created in
    /// code from scenes that have no UI prefab to hang references on.
    ///
    /// Run: Tools > Tree Frog > Build UI Sprites
    /// </summary>
    public static class UiSpriteBuilder
    {
        private const string Folder = "Assets/Resources/UI";

        [MenuItem("Tools/Tree Frog/Build UI Sprites")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);

            var report = new StringBuilder();
            report.AppendLine("[UiSpriteBuilder] Result:");

            // Border of 24 on a 64 px texture leaves an 16 px stretchable middle, which
            // is what lets one sprite serve a 90 px level button and a 900 px panel.
            Write("Panel", RoundedRect(64, radius: 18, edge: 0f), border: 24, report);
            Write("PanelOutlined", RoundedRect(64, radius: 18, edge: 2.5f), border: 24, report);
            Write("Pill", RoundedRect(64, radius: 31, edge: 0f), border: 30, report);
            Write("PillOutlined", RoundedRect(64, radius: 31, edge: 2f), border: 30, report);
            Write("Circle", Circle(64), border: 0, report);
            Write("Ring", Ring(64, thickness: 5f), border: 0, report);
            Write("Glow", RadialGlow(128), border: 0, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        // ------------------------------------------------------------ shapes --

        /// <summary>
        /// White rounded rectangle, transparent outside.
        ///
        /// Drawn from a signed distance field so the curve stays smooth at any size
        /// rather than stair-stepping, and left pure white so Image.color can tint it —
        /// one sprite serves every colour in the UI.
        ///
        /// <paramref name="edge"/> above zero produces the OUTLINE only, as a ring of
        /// that thickness. Borders are drawn by stacking an outline image over a filled
        /// one, which lets the fill and the border be tinted independently.
        /// </summary>
        private static Texture2D RoundedRect(int size, float radius, float edge)
        {
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Pixel centre relative to the middle of the texture.
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;

                    float distance = RoundedRectDistance(px, py, half - 1f, half - 1f, radius);

                    // One pixel of feathering: smooth enough to read as a curve, tight
                    // enough that a 9-sliced corner does not blur when stretched.
                    float alpha = edge > 0f
                        ? Mathf.Clamp01(0.5f - (Mathf.Abs(distance + edge * 0.5f) - edge * 0.5f))
                        : Mathf.Clamp01(0.5f - distance);

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return Finish(size, pixels);
        }

        /// <summary>Signed distance to a rounded box: negative inside, positive outside.</summary>
        private static float RoundedRectDistance(float px, float py, float halfX, float halfY, float radius)
        {
            radius = Mathf.Min(radius, Mathf.Min(halfX, halfY));

            float qx = Mathf.Abs(px) - (halfX - radius);
            float qy = Mathf.Abs(py) - (halfY - radius);

            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                       Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));

            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Texture2D Circle(int size)
        {
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;

                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - (half - 1f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance));
                }
            }

            return Finish(size, pixels);
        }

        private static Texture2D Ring(int size, float thickness)
        {
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float outer = half - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // Distance to the ring band rather than to a disc.
                    float distance = Mathf.Abs(r - (outer - thickness * 0.5f)) - thickness * 0.5f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance));
                }
            }

            return Finish(size, pixels);
        }

        /// <summary>Soft radial falloff, for backdrops behind titles and HUD groups.</summary>
        private static Texture2D RadialGlow(int size)
        {
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;

                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));

                    // Squared falloff reads as light rather than as a grey disc.
                    float alpha = (1f - r) * (1f - r);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return Finish(size, pixels);
        }

        private static Texture2D Finish(int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // ------------------------------------------------------------- import --

        private static void Write(string name, Texture2D texture, int border, StringBuilder report)
        {
            string path = $"{Folder}/{name}.png";

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                report.AppendLine($"  {name}: FAILED to import");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;

            // Uncompressed: these are tiny, and block compression puts visible mush in
            // the soft gradients that make the rounded corners read as curves.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            if (border > 0)
            {
                importer.spriteBorder = new Vector4(border, border, border, border);
            }

            importer.SaveAndReimport();

            report.AppendLine($"  {name,-14} {(border > 0 ? $"9-sliced, border {border}" : "single")}");
        }
    }
}

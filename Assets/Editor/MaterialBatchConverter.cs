using UnityEngine;
using UnityEditor;

public class MaterialBatchConverter : Editor
{
    [MenuItem("Tools/Batch Convert Nature Materials to URP Simple Lit")]
    public static void ConvertMaterials()
    {
        // Grab all materials currently selected in the Project window
        Object[] selectedObjects = Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets);

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("Please select the materials you want to convert in the Project window first.");
            return;
        }

        // Allow undoing the action just in case
        Undo.RecordObjects(selectedObjects, "Batch Convert Materials");

        // Find the URP Simple Lit shader
        Shader urpShader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (urpShader == null)
        {
            Debug.LogError("URP Simple Lit shader could not be found.");
            return;
        }

        int convertedCount = 0;

        foreach (Object obj in selectedObjects)
        {
            Material mat = (Material)obj;

            // 1. Store the original legacy texture and color
            Texture originalTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color originalColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

            // 2. Swap the shader to URP Simple Lit
            mat.shader = urpShader;

            // 3. Reassign the texture and color to the new URP property names
            if (originalTex != null)
            {
                mat.SetTexture("_BaseMap", originalTex);
            }
            mat.SetColor("_BaseColor", originalColor);

            // 4. Enable Alpha Clipping for the leaves
            mat.SetFloat("_AlphaClip", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cutoff", 0.5f); // Sets the threshold to 0.5

            // 5. Ensure the Render Queue handles transparency cutouts correctly
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = 2450;

            // Mark material as changed so Unity saves it
            EditorUtility.SetDirty(mat);
            convertedCount++;
        }

        // Save all changes to the disk
        AssetDatabase.SaveAssets();
        Debug.Log($"Success! Converted {convertedCount} materials to URP Simple Lit.");
    }
}
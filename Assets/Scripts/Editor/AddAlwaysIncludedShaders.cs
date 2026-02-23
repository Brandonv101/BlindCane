
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// Adds critical URP shaders to the Always Included Shaders list in Graphics Settings.
/// This prevents shader stripping on Android/Quest builds that causes null shader crashes.
/// 
/// Run via menu: Tools → Add Always Included Shaders
/// Safe to delete after running.
/// </summary>
public static class AddAlwaysIncludedShaders
{
    private static readonly string[] ShadersToInclude = new[]
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Particles/Unlit",
    };

    [MenuItem("Tools/Add Always Included Shaders")]
    public static void AddShaders()
    {
        SerializedObject graphicsSettings = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset")
        );
        
        SerializedProperty arrayProp = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
        
        if (arrayProp == null || !arrayProp.isArray)
        {
            Debug.LogError("[AddAlwaysIncludedShaders] Could not find m_AlwaysIncludedShaders property.");
            return;
        }

        // Collect existing shaders
        HashSet<string> existing = new HashSet<string>();
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            Shader s = arrayProp.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (s != null)
            {
                existing.Add(s.name);
            }
        }

        int addedCount = 0;
        foreach (string shaderName in ShadersToInclude)
        {
            if (existing.Contains(shaderName))
            {
                Debug.Log($"[AddAlwaysIncludedShaders] Already included: {shaderName}");
                continue;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[AddAlwaysIncludedShaders] Shader not found: {shaderName}");
                continue;
            }

            int newIndex = arrayProp.arraySize;
            arrayProp.InsertArrayElementAtIndex(newIndex);
            arrayProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = shader;
            addedCount++;
            Debug.Log($"[AddAlwaysIncludedShaders] ADDED: {shaderName}");
        }

        graphicsSettings.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        Debug.Log($"[AddAlwaysIncludedShaders] Complete: {addedCount} shaders added, {existing.Count} already present.");
    }
}
#endif

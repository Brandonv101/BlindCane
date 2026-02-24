#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Removes heavy URP shaders from Always Included Shaders.
/// Keeping these shaders always included causes very large variant builds on Android.
/// </summary>
public static class AddAlwaysIncludedShaders
{
    private static readonly HashSet<string> HeavyShaders = new HashSet<string>
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Particles/Unlit"
    };

    [MenuItem("Tools/Optimize/Remove Heavy Always Included URP Shaders")]
    public static void RemoveHeavyShaders()
    {
        SerializedObject graphicsSettings = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset")
        );

        SerializedProperty arrayProp = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
        if (arrayProp == null || !arrayProp.isArray)
        {
            Debug.LogError("[ShaderOptimize] Could not find m_AlwaysIncludedShaders.");
            return;
        }

        int removedCount = 0;
        for (int i = arrayProp.arraySize - 1; i >= 0; i--)
        {
            Shader shader = arrayProp.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (shader == null || !HeavyShaders.Contains(shader.name))
            {
                continue;
            }

            arrayProp.DeleteArrayElementAtIndex(i);
            removedCount++;
            Debug.Log($"[ShaderOptimize] Removed from Always Included: {shader.name}");
        }

        graphicsSettings.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[ShaderOptimize] Complete. Removed {removedCount} heavy URP shader entries.");
    }
}
#endif

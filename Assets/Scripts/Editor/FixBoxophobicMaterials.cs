#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repairs common material issues in this project:
/// 1) Corrupt BOXOPHOBIC mesh materials that need URP-compatible shaders.
/// 2) Corrupt project skybox materials whose shader reference is invalid.
/// </summary>
public static class FixBoxophobicMaterials
{
    private static readonly string[] ProjectSkyboxPaths =
    {
        "Assets/Materials/SkyboxBoxophobicDay.mat",
        "Assets/Materials/SkyboxBoxophobicNight.mat"
    };

    [MenuItem("Tools/Fix BOXOPHOBIC Materials")]
    public static void Fix()
    {
        int skyboxFixCount = FixProjectSkyboxMaterials();

        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/BOXOPHOBIC" });
        if (materialGuids.Length == 0)
        {
            Debug.Log("[FixBoxophobicMaterials] No materials found under Assets/BOXOPHOBIC.");
            ForceReserializeAssets(ProjectSkyboxPaths);
            return;
        }

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        Shader urpSimpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (urpLit == null && urpSimpleLit == null)
        {
            Debug.LogError("[FixBoxophobicMaterials] Neither URP/Lit nor URP/Simple Lit shader found. Is URP installed?");
            return;
        }

        Shader targetShader = urpLit != null ? urpLit : urpSimpleLit;
        int fixedCount = 0;
        int skippedCount = 0;

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                continue;
            }

            bool needsFix = false;
            string reason = string.Empty;

            if (mat.shader == null)
            {
                needsFix = true;
                reason = "null shader";
            }
            else if (!mat.shader.isSupported)
            {
                needsFix = true;
                reason = $"unsupported shader '{mat.shader.name}'";
            }
            else if (mat.shader.name == "Standard" || mat.shader.name == "Hidden/InternalErrorShader")
            {
                needsFix = true;
                reason = $"built-in '{mat.shader.name}' (incompatible with URP)";
            }
            else if (mat.shader.name.StartsWith("Hidden/"))
            {
                needsFix = true;
                reason = $"hidden/fallback shader '{mat.shader.name}'";
            }
            else if (path.Contains("LowPoly") && mat.shader.name.Contains("Skybox"))
            {
                needsFix = true;
                reason = $"skybox shader '{mat.shader.name}' assigned to mesh material";
            }

            if (!needsFix)
            {
                skippedCount++;
                continue;
            }

            Color baseColor = Color.white;
            if (mat.HasProperty("_Color"))
            {
                baseColor = mat.GetColor("_Color");
            }
            else if (mat.HasProperty("_BaseColor"))
            {
                baseColor = mat.GetColor("_BaseColor");
            }

            float smoothness = 0.5f;
            if (mat.HasProperty("_Glossiness"))
            {
                smoothness = mat.GetFloat("_Glossiness");
            }
            else if (mat.HasProperty("_Smoothness"))
            {
                smoothness = mat.GetFloat("_Smoothness");
            }

            float metallic = 0f;
            if (mat.HasProperty("_Metallic"))
            {
                metallic = mat.GetFloat("_Metallic");
            }

            mat.shader = targetShader;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", baseColor);
            }
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }
            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }

            EditorUtility.SetDirty(mat);
            fixedCount++;
            Debug.Log($"[FixBoxophobicMaterials] FIXED: {path} - was {reason}, now {targetShader.name}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ForceReserializeMaterials(materialGuids);
        ForceReserializeAssets(ProjectSkyboxPaths);

        Debug.Log($"[FixBoxophobicMaterials] Complete: {fixedCount} BOXOPHOBIC fixed, {skippedCount} already OK, {skyboxFixCount} project skyboxes fixed (total {materialGuids.Length}).");
    }

    private static int FixProjectSkyboxMaterials()
    {
        Shader panoramic = Shader.Find("Skybox/Panoramic");
        Shader cubemap = Shader.Find("Skybox/Cubemap");
        Shader procedural = Shader.Find("Skybox/Procedural");
        Shader skyboxShader = panoramic != null ? panoramic : (cubemap != null ? cubemap : procedural);

        if (skyboxShader == null)
        {
            Debug.LogError("[FixBoxophobicMaterials] No skybox shader found (Skybox/Panoramic, Skybox/Cubemap, Skybox/Procedural).");
            return 0;
        }

        int fixedCount = 0;
        foreach (string path in ProjectSkyboxPaths)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                continue;
            }

            bool invalidShader = mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader";
            bool wrongFamily = !invalidShader && !mat.shader.name.StartsWith("Skybox/");
            if (!invalidShader && !wrongFamily)
            {
                continue;
            }

            mat.shader = skyboxShader;
            EditorUtility.SetDirty(mat);
            fixedCount++;
            Debug.Log($"[FixBoxophobicMaterials] FIXED SKYBOX: {path} -> {skyboxShader.name}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return fixedCount;
    }

    private static void ForceReserializeMaterials(string[] guids)
    {
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        List<string> paths = new List<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                continue;
            }

            SerializedObject so = new SerializedObject(mat);
            so.Update();
            SerializedProperty shaderProp = so.FindProperty("m_Shader");
            if (shaderProp != null && shaderProp.objectReferenceValue != null)
            {
                Shader current = shaderProp.objectReferenceValue as Shader;
                shaderProp.objectReferenceValue = current;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(mat);
            paths.Add(path);
        }

        if (paths.Count == 0)
        {
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ForceReserializeAssets(paths);
        Debug.Log($"[FixBoxophobicMaterials] Force re-serialized {paths.Count} BOXOPHOBIC materials.");
    }

    private static void ForceReserializeAssets(string[] assetPaths)
    {
        if (assetPaths == null || assetPaths.Length == 0)
        {
            return;
        }

        List<string> validPaths = new List<string>();
        foreach (string path in assetPaths)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                continue;
            }

            EditorUtility.SetDirty(asset);
            validPaths.Add(path);
        }

        if (validPaths.Count == 0)
        {
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ForceReserializeAssets(validPaths);
        Debug.Log($"[FixBoxophobicMaterials] Force re-serialized {validPaths.Count} project assets.");
    }

    [MenuItem("Tools/Add URP Shaders to Always Included")]
    public static void AddURPShadersToAlwaysIncluded()
    {
        string[] shaderNames =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit"
        };

        Object graphicsSettings = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        if (graphicsSettings == null)
        {
            Debug.LogError("[AddURPShaders] Could not load GraphicsSettings.asset");
            return;
        }

        SerializedObject serialized = new SerializedObject(graphicsSettings);
        SerializedProperty alwaysIncluded = serialized.FindProperty("m_AlwaysIncludedShaders");
        if (alwaysIncluded == null || !alwaysIncluded.isArray)
        {
            Debug.LogError("[AddURPShaders] Could not find m_AlwaysIncludedShaders property");
            return;
        }

        int added = 0;
        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[AddURPShaders] Shader '{shaderName}' not found, skipping.");
                continue;
            }

            bool found = false;
            for (int i = 0; i < alwaysIncluded.arraySize; i++)
            {
                if (alwaysIncluded.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                continue;
            }

            int index = alwaysIncluded.arraySize;
            alwaysIncluded.InsertArrayElementAtIndex(index);
            alwaysIncluded.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            added++;
            Debug.Log($"[AddURPShaders] Added '{shaderName}' to Always Included Shaders.");
        }

        serialized.ApplyModifiedProperties();
        Debug.Log($"[AddURPShaders] Done. Added {added} shader(s).");
    }
}
#endif

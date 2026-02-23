#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot production fixup:
///   1) Repairs corrupt urpLitShader / urpSimpleLitShader references on BoxophobicScenePopulator
///   2) Runs FixBoxophobicMaterials.Fix() to patch any remaining material issues
///   3) Creates a graphene-style URP/Lit material for the stick body
///   4) Creates a dark-tip URP/Lit material for StickTip
///   5) Assigns both materials to the StickModel and StickTip renderers
///   6) Clears stale PPtr errors by marking the scene dirty and saving
/// </summary>
public static class ProductionFixup
{
    private const string GrapheneMaterialPath = "Assets/Materials/Stick_Graphene.mat";
    private const string TipMaterialPath      = "Assets/Materials/StickTip_Dark.mat";

    [MenuItem("Tools/Run Production Fixup")]
    public static void RunAll()
    {
        Debug.Log("[ProductionFixup] Starting...");

        RepairPopulatorShaderRefs();
        FixBoxophobicMaterials.Fix();
        CreateAndAssignStickMaterials();

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[ProductionFixup] Complete.");
    }

    private static void RepairPopulatorShaderRefs()
    {
        BoxophobicScenePopulator populator =
            Object.FindFirstObjectByType<BoxophobicScenePopulator>(FindObjectsInactive.Include);

        if (populator == null)
        {
            Debug.Log("[ProductionFixup] No BoxophobicScenePopulator in scene.");
            return;
        }

        Shader urpLit       = Shader.Find("Universal Render Pipeline/Lit");
        Shader urpSimpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");

        SerializedObject so = new SerializedObject(populator);
        bool changed = false;

        SerializedProperty litProp = so.FindProperty("urpLitShader");
        if (litProp != null)
        {
            Shader current = litProp.objectReferenceValue as Shader;
            if (current == null || current.name != "Universal Render Pipeline/Lit")
            {
                litProp.objectReferenceValue = urpLit;
                changed = true;
                Debug.Log("[ProductionFixup] Set urpLitShader -> URP/Lit");
            }
        }

        SerializedProperty simpleLitProp = so.FindProperty("urpSimpleLitShader");
        if (simpleLitProp != null)
        {
            Shader current = simpleLitProp.objectReferenceValue as Shader;
            if (current == null || current.name != "Universal Render Pipeline/Simple Lit")
            {
                simpleLitProp.objectReferenceValue = urpSimpleLit;
                changed = true;
                Debug.Log("[ProductionFixup] Set urpSimpleLitShader -> URP/Simple Lit");
            }
        }

        SerializedProperty terrainMat = so.FindProperty("terrainMaterial");
        if (terrainMat != null && terrainMat.objectReferenceValue != null)
        {
            Material mat = terrainMat.objectReferenceValue as Material;
            if (mat != null && (mat.shader == null || !mat.shader.isSupported))
            {
                terrainMat.objectReferenceValue = null;
                changed = true;
                Debug.Log("[ProductionFixup] Cleared stale terrainMaterial reference.");
            }
        }

        if (changed)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(populator);
            Debug.Log("[ProductionFixup] BoxophobicScenePopulator shader references repaired.");
        }
        else
        {
            Debug.Log("[ProductionFixup] Shader references already correct.");
        }
    }

    private static void CreateAndAssignStickMaterials()
    {
        EnsureFolderExists("Assets/Materials");

        Material grapheneMat = GetOrCreateGrapheneMaterial();
        Material tipMat      = GetOrCreateTipMaterial();

        VirtualStickController stick =
            Object.FindFirstObjectByType<VirtualStickController>(FindObjectsInactive.Include);

        if (stick == null)
        {
            Debug.LogWarning("[ProductionFixup] VirtualStickController not found.");
            return;
        }

        Transform stickModel = stick.transform.Find("StickModel");
        if (stickModel != null)
        {
            Renderer r = stickModel.GetComponent<Renderer>();
            if (r != null && grapheneMat != null)
            {
                r.sharedMaterial = grapheneMat;
                EditorUtility.SetDirty(r);
                Debug.Log("[ProductionFixup] Assigned graphene material to StickModel.");
            }
        }

        Transform rayOrigin = stick.transform.Find("RayOrigin");
        if (rayOrigin != null)
        {
            Transform tipTransform = rayOrigin.Find("StickTip");
            if (tipTransform != null)
            {
                Renderer r = tipTransform.GetComponent<Renderer>();
                if (r != null && tipMat != null)
                {
                    r.sharedMaterial = tipMat;
                    EditorUtility.SetDirty(r);
                    Debug.Log("[ProductionFixup] Assigned dark tip material to StickTip.");
                }
            }
        }
    }

    private static Material GetOrCreateGrapheneMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(GrapheneMaterialPath);
        if (existing != null)
        {
            ConfigureGraphene(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
        {
            Debug.LogError("[ProductionFixup] No URP shader for graphene material.");
            return null;
        }

        Material mat = new Material(shader) { name = "Stick_Graphene" };
        ConfigureGraphene(mat);
        AssetDatabase.CreateAsset(mat, GrapheneMaterialPath);
        Debug.Log("[ProductionFixup] Created graphene material at " + GrapheneMaterialPath);
        return mat;
    }

    private static void ConfigureGraphene(Material mat)
    {
        Color baseColor = new Color(0.12f, 0.13f, 0.14f, 1f);

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", baseColor);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", baseColor);

        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.75f);

        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.65f);
        else if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.65f);

        if (mat.HasProperty("_SpecColor"))
            mat.SetColor("_SpecColor", new Color(0.35f, 0.38f, 0.45f, 1f));

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", new Color(0.01f, 0.012f, 0.015f, 1f));
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", 1f);

        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    }

    private static Material GetOrCreateTipMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(TipMaterialPath);
        if (existing != null)
        {
            ConfigureTip(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
        {
            Debug.LogError("[ProductionFixup] No URP shader for tip material.");
            return null;
        }

        Material mat = new Material(shader) { name = "StickTip_Dark" };
        ConfigureTip(mat);
        AssetDatabase.CreateAsset(mat, TipMaterialPath);
        Debug.Log("[ProductionFixup] Created tip material at " + TipMaterialPath);
        return mat;
    }

    private static void ConfigureTip(Material mat)
    {
        Color baseColor = new Color(0.18f, 0.19f, 0.22f, 1f);

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", baseColor);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", baseColor);

        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.25f);

        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.4f);
        else if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.4f);

        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", 1f);

        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    }

    private static void EnsureFolderExists(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path);
        string folder = System.IO.Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolderExists(parent);

        AssetDatabase.CreateFolder(parent, folder);
    }
}
#endif

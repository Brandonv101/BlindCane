using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class BoxophobicScenePopulator : MonoBehaviour
{
    private const string SceneName = "BoxophobicScene";
    private const string RootName = "BoxophobicDecor";

    [Header("Placement")]
    [SerializeField] private Vector2 spawnArea = new Vector2(30f, 30f);
    [SerializeField, Min(0f)] private float clearRadius = 3f;
    [SerializeField] private Vector3 areaCenter = Vector3.zero;
    [SerializeField] private Vector2 heightRange = new Vector2(0f, 0.2f);
    [SerializeField] private int treeCount = 14;
    [SerializeField] private int rockCount = 12;
    [SerializeField] private int grassCount = 24;
    [SerializeField] private bool includeFences = false;

    [Header("Materials")]
    [SerializeField] private bool fixUnsupportedShaders = true;
    [SerializeField] private Color treeColor = new Color(0.25f, 0.38f, 0.25f);
    [SerializeField] private Color rockColor = new Color(0.45f, 0.45f, 0.48f);
    [SerializeField] private Color grassColor = new Color(0.22f, 0.4f, 0.22f);
    [SerializeField] private Color fenceColor = new Color(0.36f, 0.28f, 0.2f);
    [SerializeField] private Color terrainColor = new Color(0.32f, 0.35f, 0.3f);

    [Header("Options")]
    [SerializeField] private bool repopulate = false;
    [SerializeField] private bool autoPopulate = true;
    [SerializeField] private int randomSeed = 4201;

    [SerializeField, HideInInspector] private Material terrainMaterial;
    [SerializeField, HideInInspector] private Material treeMaterial;
    [SerializeField, HideInInspector] private Material rockMaterial;
    [SerializeField, HideInInspector] private Material grassMaterial;
    [SerializeField, HideInInspector] private Material fenceMaterial;

    [Header("Shader References (Do Not Remove)")]
    [SerializeField] private Shader urpLitShader;
    [SerializeField] private Shader urpSimpleLitShader;

    private void OnEnable()
    {
        if (!autoPopulate)
        {
            return;
        }

        TryPopulate();
    }

    private void OnValidate()
    {
        if (!autoPopulate)
        {
            return;
        }

        TryPopulate();
    }

    private void TryPopulate()
    {
        Scene scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneName)
        {
            return;
        }

        Transform root = GetOrCreateRoot();
        if (root == null)
        {
            return;
        }

        if (repopulate)
        {
            ClearChildren(root);
            repopulate = false;
        }

        if (root.childCount == 0)
        {
            Populate(root);
        }

        if (fixUnsupportedShaders)
        {
            FixSceneMaterials();
        }
    }

    private Transform GetOrCreateRoot()
    {
        Transform existing = transform.Find(RootName);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject(RootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root.transform;
    }

    private void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void Populate(Transform root)
    {
#if UNITY_EDITOR
        Random.InitState(randomSeed);

        GameObject terrainPrefab = LoadPrefab("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Meshes/LowPoly - Terrain.FBX");
        if (terrainPrefab != null)
        {
            GameObject terrain = (GameObject)PrefabUtility.InstantiatePrefab(terrainPrefab, root);
            terrain.name = "LowPoly_Terrain";
            terrain.transform.localPosition = new Vector3(areaCenter.x, heightRange.x, areaCenter.z);
            terrain.transform.localRotation = Quaternion.identity;
            terrain.transform.localScale = Vector3.one;
            ApplyMaterialOverride(terrain, GetTerrainMaterial());
        }

        GameObject[] treePrefabs =
        {
            LoadPrefab("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Prefabs/LowPoly - FirTree A.prefab"),
            LoadPrefab("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Prefabs/LowPoly - FirTree B.prefab")
        };

        GameObject[] rockPrefabs =
        {
            LoadPrefab("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Prefabs/LowPoly - Rock A.prefab"),
            LoadPrefab("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Prefabs/LowPoly - Rock B.prefab")
        };

        GameObject[] grassPrefabs =
        {
            LoadPrefab("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Prefabs/LowPoly - Grass A.prefab"),
            LoadPrefab("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Prefabs/LowPoly - Grass B.prefab")
        };

        SpawnGroup(root, treePrefabs, treeCount, 0.8f, 1.4f, GetTreeMaterial(), "Tree");
        SpawnGroup(root, rockPrefabs, rockCount, 0.7f, 1.3f, GetRockMaterial(), "Rock");
        SpawnGroup(root, grassPrefabs, grassCount, 0.7f, 1.1f, GetGrassMaterial(), "Grass");

        if (includeFences)
        {
            GameObject[] fencePrefabs =
            {
                LoadPrefab("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Prefabs/LowPoly - Fence A.prefab"),
                LoadPrefab("Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Prefabs/LowPoly - Fence B.prefab")
            };

            SpawnGroup(root, fencePrefabs, 6, 0.9f, 1.1f, GetFenceMaterial(), "Fence");
        }

        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

#if UNITY_EDITOR
    private GameObject LoadPrefab(string path)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
#endif

    private void SpawnGroup(Transform root, GameObject[] prefabs, int count, float minScale, float maxScale, Material overrideMat, string prefix)
    {
#if UNITY_EDITOR
        if (prefabs == null || prefabs.Length == 0)
        {
            return;
        }

        int validPrefabs = 0;
        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null)
            {
                validPrefabs++;
            }
        }

        if (validPrefabs == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null)
            {
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            instance.name = $"{prefix}_{i:00}";
            instance.transform.localPosition = GetRandomPoint();
            instance.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float scale = Random.Range(minScale, maxScale);
            instance.transform.localScale = Vector3.one * scale;

            if (overrideMat != null)
            {
                ApplyMaterialOverride(instance, overrideMat);
            }
        }
#endif
    }

    private Vector3 GetRandomPoint()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float x = Random.Range(-spawnArea.x * 0.5f, spawnArea.x * 0.5f);
            float z = Random.Range(-spawnArea.y * 0.5f, spawnArea.y * 0.5f);
            Vector3 point = new Vector3(areaCenter.x + x, Random.Range(heightRange.x, heightRange.y), areaCenter.z + z);
            if (new Vector2(point.x - areaCenter.x, point.z - areaCenter.z).magnitude >= clearRadius)
            {
                return point;
            }
        }

        return new Vector3(areaCenter.x + clearRadius, heightRange.x, areaCenter.z);
    }

    private void ApplyMaterialOverride(GameObject target, Material material)
    {
        if (target == null || material == null)
        {
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] mats = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = material;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = mats;
            }
        }
    }

    private void FixSceneMaterials()
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] mats = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (mat == null || mat.shader == null || !mat.shader.isSupported)
                {
                    mats[i] = GetFallbackMaterial();
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = mats;
            }
        }
    }

    private Material GetTerrainMaterial()
    {
        if (terrainMaterial == null)
        {
            terrainMaterial = CreateFallbackMaterial(terrainColor, "Terrain");
        }
        return terrainMaterial;
    }

    private Material GetTreeMaterial()
    {
        if (treeMaterial == null)
        {
            treeMaterial = CreateFallbackMaterial(treeColor, "Tree");
        }
        return treeMaterial;
    }

    private Material GetRockMaterial()
    {
        if (rockMaterial == null)
        {
            rockMaterial = CreateFallbackMaterial(rockColor, "Rock");
        }
        return rockMaterial;
    }

    private Material GetGrassMaterial()
    {
        if (grassMaterial == null)
        {
            grassMaterial = CreateFallbackMaterial(grassColor, "Grass");
        }
        return grassMaterial;
    }

    private Material GetFenceMaterial()
    {
        if (fenceMaterial == null)
        {
            fenceMaterial = CreateFallbackMaterial(fenceColor, "Fence");
        }
        return fenceMaterial;
    }

    private Material GetFallbackMaterial()
    {
        if (terrainMaterial == null)
        {
            terrainMaterial = CreateFallbackMaterial(terrainColor, "Fallback");
        }
        return terrainMaterial;
    }

private Material CreateFallbackMaterial(Color color, string name)
    {
        Shader shader = urpLitShader;
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            shader = urpSimpleLitShader;
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        }

        if (shader == null)
        {
            Debug.LogWarning($"BoxophobicScenePopulator: No URP shader found for '{name}'. Assign URP Lit shader in Inspector.");
            return null;
        }

        Material material = new Material(shader)
        {
            name = $"Boxophobic_{name}_Mat"
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.2f);
        }
        else if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", 0.2f);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        return material;
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void RegisterEditorCallbacks()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        TryEnsureInScene(activeScene);
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        TryEnsureInScene(scene);
    }

    private static void TryEnsureInScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != SceneName)
        {
            return;
        }

        if (FindFirstObjectByType<BoxophobicScenePopulator>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject root = new GameObject("BoxophobicScenePopulator");
        root.AddComponent<BoxophobicScenePopulator>();
        EditorSceneManager.MarkSceneDirty(scene);
    }
#endif
}

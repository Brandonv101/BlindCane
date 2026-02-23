using System.Collections;
using System.Collections.Generic;
using Meta.XR.EnvironmentDepth;
using UnityEngine;

public class SceneDepthBootstrap : MonoBehaviour
{
    private const string RootName = "SceneDepthBootstrap";

    [Header("Prefabs (Resources)")]
    [SerializeField] private string planePrefabResource = "SceneDepth/PlaneMesh";
    [SerializeField] private string volumePrefabResource = "SceneDepth/Volume";

    [Header("Options")]
    [SerializeField] private bool autoLoadScene = true;
    [SerializeField] private bool hideSceneMeshes = true;
    [SerializeField] private bool addCollidersIfMissing = true;
    [SerializeField] private bool enableEnvironmentDepth = true;
    [SerializeField] private OcclusionShadersMode depthOcclusionMode = OcclusionShadersMode.None;
    [SerializeField] private bool removeHandsFromDepth = true;

    private OVRSceneManager sceneManager;
    private EnvironmentDepthManager depthManager;
    private readonly List<OVRSceneAnchor> sceneAnchors = new List<OVRSceneAnchor>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<SceneDepthBootstrap>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject root = new GameObject(RootName);
        root.AddComponent<SceneDepthBootstrap>();
    }

    private void Awake()
    {
        EnsureSceneManager();
        EnsureEnvironmentDepth();
    }

    private void EnsureSceneManager()
    {
        sceneManager = FindFirstObjectByType<OVRSceneManager>(FindObjectsInactive.Include);
        if (sceneManager == null)
        {
            GameObject managerRoot = new GameObject("OVRSceneManager");
            sceneManager = managerRoot.AddComponent<OVRSceneManager>();
        }

        AssignPrefabs(sceneManager);

        sceneManager.SceneModelLoadedSuccessfully -= OnSceneModelLoaded;
        sceneManager.SceneModelLoadedSuccessfully += OnSceneModelLoaded;

        if (autoLoadScene)
        {
            sceneManager.LoadSceneModel();
        }
    }

    private void EnsureEnvironmentDepth()
    {
        if (!enableEnvironmentDepth)
        {
            return;
        }

        depthManager = FindFirstObjectByType<EnvironmentDepthManager>(FindObjectsInactive.Include);
        if (depthManager == null)
        {
            GameObject depthRoot = new GameObject("EnvironmentDepth");
            depthManager = depthRoot.AddComponent<EnvironmentDepthManager>();
        }

        depthManager.OcclusionShadersMode = depthOcclusionMode;
        depthManager.RemoveHands = removeHandsFromDepth;
        depthManager.enabled = true;
    }

    private void AssignPrefabs(OVRSceneManager manager)
    {
        if (manager == null)
        {
            return;
        }

        if (manager.PlanePrefab == null)
        {
            manager.PlanePrefab = LoadSceneAnchorPrefab(planePrefabResource);
        }

        if (manager.VolumePrefab == null)
        {
            manager.VolumePrefab = LoadSceneAnchorPrefab(volumePrefabResource);
        }
    }

    private OVRSceneAnchor LoadSceneAnchorPrefab(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }

        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"SceneDepthBootstrap: Prefab not found at Resources/{resourcePath}.");
            return null;
        }

        OVRSceneAnchor anchor = prefab.GetComponent<OVRSceneAnchor>();
        if (anchor == null)
        {
            Debug.LogWarning($"SceneDepthBootstrap: Prefab at Resources/{resourcePath} is missing OVRSceneAnchor.");
        }

        return anchor;
    }

    private void OnSceneModelLoaded()
    {
        OVRSceneAnchor.GetSceneAnchors(sceneAnchors);
        foreach (OVRSceneAnchor anchor in sceneAnchors)
        {
            if (anchor == null)
            {
                continue;
            }

            if (hideSceneMeshes)
            {
                foreach (Renderer renderer in anchor.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                }
            }
        }

        if (addCollidersIfMissing)
        {
            StartCoroutine(RefreshColliders());
        }
    }

    private IEnumerator RefreshColliders()
    {
        yield return null;
        yield return null;

        foreach (OVRSceneAnchor anchor in sceneAnchors)
        {
            if (anchor == null)
            {
                continue;
            }

            Collider[] colliders = anchor.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                continue;
            }

            foreach (MeshFilter filter in anchor.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }
        }
    }
}

using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class StreetBackdropSpawner : MonoBehaviour
{
    private const string RootName = "StreetBackdrop";

    [Header("Layout")]
    [SerializeField, Min(2f)] private float streetWidth = 6f;
    [SerializeField, Min(6f)] private float streetLength = 30f;
    [SerializeField, Min(1f)] private float wallHeight = 4f;
    [SerializeField, Min(0.05f)] private float wallThickness = 0.4f;
    [SerializeField, Min(0f)] private float buildingDepth = 4f;
    [SerializeField, Min(0.05f)] private float groundThickness = 0.1f;

    [Header("Materials")]
    [SerializeField] private Material groundMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Color groundColor = new Color(0.28f, 0.28f, 0.28f);
    [SerializeField] private Color wallColor = new Color(0.55f, 0.55f, 0.58f);

    [Header("Options")]
    [SerializeField] private bool addColliders = true;
    [SerializeField] private bool alignToOrigin = true;
    [SerializeField] private Vector3 originOffset = Vector3.zero;
    [SerializeField] private bool addBuildingSilhouettes = true;
    [SerializeField, Min(1)] private int buildingCountPerSide = 6;
    [SerializeField, Min(0.5f)] private float buildingMinHeight = 2.5f;
    [SerializeField, Min(0.5f)] private float buildingMaxHeight = 6.5f;
    [SerializeField, Min(0.5f)] private float buildingMinWidth = 1.8f;
    [SerializeField, Min(0.5f)] private float buildingMaxWidth = 3.8f;
    [SerializeField, Min(0.5f)] private float buildingSilhouetteDepth = 2.2f;
    [SerializeField] private Color buildingColor = new Color(0.34f, 0.34f, 0.36f);
    [SerializeField] private bool addLampPosts = true;
    [SerializeField, Min(1)] private int lampPostCountPerSide = 4;
    [SerializeField, Min(1f)] private float lampPostHeight = 2.6f;
    [SerializeField, Min(0.05f)] private float lampPostRadius = 0.06f;
    [SerializeField, Min(0.05f)] private float lampHeadRadius = 0.16f;
    [SerializeField, Min(0.05f)] private float lampHeadHeight = 0.12f;
    [SerializeField] private Color lampPostColor = new Color(0.12f, 0.12f, 0.14f);
    [SerializeField] private bool nightMode = false;
    [SerializeField] private Color lampEmissionColor = new Color(1f, 0.78f, 0.4f);
    [SerializeField, Min(0f)] private float lampLightIntensity = 2.2f;
    [SerializeField, Min(0.1f)] private float lampLightRange = 3.5f;

    private const string GroundName = "Ground";
    private const string WallLeftName = "Wall_Left";
    private const string WallRightName = "Wall_Right";
    private const string WallEndName = "Wall_End";
    private const string BuildingsRootName = "Buildings";
    private const string LampsRootName = "LampPosts";

    private void Awake()
    {
        EnsureBackdrop();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<StreetBackdropSpawner>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
        }

        root.AddComponent<StreetBackdropSpawner>();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureBackdrop();
        }
    }

    private void EnsureBackdrop()
    {
        if (alignToOrigin)
        {
            transform.position = originOffset;
            transform.rotation = Quaternion.identity;
        }

        float totalWidth = streetWidth + buildingDepth * 2f;
        Vector3 groundScale = new Vector3(totalWidth, groundThickness, streetLength);
        Vector3 groundPos = new Vector3(0f, -groundThickness * 0.5f, 0f);
        EnsurePiece(GroundName, groundPos, groundScale, GetOrCreateMaterial(ref groundMaterial, groundColor, "StreetGroundMat"), addColliders);

        float wallX = streetWidth * 0.5f + buildingDepth + wallThickness * 0.5f;
        Vector3 wallScale = new Vector3(wallThickness, wallHeight, streetLength);
        Vector3 wallLeftPos = new Vector3(-wallX, wallHeight * 0.5f, 0f);
        Vector3 wallRightPos = new Vector3(wallX, wallHeight * 0.5f, 0f);
        Material wallMat = GetOrCreateMaterial(ref wallMaterial, wallColor, "StreetWallMat");
        EnsurePiece(WallLeftName, wallLeftPos, wallScale, wallMat, addColliders);
        EnsurePiece(WallRightName, wallRightPos, wallScale, wallMat, addColliders);

        Vector3 endWallScale = new Vector3(totalWidth + wallThickness * 2f, wallHeight, wallThickness);
        Vector3 endWallPos = new Vector3(0f, wallHeight * 0.5f, streetLength * 0.5f + wallThickness * 0.5f);
        EnsurePiece(WallEndName, endWallPos, endWallScale, wallMat, addColliders);

        if (addBuildingSilhouettes)
        {
            EnsureBuildings(totalWidth);
        }
        else
        {
            ClearChildRoot(BuildingsRootName);
        }

        if (addLampPosts)
        {
            EnsureLampPosts(totalWidth);
        }
        else
        {
            ClearChildRoot(LampsRootName);
        }

        ApplyNightMode();
    }

    private void EnsurePiece(string name, Vector3 localPosition, Vector3 localScale, Material material, bool withCollider)
    {
        Transform existing = transform.Find(name);
        GameObject piece;
        if (existing != null)
        {
            piece = existing.gameObject;
        }
        else
        {
            piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(transform, false);
        }

        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = localScale;

        MeshRenderer renderer = piece.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        if (!withCollider)
        {
            Collider collider = piece.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }
        }
    }

    private Material GetOrCreateMaterial(ref Material material, Color fallbackColor, string name)
    {
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        material = new Material(shader)
        {
            name = name
        };
        material.color = fallbackColor;
        return material;
    }

    private void EnsureBuildings(float totalWidth)
    {
        Transform root = GetOrCreateRoot(BuildingsRootName);
        Material mat = GetOrCreateMaterial(ref wallMaterial, wallColor, "StreetWallMat");
        Material buildingMat = GetOrCreateMaterial(ref materialCacheBuilding, buildingColor, "StreetBuildingMat");
        if (buildingMat == null)
        {
            buildingMat = mat;
        }

        int total = buildingCountPerSide * 2;
        for (int i = 0; i < total; i++)
        {
            bool leftSide = i < buildingCountPerSide;
            int index = leftSide ? i : i - buildingCountPerSide;
            float t = buildingCountPerSide <= 1 ? 0.5f : index / (float)(buildingCountPerSide - 1);
            float z = Mathf.Lerp(-streetLength * 0.45f, streetLength * 0.45f, t);
            float width = Mathf.Lerp(buildingMinWidth, buildingMaxWidth, Mathf.Abs(Mathf.Sin((index + 1) * 1.7f)));
            float height = Mathf.Lerp(buildingMinHeight, buildingMaxHeight, Mathf.Abs(Mathf.Cos((index + 2) * 1.3f)));

            float x = (streetWidth * 0.5f + buildingDepth * 0.5f) * (leftSide ? -1f : 1f);
            Vector3 scale = new Vector3(width, height, buildingSilhouetteDepth);
            Vector3 pos = new Vector3(x, height * 0.5f, z);

            string name = leftSide ? $"Building_L_{index}" : $"Building_R_{index}";
            EnsureChildPiece(root, name, pos, scale, buildingMat, addColliders);
        }
    }

    private void EnsureLampPosts(float totalWidth)
    {
        Transform root = GetOrCreateRoot(LampsRootName);
        Material lampMat = GetOrCreateMaterial(ref materialCacheLamp, lampPostColor, "StreetLampMat");

        int total = lampPostCountPerSide * 2;
        for (int i = 0; i < total; i++)
        {
            bool leftSide = i < lampPostCountPerSide;
            int index = leftSide ? i : i - lampPostCountPerSide;
            float t = lampPostCountPerSide <= 1 ? 0.5f : index / (float)(lampPostCountPerSide - 1);
            float z = Mathf.Lerp(-streetLength * 0.42f, streetLength * 0.42f, t);
            float x = (streetWidth * 0.5f - 0.2f) * (leftSide ? -1f : 1f);

            string poleName = leftSide ? $"LampPole_L_{index}" : $"LampPole_R_{index}";
            Vector3 poleScale = new Vector3(lampPostRadius * 2f, lampPostHeight, lampPostRadius * 2f);
            Vector3 polePos = new Vector3(x, lampPostHeight * 0.5f, z);
            EnsureChildPiece(root, poleName, polePos, poleScale, lampMat, addColliders);

            string headName = leftSide ? $"LampHead_L_{index}" : $"LampHead_R_{index}";
            Vector3 headScale = new Vector3(lampHeadRadius * 2f, lampHeadHeight, lampHeadRadius * 2f);
            Vector3 headPos = new Vector3(x, lampPostHeight + lampHeadHeight * 0.5f, z);
            GameObject head = EnsureChildPiece(root, headName, headPos, headScale, lampMat, addColliders);
            EnsureLampLight(head);
        }
    }

    private Transform GetOrCreateRoot(string name)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject(name);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    private void ClearChildRoot(string name)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }
    }

    private GameObject EnsureChildPiece(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool withCollider)
    {
        Transform existing = parent.Find(name);
        GameObject piece;
        if (existing != null)
        {
            piece = existing.gameObject;
        }
        else
        {
            piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent, false);
        }

        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = localScale;

        MeshRenderer renderer = piece.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        if (!withCollider)
        {
            Collider collider = piece.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }
        }

        return piece;
    }

    private void EnsureLampLight(GameObject head)
    {
        if (head == null)
        {
            return;
        }

        Light light = head.GetComponent<Light>();
        if (light == null)
        {
            light = head.AddComponent<Light>();
            light.type = LightType.Point;
        }

        light.color = lampEmissionColor;
        light.intensity = lampLightIntensity;
        light.range = lampLightRange;
        light.enabled = nightMode;
    }

    private void ApplyNightMode()
    {
        if (materialCacheLamp != null)
        {
            if (nightMode)
            {
                materialCacheLamp.EnableKeyword("_EMISSION");
                materialCacheLamp.SetColor("_EmissionColor", lampEmissionColor);
            }
            else
            {
                materialCacheLamp.SetColor("_EmissionColor", Color.black);
                materialCacheLamp.DisableKeyword("_EMISSION");
            }
        }

        Transform lampRoot = transform.Find(LampsRootName);
        if (lampRoot == null)
        {
            return;
        }

        Light[] lights = lampRoot.GetComponentsInChildren<Light>(true);
        foreach (Light light in lights)
        {
            light.enabled = nightMode;
        }
    }

    public void SetNightMode(bool enabled)
    {
        nightMode = enabled;
        ApplyNightMode();
    }

    public bool IsNightMode()
    {
        return nightMode;
    }

    [SerializeField, HideInInspector] private Material materialCacheBuilding;
    [SerializeField, HideInInspector] private Material materialCacheLamp;
}

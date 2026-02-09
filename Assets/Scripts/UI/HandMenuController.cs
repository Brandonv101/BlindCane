using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class HandMenuController : MonoBehaviour
{
    private const string RootName = "HandMenuController";

    [Header("Menu Placement")]
    [SerializeField] private Transform leftHandAnchor;
    [SerializeField] private Vector3 menuLocalPosition = new Vector3(0.08f, 0.02f, 0.08f);
    [SerializeField] private Vector3 menuLocalEuler = new Vector3(20f, 180f, 0f);
    [SerializeField, Min(0.0005f)] private float menuScale = 0.0015f;

    [Header("Menu Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(300f, 210f);
    [SerializeField, Min(10f)] private float headerHeight = 36f;
    [SerializeField, Min(10f)] private float buttonHeight = 44f;
    [SerializeField, Min(0f)] private float buttonSpacing = 8f;

    [Header("Input")]
    [SerializeField] private bool useLeftHandPinchToToggle = true;
    [SerializeField, Min(0.1f)] private float toggleCooldown = 0.6f;
    [SerializeField, Min(0.001f)] private float pokeRadius = 0.012f;
    [SerializeField] private bool touchToSelect = true;
    [SerializeField] private LayerMask pokeMask = ~0;

    private Canvas canvas;
    private RectTransform panel;
    private MenuButton recenterButton;
    private MenuButton nightButton;
    private MenuButton passthroughButton;
    private bool menuVisible;
    private float nextToggleTime;
    private bool wasPinching;
    private bool wasTouching;

    private VirtualStickController stick;
    private StreetBackdropSpawner street;
    private bool passthroughEnabled = true;

    private OVRHand leftHand;
    private OVRSkeleton leftSkeleton;
    private Transform leftIndexTip;

    private static readonly string[] LeftAnchorNames =
    {
        "LeftHandAnchor",
        "LeftControllerInHandAnchor",
        "LeftControllerAnchor",
        "LeftHandOnControllerAnchor",
        "LeftHandAnchorDetached"
    };

    private void Awake()
    {
        ResolveReferences();
        BuildMenu();
        passthroughEnabled = GetPassthroughState();
        SetMenuVisible(false);
        UpdateLabels();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<HandMenuController>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
        }

        root.AddComponent<HandMenuController>();
    }

    private void Update()
    {
        ResolveReferences();
        bool pinching = GetLeftPinch();
        UpdateMenuToggle(pinching);

        if (menuVisible)
        {
            UpdatePokeInteraction(pinching);
        }

        wasPinching = pinching;
    }

    private void ResolveReferences()
    {
        if (leftHandAnchor == null)
        {
            leftHandAnchor = FindAnchor(LeftAnchorNames);
            AttachToHand();
        }

        if (leftHand == null)
        {
            foreach (OVRHand hand in FindObjectsByType<OVRHand>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (IsLeftHand(hand))
                {
                    leftHand = hand;
                    break;
                }
            }
        }

        if (leftSkeleton == null)
        {
            leftSkeleton = leftHand != null ? leftHand.GetComponent<OVRSkeleton>() : null;
        }

        if (leftSkeleton != null && leftIndexTip == null)
        {
            foreach (OVRBone bone in leftSkeleton.Bones)
            {
                if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                {
                    leftIndexTip = bone.Transform;
                    break;
                }
            }
        }

        if (stick == null)
        {
            stick = FindFirstObjectByType<VirtualStickController>(FindObjectsInactive.Include);
        }

        if (street == null)
        {
            street = FindFirstObjectByType<StreetBackdropSpawner>(FindObjectsInactive.Include);
        }
    }

    private Transform FindAnchor(string[] names)
    {
        foreach (string name in names)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                return found.transform;
            }
        }

        return null;
    }

    private bool IsLeftHand(OVRHand hand)
    {
        if (hand == null)
        {
            return false;
        }

        OVRSkeleton skeleton = hand.GetComponent<OVRSkeleton>();
        if (skeleton != null)
        {
            OVRSkeleton.SkeletonType type = skeleton.GetSkeletonType();
            if (type == OVRSkeleton.SkeletonType.HandLeft || type == OVRSkeleton.SkeletonType.XRHandLeft)
            {
                return true;
            }
        }

        string name = hand.gameObject.name.ToLowerInvariant();
        return name.Contains("left");
    }

    private void BuildMenu()
    {
        GameObject root = new GameObject("HandMenu");
        root.transform.SetParent(transform, false);
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = panelSize;

        panel = CreatePanel(root.transform);
        float y = -buttonSpacing;
        CreateHeader(panel, "Controls");
        y -= headerHeight + buttonSpacing;

        recenterButton = CreateButton(panel, "Recenter Stick", y);
        y -= buttonHeight + buttonSpacing;

        nightButton = CreateButton(panel, "Night Mode: Off", y);
        y -= buttonHeight + buttonSpacing;

        passthroughButton = CreateButton(panel, "Passthrough: On", y);

        recenterButton.GetComponent<Button>().onClick.AddListener(OnRecenter);
        nightButton.GetComponent<Button>().onClick.AddListener(OnToggleNight);
        passthroughButton.GetComponent<Button>().onClick.AddListener(OnTogglePassthrough);

        AttachToHand();
        UpdateLabels();
    }

    private void AttachToHand()
    {
        if (leftHandAnchor == null || canvas == null)
        {
            return;
        }

        Transform root = canvas.transform;
        if (root.parent != leftHandAnchor)
        {
            root.SetParent(leftHandAnchor, false);
        }
        root.localPosition = menuLocalPosition;
        root.localRotation = Quaternion.Euler(menuLocalEuler);
        root.localScale = Vector3.one * menuScale;
    }

    private RectTransform CreatePanel(Transform parent)
    {
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(parent, false);
        Image image = panelObj.AddComponent<Image>();
        image.color = new Color(0.08f, 0.08f, 0.09f, 0.86f);
        RectTransform rect = panelObj.GetComponent<RectTransform>();
        rect.sizeDelta = panelSize;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private void CreateHeader(RectTransform parent, string text)
    {
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(parent, false);
        Text headerText = headerObj.AddComponent<Text>();
        headerText.text = text;
        headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        headerText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        headerText.alignment = TextAnchor.MiddleLeft;
        headerText.fontSize = 20;

        RectTransform rect = headerObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(panelSize.x - 24f, headerHeight);
        rect.anchoredPosition = new Vector2(12f, -8f);
    }

    private MenuButton CreateButton(RectTransform parent, string label, float yOffset)
    {
        GameObject buttonObj = new GameObject(label.Replace(" ", "_"));
        buttonObj.transform.SetParent(parent, false);

        Image image = buttonObj.AddComponent<Image>();
        Button button = buttonObj.AddComponent<Button>();

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(panelSize.x - 24f, buttonHeight);
        rect.anchoredPosition = new Vector2(0f, yOffset);

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(buttonObj.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize = 18;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(14f, 4f);
        textRect.offsetMax = new Vector2(-14f, -4f);

        BoxCollider collider = buttonObj.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(rect.sizeDelta.x, rect.sizeDelta.y, 2f);

        MenuButton menuButton = buttonObj.AddComponent<MenuButton>();
        menuButton.Initialize(button, image, text);
        return menuButton;
    }

    private void UpdateMenuToggle(bool pinching)
    {
        if (useLeftHandPinchToToggle && pinching && !wasPinching && Time.time >= nextToggleTime)
        {
            SetMenuVisible(!menuVisible);
            nextToggleTime = Time.time + toggleCooldown;
        }
    }

    private bool GetLeftPinch()
    {
        if (leftHand != null && leftHand.IsTracked)
        {
            return leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        }

        InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!leftDevice.isValid)
        {
            return false;
        }

        bool menuButton = false;
        if (leftDevice.TryGetFeatureValue(CommonUsages.menuButton, out menuButton))
        {
            return menuButton;
        }

        bool primaryButton = false;
        if (leftDevice.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButton))
        {
            return primaryButton;
        }

        return false;
    }

    private void UpdatePokeInteraction(bool pinching)
    {
        Vector3 pokePosition = GetPokePosition();
        Collider[] hits = Physics.OverlapSphere(pokePosition, pokeRadius, pokeMask, QueryTriggerInteraction.Collide);

        MenuButton hovered = null;
        foreach (Collider hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            MenuButton button = hit.GetComponent<MenuButton>();
            if (button != null)
            {
                hovered = button;
                break;
            }
        }

        recenterButton.SetHover(hovered == recenterButton);
        nightButton.SetHover(hovered == nightButton);
        passthroughButton.SetHover(hovered == passthroughButton);

        bool touching = hovered != null;
        if (touchToSelect)
        {
            if (touching && !wasTouching)
            {
                hovered.Press();
            }
        }
        else if (touching && pinching && !wasPinching)
        {
            hovered.Press();
        }

        wasTouching = touching;
    }

    private Vector3 GetPokePosition()
    {
        if (leftIndexTip != null)
        {
            return leftIndexTip.position;
        }

        if (leftHandAnchor != null)
        {
            return leftHandAnchor.TransformPoint(new Vector3(0f, 0f, 0.08f));
        }

        return transform.position;
    }

    private void SetMenuVisible(bool visible)
    {
        menuVisible = visible;
        if (canvas != null)
        {
            canvas.enabled = visible;
        }
    }

    private void OnRecenter()
    {
        if (stick != null)
        {
            stick.RecenterNow();
        }
    }

    private void OnToggleNight()
    {
        if (street == null)
        {
            return;
        }

        bool enabled = !street.IsNightMode();
        street.SetNightMode(enabled);
        UpdateLabels();
    }

    private void OnTogglePassthrough()
    {
        passthroughEnabled = !passthroughEnabled;
        ApplyPassthrough(passthroughEnabled);
        UpdateLabels();
    }

    private void ApplyPassthrough(bool enabled)
    {
        if (OVRManager.instance != null)
        {
            OVRManager.instance.isInsightPassthroughEnabled = enabled;
        }

        foreach (OVRPassthroughLayer layer in FindObjectsByType<OVRPassthroughLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            layer.enabled = enabled;
        }
    }

    private bool GetPassthroughState()
    {
        if (OVRManager.instance != null)
        {
            return OVRManager.instance.isInsightPassthroughEnabled;
        }

        foreach (OVRPassthroughLayer layer in FindObjectsByType<OVRPassthroughLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            return layer.enabled;
        }

        return false;
    }

    private void UpdateLabels()
    {
        if (nightButton != null)
        {
            nightButton.SetLabel($"Night Mode: {(street != null && street.IsNightMode() ? "On" : "Off")}");
        }

        if (passthroughButton != null)
        {
            passthroughButton.SetLabel($"Passthrough: {(passthroughEnabled ? "On" : "Off")}");
        }
    }
}

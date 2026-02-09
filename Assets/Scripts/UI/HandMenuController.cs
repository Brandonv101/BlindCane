using System.Collections.Generic;
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
    [SerializeField] private Vector2 panelSize = new Vector2(360f, 420f);
    [SerializeField, Min(10f)] private float headerHeight = 34f;
    [SerializeField, Min(10f)] private float buttonHeight = 44f;
    [SerializeField, Min(0f)] private float buttonSpacing = 8f;
    [SerializeField, Min(18f)] private float swatchSize = 30f;
    [SerializeField, Min(0f)] private float swatchSpacing = 6f;
    [SerializeField, Min(1)] private int swatchColumns = 5;

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
    private MenuSlider hapticsSlider;
    private readonly List<MenuSwatch> raySwatches = new List<MenuSwatch>();
    private bool menuVisible;
    private float nextToggleTime;
    private bool wasPinching;
    private bool wasTouching;

    private VirtualStickController stick;
    private StreetBackdropSpawner street;
    private bool passthroughEnabled = true;
    private Material defaultSkybox;
    private Camera mainCamera;
    private int hapticsLevelIndex = 1;

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

    private readonly Color[] raySwatchColors =
    {
        new Color(1f, 0.55f, 0.2f),
        new Color(1f, 0.7f, 0.2f),
        new Color(1f, 0.85f, 0.3f),
        new Color(0.3f, 0.85f, 0.4f),
        new Color(0.2f, 0.8f, 0.7f),
        new Color(0.2f, 0.8f, 0.95f),
        new Color(0.3f, 0.55f, 1f),
        new Color(0.55f, 0.45f, 1f),
        new Color(0.95f, 0.35f, 0.25f),
        new Color(0.95f, 0.95f, 0.95f),
        new Color(0.9f, 0.85f, 0.7f),
        new Color(0.7f, 0.8f, 1f)
    };

    private readonly float[] hapticLevels = { 0.35f, 0.65f, 1f };
    private readonly string[] hapticLabels = { "Low", "Medium", "High" };

    private void Awake()
    {
        ResolveReferences();
        defaultSkybox = RenderSettings.skybox;
        BuildMenu();
        passthroughEnabled = GetPassthroughState();
        ApplyPassthrough(passthroughEnabled);
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

        if (stick != null)
        {
            stick.SetHapticStrength(hapticLevels[hapticsLevelIndex]);
        }

        if (street == null)
        {
            street = FindFirstObjectByType<StreetBackdropSpawner>(FindObjectsInactive.Include);
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
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
        CreateHeader(panel, "Controls", y);
        y -= headerHeight + buttonSpacing;

        recenterButton = CreateButton(panel, "Recenter Stick", y);
        y -= buttonHeight + buttonSpacing;

        nightButton = CreateButton(panel, "Night Mode: Off", y);
        y -= buttonHeight + buttonSpacing;

        passthroughButton = CreateButton(panel, "Passthrough: On", y);
        y -= buttonHeight + buttonSpacing * 1.5f;

        CreateHeader(panel, "Haptics", y);
        y -= headerHeight + buttonSpacing;

        hapticsSlider = CreateHapticsSlider(panel, y);
        y -= buttonHeight + buttonSpacing * 1.5f;

        CreateHeader(panel, "Ray Color", y);
        y -= headerHeight + buttonSpacing;
        y = CreateColorSwatches(panel, y);

        recenterButton.GetComponent<Button>().onClick.AddListener(OnRecenter);
        nightButton.GetComponent<Button>().onClick.AddListener(OnToggleNight);
        passthroughButton.GetComponent<Button>().onClick.AddListener(OnTogglePassthrough);

        if (raySwatches.Count > 0)
        {
            OnSelectRayColor(raySwatchColors[0], raySwatches[0]);
        }

        if (hapticsSlider != null)
        {
            hapticsSlider.SetLabel($"Haptics: {hapticLabels[hapticsLevelIndex]}");
        }

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

    private void CreateHeader(RectTransform parent, string text, float yOffset)
    {
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(parent, false);
        Text headerText = headerObj.AddComponent<Text>();
        headerText.text = text;
        headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        headerText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        headerText.alignment = TextAnchor.MiddleLeft;
        headerText.fontSize = 20;

        RectTransform rect = headerObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(panelSize.x - 24f, headerHeight);
        rect.anchoredPosition = new Vector2(12f, yOffset);
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
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

    private MenuSlider CreateHapticsSlider(RectTransform parent, float yOffset)
    {
        GameObject sliderRoot = new GameObject("HapticsSlider");
        sliderRoot.transform.SetParent(parent, false);

        RectTransform rootRect = sliderRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.sizeDelta = new Vector2(panelSize.x - 24f, buttonHeight);
        rootRect.anchoredPosition = new Vector2(0f, yOffset);

        BoxCollider collider = sliderRoot.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(rootRect.sizeDelta.x, rootRect.sizeDelta.y, 2f);

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(sliderRoot.transform, false);
        Text label = labelObj.AddComponent<Text>();
        label.text = "Haptics";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        label.alignment = TextAnchor.MiddleLeft;
        label.fontSize = 16;

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(110f, buttonHeight);
        labelRect.anchoredPosition = new Vector2(12f, 0f);

        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(sliderRoot.transform, false);
        Slider slider = sliderObj.AddComponent<Slider>();

        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.pivot = new Vector2(0f, 0.5f);
        sliderRect.offsetMin = new Vector2(130f, 12f);
        sliderRect.offsetMax = new Vector2(-12f, -12f);

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.18f, 0.18f, 0.2f, 0.9f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(1f, 0.5f);
        bgRect.sizeDelta = new Vector2(0f, 8f);
        bgRect.anchoredPosition = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.sizeDelta = new Vector2(-10f, 8f);
        fillAreaRect.anchoredPosition = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.98f, 0.6f, 0.28f, 0.9f);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.sizeDelta = Vector2.zero;

        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0f);
        handleAreaRect.anchorMax = new Vector2(1f, 1f);
        handleAreaRect.sizeDelta = Vector2.zero;

        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleArea.transform, false);
        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = new Color(0.96f, 0.96f, 0.96f, 1f);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, 18f);
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;

        slider.targetGraphic = handleImage;
        slider.handleRect = handleRect;
        slider.fillRect = fillRect;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0;
        slider.maxValue = hapticLevels.Length - 1;
        slider.wholeNumbers = true;
        slider.value = hapticsLevelIndex;
        slider.onValueChanged.AddListener(OnHapticsSliderChanged);

        MenuSlider menuSlider = sliderRoot.AddComponent<MenuSlider>();
        menuSlider.Initialize(slider, sliderRect, bgImage, label);
        return menuSlider;
    }

    private float CreateColorSwatches(RectTransform parent, float startY)
    {
        raySwatches.Clear();
        float contentWidth = panelSize.x - 24f;
        float gridWidth = swatchColumns * swatchSize + (swatchColumns - 1) * swatchSpacing;
        float xStart = -contentWidth * 0.5f + (contentWidth - gridWidth) * 0.5f + swatchSize * 0.5f;
        float y = startY - swatchSize * 0.5f;

        int index = 0;
        int rows = Mathf.CeilToInt(raySwatchColors.Length / (float)swatchColumns);
        for (int row = 0; row < rows; row++)
        {
            float x = xStart;
            for (int col = 0; col < swatchColumns; col++)
            {
                if (index >= raySwatchColors.Length)
                {
                    break;
                }

                Color color = raySwatchColors[index++];
                MenuSwatch swatch = CreateSwatch(parent, color, new Vector2(x, y));
                raySwatches.Add(swatch);
                x += swatchSize + swatchSpacing;
            }

            y -= swatchSize + swatchSpacing;
        }

        return y - buttonSpacing;
    }

    private MenuSwatch CreateSwatch(RectTransform parent, Color color, Vector2 anchoredPosition)
    {
        GameObject swatchObj = new GameObject("Swatch");
        swatchObj.transform.SetParent(parent, false);

        Image image = swatchObj.AddComponent<Image>();
        Button button = swatchObj.AddComponent<Button>();

        RectTransform rect = swatchObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(swatchSize, swatchSize);
        rect.anchoredPosition = anchoredPosition;

        BoxCollider collider = swatchObj.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(rect.sizeDelta.x, rect.sizeDelta.y, 2f);

        MenuSwatch menuSwatch = swatchObj.AddComponent<MenuSwatch>();
        menuSwatch.Initialize(button, image, color);
        button.onClick.AddListener(() => OnSelectRayColor(color, menuSwatch));
        return menuSwatch;
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

        MenuButton hoveredButton = null;
        MenuSwatch hoveredSwatch = null;
        MenuSlider hoveredSlider = null;
        foreach (Collider hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            if (hoveredSlider == null)
            {
                hoveredSlider = hit.GetComponent<MenuSlider>();
            }

            if (hoveredSwatch == null)
            {
                hoveredSwatch = hit.GetComponent<MenuSwatch>();
            }

            if (hoveredButton == null)
            {
                hoveredButton = hit.GetComponent<MenuButton>();
            }

            if (hoveredSlider != null || hoveredButton != null || hoveredSwatch != null)
            {
                break;
            }
        }

        recenterButton.SetHover(hoveredButton == recenterButton);
        nightButton.SetHover(hoveredButton == nightButton);
        passthroughButton.SetHover(hoveredButton == passthroughButton);
        if (hapticsSlider != null)
        {
            hapticsSlider.SetHover(hoveredSlider == hapticsSlider);
        }
        foreach (MenuSwatch swatch in raySwatches)
        {
            swatch.SetHover(hoveredSwatch == swatch);
        }

        bool touching = hoveredButton != null || hoveredSwatch != null || hoveredSlider != null;
        if (hoveredSlider != null && (touchToSelect || pinching))
        {
            hoveredSlider.UpdateWithPoke(pokePosition);
        }
        if (touchToSelect)
        {
            if (touching && !wasTouching)
            {
                if (hoveredButton != null)
                {
                    hoveredButton.Press();
                }
                else if (hoveredSwatch != null)
                {
                    hoveredSwatch.Press();
                }
            }
        }
        else if (touching && pinching && !wasPinching)
        {
            if (hoveredButton != null)
            {
                hoveredButton.Press();
            }
            else if (hoveredSwatch != null)
            {
                hoveredSwatch.Press();
            }
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

        ApplySkyboxState(!enabled);
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

    private void ApplySkyboxState(bool useSkybox)
    {
        if (useSkybox && defaultSkybox != null)
        {
            RenderSettings.skybox = defaultSkybox;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null)
        {
            mainCamera.clearFlags = useSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            if (!useSkybox)
            {
                mainCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
        }
    }

    private void OnHapticsSliderChanged(float value)
    {
        int index = Mathf.Clamp(Mathf.RoundToInt(value), 0, hapticLevels.Length - 1);
        hapticsLevelIndex = index;
        if (stick != null)
        {
            stick.SetHapticStrength(hapticLevels[hapticsLevelIndex]);
        }
        UpdateLabels();
    }

    private void OnSelectRayColor(Color color, MenuSwatch swatch)
    {
        if (stick != null)
        {
            stick.SetRayColor(color);
        }

        foreach (MenuSwatch sw in raySwatches)
        {
            sw.SetSelected(sw == swatch);
        }
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

        if (hapticsSlider != null)
        {
            hapticsSlider.SetLabel($"Haptics: {hapticLabels[hapticsLevelIndex]}");
        }
    }
}

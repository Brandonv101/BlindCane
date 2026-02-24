using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR;

public class VirtualStickController : MonoBehaviour
{
    public enum Handedness
    {
        Left,
        Right
    }

    [System.Serializable]
    public struct HapticPulse
    {
        [Range(0f, 1f)] public float amplitude;
        [Range(0.01f, 1f)] public float duration;
        [Range(0.01f, 1f)] public float repeatRate;

        public HapticPulse(float amplitude, float duration, float repeatRate)
        {
            this.amplitude = amplitude;
            this.duration = duration;
            this.repeatRate = repeatRate;
        }
    }

    [System.Serializable]
    public struct TipProfile
    {
        public string name;
        [Range(0.3f, 2f)] public float sizeMultiplier;
        [Range(0.01f, 0.5f)] public float contactDistance;
        public HapticPulse hoverPulse;
        public HapticPulse contactPulse;
        public HapticPulse selectPulse;

        public TipProfile(string name, float sizeMultiplier, float contactDistance, HapticPulse hoverPulse, HapticPulse contactPulse, HapticPulse selectPulse)
        {
            this.name = name;
            this.sizeMultiplier = sizeMultiplier;
            this.contactDistance = contactDistance;
            this.hoverPulse = hoverPulse;
            this.contactPulse = contactPulse;
            this.selectPulse = selectPulse;
        }
    }

    [Header("Rig")]
    [SerializeField] private Handedness stickHand = Handedness.Right;
    [SerializeField] private Transform controllerAnchor;
    [SerializeField] private Transform stickRoot;
    [SerializeField] private Transform stickModel;
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private Vector3 stickLocalPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 stickLocalRotationOffset = Vector3.zero;

    [Header("Stick Length")]
    [SerializeField] private float stickLength = 1.2f;
    [SerializeField] private float minStickLength = 0.6f;
    [SerializeField] private float maxStickLength = 2.0f;
    [SerializeField] private float lengthAdjustSpeed = 0.6f;
    [SerializeField] private bool useLeftStickForLength = true;
    [SerializeField] private Vector3 lengthScaleAxis = new Vector3(0f, 0f, 1f);

    [Header("Input")]
    [FormerlySerializedAs("rotationDeadzone")]
    [SerializeField] private float inputDeadzone = 0.2f;

    [Header("Selection Rotation")]
    [SerializeField] private bool enableSelectionRotation = true;
    [SerializeField, Range(10f, 360f)] private float rotationDegreesPerSecond = 110f;
    [SerializeField] private bool invertVerticalRotation = true;
    [SerializeField, Range(0.1f, 1f)] private float minimumRotationHapticScale = 0.25f;
    [SerializeField] private HapticPulse rotationHaptics = new HapticPulse(0.35f, 0.02f, 0.05f);

    [Header("Ray Visual")]
    [SerializeField] private LineRenderer rayLine;
    [SerializeField] private LayerMask raycastMask = ~0;
    [SerializeField] private float maxRayDistance = 4.0f;
    [SerializeField] private Gradient rayGradient;
    [SerializeField] private AnimationCurve rayWidthCurve;
    [SerializeField] private float rayStartWidth = 0.008f;
    [SerializeField] private float rayEndWidth = 0.003f;
    [SerializeField] private Color rayBaseColor = new Color(1f, 0.55f, 0.2f);
    [SerializeField, Range(0f, 1f)] private float rayStartAlpha = 0.9f;
    [SerializeField, Range(0f, 1f)] private float rayEndAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float rayStartHighlight = 0.2f;
    [SerializeField, Range(0f, 1f)] private float rayMidHighlight = 0.3f;
    [SerializeField, Range(0f, 1f)] private float rayEndShadow = 0.1f;
    [SerializeField, Range(0f, 1f)] private float rayHitBoost = 0.3f;

    [Header("Haptics")]
    [SerializeField, Range(0f, 1f)] private float hapticStrength = 0.8f;
    [SerializeField, Range(0.2f, 2f)] private float motorStrengthMultiplier = 1f;
    [SerializeField] private HapticPulse rayHoverHaptics = new HapticPulse(0.9f, 0.02f, 0.1f);
    [SerializeField] private HapticPulse contactHaptics = new HapticPulse(0.6f, 0.05f, 0.12f);
    [SerializeField] private HapticPulse selectHaptics = new HapticPulse(0.7f, 0.06f, 0.12f);
    [SerializeField] private HapticPulse releaseHaptics = new HapticPulse(0.3f, 0.045f, 0.1f);
    [SerializeField] private bool enableRayHoverHaptics = true;
    [SerializeField] private bool enableRayContactHaptics = true;
    [SerializeField, Range(0.01f, 0.5f)] private float rayContactDistance = 0.06f;
    [SerializeField] private LayerMask touchMask = ~0;
    [SerializeField, Range(0.2f, 2f)] private float quest2MotorScale = 1f;
    [SerializeField, Range(0.2f, 2f)] private float quest3MotorScale = 1f;
    [SerializeField, Range(0.2f, 2f)] private float questProMotorScale = 1f;
    [SerializeField, Range(0.2f, 2f)] private float defaultQuestMotorScale = 1f;

    [Header("Tip Profile")]
    [SerializeField] private Transform stickTip;
    [SerializeField] private TipProfile[] tipProfiles;
    [SerializeField] private int tipProfileIndex = 1;
    [SerializeField] private HapticPulse tipProfileChangeHaptics = new HapticPulse(0.4f, 0.03f, 0.08f);

    [Header("Recenter")]
    [SerializeField] private bool enableRecenter = true;
    [SerializeField, Range(0.1f, 2f)] private float recenterHoldTime = 0.5f;
    [SerializeField] private HapticPulse recenterHaptics = new HapticPulse(0.2f, 0.04f, 0.12f);

    [Header("Floor Touch (Headset Tracking)")]
    [SerializeField] private bool useHeadsetFloorTouch = true;
    [SerializeField] private Transform headset;
    [SerializeField] private LayerMask floorMask = ~0;
    [SerializeField, Range(0.5f, 5f)] private float floorProbeDistance = 3f;
    [SerializeField, Range(0f, 0.2f)] private float floorContactTolerance = 0.02f;
    [SerializeField] private bool alignRayOriginToLength = true;
    [SerializeField] private HapticPulse floorTouchHaptics = new HapticPulse(0.45f, 0.05f, 0.1f);

    private InputDevice rightDevice;
    private InputDevice leftDevice;
    private float nextTouchHapticTime;
    private float nextRayContactHapticTime;
    private float recenterPressStart = -1f;
    private bool recenterTriggered;
    private float nextFloorHapticTime;
    private float nextRotationHapticTime;
    private bool lastSelectButton;
    private RaySelectable currentHover;
    private RaySelectable currentSelection;
    private Vector3 initialStickScale;
    private Vector3 initialTipScale = Vector3.one;
    private float currentRayHitBlend;

    private void Awake()
    {
        if (stickRoot == null)
        {
            stickRoot = transform;
        }

        if (stickModel == null)
        {
            Transform found = stickRoot.Find("StickModel");
            stickModel = found != null ? found : stickRoot;
        }

        if (rayOrigin == null)
        {
            Transform found = stickRoot.Find("RayOrigin");
            rayOrigin = found != null ? found : stickRoot;
        }

        if (rayLine == null)
        {
            rayLine = GetComponent<LineRenderer>();
        }

        ResolveTip();
        EnsureTipProfiles();
        TryResolveHeadset();
        initialStickScale = stickModel.localScale;
        SetupRayVisual();
        ApplyTipProfile(tipProfileIndex, false);
        ApplyStickLength();
    }

    private void Start()
    {
        TryAttachToController();
    }

    private void Update()
    {
        if (controllerAnchor == null)
        {
            TryAttachToController();
        }

        UpdateDevices();
        UpdateStickLength();
        UpdateRaycast();
        UpdateSelectionRotation();
        UpdateFloorTouchFromHeadset();
        UpdateRecenter();
    }

    private void TryAttachToController()
    {
        if (controllerAnchor == null)
        {
            controllerAnchor = FindControllerAnchor(stickHand);
        }

        if (controllerAnchor != null)
        {
            stickRoot.SetParent(controllerAnchor, false);
            stickRoot.localPosition = stickLocalPositionOffset;
            stickRoot.localRotation = Quaternion.Euler(stickLocalRotationOffset);
        }
        else
        {
            Debug.LogWarning("VirtualStickController: Controller anchor not found. Assign one manually.");
        }
    }

    private void SetupRayVisual()
    {
        if (rayLine == null)
        {
            return;
        }

        if (rayGradient == null)
        {
            rayGradient = new Gradient();
        }

        rayLine.positionCount = 2;
        rayLine.startWidth = rayStartWidth;
        rayLine.endWidth = rayEndWidth;

        ApplyRayColor(rayBaseColor, 0f);

        if (rayWidthCurve == null || rayWidthCurve.length == 0)
        {
            rayWidthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0.4f)
            );
        }

        rayLine.widthCurve = rayWidthCurve;
    }

    private void UpdateDevices()
    {
        if (!rightDevice.isValid)
        {
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        if (!leftDevice.isValid)
        {
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        }
    }

    private void UpdateStickLength()
    {
        float lengthInput = GetLengthAxis();
        if (Mathf.Abs(lengthInput) < inputDeadzone)
        {
            return;
        }

        stickLength = Mathf.Clamp(
            stickLength + lengthInput * lengthAdjustSpeed * Time.deltaTime,
            minStickLength,
            maxStickLength
        );

        ApplyStickLength();
    }

    private void ApplyStickLength()
    {
        Vector3 axis = new Vector3(
            Mathf.Abs(lengthScaleAxis.x),
            Mathf.Abs(lengthScaleAxis.y),
            Mathf.Abs(lengthScaleAxis.z)
        );

        Vector3 scale = initialStickScale;
        if (axis.x > 0f)
        {
            scale.x = stickLength;
        }
        if (axis.y > 0f)
        {
            scale.y = stickLength;
        }
        if (axis.z > 0f)
        {
            scale.z = stickLength;
        }

        stickModel.localScale = scale;

        if (alignRayOriginToLength && rayOrigin != null)
        {
            Vector3 direction = lengthScaleAxis;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }

            rayOrigin.localPosition = direction.normalized * stickLength;
        }
    }

    private void UpdateRaycast()
    {
        if (rayLine == null || rayOrigin == null)
        {
            return;
        }

        Vector3 origin = rayOrigin.position;
        Vector3 direction = rayOrigin.forward;
        bool hit = Physics.Raycast(origin, direction, out RaycastHit hitInfo, maxRayDistance, raycastMask);
        Vector3 endPoint = hit ? hitInfo.point : origin + direction * maxRayDistance;

        rayLine.SetPosition(0, origin);
        rayLine.SetPosition(1, endPoint);
        UpdateRayVisualForHit(hit, hit ? hitInfo.distance : maxRayDistance);

        RaySelectable hover = null;
        if (hit)
        {
            hover = hitInfo.collider.GetComponentInParent<RaySelectable>();
        }

        if (hover != currentHover)
        {
            currentHover = hover;
            if (enableRayHoverHaptics && currentHover != null)
            {
                SendHapticPulse(GetPrimaryDevice(), rayHoverHaptics);
            }
        }

        if (enableRayContactHaptics && hit)
        {
            TrySendRayContactHaptics(hitInfo.distance);
        }

        bool selectPressed = GetSelectPressed();
        if (selectPressed && !lastSelectButton)
        {
            ToggleSelection();
        }
        lastSelectButton = selectPressed;
    }

    private void UpdateSelectionRotation()
    {
        if (!enableSelectionRotation || currentSelection == null)
        {
            return;
        }

        InputDevice primary = GetPrimaryDevice();
        if (!primary.isValid)
        {
            return;
        }

        if (!primary.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
        {
            return;
        }

        float magnitude = axis.magnitude;
        if (magnitude < inputDeadzone)
        {
            return;
        }

        float yawInput = axis.x;
        float pitchInput = invertVerticalRotation ? -axis.y : axis.y;

        Transform target = currentSelection.transform;
        Vector3 yawAxis = headset != null ? headset.up : Vector3.up;
        Vector3 pitchAxis = headset != null ? headset.right : Vector3.right;

        target.Rotate(yawAxis, yawInput * rotationDegreesPerSecond * Time.deltaTime, Space.World);
        target.Rotate(pitchAxis, pitchInput * rotationDegreesPerSecond * Time.deltaTime, Space.World);

        TrySendRotationHaptics(magnitude);
    }

    private void ToggleSelection()
    {
        InputDevice primary = GetPrimaryDevice();

        if (currentSelection != null)
        {
            currentSelection.SetSelected(false);
            currentSelection = null;
            SendHapticPulse(primary, releaseHaptics);
        }

        if (currentHover != null)
        {
            currentSelection = currentHover;
            currentSelection.SetSelected(true);
            SendHapticPulse(primary, selectHaptics);
        }
    }

    private void UpdateRecenter()
    {
        if (!enableRecenter)
        {
            return;
        }

        InputDevice primary = GetPrimaryDevice();
        if (!primary.isValid)
        {
            return;
        }

        bool secondaryButton = false;
        primary.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryButton);

        if (secondaryButton)
        {
            if (recenterPressStart < 0f)
            {
                recenterPressStart = Time.time;
                recenterTriggered = false;
            }

            if (!recenterTriggered && Time.time - recenterPressStart >= recenterHoldTime)
            {
                RecenterStick();
                SendHapticPulse(primary, recenterHaptics);
                recenterTriggered = true;
            }
        }
        else
        {
            recenterPressStart = -1f;
            recenterTriggered = false;
        }
    }

    private void RecenterStick()
    {
        if (stickRoot == null)
        {
            return;
        }

        if (controllerAnchor == null)
        {
            controllerAnchor = FindControllerAnchor(stickHand);
        }

        if (controllerAnchor != null)
        {
            stickRoot.SetParent(controllerAnchor, false);
        }

        stickRoot.localPosition = stickLocalPositionOffset;
        stickRoot.localRotation = Quaternion.Euler(stickLocalRotationOffset);
        ApplyStickLength();
    }

    public void RecenterNow()
    {
        RecenterStick();
    }

    private void UpdateFloorTouchFromHeadset()
    {
        if (!useHeadsetFloorTouch || rayOrigin == null)
        {
            return;
        }

        if (headset == null && !TryResolveHeadset())
        {
            return;
        }

        if (!Physics.Raycast(headset.position, Vector3.down, out RaycastHit hitInfo, floorProbeDistance, floorMask))
        {
            return;
        }

        float tipHeight = rayOrigin.position.y;
        float floorHeight = hitInfo.point.y;
        bool touching = tipHeight <= floorHeight + floorContactTolerance;
        if (!touching)
        {
            return;
        }

        if (Time.time < nextFloorHapticTime)
        {
            return;
        }

        SendHapticPulse(GetPrimaryDevice(), floorTouchHaptics);
        nextFloorHapticTime = Time.time + floorTouchHaptics.repeatRate;
    }

    private void UpdateRayVisualForHit(bool hit, float hitDistance)
    {
        float hitBlend = 0f;
        if (hit)
        {
            float normalizedDistance = Mathf.Clamp01(hitDistance / Mathf.Max(0.01f, maxRayDistance));
            hitBlend = 1f - normalizedDistance;
        }

        if (Mathf.Abs(currentRayHitBlend - hitBlend) < 0.01f)
        {
            return;
        }

        currentRayHitBlend = hitBlend;
        ApplyRayColor(rayBaseColor, currentRayHitBlend);

        if (rayLine != null)
        {
            rayLine.widthMultiplier = Mathf.Lerp(1f, 1.14f, currentRayHitBlend);
        }
    }

    private void TrySendRotationHaptics(float axisMagnitude)
    {
        if (Time.time < nextRotationHapticTime)
        {
            return;
        }

        float normalizedMagnitude = Mathf.InverseLerp(inputDeadzone, 1f, Mathf.Clamp01(axisMagnitude));
        float amplitudeScale = Mathf.Lerp(minimumRotationHapticScale, 1f, normalizedMagnitude);
        HapticPulse pulse = rotationHaptics;
        pulse.amplitude = Mathf.Clamp01(pulse.amplitude * amplitudeScale);

        SendHapticPulse(GetPrimaryDevice(), pulse);
        nextRotationHapticTime = Time.time + Mathf.Max(0.01f, pulse.repeatRate);
    }

    private float GetLengthAxis()
    {
        InputDevice device = useLeftStickForLength ? leftDevice : GetPrimaryDevice();
        if (!device.isValid)
        {
            return 0f;
        }

        Vector2 axis;
        if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out axis))
        {
            return axis.y;
        }

        return 0f;
    }

    private bool GetSelectPressed()
    {
        InputDevice primary = GetPrimaryDevice();
        if (!primary.isValid)
        {
            return false;
        }

        bool pressed;
        if (primary.TryGetFeatureValue(CommonUsages.triggerButton, out pressed))
        {
            return pressed;
        }

        return false;
    }

    private InputDevice GetPrimaryDevice()
    {
        return stickHand == Handedness.Left ? leftDevice : rightDevice;
    }

    private bool TryResolveHeadset()
    {
        if (headset != null)
        {
            return true;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            headset = mainCamera.transform;
            return true;
        }

        GameObject found = GameObject.Find("CenterEyeAnchor");
        if (found != null)
        {
            headset = found.transform;
            return true;
        }

        return false;
    }

    private Transform FindControllerAnchor(Handedness handedness)
    {
        string[] rightNames =
        {
            "RightControllerInHandAnchor",
            "RightControllerAnchor",
            "RightHandAnchor",
            "RightHandOnControllerAnchor",
            "RightControllerAnchorDetached"
        };

        string[] leftNames =
        {
            "LeftControllerInHandAnchor",
            "LeftControllerAnchor",
            "LeftHandAnchor",
            "LeftHandOnControllerAnchor",
            "LeftControllerAnchorDetached"
        };

        string[] targets = handedness == Handedness.Right ? rightNames : leftNames;
        Transform root = headset != null ? headset.root : null;
        if (root != null)
        {
            Transform match = FindAnchorUnderRoot(root, targets);
            if (match != null)
            {
                return match;
            }
        }

        foreach (string target in targets)
        {
            GameObject found = GameObject.Find(target);
            if (found != null)
            {
                return found.transform;
            }
        }

        return null;
    }

    private Transform FindAnchorUnderRoot(Transform root, string[] targets)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (string target in targets)
        {
            foreach (Transform child in children)
            {
                if (child.name == target)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private void SendHapticPulse(InputDevice device, HapticPulse pulse)
    {
        if (!device.isValid || !device.TryGetHapticCapabilities(out HapticCapabilities caps))
        {
            return;
        }

        if (!caps.supportsImpulse)
        {
            return;
        }

        float amplitude = pulse.amplitude * hapticStrength * motorStrengthMultiplier * GetQuestMotorScale();
        amplitude = Mathf.Clamp01(amplitude);
        device.SendHapticImpulse(0, amplitude, pulse.duration);
    }

    private void OnTriggerEnter(Collider other)
    {
        TrySendTouchHaptics(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TrySendTouchHaptics(other);
    }

    private void TrySendTouchHaptics(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & touchMask) == 0)
        {
            return;
        }

        if (Time.time < nextTouchHapticTime)
        {
            return;
        }

        SendHapticPulse(GetPrimaryDevice(), contactHaptics);
        nextTouchHapticTime = Time.time + contactHaptics.repeatRate;
    }

    private void TrySendRayContactHaptics(float hitDistance)
    {
        if (hitDistance > rayContactDistance)
        {
            return;
        }

        if (Time.time < nextRayContactHapticTime)
        {
            return;
        }

        SendHapticPulse(GetPrimaryDevice(), contactHaptics);
        nextRayContactHapticTime = Time.time + contactHaptics.repeatRate;
    }

    public void SetHapticStrength(float strength)
    {
        hapticStrength = Mathf.Clamp01(strength);
    }

    public float GetHapticStrength()
    {
        return hapticStrength;
    }

    public void SetMotorStrengthMultiplier(float multiplier)
    {
        motorStrengthMultiplier = Mathf.Clamp(multiplier, 0.2f, 2f);
    }

    public float GetMotorStrengthMultiplier()
    {
        return motorStrengthMultiplier;
    }

    public void SetStickLength(float length)
    {
        stickLength = Mathf.Clamp(length, minStickLength, maxStickLength);
        ApplyStickLength();
    }

    public float GetStickLength()
    {
        return stickLength;
    }

    public void SetTipProfile(int index)
    {
        ApplyTipProfile(index, true);
    }

    public void CycleTipProfile()
    {
        if (tipProfiles == null || tipProfiles.Length == 0)
        {
            return;
        }

        int nextIndex = (tipProfileIndex + 1) % tipProfiles.Length;
        ApplyTipProfile(nextIndex, true);
    }

    public int GetTipProfileIndex()
    {
        return tipProfileIndex;
    }

    public int GetTipProfileCount()
    {
        return tipProfiles == null ? 0 : tipProfiles.Length;
    }

    public string GetCurrentTipProfileName()
    {
        if (tipProfiles == null || tipProfiles.Length == 0)
        {
            return "None";
        }

        int index = Mathf.Clamp(tipProfileIndex, 0, tipProfiles.Length - 1);
        string name = tipProfiles[index].name;
        return string.IsNullOrWhiteSpace(name) ? $"Profile {index + 1}" : name;
    }

    public void SetRayColor(Color color)
    {
        rayBaseColor = color;
        ApplyRayColor(rayBaseColor, currentRayHitBlend);
    }

    public Color GetRayColor()
    {
        return rayBaseColor;
    }

    private void ResolveTip()
    {
        if (stickTip == null && rayOrigin != null)
        {
            Transform found = rayOrigin.Find("StickTip");
            if (found != null)
            {
                stickTip = found;
            }
        }

        if (stickTip != null)
        {
            initialTipScale = stickTip.localScale;
        }
    }

    private void EnsureTipProfiles()
    {
        if (tipProfiles != null && tipProfiles.Length > 0)
        {
            tipProfileIndex = Mathf.Clamp(tipProfileIndex, 0, tipProfiles.Length - 1);
            return;
        }

        tipProfiles = new[]
        {
            new TipProfile(
                "Precision",
                0.7f,
                0.04f,
                new HapticPulse(0.3f, 0.015f, 0.08f),
                new HapticPulse(0.55f, 0.03f, 0.1f),
                new HapticPulse(0.65f, 0.05f, 0.12f)
            ),
            new TipProfile(
                "Balanced",
                1f,
                0.06f,
                new HapticPulse(0.45f, 0.02f, 0.1f),
                new HapticPulse(0.65f, 0.05f, 0.12f),
                new HapticPulse(0.75f, 0.06f, 0.12f)
            ),
            new TipProfile(
                "Cushion",
                1.35f,
                0.1f,
                new HapticPulse(0.6f, 0.03f, 0.08f),
                new HapticPulse(0.8f, 0.07f, 0.1f),
                new HapticPulse(0.9f, 0.08f, 0.1f)
            )
        };

        tipProfileIndex = Mathf.Clamp(tipProfileIndex, 0, tipProfiles.Length - 1);
    }

    private void ApplyTipProfile(int index, bool sendFeedback)
    {
        if (tipProfiles == null || tipProfiles.Length == 0)
        {
            return;
        }

        tipProfileIndex = Mathf.Clamp(index, 0, tipProfiles.Length - 1);
        TipProfile profile = tipProfiles[tipProfileIndex];

        if (stickTip != null)
        {
            float multiplier = Mathf.Max(0.1f, profile.sizeMultiplier);
            stickTip.localScale = initialTipScale * multiplier;
        }

        rayContactDistance = Mathf.Clamp(profile.contactDistance, 0.01f, 0.5f);
        rayHoverHaptics = profile.hoverPulse;
        contactHaptics = profile.contactPulse;
        selectHaptics = profile.selectPulse;

        if (sendFeedback)
        {
            SendHapticPulse(GetPrimaryDevice(), tipProfileChangeHaptics);
        }
    }

    private float GetQuestMotorScale()
    {
        string model = SystemInfo.deviceModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            return defaultQuestMotorScale;
        }

        string normalized = model.ToLowerInvariant();
        if (normalized.Contains("quest pro") || normalized.Contains("questpro"))
        {
            return questProMotorScale;
        }

        if (normalized.Contains("quest 3") || normalized.Contains("quest3"))
        {
            return quest3MotorScale;
        }

        if (normalized.Contains("quest 2") || normalized.Contains("quest2"))
        {
            return quest2MotorScale;
        }

        return defaultQuestMotorScale;
    }

    private void ApplyRayColor(Color baseColor, float hitBlend)
    {
        if (rayGradient == null)
        {
            rayGradient = new Gradient();
        }

        float clampedHitBlend = Mathf.Clamp01(hitBlend);
        float hitHighlight = clampedHitBlend * rayHitBoost;

        Color warmMid = new Color(1f, 0.84f, 0.58f, 1f);
        Color startColor = Color.Lerp(baseColor, Color.white, Mathf.Clamp01(rayStartHighlight + hitHighlight));
        Color midColor = Color.Lerp(baseColor, warmMid, Mathf.Clamp01(rayMidHighlight + hitHighlight * 0.7f));
        Color endColor = Color.Lerp(baseColor, Color.black, Mathf.Clamp01(rayEndShadow + (1f - clampedHitBlend) * 0.1f));
        GradientColorKey[] colors =
        {
            new GradientColorKey(startColor, 0f),
            new GradientColorKey(midColor, 0.45f),
            new GradientColorKey(endColor, 1f)
        };

        float midAlpha = Mathf.Clamp01(Mathf.Lerp(rayStartAlpha, rayEndAlpha, 0.5f) + hitHighlight * 0.08f);
        GradientAlphaKey[] alphas =
        {
            new GradientAlphaKey(Mathf.Clamp01(rayStartAlpha + hitHighlight * 0.1f), 0f),
            new GradientAlphaKey(midAlpha, 0.45f),
            new GradientAlphaKey(rayEndAlpha, 1f)
        };

        rayGradient.SetKeys(colors, alphas);

        if (rayLine != null)
        {
            rayLine.colorGradient = rayGradient;
        }
    }
}

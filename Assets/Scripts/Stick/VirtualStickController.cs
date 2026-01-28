using UnityEngine;
using UnityEngine.XR;

public class VirtualStickController : MonoBehaviour
{
    public enum Handedness
    {
        Left,
        Right
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

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float rotationDeadzone = 0.2f;

    [Header("Ray Visual")]
    [SerializeField] private LineRenderer rayLine;
    [SerializeField] private LayerMask raycastMask = ~0;
    [SerializeField] private float maxRayDistance = 4.0f;
    [SerializeField] private Gradient rayGradient;
    [SerializeField] private AnimationCurve rayWidthCurve;
    [SerializeField] private float rayStartWidth = 0.008f;
    [SerializeField] private float rayEndWidth = 0.003f;

    [Header("Haptics")]
    [SerializeField, Range(0f, 1f)] private float hapticStrength = 0.8f;
    [SerializeField, Range(0.01f, 0.5f)] private float hapticStrengthStep = 0.1f;
    [SerializeField] private HapticPulse rotationHaptics = new HapticPulse(0.25f, 0.03f, 0.05f);
    [SerializeField] private HapticPulse lengthHaptics = new HapticPulse(0.2f, 0.03f, 0.1f);
    [SerializeField] private HapticPulse rayHoverHaptics = new HapticPulse(0.5f, 0.06f, 0.12f);
    [SerializeField] private HapticPulse selectHaptics = new HapticPulse(0.7f, 0.08f, 0.12f);
    [SerializeField] private HapticPulse touchHaptics = new HapticPulse(0.55f, 0.05f, 0.08f);
    [SerializeField] private bool enableRayHoverHaptics = false;
    [SerializeField] private LayerMask touchMask = ~0;

    [Header("Floor Touch (Headset Tracking)")]
    [SerializeField] private bool useHeadsetFloorTouch = true;
    [SerializeField] private Transform headset;
    [SerializeField] private LayerMask floorMask = ~0;
    [SerializeField, Range(0.5f, 5f)] private float floorProbeDistance = 3f;
    [SerializeField, Range(0f, 0.2f)] private float floorContactTolerance = 0.02f;
    [SerializeField] private bool alignRayOriginToLength = true;
    [SerializeField] private HapticPulse floorTouchHaptics = new HapticPulse(0.6f, 0.05f, 0.08f);

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

    private InputDevice rightDevice;
    private InputDevice leftDevice;
    private float nextRotationHapticTime;
    private float nextLengthHapticTime;
    private float nextTouchHapticTime;
    private float nextFloorHapticTime;
    private bool lastSelectButton;
    private bool lastPrimaryButton;
    private bool lastSecondaryButton;
    private RaySelectable currentHover;
    private RaySelectable currentSelection;
    private Vector3 initialStickScale;

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

        TryResolveHeadset();
        initialStickScale = stickModel.localScale;
        SetupRayVisual();
        ApplyStickLength();
    }

    private void Start()
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

    private void Update()
    {
        UpdateDevices();
        UpdateStickRotation();
        UpdateStickLength();
        UpdateRaycast();
        UpdateFloorTouchFromHeadset();
        UpdateHapticStrength();
    }

    private void SetupRayVisual()
    {
        if (rayLine == null)
        {
            return;
        }

        rayLine.positionCount = 2;
        rayLine.startWidth = rayStartWidth;
        rayLine.endWidth = rayEndWidth;

        if (rayGradient.colorKeys.Length == 0)
        {
            GradientColorKey[] colors =
            {
                new GradientColorKey(new Color(0.98f, 0.92f, 0.75f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.2f), 1f)
            };
            GradientAlphaKey[] alphas =
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.4f, 1f)
            };
            rayGradient.SetKeys(colors, alphas);
        }

        rayLine.colorGradient = rayGradient;

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

    private void UpdateStickRotation()
    {
        Vector2 rotationAxis = GetRotationAxis();
        float magnitude = rotationAxis.magnitude;
        if (magnitude < rotationDeadzone)
        {
            return;
        }

        float yaw = rotationAxis.x * rotationSpeed * Time.deltaTime;
        float pitch = -rotationAxis.y * rotationSpeed * Time.deltaTime;

        stickRoot.Rotate(Vector3.up, yaw, Space.Self);
        stickRoot.Rotate(Vector3.right, pitch, Space.Self);

        if (Time.time >= nextRotationHapticTime)
        {
            SendHapticPulse(GetPrimaryDevice(), rotationHaptics);
            nextRotationHapticTime = Time.time + rotationHaptics.repeatRate;
        }
    }

    private void UpdateStickLength()
    {
        float lengthInput = GetLengthAxis();
        if (Mathf.Abs(lengthInput) < rotationDeadzone)
        {
            return;
        }

        stickLength = Mathf.Clamp(
            stickLength + lengthInput * lengthAdjustSpeed * Time.deltaTime,
            minStickLength,
            maxStickLength
        );

        ApplyStickLength();

        if (Time.time >= nextLengthHapticTime)
        {
            SendHapticPulse(GetPrimaryDevice(), lengthHaptics);
            nextLengthHapticTime = Time.time + lengthHaptics.repeatRate;
        }
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

        bool selectPressed = GetSelectPressed();
        if (selectPressed && !lastSelectButton)
        {
            ToggleSelection();
        }
        lastSelectButton = selectPressed;
    }

    private void ToggleSelection()
    {
        if (currentSelection != null)
        {
            currentSelection.SetSelected(false);
            currentSelection = null;
        }

        if (currentHover != null)
        {
            currentSelection = currentHover;
            currentSelection.SetSelected(true);
            SendHapticPulse(GetPrimaryDevice(), selectHaptics);
        }
    }

    private void UpdateHapticStrength()
    {
        InputDevice primary = GetPrimaryDevice();
        if (!primary.isValid)
        {
            return;
        }

        bool primaryButton = false;
        bool secondaryButton = false;
        primary.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButton);
        primary.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryButton);

        if (primaryButton && !lastPrimaryButton)
        {
            hapticStrength = Mathf.Clamp01(hapticStrength + hapticStrengthStep);
        }

        if (secondaryButton && !lastSecondaryButton)
        {
            hapticStrength = Mathf.Clamp01(hapticStrength - hapticStrengthStep);
        }

        lastPrimaryButton = primaryButton;
        lastSecondaryButton = secondaryButton;
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

    private Vector2 GetRotationAxis()
    {
        InputDevice primary = GetPrimaryDevice();
        if (!primary.isValid)
        {
            return Vector2.zero;
        }

        Vector2 axis;
        if (primary.TryGetFeatureValue(CommonUsages.primary2DAxis, out axis))
        {
            return axis;
        }

        return Vector2.zero;
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

        float amplitude = Mathf.Clamp01(pulse.amplitude * hapticStrength);
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

        SendHapticPulse(GetPrimaryDevice(), touchHaptics);
        nextTouchHapticTime = Time.time + touchHaptics.repeatRate;
    }
}

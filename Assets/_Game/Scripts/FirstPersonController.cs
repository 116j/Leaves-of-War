using System;
using System.Collections.Generic;
using Hortensia.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    public readonly struct RebindableAction
    {
        public RebindableAction(string id, string displayName, string actionName,
            int bindingIndex, string defaultPath, string expectedControlLayout,
            string bindingId)
        {
            Id = id;
            DisplayName = displayName;
            ActionName = actionName;
            BindingIndex = bindingIndex;
            DefaultPath = defaultPath;
            ExpectedControlLayout = expectedControlLayout;
            BindingId = bindingId;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string ActionName { get; }
        public int BindingIndex { get; }
        public string DefaultPath { get; }
        public string ExpectedControlLayout { get; }
        public string BindingId { get; }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    public const string PauseDefaultPath = "<Keyboard>/p";
#else
    public const string PauseDefaultPath = "<Keyboard>/escape";
#endif
    private const string CursorDefaultPath = "<Keyboard>/tab";

    public static readonly IReadOnlyList<RebindableAction> RebindableActions = new[]
    {
        new RebindableAction("move_up",    "MOVE FORWARD",  "Move",          1, "<Keyboard>/upArrow",    "Button", "a1000000-0000-4000-8000-000000000001"),
        new RebindableAction("move_down",  "MOVE BACKWARD", "Move",          2, "<Keyboard>/downArrow",  "Button", "a1000000-0000-4000-8000-000000000002"),
        new RebindableAction("move_left",  "MOVE LEFT",     "Move",          3, "<Keyboard>/leftArrow",  "Button", "a1000000-0000-4000-8000-000000000003"),
        new RebindableAction("move_right", "MOVE RIGHT",    "Move",          4, "<Keyboard>/rightArrow", "Button", "a1000000-0000-4000-8000-000000000004"),
        new RebindableAction("sprint",     "SPRINT",        "Sprint",        0, "<Keyboard>/leftShift",  "Button", "a1000000-0000-4000-8000-00000000000a"),
        new RebindableAction("interact",   "INTERACT",      "Interact",      0, "<Keyboard>/e",          "Button", "a1000000-0000-4000-8000-000000000005"),
        new RebindableAction("cursor",     "TOGGLE CURSOR", "Toggle Cursor", 0, CursorDefaultPath,       "Button", "a1000000-0000-4000-8000-000000000006"),
        new RebindableAction("pause",      "PAUSE",         "Pause",         0, PauseDefaultPath,        "Button", "a1000000-0000-4000-8000-000000000007"),
    };

    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.2f;
    [SerializeField] private float sprintSpeed = 5.6f;
    [SerializeField] private float gravity = -22f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float lookLimit = 82f;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private float interactionRadius = 0.16f;
    [SerializeField] private float focusGraceTime = 0.2f;
    [SerializeField] private LayerMask interactionMask = ~0;

    private CharacterController controller;
    private float verticalVelocity;
    private float pitch;
    private bool sprintToggled;
    private float lastFocusedTime = float.NegativeInfinity;
    private Component focusedInteractableComponent;
    private IInteractable focusedInteractable;
    private InputActionMap inputActions;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction interactAction;
    private InputAction pointerClickAction;
    private InputAction cursorToggleAction;

    public event Action<string> InteractionPromptChanged;

    public Camera PlayerCamera => playerCamera;
    public bool IsSprinting { get; private set; }
    public bool IsGrounded => controller != null && controller.isGrounded;
    public InputAction PauseAction { get; private set; }
    public InputAction InteractAction => interactAction;

    public string InteractionPrompt =>
        focusedInteractableComponent != null ? focusedInteractable.Prompt : string.Empty;

    private static bool IsLocked =>
        DialogueRunner.LocksPlayer ||
        (PauseController.Instance != null && PauseController.Instance.IsPaused);

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        inputActions = BuildActionMap();
        moveAction = inputActions.FindAction("Move");
        lookAction = inputActions.FindAction("Look");
        sprintAction = inputActions.FindAction("Sprint");
        interactAction = inputActions.FindAction("Interact");
        pointerClickAction = inputActions.FindAction("Pointer Click");
        cursorToggleAction = inputActions.FindAction("Toggle Cursor");
        PauseAction = inputActions.FindAction("Pause");
        ApplySettings();
    }

    private void Start() => LockCursor(true);

    private void OnEnable()
    {
        inputActions.Enable();
        ApplyInputOverrides(GameSettings.Current.inputOverridesJson);
        InputSystem.onDeviceChange += HandleInputDeviceChange;
        GameSettings.Applied += ApplySettings;
        LockCursor(true);
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= HandleInputDeviceChange;
        GameSettings.Applied -= ApplySettings;
        inputActions?.Disable();
        SetFocusedInteractable(null);
        LockCursor(false);
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
        inputActions = null;
    }

    private void ApplySettings()
    {
        SettingsData s = GameSettings.Current;
        SetMouseSensitivity(s.mouseSensitivity);
        SetFieldOfView(s.fieldOfView);
        SetAntiAliasing(s.antiAliasing);
        ApplyInputOverrides(s.inputOverridesJson);
    }

    private void ApplyInputOverrides(string overridesJson)
    {
        if (inputActions == null)
            return;

        bool wasEnabled = inputActions.enabled;
        if (wasEnabled)
            inputActions.Disable();

        if (string.IsNullOrEmpty(overridesJson))
        {
            inputActions.RemoveAllBindingOverrides();
        }
        else
        {
            try
            {
                inputActions.LoadBindingOverridesFromJson(overridesJson);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Discarding incompatible key binding overrides: {e.Message}");
                inputActions.RemoveAllBindingOverrides();
            }
        }

        if (wasEnabled)
            inputActions.Enable();
    }

    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = Mathf.Max(0.001f, value);
    }

    public void SetFieldOfView(float horizontalFov)
    {
        if (playerCamera == null)
            return;

        float aspect = playerCamera.aspect > 0.01f ? playerCamera.aspect : 16f / 9f;
        float verticalRad = 2f * Mathf.Atan(Mathf.Tan(horizontalFov * Mathf.Deg2Rad * 0.5f) / aspect);
        playerCamera.fieldOfView = Mathf.Clamp(verticalRad * Mathf.Rad2Deg, 30f, 120f);
    }

    public void SetAntiAliasing(bool enabled)
    {
        if (playerCamera == null)
            return;

        UniversalAdditionalCameraData cameraData = playerCamera.GetUniversalAdditionalCameraData();
        if (cameraData == null)
            return;

        cameraData.antialiasing = enabled ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
        cameraData.antialiasingQuality = AntialiasingQuality.High;
    }

    private void Update()
    {
        if (IsLocked)
        {
            SetFocusedInteractable(null);
            return;
        }

        bool recapturedCursor = false;
        if (cursorToggleAction.WasPressedThisFrame())
        {
            LockCursor(Cursor.lockState != CursorLockMode.Locked);
        }
        else if (pointerClickAction.WasPressedThisFrame() && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor(true);
            recapturedCursor = true;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            SetFocusedInteractable(null);
            return;
        }

        if (lookAction.controls.Count > 0)
        {
            Vector2 delta = lookAction.ReadValue<Vector2>() * mouseSensitivity;
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -lookLimit, lookLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        UpdateMovement();
        UpdateInteraction(!recapturedCursor);
    }

    private void UpdateMovement()
    {
        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        if (GameSettings.Current.holdToSprint)
        {
            IsSprinting = sprintAction.IsPressed();
        }
        else
        {
            if (sprintAction.WasPressedThisFrame())
                sprintToggled = !sprintToggled;
            IsSprinting = sprintToggled;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * (IsSprinting ? sprintSpeed : walkSpeed);
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateInteraction(bool acceptInput)
    {
        if (interactAction.controls.Count == 0)
        {
            SetFocusedInteractable(null);
            return;
        }

        IInteractable candidate = null;
        if (Physics.SphereCast(playerCamera.transform.position, interactionRadius, playerCamera.transform.forward,
                out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore))
            candidate = hit.collider.GetComponentInParent<IInteractable>();

        if (candidate is Component candidateComponent && candidateComponent != null)
        {
            SetFocusedInteractable(candidate);
            lastFocusedTime = Time.unscaledTime;
        }
        else if (Time.unscaledTime - lastFocusedTime > focusGraceTime)
        {
            SetFocusedInteractable(null);
        }

        if (acceptInput && focusedInteractableComponent != null && interactAction.WasPressedThisFrame())
            focusedInteractable.Interact();
    }

    public static InputActionMap BuildActionMap()
    {
        var map = new InputActionMap("First Person");

        InputAction move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        map.AddAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
        map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift", expectedControlLayout: "Button");
        map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e", expectedControlLayout: "Button")
            .AddBinding("<Mouse>/leftButton");
        map.AddAction("Pointer Click", InputActionType.Button, "<Mouse>/leftButton", expectedControlLayout: "Button");
        map.AddAction("Toggle Cursor", InputActionType.Button, CursorDefaultPath, expectedControlLayout: "Button");
        map.AddAction("Pause", InputActionType.Button, PauseDefaultPath, expectedControlLayout: "Button");

        foreach (RebindableAction entry in RebindableActions)
        {
            InputAction action = map.FindAction(entry.ActionName);
            if (action == null || entry.BindingIndex >= action.bindings.Count)
                continue;

            InputBinding binding = action.bindings[entry.BindingIndex];
            binding.id = new Guid(entry.BindingId);
            action.ChangeBinding(entry.BindingIndex).To(binding);
        }

        return map;
    }

    private void HandleInputDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if ((device is Keyboard || device is Mouse) &&
            (change == InputDeviceChange.Removed ||
             change == InputDeviceChange.Disconnected ||
             change == InputDeviceChange.Disabled))
        {
            SetFocusedInteractable(null);
        }
    }

    private void SetFocusedInteractable(IInteractable interactable)
    {
        Component component = interactable as Component;
        if (ReferenceEquals(focusedInteractable, interactable) && focusedInteractableComponent == component)
            return;

        focusedInteractable = interactable;
        focusedInteractableComponent = component;
        InteractionPromptChanged?.Invoke(InteractionPrompt);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}

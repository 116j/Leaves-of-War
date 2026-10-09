using System;
using System.Collections.Generic;
using Hortensia.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    // ----------------------------------------------------------------------
    //  Rebindable action catalogue
    //
    //  A single source of truth for every binding the player can remap. Both
    //  the live gameplay action map (below) and the menu's throwaway "capture"
    //  map (KeyRebinding.cs) are built from this table, so they can never drift
    //  apart. Each entry maps a human-facing row in the OPTIONS > KEY BINDINGS
    //  screen to one concrete binding inside one InputAction.
    //
    //  Look (mouse delta) is intentionally absent: it is not a discrete key and
    //  is not rebindable. Pointer Click is absent too - it exists only so an
    //  unlocked click can recapture the cursor, and must stay the left button.
    // ----------------------------------------------------------------------
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
        // Fixed GUID stamped onto this binding in BuildActionMap. Because the same
        // id is used in every built instance (gameplay map and capture map alike),
        // serialized overrides saved against one map load cleanly into the other.
        public string BindingId { get; }
    }

    // The Move action has arrow-key and WASD 2DVector composites. The arrow
    // composite's four parts live at binding indices 1..4 (index 0 is its
    // composite head) and remain the rebindable defaults. Interact, Sprint,
    // and Toggle Cursor are simple bindings at index 0.
    //
    // On WebGL, the browser reserves Escape to exit fullscreen and the game
    // cannot intercept or override that - so Escape must never be the default
    // for anything there. Pause defaults to P instead on that platform.
#if UNITY_WEBGL && !UNITY_EDITOR
    private const string PauseDefaultPath = "<Keyboard>/p";
    private const string CursorDefaultPath = "<Keyboard>/tab";
#else
    private const string PauseDefaultPath = "<Keyboard>/escape";
    // Escape belongs exclusively to Pause so it remains available while a
    // narrative panel is open.  Keeping the cursor toggle on Tab also avoids
    // serializing two rebindable actions with the same default path.
    private const string CursorDefaultPath = "<Keyboard>/tab";
#endif

    public static readonly IReadOnlyList<RebindableAction> RebindableActions = new[]
    {
        new RebindableAction("move_up",     "MOVE FORWARD",  "Move",          1, "<Keyboard>/upArrow",      "Button", "a1000000-0000-4000-8000-000000000001"),
        new RebindableAction("move_down",   "MOVE BACKWARD", "Move",          2, "<Keyboard>/downArrow",      "Button", "a1000000-0000-4000-8000-000000000002"),
        new RebindableAction("move_left",   "MOVE LEFT",     "Move",          3, "<Keyboard>/leftArrow",      "Button", "a1000000-0000-4000-8000-000000000003"),
        new RebindableAction("move_right",  "MOVE RIGHT",    "Move",          4, "<Keyboard>/rightArrow",      "Button", "a1000000-0000-4000-8000-000000000004"),
        new RebindableAction("sprint",      "SPRINT",        "Sprint",        0, "<Keyboard>/leftShift", "Button", "a1000000-0000-4000-8000-00000000000a"),
        new RebindableAction("interact",    "INTERACT",      "Interact",      0, "<Keyboard>/e",      "Button", "a1000000-0000-4000-8000-000000000005"),
        new RebindableAction("cursor",      "TOGGLE CURSOR", "Toggle Cursor", 0, CursorDefaultPath,   "Button", "a1000000-0000-4000-8000-000000000006"),
        new RebindableAction("pause",       "PAUSE",         "Pause",         0, PauseDefaultPath,    "Button", "a1000000-0000-4000-8000-000000000007"),
        new RebindableAction("close_document", "CLOSE DOCUMENT", "Close Document", 0, "<Keyboard>/backspace", "Button", "a1000000-0000-4000-8000-000000000008"),
        new RebindableAction("open_inventory", "OPEN INVENTORY", "Open Inventory", 0, "<Keyboard>/i", "Button", "a1000000-0000-4000-8000-000000000009"),
    };

    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [Header("Exploration visibility")]
    [Tooltip("A small, shadowless fill carried by the player so greybox darkness never obscures the traversable route.")]
    [SerializeField] private bool useExplorationFillLight = true;
    [SerializeField, Min(0f)] private float explorationFillRange = 11f;
    [SerializeField, Min(0f)] private float explorationFillIntensity = 2.2f;
    private const float MaximumExplorationFogDensity = 0.008f;
    private static readonly Color ExplorationAmbientColor = new(0.5f, 0.43f, 0.34f, 1f);

    /// <summary>The first-person camera this controller drives.</summary>
    public Camera PlayerCamera => playerCamera;

    /// <summary>The camera's signed local pitch, kept in sync with look input and restored poses.</summary>
    public float ViewPitch => pitch;

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
    private GameSessionRegistration sessionRegistration;
    private float verticalVelocity;
    private float pitch;
    private bool sprintToggled;

    /// <summary>Whether the player is currently sprinting this frame - exposed so other components (e.g. FootstepController) can react.</summary>
    public bool IsSprinting { get; private set; }

    /// <summary>Grounded state from the production collision controller.</summary>
    public bool IsGrounded => controller != null && controller.isGrounded;

    /// <summary>Collision result from the most recent production movement step.</summary>
    public CollisionFlags LastCollisionFlags { get; private set; }
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
    private InputAction pauseAction;
    private InputAction closeDocumentAction;
    private InputAction openInventoryAction;

    public event Action<string> InteractionPromptChanged;

    public string InteractionPrompt =>
        focusedInteractableComponent != null ? focusedInteractable.Prompt : string.Empty;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        sessionRegistration = new GameSessionRegistration(
            session => session.RegisterPlayer(this, playerCamera),
            session => session.UnregisterPlayer(this));

        ApplyExplorationVisibilitySettings();
        EnsureExplorationFillLight();
        EnsureManorWorldBoundarySafety();

        CreateInputActions();
        ApplySettings();
    }

    private void EnsureExplorationFillLight()
    {
        if (!useExplorationFillLight || playerCamera == null)
            return;

        const string lightName = "Player Exploration Fill";
        Transform existing = playerCamera.transform.Find(lightName);
        Light fill = existing != null ? existing.GetComponent<Light>() : null;
        if (fill == null)
        {
            var lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(playerCamera.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, -0.08f, 0.12f);
            fill = lightObject.AddComponent<Light>();
        }

        // This is deliberately a modest, unshadowed warm fill rather than a
        // flashlight cone: it preserves the scene's horror palette while making
        // floors, doors, and collision boundaries readable in every location.
        fill.type = LightType.Point;
        fill.color = new Color(1f, 0.84f, 0.62f);
        fill.intensity = explorationFillIntensity;
        fill.range = explorationFillRange;
        fill.shadows = LightShadows.None;
        // RetroLit evaluates additional punctual lights per-pixel so it remains
        // compatible with URP Forward+. Force that supported path rather than
        // dropping this fill into URP's unused vertex-lighting channel.
        fill.renderMode = LightRenderMode.ForcePixel;
        fill.enabled = true;
    }

    private void ApplyExplorationVisibilitySettings()
    {
        // The authored scenes were built with an exponential fog density of
        // 0.2, which hides even nearby doors and paths. Keep fog's atmosphere,
        // but cap it at a navigable distance for every gameplay scene.
        if (RenderSettings.fog && RenderSettings.fogDensity > MaximumExplorationFogDensity)
            RenderSettings.fogDensity = MaximumExplorationFogDensity;

        // The imported locations use skybox ambient against a deliberately
        // black sky. A flat, muted base light keeps their silhouette language
        // while ensuring the world still receives visible illumination.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ExplorationAmbientColor;
    }

    private void EnsureManorWorldBoundarySafety()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, "Manor", System.StringComparison.Ordinal))
            return;

        const string rootName = "MANOR RUNTIME WORLD BOUNDARY SAFETY";
        if (GameObject.Find(rootName) != null)
            return;

        // The Manor owns matching scene colliders, but imported environment
        // revisions have intermittently left a controller-sized gap at their
        // outer edge. Keep an invisible, scene-local fallback perimeter using
        // those same authored bounds so the player never reaches the empty
        // ground beyond the location.
        var root = new GameObject(rootName);
        SceneManager.MoveGameObjectToScene(root, activeScene);

        CreateBoundaryWall(root.transform, "West", new Vector3(-86.2f, 2f, 0f), new Vector3(1.2f, 8f, 114f));
        CreateBoundaryWall(root.transform, "East", new Vector3(52f, 2f, 0f), new Vector3(1.2f, 8f, 114f));
        CreateBoundaryWall(root.transform, "South", new Vector3(-16.7f, 2f, -56.8f), new Vector3(140f, 8f, 1.2f));
        CreateBoundaryWall(root.transform, "North", new Vector3(-16.5f, 2f, 56.5f), new Vector3(140f, 8f, 1.2f));
    }

    private static void CreateBoundaryWall(
        Transform parent,
        string name,
        Vector3 worldPosition,
        Vector3 size)
    {
        var wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.position = worldPosition;
        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.size = size;
    }

    private void OnEnable()
    {
        // Domain reload can preserve a live controller while rebuilding scripts
        // in the Editor, so recreate the runtime-only light here as well as Awake.
        ApplyExplorationVisibilitySettings();
        EnsureExplorationFillLight();
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        inputActions.Enable();
        // Load the saved key overrides *after* the map is enabled. Applying them
        // before the map has ever been active (as happened in Awake) can leave the
        // resolved controls on the default keys until a proper disable/enable cycle
        // runs. ApplyInputOverrides now performs that cycle whenever the map is live.
        ApplyInputOverrides(GameSettings.Current.inputOverridesJson);
        InputSystem.onDeviceChange += HandleInputDeviceChange;
        GameSettings.Applied += ApplySettings;
        sessionRegistration?.Enable();
        LockCursor(true);
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        InputSystem.onDeviceChange -= HandleInputDeviceChange;
        GameSettings.Applied -= ApplySettings;
        sessionRegistration?.Disable();
        inputActions?.Disable();
        SetFocusedInteractable(null);
        LockCursor(false);
    }

    private void HandleActiveSceneChanged(
        Scene previous,
        Scene next)
    {
        ApplyExplorationVisibilitySettings();
        EnsureManorWorldBoundarySafety();
    }

    private void OnDestroy()
    {
        sessionRegistration?.Dispose();
        sessionRegistration = null;
        inputActions?.Dispose();
        inputActions = null;
    }

    /// <summary>
    /// Pulls the look/camera values from the saved settings. Called at Awake and
    /// whenever the options screen commits changes via GameSettings.Applied.
    /// </summary>
    private void ApplySettings()
    {
        SettingsData s = GameSettings.Current;
        SetMouseSensitivity(s.mouseSensitivity);
        SetFieldOfView(s.fieldOfView);
        SetAntiAliasing(s.antiAliasing);
        // Key overrides are re-applied here too, so pressing APPLY while already
        // in-game takes effect immediately. When the map is enabled (the normal
        // in-game case) ApplyInputOverrides forces a disable/enable cycle so the
        // new keys resolve on the controls right away.
        ApplyInputOverrides(s.inputOverridesJson);
    }

    /// <summary>
    /// Loads serialized binding overrides onto the live action map. Passing null
    /// or empty restores the defaults built in <see cref="CreateInputActions"/>.
    /// Safe to call at any time; re-enables the map if it was active.
    /// </summary>
    private void ApplyInputOverrides(string overridesJson)
    {
        if (inputActions == null)
            return;

        // When the map is live, wrap the override load in a disable/enable cycle:
        // the Input System re-resolves each binding's effective control on enable,
        // which is what makes a newly-loaded override actually take over the key.
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
                // A saved override string from an older build can reference binding
                // ids that no longer exist. Rather than spam the log and run with a
                // half-applied map, discard it and fall back to the default keys.
                Debug.LogWarning(
                    $"Discarding incompatible key binding overrides: {e.Message}");
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

    public void SetFieldOfView(float verticalOrHorizontalFov)
    {
        if (playerCamera == null)
            return;

        // Options exposes a horizontal-ish FOV number (60..110). Unity's Camera.fieldOfView
        // is vertical, so convert using the current aspect to keep the felt width consistent.
        float aspect = playerCamera.aspect > 0.01f ? playerCamera.aspect : (16f / 9f);
        float horizontalRad = verticalOrHorizontalFov * Mathf.Deg2Rad;
        float verticalRad = 2f * Mathf.Atan(Mathf.Tan(horizontalRad * 0.5f) / aspect);
        playerCamera.fieldOfView = Mathf.Clamp(verticalRad * Mathf.Rad2Deg, 30f, 120f);
    }

    /// <summary>
    /// QualitySettings.antiAliasing (legacy MSAA) has no effect under URP, which
    /// controls anti-aliasing per camera instead. Set it directly here so the
    /// ANTI-ALIASING option actually does something.
    /// </summary>
    public void SetAntiAliasing(bool enabled)
    {
        if (playerCamera == null)
            return;

        UniversalAdditionalCameraData cameraData = playerCamera.GetUniversalAdditionalCameraData();
        if (cameraData == null)
            return;

        cameraData.antialiasing = enabled
            ? AntialiasingMode.FastApproximateAntialiasing
            : AntialiasingMode.None;
        cameraData.antialiasingQuality = AntialiasingQuality.High;
    }

    private void Update()
    {
        // Scene activation can apply its RenderSettings after activeSceneChanged
        // and after this controller enables. Keep the cap live so travel cannot
        // restore the unusably dense authored fog on the following frame.
        ApplyExplorationVisibilitySettings();
        GameSession session = GameSession.Instance;
        if (session != null && session.IsPlayerLocked)
        {
            SetFocusedInteractable(null);
            return;
        }

        if (AgentNavigationOverlay.IsVisible)
        {
            // The overlay owns the single production movement sample while it
            // is open. This also keeps cursor-toggle input from relocking the
            // mouse and causing a second movement/gravity integration.
            SetFocusedInteractable(null);
            return;
        }

        bool narrativeInputCaptured = session != null && session.IsNarrativeInputCaptured;
        bool recapturedCursor = false;

        if (cursorToggleAction.WasPressedThisFrame())
        {
            LockCursor(Cursor.lockState != CursorLockMode.Locked);
        }
        else if (!narrativeInputCaptured &&
                 pointerClickAction.WasPressedThisFrame() &&
                 Cursor.lockState != CursorLockMode.Locked)
        {
            // Regain mouse look when the player clicks back into the game window.
            LockCursor(true);
            recapturedCursor = true;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            if (lookAction.controls.Count > 0)
                UpdateLook();
            // Vertical physics is part of controller movement too. Keep it
            // running with zero horizontal input when no keyboard is present.
            UpdateMovement();

            if (narrativeInputCaptured)
                SetFocusedInteractable(null);
            else
                UpdateInteraction(!recapturedCursor);
        }
        else
        {
            SetFocusedInteractable(null);
        }
    }

    private void UpdateLook()
    {
        Vector2 mouseDelta = lookAction.ReadValue<Vector2>() * mouseSensitivity;
        ApplyLookDelta(mouseDelta.x, mouseDelta.y);
    }

    private void UpdateMovement()
    {
        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        bool sprinting;
        if (GameSettings.Current.holdToSprint)
        {
            sprinting = sprintAction != null && sprintAction.IsPressed();
        }
        else
        {
            if (sprintAction != null && sprintAction.WasPressedThisFrame())
                sprintToggled = !sprintToggled;
            sprinting = sprintToggled;
        }

        ApplyMovement(input, sprinting, Time.deltaTime);
    }

    /// <summary>
    /// Applies one deterministic movement sample through the same
    /// CharacterController path as live keyboard input.
    /// </summary>
    public bool TryApplyAgentMovement(
        Vector2 localInput,
        bool sprinting,
        float deltaTime,
        out CollisionFlags collisionFlags)
    {
        collisionFlags = CollisionFlags.None;
        GameSession session = GameSession.Instance;
        if (!isActiveAndEnabled ||
            controller == null ||
            deltaTime <= 0f ||
            (session != null && session.IsPlayerLocked))
        {
            return false;
        }

        collisionFlags = ApplyMovement(
            Vector2.ClampMagnitude(localInput, 1f),
            sprinting,
            deltaTime);
        return true;
    }

    /// <summary>
    /// Applies a look sample while keeping the controller's internal pitch
    /// synchronized with the camera.
    /// </summary>
    public bool TryApplyAgentLook(float yawDegrees, float lookUpDegrees)
    {
        GameSession session = GameSession.Instance;
        if (!isActiveAndEnabled ||
            playerCamera == null ||
            (session != null && session.IsPlayerLocked))
        {
            return false;
        }

        ApplyLookDelta(yawDegrees, lookUpDegrees);
        return true;
    }

    /// <summary>
    /// Places the production controller at a persisted world pose without asking
    /// <see cref="CharacterController"/> to resolve the teleport as movement.
    /// The body keeps the complete saved rotation while camera pitch remains a
    /// separate, clamped local rotation, matching the live look path.
    /// </summary>
    public bool TrySetPose(
        Vector3 worldPosition,
        Quaternion bodyRotation,
        float viewPitch,
        out string error)
    {
        if (controller == null)
        {
            error = $"Cannot place player because its {nameof(CharacterController)} is missing.";
            return false;
        }

        if (playerCamera == null)
        {
            error = "Cannot place player because its camera is missing.";
            return false;
        }

        if (!IsFinite(worldPosition.x) ||
            !IsFinite(worldPosition.y) ||
            !IsFinite(worldPosition.z))
        {
            error = "Cannot place player because its world position is not finite.";
            return false;
        }

        if (!IsFinite(bodyRotation.x) ||
            !IsFinite(bodyRotation.y) ||
            !IsFinite(bodyRotation.z) ||
            !IsFinite(bodyRotation.w))
        {
            error = "Cannot place player because its body rotation is not finite.";
            return false;
        }

        float rotationMagnitudeSquared =
            bodyRotation.x * bodyRotation.x +
            bodyRotation.y * bodyRotation.y +
            bodyRotation.z * bodyRotation.z +
            bodyRotation.w * bodyRotation.w;
        if (!IsFinite(rotationMagnitudeSquared) ||
            rotationMagnitudeSquared <= Mathf.Epsilon)
        {
            error = "Cannot place player because its body rotation is invalid.";
            return false;
        }

        if (!IsFinite(viewPitch))
        {
            error = "Cannot place player because its view pitch is not finite.";
            return false;
        }

        float inverseRotationMagnitude = 1f / Mathf.Sqrt(rotationMagnitudeSquared);
        var normalizedBodyRotation = new Quaternion(
            bodyRotation.x * inverseRotationMagnitude,
            bodyRotation.y * inverseRotationMagnitude,
            bodyRotation.z * inverseRotationMagnitude,
            bodyRotation.w * inverseRotationMagnitude);
        float clampedPitch = Mathf.Clamp(viewPitch, -lookLimit, lookLimit);
        bool controllerWasEnabled = controller.enabled;

        try
        {
            if (controllerWasEnabled)
                controller.enabled = false;

            transform.SetPositionAndRotation(worldPosition, normalizedBodyRotation);
            pitch = clampedPitch;
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            verticalVelocity = 0f;
            LastCollisionFlags = CollisionFlags.None;
            IsSprinting = false;
            sprintToggled = false;
            SetFocusedInteractable(null);
        }
        finally
        {
            if (controller != null)
                controller.enabled = controllerWasEnabled;
        }

        error = string.Empty;
        return true;
    }

    private CollisionFlags ApplyMovement(
        Vector2 input,
        bool sprinting,
        float deltaTime)
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * deltaTime;

        IsSprinting = sprinting;
        float currentSpeed = sprinting ? sprintSpeed : walkSpeed;

        Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * currentSpeed;
        velocity.y = verticalVelocity;
        LastCollisionFlags = controller.Move(velocity * deltaTime);
        return LastCollisionFlags;
    }

    private void ApplyLookDelta(float yawDegrees, float lookUpDegrees)
    {
        transform.Rotate(0f, yawDegrees, 0f);
        pitch = Mathf.Clamp(pitch - lookUpDegrees, -lookLimit, lookLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private void UpdateInteraction(bool acceptInput)
    {
        if (interactAction.controls.Count == 0)
        {
            SetFocusedInteractable(null);
            return;
        }

        IInteractable candidate = null;
        if (Physics.SphereCast(playerCamera.transform.position, interactionRadius, playerCamera.transform.forward, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore))
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

        if (acceptInput &&
            focusedInteractableComponent != null &&
            interactAction.WasPressedThisFrame())
        {
            focusedInteractable.Interact();
        }
    }

    private void CreateInputActions()
    {
        inputActions = BuildActionMap();
        moveAction = inputActions.FindAction("Move");
        lookAction = inputActions.FindAction("Look");
        sprintAction = inputActions.FindAction("Sprint");
        interactAction = inputActions.FindAction("Interact");
        pointerClickAction = inputActions.FindAction("Pointer Click");
        cursorToggleAction = inputActions.FindAction("Toggle Cursor");
        pauseAction = inputActions.FindAction("Pause");
        closeDocumentAction = inputActions.FindAction("Close Document");
        openInventoryAction = inputActions.FindAction("Open Inventory");
    }

    /// <summary>
    /// The Pause action from the live player map, exposed so the PauseController
    /// can poll it. Reflects any user rebind applied to this controller's map.
    /// </summary>
    public InputAction PauseAction => pauseAction;

    /// <summary>
    /// The Interact action from the live player map, exposed so sequence input
    /// can honour the player's rebound interact key.
    /// </summary>
    public InputAction InteractAction => interactAction;

    /// <summary>
    /// The Close Document action from the live player map. Documents used to
    /// close on Escape; they now use this dedicated, rebindable key so Escape
    /// is always free to open Pause instead.
    /// </summary>
    public InputAction CloseDocumentAction => closeDocumentAction;

    /// <summary>
    /// The Open Inventory action from the live player map - opens the item
    /// grid (documents/letters and other carried items), independent of any
    /// world Interactable.
    /// </summary>
    public InputAction OpenInventoryAction => openInventoryAction;

    /// <summary>
    /// Builds the canonical player action map with its default bindings. This is
    /// the single definition used both by the live controller and by the menu's
    /// capture map, so the binding indices referenced in <see cref="RebindableActions"/>
    /// are guaranteed to line up in both places.
    ///
    /// IMPORTANT: the order in which bindings are added fixes their indices.
    /// The arrow-key Move composite occupies indices 0..4 (0 = composite head,
    /// 1 = Up, 2 = Down, 3 = Left, 4 = Right), and the supplemental WASD
    /// composite occupies indices 5..9. Do not reorder the arrow-key composite
    /// without updating <see cref="RebindableActions"/>.
    /// </summary>
    public static InputActionMap BuildActionMap()
    {
        var map = new InputActionMap("First Person");

        InputAction move = map.AddAction(
            "Move",
            InputActionType.Value,
            expectedControlLayout: "Vector2");
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

        map.AddAction(
            "Look",
            InputActionType.Value,
            "<Mouse>/delta",
            expectedControlLayout: "Vector2");

        map.AddAction(
            "Sprint",
            InputActionType.Button,
            "<Keyboard>/leftShift",
            expectedControlLayout: "Button");

        InputAction interact = map.AddAction(
            "Interact",
            InputActionType.Button,
            "<Keyboard>/e",
            expectedControlLayout: "Button");
        interact.AddBinding("<Mouse>/leftButton");

        // Keep pointer clicks distinct so an unlocked click can recapture the
        // cursor without also interacting with the focused world object.
        map.AddAction(
            "Pointer Click",
            InputActionType.Button,
            "<Mouse>/leftButton",
            expectedControlLayout: "Button");

        map.AddAction(
            "Toggle Cursor",
            InputActionType.Button,
            CursorDefaultPath,
            expectedControlLayout: "Button");

        // Pause is its own action (default Escape, or P on WebGL where the
        // browser reserves Escape for exiting fullscreen and the game cannot
        // intercept it). Separately rebindable. The PauseController reads it
        // from the live player's map; it is not consumed here in the
        // controller, so it keeps working even while the player is
        // input-locked during a pause.
        map.AddAction(
            "Pause",
            InputActionType.Button,
            PauseDefaultPath,
            expectedControlLayout: "Button");

        // Documents used to close on Escape, which meant Escape could not
        // also open the pause menu while reading one. Closing now has its
        // own dedicated, rebindable key, so Escape always opens Pause.
        map.AddAction(
            "Close Document",
            InputActionType.Button,
            "<Keyboard>/backspace",
            expectedControlLayout: "Button");

        map.AddAction(
            "Open Inventory",
            InputActionType.Button,
            "<Keyboard>/i",
            expectedControlLayout: "Button");

        // Stamp fixed GUIDs onto each rebindable binding. Overrides are matched by
        // binding id, so giving both the gameplay map and the capture map the same
        // ids is what lets a saved override load into a freshly built map. Without
        // this, every BuildActionMap() call would mint random ids and the override
        // JSON would fail to match ("no existing binding was found with the id ...").
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
        if (!(device is Keyboard) && !(device is Mouse))
            return;

        if (change == InputDeviceChange.Removed ||
            change == InputDeviceChange.Disconnected ||
            change == InputDeviceChange.Disabled)
        {
            SetFocusedInteractable(null);
        }
    }

    private void SetFocusedInteractable(IInteractable interactable)
    {
        Component component = interactable as Component;
        if (ReferenceEquals(focusedInteractable, interactable) &&
            focusedInteractableComponent == component)
        {
            return;
        }

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

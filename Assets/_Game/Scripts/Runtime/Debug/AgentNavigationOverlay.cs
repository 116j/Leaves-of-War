using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Hortensia.Runtime
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class AgentNavigationOverlay : MonoBehaviour
    {
        private const float MinimumProgress = 0.0001f;
        private const float StallTimeout = 0.45f;
        private const float BaseCommandTimeout = 2f;

        private static readonly float[] StepDistances = { 0.25f, 0.5f, 1f, 2f };
        private static readonly float[] TurnAngles = { 5f, 15f, 30f, 45f };

        private static AgentNavigationOverlay instance;

        private FirstPersonController player;
        private Rect windowRect;
        private bool visible;
        private bool commandActive;
        private Vector2 commandInput;
        private Vector3 commandWorldDirection;
        private float commandTargetDistance;
        private float commandProgress;
        private float commandElapsed;
        private float stalledFor;
        private float stepDistance = 0.5f;
        private float turnAngle = 15f;
        private string commandLabel = "IDLE";
        private string status = "READY";
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        public static AgentNavigationOverlay Instance => instance;
        public static bool IsVisible => instance != null && instance.visible;
        public bool CommandActive => commandActive;
        public string Status => status;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureInstance();
#endif
        }

        public static AgentNavigationOverlay EnsureInstance()
        {
            if (instance != null)
                return instance;

            AgentNavigationOverlay existing =
                FindFirstObjectByType<AgentNavigationOverlay>(FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            var overlayObject = new GameObject("Agent Navigation Overlay");
            overlayObject.hideFlags = HideFlags.HideAndDontSave;
            return overlayObject.AddComponent<AgentNavigationOverlay>();
        }

        public static void SetVisible(bool shouldShow)
        {
            AgentNavigationOverlay overlay = EnsureInstance();
            overlay.SetVisibleInternal(shouldShow);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            windowRect = new Rect(
                Mathf.Max(20f, Screen.width - 390f),
                20f,
                370f,
                430f);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
                SetVisibleInternal(!visible);

            if (!visible)
                return;

            ResolvePlayer();
            if (player == null)
            {
                status = "NO ACTIVE PLAYER";
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                status = "PAUSED";
                return;
            }

            if (commandActive)
            {
                TickCommand(deltaTime);
                return;
            }

            // Keep production gravity active while the cursor is unlocked for
            // clicking, including after stepping over an unsupported edge.
            player.TryApplyAgentMovement(
                Vector2.zero,
                false,
                deltaTime,
                out _);
        }

        private void OnDisable()
        {
            if (visible)
                RestoreCursor();
            visible = false;
            StopCommand("DISABLED");
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            windowRect.x = Mathf.Clamp(
                windowRect.x,
                0f,
                Mathf.Max(0f, Screen.width - windowRect.width));
            windowRect.y = Mathf.Clamp(
                windowRect.y,
                0f,
                Mathf.Max(0f, Screen.height - 40f));
            windowRect = GUI.Window(
                GetEntityId().GetHashCode(),
                windowRect,
                DrawWindow,
                "AGENT NAVIGATION");
        }

        public bool QueueMove(Vector2 localDirection)
        {
            ResolvePlayer();
            if (player == null)
            {
                status = "NO ACTIVE PLAYER";
                return false;
            }

            if (localDirection.sqrMagnitude < 0.001f)
                return false;

            GameSession session = GameSession.Instance;
            if (session != null && session.IsPlayerLocked)
            {
                status = "PLAYER LOCKED";
                return false;
            }

            commandInput = Vector2.ClampMagnitude(localDirection, 1f);
            commandWorldDirection =
                player.transform.right * commandInput.x +
                player.transform.forward * commandInput.y;
            commandWorldDirection.y = 0f;
            commandWorldDirection.Normalize();
            commandTargetDistance = stepDistance;
            commandProgress = 0f;
            commandElapsed = 0f;
            stalledFor = 0f;
            commandActive = true;
            commandLabel = DescribeDirection(commandInput);
            status = $"{commandLabel} 0.00/{commandTargetDistance:0.00} M";
            return true;
        }

        public bool Turn(float yawDegrees)
        {
            ResolvePlayer();
            StopCommand("STOPPED");
            if (player == null)
            {
                status = "NO ACTIVE PLAYER";
                return false;
            }

            if (!player.TryApplyAgentLook(yawDegrees, 0f))
            {
                status = "PLAYER LOCKED";
                return false;
            }

            status = $"YAW {NormalizeYaw(player.transform.eulerAngles.y):0.0}";
            return true;
        }

        public bool Look(float lookUpDegrees)
        {
            ResolvePlayer();
            StopCommand("STOPPED");
            if (player == null)
            {
                status = "NO ACTIVE PLAYER";
                return false;
            }

            if (!player.TryApplyAgentLook(0f, lookUpDegrees))
            {
                status = "PLAYER LOCKED";
                return false;
            }

            status = lookUpDegrees >= 0f ? "LOOK UP" : "LOOK DOWN";
            return true;
        }

        public void StopCommand(string nextStatus = "STOPPED")
        {
            commandActive = false;
            commandInput = Vector2.zero;
            commandProgress = 0f;
            commandElapsed = 0f;
            stalledFor = 0f;
            commandLabel = "IDLE";
            status = nextStatus;
        }

        private void TickCommand(float deltaTime)
        {
            Vector3 before = player.transform.position;
            if (!player.TryApplyAgentMovement(
                    commandInput,
                    false,
                    deltaTime,
                    out CollisionFlags collisionFlags))
            {
                StopCommand("PLAYER LOCKED");
                return;
            }

            Vector3 displacement = player.transform.position - before;
            displacement.y = 0f;
            float progress = Mathf.Max(
                0f,
                Vector3.Dot(displacement, commandWorldDirection));
            commandProgress += progress;
            commandElapsed += deltaTime;
            stalledFor = progress > MinimumProgress ? 0f : stalledFor + deltaTime;

            if (commandProgress + 0.001f >= commandTargetDistance)
            {
                StopCommand($"{commandLabel} COMPLETE {commandProgress:0.00} M");
                return;
            }

            if (stalledFor >= StallTimeout &&
                (collisionFlags & CollisionFlags.Sides) != 0)
            {
                StopCommand($"{commandLabel} BLOCKED {commandProgress:0.00} M");
                return;
            }

            float timeout = BaseCommandTimeout + commandTargetDistance;
            if (commandElapsed >= timeout)
            {
                StopCommand($"{commandLabel} STALLED {commandProgress:0.00} M");
                return;
            }

            status =
                $"{commandLabel} {commandProgress:0.00}/{commandTargetDistance:0.00} M";
        }

        private void DrawWindow(int windowId)
        {
            ResolvePlayer();
            string sceneName = SceneManager.GetActiveScene().name;
            GUILayout.Label(sceneName.Length > 0 ? sceneName : "NO SCENE");

            if (player == null)
            {
                GUILayout.Label("PLAYER: NONE");
            }
            else
            {
                Vector3 position = player.transform.position;
                GUILayout.Label(
                    $"POS {position.x:0.00}, {position.y:0.00}, {position.z:0.00}");
                GUILayout.Label(
                    $"YAW {NormalizeYaw(player.transform.eulerAngles.y):0.0}  " +
                    $"GROUND {(player.IsGrounded ? "YES" : "NO")}  " +
                    $"HIT {DescribeCollision(player.LastCollisionFlags)}");
            }

            GUILayout.Space(6f);
            GUILayout.Label("STEP (M)");
            DrawValueButtons(StepDistances, ref stepDistance);

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("^", GUILayout.Width(72f), GUILayout.Height(42f)))
                QueueMove(Vector2.up);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("<", GUILayout.Width(72f), GUILayout.Height(42f)))
                QueueMove(Vector2.left);
            if (GUILayout.Button("STOP", GUILayout.Width(88f), GUILayout.Height(42f)))
                StopCommand();
            if (GUILayout.Button(">", GUILayout.Width(72f), GUILayout.Height(42f)))
                QueueMove(Vector2.right);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("v", GUILayout.Width(72f), GUILayout.Height(42f)))
                QueueMove(Vector2.down);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("TURN (DEG)");
            DrawValueButtons(TurnAngles, ref turnAngle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("YAW <", GUILayout.Height(34f)))
                Turn(-turnAngle);
            if (GUILayout.Button("180", GUILayout.Height(34f)))
                Turn(180f);
            if (GUILayout.Button("YAW >", GUILayout.Height(34f)))
                Turn(turnAngle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("LOOK UP", GUILayout.Height(32f)))
                Look(turnAngle);
            if (GUILayout.Button("LOOK DOWN", GUILayout.Height(32f)))
                Look(-turnAngle);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label($"STATUS: {status}");
            if (GUILayout.Button("CLOSE", GUILayout.Height(30f)))
                SetVisibleInternal(false);

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
        }

        private static void DrawValueButtons(float[] values, ref float selected)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < values.Length; i++)
            {
                float value = values[i];
                string label = Mathf.Approximately(selected, value)
                    ? $"[{value:0.##}]"
                    : value.ToString("0.##");
                if (GUILayout.Button(label, GUILayout.Height(28f)))
                    selected = value;
            }
            GUILayout.EndHorizontal();
        }

        private void ResolvePlayer()
        {
            if (player != null && player.isActiveAndEnabled)
                return;

            player = FindFirstObjectByType<FirstPersonController>(
                FindObjectsInactive.Exclude);
        }

        private void SetVisibleInternal(bool shouldShow)
        {
            if (visible == shouldShow)
                return;

            if (shouldShow)
            {
                previousCursorLock = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ResolvePlayer();
                status = player != null ? "READY" : "NO ACTIVE PLAYER";
                visible = true;
                return;
            }

            StopCommand("CLOSED");
            visible = false;
            RestoreCursor();
        }

        private void RestoreCursor()
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private static string DescribeDirection(Vector2 input)
        {
            if (input.y > 0.5f)
                return "FORWARD";
            if (input.y < -0.5f)
                return "BACK";
            if (input.x < -0.5f)
                return "LEFT";
            return "RIGHT";
        }

        private static string DescribeCollision(CollisionFlags flags)
        {
            if (flags == CollisionFlags.None)
                return "NONE";

            string value = string.Empty;
            if ((flags & CollisionFlags.Below) != 0)
                value = "DOWN";
            if ((flags & CollisionFlags.Sides) != 0)
                value += value.Length > 0 ? "+SIDE" : "SIDE";
            if ((flags & CollisionFlags.Above) != 0)
                value += value.Length > 0 ? "+UP" : "UP";
            return value;
        }

        private static float NormalizeYaw(float yaw)
        {
            return Mathf.Repeat(yaw + 180f, 360f) - 180f;
        }
    }
}

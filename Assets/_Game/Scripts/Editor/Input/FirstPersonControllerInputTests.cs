using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hortensia.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class FirstPersonControllerInputTests
    {
        private GameObject playerObject;
        private GameObject interactableObject;
        private GameObject sessionObject;
        private FirstPersonController controller;
        private Keyboard keyboard;
        private Mouse mouse;

        [Test]
        public void RebindableActions_HaveStableUniqueKeyboardBindings()
        {
            var defaultPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (InputActionMap actions = FirstPersonController.BuildActionMap())
            {
                foreach (FirstPersonController.RebindableAction entry in
                    FirstPersonController.RebindableActions)
                {
                    InputAction action = actions.FindAction(entry.ActionName);
                    Assert.That(action, Is.Not.Null, $"Missing action '{entry.ActionName}'.");
                    Assert.That(entry.BindingIndex, Is.LessThan(action.bindings.Count));

                    InputBinding binding = action.bindings[entry.BindingIndex];
                    Assert.That(binding.id.ToString(), Is.EqualTo(entry.BindingId));
                    Assert.That(binding.path, Is.EqualTo(entry.DefaultPath));
                    Assert.That(
                        defaultPaths.Add(entry.DefaultPath),
                        Is.True,
                        $"Default key '{entry.DefaultPath}' is assigned more than once.");
                }
            }
        }

        [Test]
        public void MoveAction_HasArrowKeyAndWasdComposites()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (InputActionMap actions = FirstPersonController.BuildActionMap())
            {
                foreach (InputBinding binding in actions["Move"].bindings)
                    paths.Add(binding.path);
            }

            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "<Keyboard>/upArrow", "<Keyboard>/downArrow",
                    "<Keyboard>/leftArrow", "<Keyboard>/rightArrow",
                    "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d"
                },
                paths);
        }

        [Test]
        public void DuplicateRebinding_IsRestoredToTheActionDefault()
        {
            string conflictingOverrides;
            using (InputActionMap source = FirstPersonController.BuildActionMap())
            {
                source.FindAction("Pause").ApplyBindingOverride(0, "<Keyboard>/e");
                conflictingOverrides = source.SaveBindingOverridesAsJson();
            }

            FirstPersonController.RebindableAction pause = RebindableActionWithId("pause");

            using (var rebinding = new KeyRebinding())
            {
                rebinding.LoadFromJson(conflictingOverrides);

                Assert.That(rebinding.RevertIfBindingConflicts(pause), Is.True);
                Assert.That(rebinding.DisplayKeyFor(pause), Is.EqualTo("ESC"));
            }
        }

        [TestCase("<Keyboard>/space")]
        [TestCase("<Keyboard>/e")]
        public void PauseRebinding_RejectsReservedNarrativeKeys(string reservedPath)
        {
            string reservedOverrides;
            using (InputActionMap source = FirstPersonController.BuildActionMap())
            {
                // Move Interact away before using E so this verifies the raw
                // narrative-input reservation, not merely duplicate action detection.
                source.FindAction("Interact").ApplyBindingOverride(0, "<Keyboard>/f");
                source.FindAction("Pause").ApplyBindingOverride(0, reservedPath);
                reservedOverrides = source.SaveBindingOverridesAsJson();
            }

            FirstPersonController.RebindableAction pause = RebindableActionWithId("pause");
            using (var rebinding = new KeyRebinding())
            {
                rebinding.LoadFromJson(reservedOverrides);

                Assert.That(rebinding.RevertIfBindingConflicts(pause), Is.True);
                Assert.That(rebinding.DisplayKeyFor(pause), Is.EqualTo("ESC"));
            }
        }

        [UnityTest]
        public IEnumerator KeyboardActionsRemainAvailableWithoutAMouse()
        {
            yield return new EnterPlayMode();

            keyboard = InputSystem.AddDevice<Keyboard>();
            controller = CreateController();
            InputActionMap actions = GetActions(controller);
            actions.devices = new InputDevice[] { keyboard };
            InputSystem.Update();

            Assert.That(actions["Move"].controls, Is.Not.Empty);
            Assert.That(HasControlFrom(actions["Move"], keyboard), Is.True);
            Assert.That(HasControlFrom(actions["Interact"], keyboard), Is.True);
            Assert.That(HasControlFrom(actions["Toggle Cursor"], keyboard), Is.True);
            Assert.That(HasControlFrom(actions["Pause"], keyboard), Is.True);
            Assert.That(actions["Look"].controls, Is.Empty);
            Assert.That(actions["Pointer Click"].controls, Is.Empty);
        }

        [UnityTest]
        public IEnumerator MouseActionsRemainAvailableWithoutAKeyboard()
        {
            yield return new EnterPlayMode();

            mouse = InputSystem.AddDevice<Mouse>();
            controller = CreateController();
            InputActionMap actions = GetActions(controller);
            actions.devices = new InputDevice[] { mouse };
            InputSystem.Update();

            Assert.That(actions["Look"].controls, Is.Not.Empty);
            Assert.That(HasControlFrom(actions["Look"], mouse), Is.True);
            Assert.That(HasControlFrom(actions["Interact"], mouse), Is.True);
            Assert.That(HasControlFrom(actions["Pointer Click"], mouse), Is.True);
            Assert.That(actions["Move"].controls, Is.Empty);
            Assert.That(actions["Toggle Cursor"].controls, Is.Empty);
            Assert.That(actions["Pause"].controls, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DeviceRemovalClearsFocusAndActionsRecoverAfterReconnection()
        {
            yield return new EnterPlayMode();

            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            controller = CreateController();
            InputActionMap actions = GetActions(controller);
            RestrictActionsToTestDevices(actions);
            InputSystem.Update();

            TestInteractable interactable = CreateInteractable();
            SetFocusedInteractable(controller, interactable);
            Assert.That(controller.InteractionPrompt, Is.EqualTo(TestInteractable.DisplayPrompt));

            InputSystem.RemoveDevice(mouse);
            InputSystem.Update();

            Assert.That(controller.InteractionPrompt, Is.Empty);
            Assert.That(HasControlFrom(actions["Move"], keyboard), Is.True);
            Assert.That(HasControlFrom(actions["Interact"], keyboard), Is.True);
            Assert.That(actions["Look"].controls, Is.Empty);

            InputSystem.AddDevice(mouse);
            RestrictActionsToTestDevices(actions);
            InputSystem.Update();
            Assert.That(HasControlFrom(actions["Look"], mouse), Is.True);

            GameSession session = GameSession.Create(null, null);
            sessionObject = session.gameObject;
            session.PushNarrativeInputLock();
            SetFocusedInteractable(controller, interactable);

            InputSystem.RemoveDevice(mouse);

            Assert.That(controller.InteractionPrompt, Is.Empty);

            InputSystem.AddDevice(mouse);
            RestrictActionsToTestDevices(actions);
            InputSystem.Update();
            Assert.That(HasControlFrom(actions["Look"], mouse), Is.True);
            InvokeUpdate(controller);

            Assert.That(controller.InteractionPrompt, Is.Empty);
            Assert.That(interactable.InteractionCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TrySetPose_RestoresBodyAndViewAndClearsMovementState()
        {
            yield return new EnterPlayMode();

            controller = CreateController();
            CharacterController character = controller.GetComponent<CharacterController>();
            Camera playerCamera = controller.PlayerCamera;
            var position = new Vector3(12.5f, 3.25f, -8.75f);
            Quaternion expectedBodyRotation = Quaternion.Euler(14f, 127f, -9f);
            var nonUnitBodyRotation = new Quaternion(
                expectedBodyRotation.x * 4f,
                expectedBodyRotation.y * 4f,
                expectedBodyRotation.z * 4f,
                expectedBodyRotation.w * 4f);

            SetPrivateField(controller, "verticalVelocity", -19f);
            SetPrivateField(controller, "sprintToggled", true);
            SetPrivateProperty(controller, "IsSprinting", true);
            SetPrivateProperty(
                controller,
                "LastCollisionFlags",
                CollisionFlags.Below | CollisionFlags.Sides);

            Assert.That(
                controller.TrySetPose(
                    position,
                    nonUnitBodyRotation,
                    120f,
                    out string error),
                Is.True,
                error);

            Assert.That(character.enabled, Is.True);
            Assert.That(Vector3.Distance(controller.transform.position, position), Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(controller.transform.rotation, expectedBodyRotation),
                Is.LessThan(0.001f));
            Assert.That(controller.ViewPitch, Is.EqualTo(82f));
            Assert.That(
                Quaternion.Angle(
                    playerCamera.transform.localRotation,
                    Quaternion.Euler(82f, 0f, 0f)),
                Is.LessThan(0.001f));
            Assert.That(GetPrivateField<float>(controller, "verticalVelocity"), Is.Zero);
            Assert.That(GetPrivateField<bool>(controller, "sprintToggled"), Is.False);
            Assert.That(controller.IsSprinting, Is.False);
            Assert.That(controller.LastCollisionFlags, Is.EqualTo(CollisionFlags.None));

            character.enabled = false;
            Assert.That(
                controller.TrySetPose(
                    Vector3.zero,
                    Quaternion.identity,
                    -120f,
                    out error),
                Is.True,
                error);
            Assert.That(character.enabled, Is.False);
            Assert.That(controller.ViewPitch, Is.EqualTo(-82f));
        }

        [UnityTest]
        public IEnumerator TrySetPose_RejectsInvalidPoseWithoutChangingThePlayer()
        {
            yield return new EnterPlayMode();

            controller = CreateController();
            CharacterController character = controller.GetComponent<CharacterController>();
            controller.transform.SetPositionAndRotation(
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(0f, 45f, 0f));
            Vector3 originalPosition = controller.transform.position;
            Quaternion originalRotation = controller.transform.rotation;

            Assert.That(
                controller.TrySetPose(
                    new Vector3(float.NaN, 0f, 0f),
                    Quaternion.identity,
                    0f,
                    out string error),
                Is.False);

            Assert.That(error, Does.Contain("position"));
            Assert.That(character.enabled, Is.True);
            Assert.That(controller.transform.position, Is.EqualTo(originalPosition));
            Assert.That(controller.transform.rotation, Is.EqualTo(originalRotation));

            Assert.That(
                controller.TrySetPose(
                    Vector3.zero,
                    new Quaternion(0f, 0f, 0f, 0f),
                    0f,
                    out error),
                Is.False);
            Assert.That(error, Does.Contain("rotation"));
            Assert.That(character.enabled, Is.True);
            Assert.That(controller.transform.position, Is.EqualTo(originalPosition));
            Assert.That(controller.transform.rotation, Is.EqualTo(originalRotation));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (playerObject != null)
            {
                playerObject.SetActive(false);
                Object.Destroy(playerObject);
            }

            if (interactableObject != null)
                Object.Destroy(interactableObject);
            if (sessionObject != null)
                Object.Destroy(sessionObject);

            if (mouse != null && mouse.added)
                InputSystem.RemoveDevice(mouse);
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);

            if (EditorApplication.isPlaying)
            {
                yield return null;
                yield return new ExitPlayMode();
            }

            playerObject = null;
            interactableObject = null;
            sessionObject = null;
            controller = null;
            keyboard = null;
            mouse = null;
        }

        private FirstPersonController CreateController()
        {
            playerObject = new GameObject("First Person Input Test");
            playerObject.SetActive(false);
            playerObject.AddComponent<CharacterController>();

            var cameraObject = new GameObject("Camera", typeof(Camera));
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.GetComponent<Camera>().enabled = false;

            FirstPersonController result = playerObject.AddComponent<FirstPersonController>();
            playerObject.SetActive(true);
            return result;
        }

        private TestInteractable CreateInteractable()
        {
            interactableObject = new GameObject("Input Test Interactable");
            return interactableObject.AddComponent<TestInteractable>();
        }

        private static InputActionMap GetActions(FirstPersonController target)
        {
            FieldInfo field = typeof(FirstPersonController).GetField(
                "inputActions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var actions = field.GetValue(target) as InputActionMap;
            Assert.That(actions, Is.Not.Null);
            Assert.That(actions.enabled, Is.True);
            return actions;
        }

        private static bool HasControlFrom(InputAction action, InputDevice device)
        {
            foreach (InputControl control in action.controls)
            {
                if (control.device == device)
                    return true;
            }

            return false;
        }

        private void RestrictActionsToTestDevices(InputActionMap actions)
        {
            // InputActionMap removes a restricted device from this list when
            // it disconnects, so the test reapplies its isolated device scope
            // after reconnecting the same synthetic mouse.
            actions.devices = new InputDevice[] { keyboard, mouse };
        }

        private static void SetFocusedInteractable(
            FirstPersonController target,
            IInteractable interactable)
        {
            MethodInfo method = typeof(FirstPersonController).GetMethod(
                "SetFocusedInteractable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, new object[] { interactable });
        }

        private static void InvokeUpdate(FirstPersonController target)
        {
            MethodInfo method = typeof(FirstPersonController).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

        private static void SetPrivateField<T>(
            FirstPersonController target,
            string fieldName,
            T value)
        {
            FieldInfo field = typeof(FirstPersonController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(
            FirstPersonController target,
            string fieldName)
        {
            FieldInfo field = typeof(FirstPersonController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateProperty<T>(
            FirstPersonController target,
            string propertyName,
            T value)
        {
            PropertyInfo property = typeof(FirstPersonController).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            property.SetValue(target, value);
        }

        private static FirstPersonController.RebindableAction RebindableActionWithId(string id)
        {
            foreach (FirstPersonController.RebindableAction entry in
                FirstPersonController.RebindableActions)
            {
                if (entry.Id == id)
                    return entry;
            }

            Assert.Fail($"Missing rebindable action '{id}'.");
            return default;
        }
    }

    public sealed class TestInteractable : MonoBehaviour, IInteractable
    {
        public const string DisplayPrompt = "TEST INTERACTION";

        public int InteractionCount { get; private set; }
        public string Prompt => DisplayPrompt;

        public void Interact()
        {
            InteractionCount++;
        }
    }
}

using System.Collections;
using FlagFootball.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace FlagFootball.Tests.PlayMode
{
    public sealed class TouchJoystickTests
    {
        private Touchscreen screen;
        private PlayerMovement player;
        private TouchJoystick joystick;
        private RectTransform pad;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private InputSettings.BackgroundBehavior backgroundInput;
        private float previousCaptureDelta;

        [UnitySetUp]
        public IEnumerator LoadTouchScene()
        {
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            backgroundInput = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            screen = InputSystem.AddDevice<Touchscreen>();
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            player = Object.FindFirstObjectByType<PlayerMovement>();
            joystick = Object.FindFirstObjectByType<TouchJoystick>();
            Assert.That(joystick, Is.Not.Null);
            joystick.SendMessage("OnApplicationFocus", true);
            joystick.SendMessage("OnApplicationPause", false);
            pad = GameObject.Find("MovementJoystick").GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
        }

        private Vector2 Point(Vector2 offset) => RectTransformUtility.WorldToScreenPoint(null,
            pad.TransformPoint(offset * pad.rect.width * 0.5f));

        private IEnumerator Touch(int id, TouchPhase phase, Vector2 offset)
        {
            InputSystem.QueueStateEvent(screen, new TouchState
            {
                touchId = id, phase = phase, position = Point(offset)
            });
            yield return null;
        }

        private void AssertStopped(Vector3 position) =>
            Assert.That(Vector3.Distance(player.transform.position, position), Is.LessThan(0.0001f));

        [UnityTest]
        public IEnumerator ActualTouchPath_ControlsAnalogSpeedOverdragDirectionAndRelease()
        {
            var before = player.transform.position;
            yield return Touch(1, TouchPhase.Began, Vector2.right * 0.25f);
            Assert.That(player.transform.position.x - before.x,
                Is.EqualTo(1.25f * Time.deltaTime).Within(0.0002f));
            foreach (var magnitude in new[] { 0.5f, 1f, 3f })
            {
                before = player.transform.position;
                yield return Touch(1, TouchPhase.Moved, Vector2.right * magnitude);
                Assert.That(player.transform.position.x - before.x,
                    Is.EqualTo(5f * Mathf.Min(1f, magnitude) * Time.deltaTime).Within(0.0002f));
            }
            before = player.transform.position;
            yield return Touch(1, TouchPhase.Moved, Vector2.up * 3f);
            Assert.That(player.transform.position.z, Is.GreaterThan(before.z));
            Assert.That(player.transform.position.x, Is.EqualTo(before.x).Within(0.0001f));
            Assert.That(Vector3.Dot(player.transform.forward, Vector3.forward), Is.GreaterThan(0.999f));
            before = player.transform.position;
            yield return Touch(1, TouchPhase.Ended, Vector2.up * 3f);
            AssertStopped(before);
            yield return null;
            AssertStopped(before);
        }

        [UnityTest]
        public IEnumerator PublicReset_StopsHeldMovementAndRequiresFreshTouch()
        {
            var start = player.transform.position;
            var handle = GameObject.Find("JoystickHandle").GetComponent<RectTransform>();
            yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
            Assert.That(player.transform.position.x, Is.GreaterThan(start.x));
            Assert.That(handle.anchoredPosition.sqrMagnitude, Is.GreaterThan(0f));
            var stopped = player.transform.position;
            Assert.That(Mathf.Abs(stopped.x), Is.LessThan(1f)); // Interior: no clamp can conceal stale input.
            Assert.That(Mathf.Abs(stopped.z), Is.LessThan(1f));

            joystick.ResetInput();
            Assert.That(handle.anchoredPosition, Is.EqualTo(Vector2.zero)); // Synchronous visual neutralization.
            yield return null; // The device may still hold the original Began state.
            AssertStopped(stopped);
            yield return Touch(1, TouchPhase.Moved, Vector2.up);
            for (var frame = 0; frame < 3; frame++)
            {
                yield return null;
                AssertStopped(stopped);
                Assert.That(handle.anchoredPosition, Is.EqualTo(Vector2.zero));
            }
            yield return Touch(1, TouchPhase.Ended, Vector2.up);
            AssertStopped(stopped);
            yield return null;
            AssertStopped(stopped);

            yield return Touch(1, TouchPhase.Began, Vector2.left * 0.5f);
            Assert.That(player.transform.position.x, Is.LessThan(stopped.x));
            Assert.That(handle.anchoredPosition.sqrMagnitude, Is.GreaterThan(0f));
            stopped = player.transform.position;
            yield return Touch(1, TouchPhase.Ended, Vector2.left * 0.5f);
            AssertStopped(stopped);
        }

        [UnityTest]
        public IEnumerator PublicReset_RepeatedAndDisabledResetsDoNotReviveHeldTouch()
        {
            joystick.ResetInput();
            joystick.ResetInput();
            var start = player.transform.position;
            yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
            Assert.That(player.transform.position.x, Is.GreaterThan(start.x));
            var stopped = player.transform.position;
            Assert.That(Mathf.Abs(stopped.x), Is.LessThan(1f));
            Assert.That(Mathf.Abs(stopped.z), Is.LessThan(1f));

            joystick.ResetInput();
            joystick.ResetInput();
            joystick.enabled = false;
            joystick.ResetInput();
            joystick.ResetInput();
            yield return Touch(1, TouchPhase.Moved, Vector2.up);
            AssertStopped(stopped);
            joystick.enabled = true;
            yield return null;
            AssertStopped(stopped);
            yield return Touch(1, TouchPhase.Moved, Vector2.left);
            AssertStopped(stopped);
            yield return Touch(1, TouchPhase.Ended, Vector2.left);
            AssertStopped(stopped);
            joystick.ResetInput();
            joystick.ResetInput();
            yield return null;
            AssertStopped(stopped);

            yield return Touch(1, TouchPhase.Began, Vector2.up * 0.5f);
            Assert.That(player.transform.position.z, Is.GreaterThan(stopped.z));
            stopped = player.transform.position;
            yield return Touch(1, TouchPhase.Ended, Vector2.up * 0.5f);
            AssertStopped(stopped);
        }

        [UnityTest]
        public IEnumerator OtherFinger_CannotStealOrAutomaticallyInheritTheGesture()
        {
            yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
            var before = player.transform.position;
            yield return Touch(2, TouchPhase.Began, Vector2.up * 0.5f);
            Assert.That(player.transform.position.x, Is.GreaterThan(before.x));
            Assert.That(player.transform.position.z, Is.EqualTo(before.z).Within(0.0001f));
            yield return Touch(2, TouchPhase.Moved, Vector2.up * 3f);
            before = player.transform.position;
            yield return Touch(1, TouchPhase.Ended, Vector2.right * 0.5f);
            AssertStopped(before);
            yield return Touch(2, TouchPhase.Moved, Vector2.up);
            AssertStopped(before);
            yield return Touch(2, TouchPhase.Ended, Vector2.up);
            yield return Touch(2, TouchPhase.Began, Vector2.up * 0.5f);
            Assert.That(player.transform.position.z, Is.GreaterThan(before.z));
        }

        [UnityTest]
        public IEnumerator TouchCancellation_StopsWithoutRetainingInput()
        {
            yield return Touch(1, TouchPhase.Began, Vector2.right);
            var before = player.transform.position;
            yield return Touch(1, TouchPhase.Canceled, Vector2.right);
            AssertStopped(before);
            yield return null;
            AssertStopped(before);
        }

        [UnityTest]
        public IEnumerator OutsideTouch_DoesNotActivateWhenDraggedIntoThePad()
        {
            var before = player.transform.position;
            yield return Touch(1, TouchPhase.Began, Vector2.right * 3f);
            yield return Touch(1, TouchPhase.Moved, Vector2.right * 0.5f);
            AssertStopped(before);
        }

        [UnityTest]
        public IEnumerator FocusPauseAndDisable_RequireFreshTouchesAfterReturn()
        {
            foreach (var interruption in new[] { "Focus", "Pause", "Disable" })
            {
                yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
                var before = player.transform.position;
                if (interruption == "Focus") joystick.SendMessage("OnApplicationFocus", false);
                else if (interruption == "Pause") joystick.SendMessage("OnApplicationPause", true);
                else
                {
                    joystick.enabled = false;
                    joystick.enabled = true; // Re-enable while the device still holds the original Began state.
                }
                yield return null;
                AssertStopped(before);
                if (interruption == "Focus") joystick.SendMessage("OnApplicationFocus", true);
                else if (interruption == "Pause") joystick.SendMessage("OnApplicationPause", false);
                yield return Touch(1, TouchPhase.Moved, Vector2.up);
                AssertStopped(before);
                yield return Touch(1, TouchPhase.Ended, Vector2.up);
                yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
                Assert.That(player.transform.position.x, Is.GreaterThan(before.x), interruption);
                yield return Touch(1, TouchPhase.Ended, Vector2.right * 0.5f);
            }
        }

        [UnityTest]
        public IEnumerator FocusReturningBeforeUnpause_DoesNotEnableHeldTouch()
        {
            joystick.SendMessage("OnApplicationFocus", false);
            joystick.SendMessage("OnApplicationPause", true);
            joystick.SendMessage("OnApplicationFocus", true);
            var before = player.transform.position;
            yield return Touch(1, TouchPhase.Began, Vector2.right);
            AssertStopped(before);
            joystick.SendMessage("OnApplicationPause", false);
            yield return Touch(1, TouchPhase.Moved, Vector2.right);
            AssertStopped(before);
            yield return Touch(1, TouchPhase.Ended, Vector2.right);
            yield return Touch(1, TouchPhase.Began, Vector2.right);
            Assert.That(player.transform.position.x, Is.GreaterThan(before.x));
        }

        private void AssertIntegratedFraming()
        {
            var view = Camera.main;
            foreach (var visual in player.GetComponentsInChildren<Renderer>())
            for (var corner = 0; corner < 8; corner++)
            {
                var bounds = visual.bounds;
                var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                var projected = view.WorldToViewportPoint(point);
                Assert.That(projected.z, Is.InRange(view.nearClipPlane, view.farClipPlane));
                Assert.That(projected.x, Is.InRange(-0.0001f, 1.0001f));
                Assert.That(projected.y, Is.InRange(-0.0001f, 1.0001f));
            }
            var field = GameObject.Find("Ground").GetComponent<Renderer>().bounds;
            for (var x = 0; x <= 1; x++)
            for (var y = 0; y <= 1; y++)
            {
                var ray = view.ViewportPointToRay(new Vector3(x, y, 0f));
                var ground = ray.GetPoint((field.center.y - ray.origin.y) / ray.direction.y);
                Assert.That(ground.x, Is.InRange(field.min.x - 0.002f, field.max.x + 0.002f));
                Assert.That(ground.z, Is.InRange(field.min.z - 0.002f, field.max.z + 0.002f));
            }
        }

        [UnityTest]
        public IEnumerator IntegratedTouch_MovesFacesAndDrivesCameraThroughTurnAndRelease()
        {
            var view = Camera.main;
            var start = view.transform.position;
            var rotation = view.transform.rotation;
            yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
            for (var frame = 0; frame < 24; frame++) { yield return null; AssertIntegratedFraming(); }
            Assert.That(player.transform.position.x, Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(player.transform.forward, Vector3.right), Is.GreaterThan(0.999f));
            Assert.That(view.transform.position.x, Is.GreaterThan(start.x).And.LessThan(player.transform.position.x));
            var beforeTurn = player.transform.position;
            yield return Touch(1, TouchPhase.Moved, Vector2.up * 0.5f);
            for (var frame = 0; frame < 24; frame++) { yield return null; AssertIntegratedFraming(); }
            Assert.That(player.transform.position.z, Is.GreaterThan(beforeTurn.z));
            Assert.That(player.transform.position.x, Is.EqualTo(beforeTurn.x).Within(0.0001f));
            Assert.That(Vector3.Dot(player.transform.forward, Vector3.forward), Is.GreaterThan(0.999f));
            var stopped = player.transform.position;
            yield return Touch(1, TouchPhase.Ended, Vector2.up * 0.5f);
            for (var frame = 0; frame < 60; frame++) { yield return null; AssertStopped(stopped); }
            var centered = view.WorldToViewportPoint(stopped);
            Assert.That(centered.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(centered.y, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(Quaternion.Angle(rotation, view.transform.rotation), Is.LessThan(0.001f));
            AssertIntegratedFraming();
        }

        [UnityTest]
        public IEnumerator IntegratedTouch_BoundsFramingAndFreshInputSurviveInterruption()
        {
            var rotation = Camera.main.transform.rotation;
            yield return Touch(1, TouchPhase.Began, Vector2.right);
            for (var frame = 0; frame < 120; frame++) { yield return null; AssertIntegratedFraming(); }
            Assert.That(player.transform.position.x, Is.EqualTo(player.MaximumPosition.x).Within(0.001f));
            yield return Touch(1, TouchPhase.Moved, Vector2.up);
            for (var frame = 0; frame < 120; frame++) { yield return null; AssertIntegratedFraming(); }
            Assert.That(player.transform.position.z, Is.EqualTo(player.MaximumPosition.y).Within(0.001f));
            yield return Touch(1, TouchPhase.Moved, -Vector2.one * 0.5f);
            for (var frame = 0; frame < 12; frame++) yield return null;
            var stopped = player.transform.position; // Leave the corner so a stale input cannot hide against a bound.
            joystick.SendMessage("OnApplicationFocus", false);
            joystick.SendMessage("OnApplicationPause", true);
            yield return null;
            AssertStopped(stopped);
            joystick.SendMessage("OnApplicationFocus", true);
            yield return Touch(1, TouchPhase.Moved, Vector2.left);
            AssertStopped(stopped);
            joystick.SendMessage("OnApplicationPause", false);
            yield return Touch(1, TouchPhase.Moved, Vector2.left);
            AssertStopped(stopped);
            yield return Touch(1, TouchPhase.Ended, Vector2.left);
            yield return Touch(1, TouchPhase.Began, Vector2.left * 0.5f);
            Assert.That(player.transform.position.x, Is.LessThan(stopped.x));
            Assert.That(Quaternion.Angle(rotation, Camera.main.transform.rotation), Is.LessThan(0.001f));
            AssertIntegratedFraming();
        }

        [UnityTest]
        public IEnumerator JoystickLayout_StaysInBottomLeftSafeArea()
        {
            var corners = new Vector3[4];
            pad.GetWorldCorners(corners);
            var minimum = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var maximum = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            Assert.That(minimum.x, Is.GreaterThanOrEqualTo(Screen.safeArea.xMin));
            Assert.That(minimum.y, Is.GreaterThanOrEqualTo(Screen.safeArea.yMin));
            Assert.That(maximum.x, Is.LessThan(Screen.safeArea.center.x));
            Assert.That(maximum.y, Is.LessThan(Screen.safeArea.center.y));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadTouchScene()
        {
            Time.captureDeltaTime = previousCaptureDelta;
            var scene = SceneManager.GetSceneByName("Bootstrap");
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("TouchTestCleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            InputSystem.settings.backgroundBehavior = backgroundInput;
        }
    }
}

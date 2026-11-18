using System.Collections;
using FlagFootball.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace FlagFootball.Tests.PlayMode
{
    public sealed class TouchJoystickTests
    {
        private SceneBootstrap attempt;
        private Touchscreen screen;
        private PlayerMovement player;
        private TouchJoystick joystick;
        private RectTransform pad;
        private Transform receiver, ball;
        private Vector3 receiverPosition, ballPosition, ballScale;
        private Quaternion receiverRotation, ballRotation;
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
            attempt = Object.FindFirstObjectByType<SceneBootstrap>();
            player = attempt.Quarterback;
            receiver = attempt.Receiver;
            ball = attempt.Ball;
            receiverPosition = receiver.position;
            receiverRotation = receiver.rotation;
            ballPosition = ball.localPosition;
            ballRotation = ball.localRotation;
            ballScale = ball.localScale;
            Assert.That(attempt.IsActive, Is.False);
            Assert.That(player.MovementAllowed, Is.False);
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

        private void AssertPreparedActors()
        {
            Assert.That(receiver.position, Is.EqualTo(receiverPosition));
            Assert.That(receiver.rotation, Is.EqualTo(receiverRotation));
            AssertHeldBall();
        }

        private void AssertHeldBall()
        {
            Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
            Assert.That(ball.parent, Is.EqualTo(player.transform));
            Assert.That(ball.localPosition, Is.EqualTo(ballPosition));
            Assert.That(ball.localRotation, Is.EqualTo(ballRotation));
            Assert.That(ball.localScale, Is.EqualTo(ballScale));
            Assert.That(Vector3.Distance(ball.position, player.transform.TransformPoint(ballPosition)),
                Is.LessThan(0.0001f));
        }

        private void DisturbPreparedActors()
        {
            receiver.SetPositionAndRotation(new Vector3(-2f, 1f, -1f), Quaternion.Euler(15f, 70f, 5f));
            ball.SetParent(null);
            ball.SetPositionAndRotation(new Vector3(3f, 2f, -1f), Quaternion.Euler(35f, 15f, 90f));
            ball.localScale = Vector3.one * 2f;
            Assert.That(attempt.BallOwner, Is.Null, "A detached ball is not QB-held.");
        }

        private void QueueAction(int id, TouchPhase phase, RectTransform region)
        {
            InputSystem.QueueStateEvent(screen, new TouchState
            {
                touchId = id, phase = phase,
                position = RectTransformUtility.WorldToScreenPoint(null, region.position)
            });
        }

        [UnityTest]
        public IEnumerator Actions_StartRestartRestoreAndRequireFreshMovement()
        {
            var controls = Object.FindFirstObjectByType<AttemptControls>();
            var initial = player.transform.position;
            var rotation = player.transform.rotation;
            QueueAction(1, TouchPhase.Began, controls.StartRegion);
            yield return null;
            Assert.That(attempt.IsActive, Is.True);
            Assert.That(controls.StartRegion.GetComponentInChildren<Text>().text, Does.Contain("Active"));
            AssertStopped(initial);
            yield return Touch(2, TouchPhase.Began, Vector2.right * 0.5f);
            Assert.That(player.transform.position.x, Is.GreaterThan(initial.x));
            var moving = player.transform.position;
            QueueAction(3, TouchPhase.Began, controls.StartRegion); // Duplicate must preserve held movement.
            yield return null;
            Assert.That(player.transform.position.x, Is.GreaterThan(moving.x));
            var cameraDriver = Camera.main.GetComponent<RunningCamera>();
            cameraDriver.enabled = false; // Disturbed detached geometry is intentionally not camera-feasible.
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            DisturbPreparedActors();
            QueueAction(4, TouchPhase.Began, controls.RestartRegion);
            yield return null;
            Assert.That(attempt.IsActive, Is.False);
            AssertStopped(initial);
            Assert.That(player.transform.rotation, Is.EqualTo(rotation));
            AssertPreparedActors();
            Assert.That(controls.StartRegion.GetComponentInChildren<Text>().text, Does.Contain("Preparation"));
            QueueAction(1, TouchPhase.Moved, controls.StartRegion);
            yield return Touch(2, TouchPhase.Moved, Vector2.up);
            Assert.That(attempt.IsActive, Is.False);
            AssertStopped(initial);
            QueueAction(1, TouchPhase.Ended, controls.StartRegion);
            yield return Touch(2, TouchPhase.Ended, Vector2.up);
            Assert.That(attempt.IsActive, Is.False);
            QueueAction(5, TouchPhase.Began, controls.RestartRegion); // Duplicate prepared reset repairs damage.
            DisturbPreparedActors();
            yield return null;
            AssertPreparedActors();
            cameraDriver.enabled = true;
            QueueAction(6, TouchPhase.Began, controls.StartRegion);
            yield return null;
            Assert.That(attempt.IsActive, Is.True);
            AssertStopped(initial);
            yield return Touch(7, TouchPhase.Began, Vector2.left * 0.5f);
            Assert.That(player.transform.position.x, Is.LessThan(initial.x));
            Assert.That(receiver.position.z, Is.GreaterThan(receiverPosition.z));
            AssertHeldBall();
            QueueAction(4, TouchPhase.Moved, controls.RestartRegion);
            yield return null;
            Assert.That(attempt.IsActive, Is.True, "Old Restart pointer cannot reset a new attempt.");
            QueueAction(4, TouchPhase.Ended, controls.RestartRegion);
            yield return null;
            Assert.That(attempt.IsActive, Is.True);
        }

        [UnityTest]
        public IEnumerator Actions_ResetRejectsPendingSameFrameAndOutsideGestures()
        {
            var controls = Object.FindFirstObjectByType<AttemptControls>();
            foreach (var restartFirst in new[] { false, true })
            {
                QueueAction(1, TouchPhase.Began, restartFirst ? controls.RestartRegion : controls.StartRegion);
                QueueAction(2, TouchPhase.Began, restartFirst ? controls.StartRegion : controls.RestartRegion);
                yield return null;
                Assert.That(attempt.IsActive, Is.False, "Restart wins the whole input batch.");
                QueueAction(1, TouchPhase.Ended, controls.StartRegion);
                QueueAction(2, TouchPhase.Ended, controls.RestartRegion);
                yield return null;
                Assert.That(attempt.IsActive, Is.False, "Release never performs an action.");
            }
            QueueAction(3, TouchPhase.Began, controls.StartRegion);
            Assert.That(attempt.RestartAttempt(), Is.True); // Queued before reset, dispatched afterward.
            yield return null;
            Assert.That(attempt.IsActive, Is.False);
            QueueAction(3, TouchPhase.Ended, controls.StartRegion);
            yield return null;
            QueueAction(3, TouchPhase.Began, controls.StartRegion);
            InputSystem.Update(); // Began is pending polling when an external reset closes the epoch.
            Assert.That(attempt.RestartAttempt(), Is.True);
            Assert.That(attempt.RestartAttempt(), Is.True);
            yield return null;
            Assert.That(attempt.IsActive, Is.False);
            QueueAction(3, TouchPhase.Moved, controls.StartRegion);
            yield return null;
            QueueAction(3, TouchPhase.Ended, controls.StartRegion);
            yield return null;
            Assert.That(attempt.IsActive, Is.False);
            yield return Touch(4, TouchPhase.Began, Vector2.zero); // Starts outside action regions.
            QueueAction(4, TouchPhase.Moved, controls.StartRegion);
            yield return null;
            QueueAction(4, TouchPhase.Ended, controls.StartRegion);
            yield return null;
            Assert.That(attempt.IsActive, Is.False);
            QueueAction(5, TouchPhase.Began, controls.StartRegion);
            yield return null;
            Assert.That(attempt.IsActive, Is.True);
        }

        [UnityTest]
        public IEnumerator Actions_InterruptionsNeutralizePendingGesturesAndExternalState()
        {
            var controls = Object.FindFirstObjectByType<AttemptControls>();
            foreach (var interruption in new[] { "Focus", "Pause", "Disable" })
            {
                QueueAction(1, TouchPhase.Began, controls.StartRegion);
                InputSystem.Update();
                if (interruption == "Disable") controls.enabled = false;
                else controls.SendMessage("OnApplication" + interruption, interruption == "Pause");
                yield return null;
                QueueAction(3, TouchPhase.Began, controls.StartRegion); // Fresh input while unavailable is also inert.
                yield return null;
                Assert.That(attempt.IsActive, Is.False);
                if (interruption == "Disable") controls.enabled = true;
                else controls.SendMessage("OnApplication" + interruption, interruption == "Focus");
                yield return null;
                Assert.That(attempt.IsActive, Is.False, interruption);
                QueueAction(1, TouchPhase.Ended, controls.StartRegion);
                QueueAction(3, TouchPhase.Ended, controls.StartRegion);
                yield return null;
                Assert.That(attempt.IsActive, Is.False);
                QueueAction(2, TouchPhase.Began, controls.StartRegion);
                yield return null;
                Assert.That(attempt.IsActive, Is.True, interruption);
                Assert.That(attempt.RestartAttempt(), Is.True);
                Assert.That(controls.StartRegion.GetComponentInChildren<Text>().text, Does.Contain("Preparation"));
                QueueAction(2, TouchPhase.Ended, controls.StartRegion);
                yield return null;
            }
            Assert.That(attempt.StartAttempt(), Is.True);
            Assert.That(controls.StartRegion.GetComponentInChildren<Text>().text, Does.Contain("Active"));
            System.Action failure = () => throw new System.InvalidOperationException("action reset failure");
            attempt.Resetting += failure;
            Assert.Throws<System.InvalidOperationException>(() => attempt.RestartAttempt());
            Assert.That(controls.StartRegion.GetComponentInChildren<Text>().text, Does.Not.Contain("Active"));
            Assert.That(attempt.StartAttempt(), Is.False);
            attempt.Resetting -= failure;
            Assert.That(attempt.RestartAttempt(), Is.True);
        }

        [UnityTest]
        public IEnumerator Actions_LayoutIsReadableDisjointAndSafeAcrossLandscapeAspects()
        {
            var controls = Object.FindFirstObjectByType<AttemptControls>();
            joystick.enabled = false;
            var canvas = controls.GetComponentInParent<Canvas>();
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace; // Batch Screen remains 4:3; exercise real rects in reference pixels.
            var canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.localScale = Vector3.one;
            var safeArea = pad.parent.GetComponent<RectTransform>();
            foreach (var targetWidth in new[] { 1920, 2400, 2700 })
            {
                canvasRect.sizeDelta = new Vector2(targetWidth, 1080f);
                canvasRect.position = new Vector3(targetWidth * 0.5f, 540f);
                var area = new Rect(64f, 32f, targetWidth - 128f, 1016f);
                safeArea.anchorMin = new Vector2(area.xMin / targetWidth, area.yMin / 1080f);
                safeArea.anchorMax = new Vector2(area.xMax / targetWidth, area.yMax / 1080f);
                Camera.main.aspect = targetWidth / 1080f;
                yield return null;
                Canvas.ForceUpdateCanvases();
                var regions = new[] { controls.StartRegion, controls.RestartRegion, pad };
                var rectangles = new Rect[3];
                for (var i = 0; i < regions.Length; i++)
                {
                    var corners = new Vector3[4];
                    regions[i].GetWorldCorners(corners);
                    rectangles[i] = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                    Assert.That(area.Contains(rectangles[i].min), Is.True);
                    Assert.That(area.Contains(rectangles[i].max), Is.True);
                    Assert.That(rectangles[i].height / 1080f, Is.GreaterThanOrEqualTo(0.09f));
                    if (i == 2) continue;
                    var label = regions[i].GetComponentInChildren<Text>();
                    Assert.That(label.text, Does.StartWith(i == 0 ? "Start" : "Restart"));
                    Assert.That(label.font && label.enabled && label.color.a == 1f, Is.True);
                    Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(28));
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(regions[i].rect.width));
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(regions[i].rect.height));
                    foreach (var actor in new[] { player.transform, receiver, ball })
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var bounds = actor.GetComponent<Renderer>().bounds;
                        var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                            (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                            (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                        var viewport = Camera.main.WorldToViewportPoint(point);
                        Assert.That(rectangles[i].Contains(new Vector2(viewport.x * targetWidth,
                            viewport.y * 1080f)), Is.False);
                    }
                }
                Assert.That(rectangles[0].Overlaps(rectangles[1]) || rectangles[0].Overlaps(rectangles[2]) ||
                    rectangles[1].Overlaps(rectangles[2]), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Attempt_PreparationRejectsDirectAndHeldTouchUntilFreshStart()
        {
            var initial = player.transform.position;
            Assert.That(Mathf.Abs(initial.x), Is.LessThan(1f));
            Assert.That(Mathf.Abs(initial.z), Is.LessThan(1f));
            joystick.enabled = false;
            player.SetMoveInput(Vector2.up);
            yield return null;
            AssertStopped(initial);
            player.enabled = false;
            player.SetMoveInput(Vector2.right);
            player.enabled = true;
            yield return null;
            AssertStopped(initial);
            joystick.enabled = true;
            yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
            AssertStopped(initial);
            AssertPreparedActors();
            var initialBall = ball.position;
            player.enabled = false;
            player.SetMoveInput(Vector2.right);
            Assert.That(attempt.StartAttempt(), Is.True);
            player.enabled = true;
            yield return null;
            AssertStopped(initial);
            yield return Touch(1, TouchPhase.Moved, Vector2.up);
            AssertStopped(initial);
            yield return Touch(1, TouchPhase.Ended, Vector2.up);
            AssertStopped(initial);
            yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
            Assert.That(player.transform.position.x, Is.GreaterThan(initial.x));
            var moving = player.transform.position;
            Assert.That(attempt.StartAttempt(), Is.False);
            yield return null;
            Assert.That(player.transform.position.x, Is.GreaterThan(moving.x));
            Assert.That(ball.position, Is.Not.EqualTo(initialBall));
            Assert.That(receiver.position.z, Is.GreaterThan(receiverPosition.z));
            AssertHeldBall(); // QB input and the autonomous route progress independently.
        }

        [UnityTest]
        public IEnumerator Attempt_RestartBlocksCallbacksRestoresPoseAndRejectsOldGesture()
        {
            Assert.That(attempt.StartAttempt(), Is.True);
            var initial = player.transform.position;
            var rotation = player.transform.rotation;
            var callbacks = 0;
            attempt.Resetting += () =>
            {
                callbacks++;
                Assert.That(attempt.IsActive, Is.False);
                Assert.That(player.MovementAllowed, Is.False);
                Assert.That(GameObject.Find("JoystickHandle").GetComponent<RectTransform>().anchoredPosition,
                    Is.EqualTo(Vector2.zero));
                Assert.That(player.transform.position, Is.Not.EqualTo(initial));
                Assert.That(receiver.position, Is.Not.EqualTo(receiverPosition));
                Assert.That(attempt.BallOwner, Is.Null); // Callbacks precede all pose/possession restoration.
                Assert.That(attempt.StartAttempt(), Is.False);
                Assert.That(attempt.RestartAttempt(), Is.False);
                player.SetMoveInput(Vector2.up);
            };
            yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
            player.transform.SetPositionAndRotation(new Vector3(1f, initial.y, 1f), Quaternion.Euler(0f, 90f, 0f));
            DisturbPreparedActors();
            Assert.That(attempt.RestartAttempt(), Is.True);
            AssertStopped(initial);
            AssertPreparedActors();
            Assert.That(player.transform.rotation, Is.EqualTo(rotation));
            player.transform.SetPositionAndRotation(new Vector3(-1f, initial.y, -1f), Quaternion.Euler(0f, 180f, 0f));
            DisturbPreparedActors();
            Assert.That(attempt.RestartAttempt(), Is.True); // Prepared restart still repairs disturbance.
            AssertStopped(initial);
            Assert.That(player.transform.rotation, Is.EqualTo(rotation));
            Assert.That(callbacks, Is.EqualTo(2));
            AssertPreparedActors();
            Assert.That(attempt.IsActive, Is.False);
            Assert.That(attempt.StartAttempt(), Is.True);
            yield return null;
            AssertStopped(initial); // Interior, so no field clamp can hide stale movement.
            yield return Touch(1, TouchPhase.Moved, Vector2.up);
            AssertStopped(initial);
            yield return Touch(1, TouchPhase.Ended, Vector2.up);
            AssertStopped(initial);
            yield return Touch(1, TouchPhase.Began, Vector2.left * 0.5f);
            Assert.That(player.transform.position.x, Is.LessThan(initial.x));
            Assert.That(receiver.position.z, Is.GreaterThan(receiverPosition.z));
            AssertHeldBall();
        }

        [UnityTest]
        public IEnumerator Attempt_CallbackFailureRestoresPreparationAndRequiresSuccessfulReset()
        {
            var initial = player.transform.position;
            var rotation = player.transform.rotation;
            Assert.That(attempt.StartAttempt(), Is.True);
            System.Action failure = () => throw new System.InvalidOperationException("reset failure");
            attempt.Resetting += failure;
            player.transform.SetPositionAndRotation(new Vector3(1f, initial.y, 1f), Quaternion.Euler(0f, 90f, 0f));
            DisturbPreparedActors();
            Assert.Throws<System.InvalidOperationException>(() => attempt.RestartAttempt());
            AssertStopped(initial);
            AssertPreparedActors();
            Assert.That(player.transform.rotation, Is.EqualTo(rotation));
            Assert.That(attempt.IsActive, Is.False);
            Assert.That(player.MovementAllowed, Is.False);
            Assert.That(attempt.StartAttempt(), Is.False);
            DisturbPreparedActors();
            Assert.Throws<System.InvalidOperationException>(() => attempt.RestartAttempt());
            AssertPreparedActors();
            Assert.That(attempt.StartAttempt(), Is.False);
            player.SetMoveInput(Vector2.right);
            yield return null;
            AssertStopped(initial);
            AssertPreparedActors(); // A late route Update cannot undo failure restoration.
            attempt.Resetting -= failure;
            DisturbPreparedActors();
            Assert.That(attempt.RestartAttempt(), Is.True);
            AssertPreparedActors();
            Assert.That(attempt.StartAttempt(), Is.True);
            yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
            Assert.That(player.transform.position.x, Is.GreaterThan(initial.x));
        }

        [UnityTest]
        public IEnumerator Route_PublicAttemptsAndTouch_ProgressCutWaitAndRepeatIndependently()
        {
            for (var run = 0; run < 3; run++)
            {
                for (var frame = 0; frame < 3; frame++) yield return null;
                AssertPreparedActors();
                Assert.That(attempt.StartAttempt(), Is.True);
                var qbStart = player.transform.position;
                yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
                for (var frame = 0; frame < 29; frame++) yield return null;
                Assert.That(receiver.position, Is.EqualTo(receiverPosition + Vector3.forward * 1.5f)
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(Vector3.Dot(receiver.forward, Vector3.forward), Is.GreaterThan(0.999f));
                Assert.That(player.transform.position.x, Is.GreaterThan(qbStart.x));
                AssertHeldBall();
                if (run == 0) // Interrupt an actual moving route, then repeat the same start.
                {
                    var paused = receiver.position;
                    attempt.enabled = false;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    Assert.That(receiver.position, Is.EqualTo(paused));
                    attempt.enabled = true;
                    Assert.That(attempt.RestartAttempt(), Is.True);
                    AssertPreparedActors();
                    yield return Touch(1, TouchPhase.Ended, Vector2.right * 0.5f);
                    continue;
                }
                Assert.That(attempt.StartAttempt(), Is.False);
                var qbTurn = player.transform.position;
                yield return Touch(1, TouchPhase.Moved, Vector2.up * 0.5f);
                for (var frame = 0; frame < 30; frame++) yield return null;
                Assert.That(receiver.position.x, Is.GreaterThan(receiverPosition.x));
                Assert.That(receiver.position.z, Is.EqualTo(4f).Within(0.0001f));
                Assert.That(Vector3.Dot(receiver.forward, Vector3.right), Is.GreaterThan(0.999f));
                Assert.That(player.transform.position.x, Is.EqualTo(qbTurn.x).Within(0.0001f));
                Assert.That(player.transform.position.z, Is.GreaterThan(qbTurn.z));
                for (var frame = 0; frame < 65; frame++) yield return null;
                var endpoint = new Vector3(4.5f, receiverPosition.y, 4f);
                Assert.That(Vector3.Distance(receiver.position, endpoint), Is.LessThan(0.0001f));
                Assert.That(attempt.IsActive, Is.True);
                Assert.That(attempt.StartAttempt(), Is.False);
                var qbWaiting = player.transform.position;
                yield return Touch(1, TouchPhase.Moved, Vector2.left * 0.5f);
                for (var frame = 0; frame < 10; frame++) yield return null;
                Assert.That(receiver.position, Is.EqualTo(endpoint));
                Assert.That(Vector3.Dot(receiver.forward, Vector3.right), Is.GreaterThan(0.999f));
                Assert.That(player.transform.position.x, Is.LessThan(qbWaiting.x));
                AssertHeldBall();
                Assert.That(attempt.RestartAttempt(), Is.True);
                AssertPreparedActors();
                AssertStopped(qbStart);
                yield return Touch(1, TouchPhase.Ended, Vector2.left * 0.5f);
            }
        }

        [UnityTest]
        public IEnumerator ActualTouchPath_ControlsAnalogSpeedOverdragDirectionAndRelease()
        {
            Assert.That(attempt.StartAttempt(), Is.True);
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
            Assert.That(attempt.StartAttempt(), Is.True);
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
            Assert.That(attempt.StartAttempt(), Is.True);
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
            Assert.That(attempt.StartAttempt(), Is.True);
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
            Assert.That(attempt.StartAttempt(), Is.True);
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
            Assert.That(attempt.StartAttempt(), Is.True);
            var before = player.transform.position;
            yield return Touch(1, TouchPhase.Began, Vector2.right * 3f);
            yield return Touch(1, TouchPhase.Moved, Vector2.right * 0.5f);
            AssertStopped(before);
        }

        [UnityTest]
        public IEnumerator FocusPauseAndDisable_RequireFreshTouchesAfterReturn()
        {
            Assert.That(attempt.StartAttempt(), Is.True);
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
            Assert.That(attempt.StartAttempt(), Is.True);
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
            Assert.That(attempt.StartAttempt(), Is.True);
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
            Assert.That(attempt.StartAttempt(), Is.True);
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

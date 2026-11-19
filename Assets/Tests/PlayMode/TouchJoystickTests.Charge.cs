using System.Collections;
using FlagFootball.Core;
using FlagFootball.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace FlagFootball.Tests.PlayMode
{
    public sealed partial class TouchJoystickTests
    {
        private PassIndicator PassView => Object.FindFirstObjectByType<PassIndicator>();
        private Image ReceiverBadge => PassView.transform.Find("ReceiverMarker").GetComponent<Image>();
        private Vector2 PassPoint => RectTransformUtility.WorldToScreenPoint(null, PassView.Button.position);
        private Vector2 ShiftReference(Vector2 point, Vector2 displacement)
        {
            var canvas = PassView.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, point, null, out var local);
            return RectTransformUtility.WorldToScreenPoint(null, canvas.TransformPoint(local + displacement));
        }
        private IEnumerator PassTouch(int id, TouchPhase phase, Vector2 position, Touchscreen device = null)
        {
            InputSystem.QueueStateEvent(device ?? screen, new TouchState { touchId = id, phase = phase, position = position });
            yield return null;
        }
        private void AssertBorder(bool visible)
        {
            var border = PassView.transform.Find("ChargeBorder");
            Assert.That(border, Is.Not.Null, "Charge-only edge feedback must exist.");
            Assert.That(border.gameObject.activeInHierarchy, Is.EqualTo(visible));
            if (!visible) return;
            var images = border.GetComponentsInChildren<Image>();
            Assert.That(images.Length, Is.EqualTo(8));
            var color = ReceiverBadge.color;
            Assert.That(PassView.Button.GetComponent<Image>().color, Is.EqualTo(new Color(0.08f, 0.12f, 0.18f, 0.95f)));
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            foreach (var image in images)
            {
                Assert.That(new Color(image.color.r, image.color.g, image.color.b), Is.EqualTo(color));
                Assert.That(image.color.a, Is.InRange(0.01f, 0.3f));
                Assert.That(image.raycastTarget, Is.False);
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform, center), Is.False);
            }
            foreach (var edge in new[] { new Vector2(1f, center.y), new Vector2(Screen.width - 1f, center.y),
                new Vector2(center.x, 1f), new Vector2(center.x, Screen.height - 1f) })
                Assert.That(System.Array.Exists(images, image =>
                    RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform, edge)), Is.True);
        }
        private IEnumerator ChargeReady()
        {
            Assert.That(attempt.StartAttempt(), Is.True);
            yield return null;
            Assert.That(PassView.IsAvailable, Is.True);
            Assert.That(attempt.ReceptionProbability, Is.Null);
            AssertBorder(false);
        }

        [UnityTest]
        public IEnumerator Charge_RedOrangeGreenRelease_UsesCurrentQualityAndActualFlight()
        {
            foreach (var frames in new[] { 0, 30, 65, 95, 135, 200 })
            {
                attempt.RestartAttempt();
                yield return ChargeReady();
                var point = PassPoint;
                yield return PassTouch(2, TouchPhase.Began, point);
                for (var i = 0; i < frames; i++) yield return null;
                AssertBorder(true);
                var color = ReceiverBadge.color;
                Assert.That(color.b, Is.Zero);
                if (frames == 0) Assert.That(color, Is.EqualTo(Color.red));
                if (frames == 30) Assert.That(color.g, Is.EqualTo(0.5f).Within(0.04f));
                if (frames == 65 || frames == 200) Assert.That(color, Is.EqualTo(Color.green));
                var phase = ((frames + 1) * Time.deltaTime) % 2.2f;
                var raw = phase <= 1f ? phase : phase <= 1.2f ? 1f : 2.2f - phase;
                var quality = Mathf.RoundToInt(raw * 100f) / 100f;
                var ground = GameObject.Find("Ground").GetComponent<Renderer>().bounds;
                var velocity = attempt.ReceiverVelocity;
                System.Numerics.Vector3 Core(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
                var expected = new PassFlight(Core(ball.position), Core(receiver.position),
                    new System.Numerics.Vector2(velocity.x, velocity.z), quality,
                    new System.Numerics.Vector2(ground.min.x, ground.min.z), new System.Numerics.Vector2(ground.max.x, ground.max.z),
                    groundHeight: ground.max.y + ball.GetComponent<Renderer>().bounds.extents.y);
                yield return PassTouch(2, TouchPhase.Ended, point);
                Assert.That(attempt.BallOwner, Is.Null);
                Assert.That(attempt.ReceptionProbability.Value, Is.EqualTo(quality).Within(0.000001f));
                AssertBorder(false);
                var predicted = expected.Sample(Time.deltaTime);
                Assert.That(Vector3.Distance(ball.position, new Vector3(predicted.X, predicted.Y, predicted.Z)), Is.LessThan(0.002f));
                yield return PassTouch(2, TouchPhase.Ended, point);
                Assert.That(ReceiverBadge.GetComponentInChildren<Text>().text, Is.EqualTo($"L1\n{Mathf.RoundToInt(quality * 100f)}%"));
                Assert.That(attempt.LaunchPass(receiver, 1f), Is.False);
                for (var i = 0; i < 240 && attempt.IsActive; i++) yield return null;
                Assert.That(attempt.State, Is.EqualTo(AttemptState.Incomplete));
                Assert.That(attempt.ReceptionProbability.Value, Is.EqualTo(quality).Within(0.000001f));
            }
        }

        [UnityTest]
        public IEnumerator Charge_MovingIndicatorAndJoystick_IgnoreOtherFingersAndDeviceWithSameTouchId()
        {
            yield return ChargeReady();
            yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
            var start = player.transform.position;
            var marker = PassView.transform.Find("ReceiverMarker");
            var markerStart = RectTransformUtility.WorldToScreenPoint(null, marker.position);
            var point = PassPoint;
            yield return PassTouch(2, TouchPhase.Began, point);
            var other = InputSystem.AddDevice<Touchscreen>();
            try
            {
                yield return PassTouch(2, TouchPhase.Began, point, other);
                yield return PassTouch(3, TouchPhase.Began, point);
                yield return PassTouch(2, TouchPhase.Ended, point, other);
                yield return PassTouch(3, TouchPhase.Ended, point);
                for (var frame = 0; frame < 40; frame++) yield return null;
                Assert.That(player.transform.position.x, Is.GreaterThan(start.x));
                Assert.That(Vector2.Distance(PassPoint, point), Is.LessThan(0.01f));
                Assert.That(Vector2.Distance(markerStart, RectTransformUtility.WorldToScreenPoint(null, marker.position)), Is.GreaterThan(10f));
                AssertBorder(true);
                Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
                Assert.That(PassView.Halo.rect.width, Is.LessThan(150f));
                yield return Touch(1, TouchPhase.Moved, Vector2.up * 0.5f);
                var greenWindows = 0;
                var wasGreen = false;
                for (var frame = 0; frame < 180; frame++)
                {
                    yield return null;
                    Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
                    AssertBorder(true);
                    var green = ReceiverBadge.color == Color.green;
                    if (green && !wasGreen) greenWindows++;
                    wasGreen = green;
                    var corners = new Vector3[4];
                    PassView.Halo.GetWorldCorners(corners);
                    foreach (var corner in corners)
                        Assert.That(Screen.safeArea.Contains(RectTransformUtility.WorldToScreenPoint(null, corner)), Is.True);
                }
                Assert.That(greenWindows, Is.GreaterThanOrEqualTo(2));
                yield return PassTouch(2, TouchPhase.Ended, point);
                Assert.That(attempt.BallOwner, Is.Null);
                var releasedAt = player.transform.position;
                for (var frame = 0; frame < 6; frame++) yield return null;
                Assert.That(player.transform.position.z, Is.GreaterThan(releasedAt.z));
                yield return Touch(1, TouchPhase.Ended, Vector2.up * 0.5f);
            }
            finally { InputSystem.RemoveDevice(other); }
        }

        [UnityTest]
        public IEnumerator Charge_OutsidePressAndDownSwipe_CannotThrowWithoutAnotherFreshPress()
        {
            yield return ChargeReady();
            var point = PassPoint;
            foreach (var outside in new[] { Vector2.right * 70f, new Vector2(40f, 40f) })
            {
                point = PassPoint;
                yield return PassTouch(2, TouchPhase.Began, ShiftReference(point, outside));
                yield return PassTouch(2, TouchPhase.Moved, point);
                yield return PassTouch(2, TouchPhase.Ended, point);
                Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
                Assert.That(PassView.Halo.rect.width, Is.EqualTo(192f));
            }
            point = PassPoint;
            yield return PassTouch(3, TouchPhase.Began, point);
            for (var frame = 0; frame < 20; frame++) yield return null;
            yield return PassTouch(3, TouchPhase.Moved, ShiftReference(point, Vector2.right * 300f));
            Assert.That(PassView.Halo.rect.width, Is.LessThan(190f));
            yield return PassTouch(3, TouchPhase.Moved, ShiftReference(point, Vector2.down * 90f));
            Assert.That(PassView.Halo.rect.width, Is.EqualTo(192f));
            AssertBorder(false);
            yield return PassTouch(3, TouchPhase.Ended, point);
            Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
            Assert.That(attempt.IsActive, Is.True);
            Assert.That(Vector3.Distance(receiver.position, receiverPosition), Is.GreaterThan(0.1f));
            point = PassPoint;
            yield return PassTouch(4, TouchPhase.Began, point);
            yield return PassTouch(4, TouchPhase.Ended, ShiftReference(point, Vector2.down * 90f));
            Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
            yield return PassTouch(5, TouchPhase.Began, PassPoint);
            yield return PassTouch(5, TouchPhase.Ended, PassPoint);
            Assert.That(attempt.BallOwner, Is.Null);
        }

        [UnityTest]
        public IEnumerator Charge_VisualMarkerAndScreenEdge_CannotStartPass()
        {
            yield return ChargeReady();
            var marker = PassView.transform.Find("ReceiverMarker").GetComponent<RectTransform>();
            Assert.That(marker.GetComponentInChildren<Text>().text, Is.EqualTo("L1"));
            foreach (var graphic in marker.GetComponentsInChildren<Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
            var markerPoint = RectTransformUtility.WorldToScreenPoint(null, marker.position);
            foreach (var point in new[] { markerPoint, ShiftReference(markerPoint, Vector2.right * 70f), new Vector2(1f, Screen.height * 0.5f) })
            {
                yield return PassTouch(2, TouchPhase.Began, point);
                yield return PassTouch(2, TouchPhase.Moved, PassPoint);
                yield return PassTouch(2, TouchPhase.Ended, PassPoint);
                Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
                Assert.That(PassView.Halo.rect.width, Is.EqualTo(192f));
                AssertBorder(false);
            }
            yield return PassTouch(3, TouchPhase.Began, PassPoint);
            markerPoint = RectTransformUtility.WorldToScreenPoint(null, marker.position);
            yield return PassTouch(4, TouchPhase.Began, ShiftReference(markerPoint, Vector2.right * 70f));
            yield return PassTouch(4, TouchPhase.Ended, markerPoint);
            AssertBorder(true);
            Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
            yield return PassTouch(3, TouchPhase.Ended, PassPoint);
            Assert.That(attempt.BallOwner, Is.Null);
        }

        [UnityTest]
        public IEnumerator Charge_RestartAndInvalidPossessionPhases_RejectHeldMovedReleasedInput()
        {
            yield return PassTouch(2, TouchPhase.Began, PassPoint);
            yield return ChargeReady();
            yield return PassTouch(2, TouchPhase.Moved, PassPoint);
            yield return PassTouch(2, TouchPhase.Ended, PassPoint);
            Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
            yield return PassTouch(3, TouchPhase.Began, PassPoint);
            for (var frame = 0; frame < 15; frame++) yield return null;
            Assert.That(PassView.Halo.rect.width, Is.LessThan(192f));
            attempt.RestartAttempt();
            AssertBorder(false);
            Assert.That(attempt.StartAttempt(), Is.True);
            PassView.SetProgress(0f); // Expose the same-frame restart/start window without resetting input in the fixture.
            yield return PassTouch(3, TouchPhase.Moved, PassPoint);
            yield return PassTouch(3, TouchPhase.Ended, PassPoint);
            Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
            ball.SetParent(receiver, true); // Non-QB possession gate, not catch/control-transfer implementation.
            yield return PassTouch(4, TouchPhase.Began, PassPoint);
            yield return PassTouch(4, TouchPhase.Ended, PassPoint);
            Assert.That(ball.parent, Is.EqualTo(receiver));
            attempt.RestartAttempt();
            yield return ChargeReady();
            Assert.That(attempt.LaunchPass(receiver, 1f), Is.True);
            yield return PassTouch(5, TouchPhase.Began, PassPoint);
            yield return PassTouch(5, TouchPhase.Ended, PassPoint);
            for (var frame = 0; frame < 240 && attempt.IsActive; frame++) yield return null;
            yield return PassTouch(6, TouchPhase.Began, PassPoint);
            yield return PassTouch(6, TouchPhase.Ended, PassPoint);
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Incomplete));
            attempt.RestartAttempt();
            yield return ChargeReady();
            yield return PassTouch(3, TouchPhase.Began, PassPoint); // Reused ID, fresh touch birth.
            yield return PassTouch(3, TouchPhase.Ended, PassPoint);
            Assert.That(attempt.BallOwner, Is.Null);
        }

        [UnityTest]
        public IEnumerator Charge_InputInterruptionsCancelOrLoseOwner_RequireFreshTouchWithoutAbortingAttempt()
        {
            foreach (var interruption in new[] { "focus", "pause", "disable", "cancel", "device" })
            {
                attempt.RestartAttempt();
                yield return ChargeReady();
                var input = Object.FindFirstObjectByType<ChargedPassInput>();
                var point = PassPoint;
                yield return PassTouch(2, TouchPhase.Began, point);
                for (var frame = 0; frame < 10; frame++) yield return null;
                AssertBorder(true);
                if (interruption == "focus") input.SendMessage("OnApplicationFocus", false);
                if (interruption == "pause") { input.SendMessage("OnApplicationPause", true); input.SendMessage("OnApplicationFocus", false); }
                if (interruption == "disable") input.enabled = false;
                if (interruption == "focus" || interruption == "pause" || interruption == "disable")
                {
                    yield return PassTouch(8, TouchPhase.Began, point);
                    yield return PassTouch(8, TouchPhase.Ended, point);
                    Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
                }
                if (interruption == "cancel") yield return PassTouch(2, TouchPhase.Canceled, point);
                if (interruption == "device") { InputSystem.RemoveDevice(screen); yield return null; screen = InputSystem.AddDevice<Touchscreen>(); }
                AssertBorder(false);
                if (interruption == "pause")
                {
                    input.SendMessage("OnApplicationFocus", true);
                    yield return PassTouch(2, TouchPhase.Moved, point);
                    Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
                    input.SendMessage("OnApplicationPause", false);
                }
                if (interruption == "focus") input.SendMessage("OnApplicationFocus", true);
                if (interruption == "disable") input.enabled = true;
                yield return PassTouch(2, TouchPhase.Moved, point);
                yield return PassTouch(2, TouchPhase.Ended, point);
                Assert.That(attempt.BallOwner, Is.EqualTo(player.transform));
                Assert.That(attempt.IsActive, Is.True);
                Assert.That(PassView.Halo.rect.width, Is.EqualTo(192f));
                yield return PassTouch(9, TouchPhase.Began, PassPoint);
                yield return PassTouch(9, TouchPhase.Ended, PassPoint);
                Assert.That(attempt.BallOwner, Is.Null, interruption);
            }
        }
    }
}

using System.Collections;
using FlagFootball.Core;
using FlagFootball.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace FlagFootball.Tests.PlayMode
{
    public sealed partial class TouchJoystickTests
    {
        private IEnumerator CatchWithActualTouch(bool held)
        {
            Reception.enabled = true;
            Assert.That(attempt.StartAttempt(), Is.True);
            for (var frame = 0; frame < 132; frame++) yield return null;
            yield return Touch(1, TouchPhase.Began, held ? Vector2.right * 0.4f : Vector2.zero);
            var point = PassPoint;
            yield return PassTouch(2, TouchPhase.Began, point);
            for (var frame = 0; frame < 120 && ReceiverBadge.GetComponentInChildren<Text>().text != "L1\n100%"; frame++) yield return null;
            Assert.That(ReceiverBadge.GetComponentInChildren<Text>().text, Is.EqualTo("L1\n100%"));
            yield return PassTouch(2, TouchPhase.Ended, point);
            var qbBefore = player.transform.position;
            var receiverBefore = receiver.position;
            for (var frame = 0; frame < 240 && attempt.IsActive && ball.parent != receiver; frame++)
            {
                qbBefore = player.transform.position;
                receiverBefore = receiver.position;
                yield return null;
            }
            Assert.That(attempt.BallOwner, Is.EqualTo(receiver));
            Assert.That(receiver.GetComponent<PlayerMovement>(), Is.Not.Null);
            Assert.That(receiver.GetComponent<PlayerMovement>().MovementAllowed, Is.True);
            Assert.That(player.MovementAllowed, Is.False);
            if (held)
            {
                AssertStopped(qbBefore);
                Assert.That(receiver.position.x, Is.GreaterThan(receiverBefore.x), "The existing finger must move the receiver on the handoff frame.");
            }
        }

        [UnityTest]
        public IEnumerator Handoff_HeldGestureMovesOnlyReceiverThroughDirectionsAndOtherFingers()
        {
            yield return CatchWithActualTouch(true);
            var qb = player.transform.position;
            var before = receiver.position;
            yield return null;
            Assert.That(receiver.position.x - before.x, Is.EqualTo(2f * Time.deltaTime).Within(0.0001f));
            AssertStopped(qb);
            var other = InputSystem.AddDevice<Touchscreen>();
            try
            {
                yield return Touch(3, TouchPhase.Began, Vector2.left);
                before = receiver.position;
                yield return PassTouch(1, TouchPhase.Began, Point(Vector2.left), other);
                Assert.That(receiver.position.x, Is.GreaterThan(before.x), "Another device with the same touch id must not take ownership.");
                yield return PassTouch(1, TouchPhase.Ended, Point(Vector2.left), other);
                yield return Touch(3, TouchPhase.Ended, Vector2.left);
            }
            finally { InputSystem.RemoveDevice(other); }
            before = receiver.position;
            yield return Touch(1, TouchPhase.Moved, Vector2.left * 0.4f);
            Assert.That(receiver.position.x, Is.LessThan(before.x));
            Camera.main.GetComponent<RunningCamera>().enabled = false;
            Camera.main.transform.rotation = Quaternion.Euler(60f, 90f, 0f);
            before = receiver.position;
            yield return Touch(1, TouchPhase.Moved, Vector2.up * 0.4f);
            Assert.That(receiver.position.x, Is.GreaterThan(before.x));
            Assert.That(receiver.position.z, Is.EqualTo(before.z).Within(0.0001f));
            AssertStopped(qb);
            Assert.That(ball.parent, Is.EqualTo(receiver));
            Assert.That(attempt.LaunchPass(receiver, 1f), Is.False);
            before = receiver.position;
            yield return Touch(1, TouchPhase.Ended, Vector2.up * 0.4f);
            yield return null;
            Assert.That(receiver.position, Is.EqualTo(before));
            AssertStopped(qb);
        }

        [UnityTest]
        public IEnumerator Handoff_NeutralCatchEdgesAndRestart_RestoreFreshQbControl()
        {
            yield return CatchWithActualTouch(false);
            var mover = receiver.GetComponent<PlayerMovement>();
            Assert.That(mover.MinimumPosition, Is.EqualTo(player.MinimumPosition));
            Assert.That(mover.MaximumPosition, Is.EqualTo(player.MaximumPosition));
            var qb = player.transform.position;
            var before = receiver.position;
            for (var frame = 0; frame < 10; frame++) yield return null;
            Assert.That(receiver.position, Is.EqualTo(before));
            yield return Touch(1, TouchPhase.Moved, Vector2.zero);
            Time.captureDeltaTime = 2f;
            foreach (var direction in new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down,
                Vector2.one, new Vector2(-1f, 1f), -Vector2.one, new Vector2(1f, -1f) })
            {
                yield return Touch(1, TouchPhase.Moved, direction);
                for (var frame = 0; frame < 4; frame++) yield return null;
                var minimum = mover.MinimumPosition;
                var maximum = mover.MaximumPosition;
                if (direction.x != 0f) Assert.That(receiver.position.x, Is.EqualTo(direction.x > 0f ? maximum.x : minimum.x).Within(0.0001f));
                if (direction.y != 0f) Assert.That(receiver.position.z, Is.EqualTo(direction.y > 0f ? maximum.y : minimum.y).Within(0.0001f));
                Assert.That(receiver.position.y, Is.EqualTo(receiverPosition.y));
                Assert.That(attempt.IsActive, Is.True);
                Assert.That(attempt.BallOwner, Is.EqualTo(receiver));
                AssertStopped(qb);
            }
            Time.captureDeltaTime = 1f / 60f;
            Assert.That(attempt.RestartAttempt(), Is.True);
            Assert.That(mover.MovementAllowed, Is.False);
            AssertPreparedActors();
            var prepared = player.transform.position;
            attempt.StartAttempt();
            yield return Touch(1, TouchPhase.Moved, Vector2.right);
            AssertStopped(prepared);
            yield return Touch(1, TouchPhase.Ended, Vector2.right);
            yield return Touch(4, TouchPhase.Began, Vector2.right * 0.4f);
            Assert.That(player.transform.position.x, Is.GreaterThan(prepared.x));
            Assert.That(mover.MovementAllowed, Is.False);
            Assert.That(receiver.position.x, Is.EqualTo(receiverPosition.x));
            AssertHeldBall();
        }

        [UnityTest]
        public IEnumerator Handoff_FailedCarryReset_BlocksBothActorsUntilSuccessfulRetry()
        {
            yield return CatchWithActualTouch(true);
            var mover = receiver.GetComponent<PlayerMovement>();
            System.Action fail = () =>
            {
                Assert.That(mover.MovementAllowed || player.MovementAllowed, Is.False);
                throw new System.InvalidOperationException("Carry reset probe");
            };
            attempt.Resetting += fail;
            try { Assert.Throws<System.InvalidOperationException>(() => attempt.RestartAttempt()); }
            finally { attempt.Resetting -= fail; }
            AssertPreparedActors();
            Assert.That(attempt.StartAttempt(), Is.False);
            var qb = player.transform.position;
            yield return Touch(1, TouchPhase.Moved, Vector2.right);
            AssertStopped(qb);
            AssertPreparedActors();
            Assert.That(attempt.RestartAttempt(), Is.True);
            Assert.That(attempt.StartAttempt(), Is.True);
            yield return Touch(1, TouchPhase.Ended, Vector2.right);
            yield return Touch(4, TouchPhase.Began, Vector2.right * 0.4f);
            Assert.That(player.transform.position.x, Is.GreaterThan(qb.x));
            Assert.That(mover.MovementAllowed, Is.False);
            AssertHeldBall();
        }

        [UnityTest]
        public IEnumerator Handoff_FocusPauseAndDisable_NeutralizeReceiverAndRequireFreshTouch()
        {
            yield return CatchWithActualTouch(true);
            var qb = player.transform.position;
            yield return Touch(1, TouchPhase.Ended, Vector2.right * 0.4f);
            foreach (var interruption in new[] { "Focus", "Pause", "Disable" })
            {
                yield return Touch(1, TouchPhase.Began, Vector2.right * 0.4f);
                var before = receiver.position;
                if (interruption == "Focus") joystick.SendMessage("OnApplicationFocus", false);
                else if (interruption == "Pause") joystick.SendMessage("OnApplicationPause", true);
                else { joystick.enabled = false; joystick.enabled = true; }
                yield return null;
                Assert.That(receiver.position, Is.EqualTo(before));
                if (interruption == "Focus") joystick.SendMessage("OnApplicationFocus", true);
                if (interruption == "Pause") joystick.SendMessage("OnApplicationPause", false);
                yield return Touch(1, TouchPhase.Moved, Vector2.up);
                Assert.That(receiver.position, Is.EqualTo(before));
                yield return Touch(1, TouchPhase.Ended, Vector2.up);
                yield return Touch(1, TouchPhase.Began, Vector2.right * 0.4f);
                Assert.That(receiver.position.x, Is.GreaterThan(before.x), interruption);
                yield return Touch(1, TouchPhase.Ended, Vector2.right * 0.4f);
                AssertStopped(qb);
                Assert.That(attempt.BallOwner, Is.EqualTo(receiver));
            }
        }
    }
}

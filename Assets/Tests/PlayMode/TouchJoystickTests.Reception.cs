using System.Collections;
using FlagFootball.Core;
using FlagFootball.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace FlagFootball.Tests.PlayMode
{
    public sealed partial class TouchJoystickTests
    {
        private PassReception Reception => Object.FindFirstObjectByType<PassReception>();

        [UnityTest]
        public IEnumerator Reception_MovingAndWaitingTargets_AttachStopAndRestartWithoutDroppingGesture()
        {
            foreach (var wait in new[] { 0, 132 })
            {
                attempt.RestartAttempt();
                Reception.enabled = true;
                var preparedQb = player.transform.position;
                attempt.StartAttempt();
                for (var frame = 0; frame < wait; frame++) yield return null;
                yield return Touch(1, TouchPhase.Began, Vector2.right * 0.5f);
                var scale = ball.lossyScale;
                Assert.That(attempt.LaunchPass(receiver, 1f), Is.True);
                for (var frame = 0; frame < 240 && attempt.IsActive && ball.parent != receiver; frame++) yield return null;
                Assert.That(ball.parent, Is.EqualTo(receiver));
                Assert.That(attempt.BallOwner, Is.EqualTo(receiver));
                Assert.That(Reception.Result, Is.EqualTo(ReceptionResult.Caught));
                Assert.That(attempt.IsActive, Is.True);
                Assert.That(attempt.FlightOutcome, Is.EqualTo(FlightEnd.None));
                Assert.That(player.MovementAllowed, Is.False);
                Assert.That(attempt.ReceiverVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(Vector3.Distance(ball.localPosition, ballPosition), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(ball.lossyScale, scale), Is.LessThan(0.0001f));
                Assert.That(attempt.LaunchPass(receiver, 0f), Is.False);
                Assert.That(attempt.ReceptionProbability.Value, Is.EqualTo(1f));
                var qb = player.transform.position;
                var target = receiver.position;
                for (var frame = 0; frame < 10; frame++) yield return null;
                AssertStopped(qb);
                Assert.That(receiver.position.x, Is.GreaterThan(target.x), "The held gesture now controls the receiver, not the route or QB.");
                Assert.That(receiver.position.z, Is.EqualTo(target.z));
                Assert.That(GameObject.Find("JoystickHandle").GetComponent<RectTransform>().anchoredPosition.magnitude, Is.GreaterThan(1f));
                if (wait == 0) Assert.That(receiver.position.z, Is.LessThan(4f), "The route must stop at catch, not finish its first leg.");
                Assert.That(attempt.RestartAttempt(), Is.True);
                AssertPreparedActors();
                Assert.That(Reception.Result, Is.EqualTo(ReceptionResult.Pending));
                Assert.That(attempt.ReceptionProbability, Is.Null);
                attempt.StartAttempt();
                yield return Touch(1, TouchPhase.Moved, Vector2.right * 0.5f);
                AssertStopped(preparedQb);
                yield return Touch(1, TouchPhase.Ended, Vector2.right * 0.5f);
            }
        }

        [UnityTest]
        public IEnumerator Reception_ZeroChanceFailsOnceAndRestartClearsFlightAndPossession()
        {
            Reception.enabled = true;
            attempt.StartAttempt();
            player.transform.position = receiver.position; // Controlled initial overlap, not production rescue movement.
            Assert.That(attempt.LaunchPass(receiver, 0f), Is.True);
            yield return null;
            Assert.That(Reception.Result, Is.EqualTo(ReceptionResult.Missed));
            Assert.That(attempt.BallOwner, Is.Null);
            Reception.enabled = false;
            Reception.enabled = true;
            Assert.That(Reception.Result, Is.EqualTo(ReceptionResult.Missed), "Disabling must not reset the latched trial.");
            for (var frame = 0; frame < 240 && attempt.IsActive; frame++) yield return null;
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Incomplete));
            Assert.That(Reception.Result, Is.EqualTo(ReceptionResult.Missed));
            Assert.That(ball.parent, Is.Null);
            attempt.RestartAttempt();
            AssertPreparedActors();
            Assert.That(Reception.Result, Is.EqualTo(ReceptionResult.Pending));
            attempt.StartAttempt();
            Assert.That(attempt.LaunchPass(receiver, 1f), Is.True);
            yield return null;
            Assert.That(ball.parent, Is.Null);
            attempt.RestartAttempt();
            for (var frame = 0; frame < 10; frame++) yield return null;
            AssertPreparedActors();
            Assert.That(attempt.FlightOutcome, Is.EqualTo(FlightEnd.None));
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Preparation));
        }

        [UnityTest]
        public IEnumerator Reception_CutStopAndTerminalEvents_AgreeAcrossSmallAndLargeFrames()
        {
            joystick.enabled = false;
            foreach (var setup in new[] { 0, 1, 2 })
            {
                Vector3? expectedPosition = null;
                bool? expectedCatch = null;
                FlightEnd? expectedEnd = null;
                foreach (var delta in new[] { 1f / 60f, 2f })
                {
                    Time.captureDeltaTime = 1f / 60f;
                    attempt.RestartAttempt();
                    Reception.enabled = true;
                    attempt.StartAttempt();
                    if (setup == 1)
                    {
                        player.SetMoveInput(Vector2.left);
                        for (var frame = 0; frame < 240 && player.transform.position.x > -8f; frame++) yield return null;
                        player.SetMoveInput(Vector2.zero);
                    }
                    else for (var frame = 0; frame < (setup == 0 ? 48 : 132); frame++) yield return null;
                    Assert.That(attempt.LaunchPass(receiver, 1f), Is.True);
                    Time.captureDeltaTime = delta;
                    for (var frame = 0; frame < 240 && attempt.IsActive && ball.parent != receiver; frame++) yield return null;
                    var caught = ball.parent == receiver;
                    Assert.That(caught || attempt.State == AttemptState.Incomplete, Is.True);
                    if (setup < 2)
                    {
                        Assert.That(caught, Is.False, "Route changes cannot be rescued by a fictitious receiver chord.");
                        Assert.That(Reception.Result, Is.EqualTo(ReceptionResult.Pending));
                        Assert.That(ball.parent, Is.Null);
                    }
                    else Assert.That(caught, Is.True);
                    if (setup == 0) Assert.That(attempt.FlightOutcome, Is.EqualTo(FlightEnd.Ground));
                    if (setup == 1) Assert.That(attempt.FlightOutcome, Is.EqualTo(FlightEnd.FieldExit));
                    if (expectedPosition.HasValue)
                    {
                        Assert.That(Vector3.Distance(receiver.position, expectedPosition.Value), Is.LessThan(0.0002f));
                        Assert.That(caught, Is.EqualTo(expectedCatch.Value));
                        Assert.That(attempt.FlightOutcome, Is.EqualTo(expectedEnd.Value));
                    }
                    expectedPosition = receiver.position;
                    expectedCatch = caught;
                    expectedEnd = attempt.FlightOutcome;
                }
            }
        }
    }
}

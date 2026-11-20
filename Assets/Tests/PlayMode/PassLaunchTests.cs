using System;
using System.Collections;
using System.Linq;
using FlagFootball.Core;
using FlagFootball.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FlagFootball.Tests.PlayMode
{
    public sealed class PassLaunchTests
    {
        private SceneBootstrap attempt;
        private Camera view;
        private Renderer ground;
        private float previousDelta;
        private Vector3 observedAim;
        private static System.Numerics.Vector3 Core(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
        private static Vector3 World(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        private static void Near(Vector3 a, Vector3 b) => Assert.That(Vector3.Distance(a, b), Is.LessThan(0.0002f));

        [UnitySetUp]
        public IEnumerator Prepare()
        {
            previousDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            attempt = Object.FindFirstObjectByType<SceneBootstrap>();
            Object.FindFirstObjectByType<PassReception>().enabled = false; // Keep flight/arc assertions independent of reception.
            Object.FindFirstObjectByType<TouchJoystick>().enabled = false;
            view = Camera.main;
            ground = GameObject.Find("Ground").GetComponent<Renderer>();
        }

        private PassFlight Expected(float quality)
        {
            var bounds = ground.bounds;
            var velocity = attempt.ReceiverVelocity;
            return new PassFlight(Core(attempt.Ball.position), Core(attempt.Receiver.position),
                new System.Numerics.Vector2(velocity.x, velocity.z), quality,
                new System.Numerics.Vector2(bounds.min.x, bounds.min.z),
                new System.Numerics.Vector2(bounds.max.x, bounds.max.z),
                groundHeight: bounds.max.y + attempt.Ball.GetComponent<Renderer>().bounds.extents.y);
        }

        private void AssertFrame()
        {
            foreach (var visual in attempt.Quarterback.GetComponentsInChildren<Renderer>().Concat(
                new[] { attempt.Receiver.GetComponent<Renderer>(), attempt.Ball.GetComponent<Renderer>() }))
            for (var c = 0; c < 8; c++)
            {
                var b = visual.bounds;
                var p = view.WorldToViewportPoint(new Vector3((c & 1) == 0 ? b.min.x : b.max.x,
                    (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z));
                Assert.That(p.x, Is.InRange(0.0399f, 0.9601f), visual.name);
                Assert.That(p.y, Is.InRange(0.3599f, 0.9601f), visual.name);
                Assert.That(p.z, Is.InRange(view.nearClipPlane, view.farClipPlane));
            }
            var anchor = view.WorldToViewportPoint(attempt.Receiver.position + Vector3.up * 1.2f);
            Assert.That(anchor.x, Is.InRange(0.04f + 96f / (1080f * view.aspect), 0.96f - 96f / (1080f * view.aspect)));
            Assert.That(anchor.y, Is.InRange(0.36f + 96f / 1080f, 0.96f - 96f / 1080f));
            Assert.That(Quaternion.Angle(view.transform.rotation, Quaternion.Euler(60f, 0f, 0f)), Is.LessThan(0.001f));
        }

        private IEnumerator ObserveFlight(PassFlight expected)
        {
            double elapsed = 0d;
            var previous = attempt.Ball.position;
            observedAim = previous;
            for (var frame = 0; frame < 600 && attempt.IsActive; frame++)
            {
                var before = elapsed;
                yield return null;
                elapsed += Time.deltaTime;
                var actual = attempt.Ball.position;
                Near(actual, World(expected.Sample(elapsed)));
                if (before < expected.AimTime && elapsed >= expected.AimTime)
                    observedAim = Vector3.LerpUnclamped(previous, actual, (float)((expected.AimTime - before) / Time.deltaTime));
                previous = actual;
                AssertFrame();
            }
            expected.Step(100f);
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Incomplete));
            Assert.That(attempt.FlightOutcome, Is.EqualTo(expected.End));
            Assert.That(attempt.Quarterback.MovementAllowed, Is.False);
            Near(attempt.ReceiverVelocity, Vector3.zero);
            Assert.That(Object.FindFirstObjectByType<AttemptControls>().StartRegion.GetComponent<Text>().text,
                Does.Contain("Incomplete"));
            if (attempt.FlightOutcome == FlightEnd.Ground)
                Assert.That(attempt.Ball.GetComponent<Renderer>().bounds.min.y, Is.EqualTo(ground.bounds.max.y).Within(0.00001f));
        }

        [UnityTest]
        public IEnumerator Launch_RejectsInvalidCallsAndSamplesCurrentReleaseWithoutHoming()
        {
            void Reject(Transform target, float quality)
            {
                var position = attempt.Ball.position;
                var parent = attempt.Ball.parent;
                Assert.That(attempt.LaunchPass(target, quality), Is.False);
                Near(attempt.Ball.position, position);
                Assert.That(attempt.Ball.parent, Is.EqualTo(parent));
            }
            Reject(attempt.Receiver, 1f);
            attempt.StartAttempt();
            foreach (var q in new[] { -1f, 2f, float.NaN, float.PositiveInfinity }) Reject(attempt.Receiver, q);
            Reject(null, 1f);
            Reject(attempt.Quarterback.transform, 1f);
            view.GetComponent<RunningCamera>().SetBallFramingTestInput(attempt.Ball.position, new Rect(0f, 0f, 1f, 1f));
            Reject(attempt.Receiver, 1f);
            attempt.RestartAttempt();
            attempt.StartAttempt();
            var initialBall = attempt.Ball.position;
            attempt.Quarterback.SetMoveInput(Vector2.right);
            for (var frame = 0; frame < 24; frame++) yield return null;
            Near(attempt.ReceiverVelocity, Vector3.forward * 3f);
            Assert.That(Vector3.Distance(initialBall, attempt.Ball.position), Is.GreaterThan(1f));
            var expected = Expected(1f);
            var scale = attempt.Ball.lossyScale;
            Assert.That(attempt.LaunchPass(attempt.Receiver, 1f), Is.True);
            Near(attempt.Ball.position, World(expected.Position));
            Near(attempt.Ball.lossyScale, scale);
            Assert.That(attempt.BallOwner, Is.Null);
            Reject(attempt.Receiver, 1f);
            attempt.Quarterback.SetMoveInput(Vector2.left);
            yield return ObserveFlight(expected);
            var qb = attempt.Quarterback.transform.position;
            var receiver = attempt.Receiver.position;
            var ball = attempt.Ball.position;
            Assert.That(attempt.StartAttempt(), Is.False);
            Reject(attempt.Receiver, 1f);
            attempt.Quarterback.SetMoveInput(Vector2.up);
            for (var frame = 0; frame < 30; frame++) yield return null;
            Near(attempt.Quarterback.transform.position, qb);
            Near(attempt.Receiver.position, receiver);
            Near(attempt.Ball.position, ball);
            Near(attempt.ReceiverVelocity, Vector3.zero);
        }

        [UnityTest]
        public IEnumerator QualityCutStopAndWaitingTarget_UseActualVisibleFixedFlights()
        {
            foreach (var releaseFrame in new[] { 48, 108, 132 })
            foreach (var quality in new[] { 0f, 0.5f, 1f })
            {
                attempt.RestartAttempt();
                attempt.StartAttempt();
                for (var frame = 0; frame < releaseFrame; frame++) yield return null;
                if (releaseFrame == 132) Near(attempt.ReceiverVelocity, Vector3.zero);
                var origin = attempt.Ball.position;
                var full = Expected(1f);
                var expected = Expected(quality);
                Assert.That(attempt.LaunchPass(attempt.Receiver, quality), Is.True);
                yield return ObserveFlight(expected);
                var actualRange = Vector2.Distance(new Vector2(origin.x, origin.z), new Vector2(observedAim.x, observedAim.z));
                var greenAim = World(full.Sample(full.AimTime));
                var greenRange = Vector2.Distance(new Vector2(origin.x, origin.z), new Vector2(greenAim.x, greenAim.z));
                Assert.That(actualRange, Is.EqualTo(greenRange * (0.5f + 0.5f * quality)).Within(0.0002f));
                if (quality == 1f && releaseFrame < 120)
                    Assert.That(Vector2.Distance(new Vector2(observedAim.x, observedAim.z),
                        new Vector2(attempt.Receiver.position.x, attempt.Receiver.position.z)), Is.GreaterThan(0.3f));
                if (quality == 1f && releaseFrame == 132)
                    Assert.That(Vector3.Distance(observedAim, attempt.Receiver.position), Is.LessThan(0.02f));
            }
        }

        [UnityTest]
        public IEnumerator LargeFrame_StopsRouteAtFirstBallEventRatherThanFrameEnd()
        {
            attempt.StartAttempt();
            var expected = Expected(1f);
            var receiverAtContact = attempt.Receiver.position + attempt.ReceiverVelocity * (float)expected.EndTime;
            Assert.That(expected.EndTime, Is.LessThan(1d), "Contact occurs before the first route cut.");
            Assert.That(attempt.LaunchPass(attempt.Receiver, 1f), Is.True);
            Time.captureDeltaTime = 2f;
            yield return ObserveFlight(expected);
            Near(attempt.Receiver.position, receiverAtContact);
        }

        [UnityTest]
        public IEnumerator MidflightRestartAndFailedReset_RestorePreparationWithoutStaleCompletion()
        {
            var preparedCamera = view.transform.position;
            var preparedSize = view.orthographicSize;
            var held = attempt.Ball.localPosition;
            attempt.StartAttempt();
            Assert.That(attempt.LaunchPass(attempt.Receiver, 1f), Is.True);
            yield return null;
            Action fail = () =>
            {
                Assert.That(attempt.LaunchPass(attempt.Receiver, 1f), Is.False);
                Assert.That(attempt.StartAttempt(), Is.False);
                throw new InvalidOperationException("Flight reset probe");
            };
            attempt.Resetting += fail;
            try { Assert.Throws<InvalidOperationException>(() => attempt.RestartAttempt()); }
            finally { attempt.Resetting -= fail; }
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Preparation));
            Assert.That(attempt.BallOwner, Is.EqualTo(attempt.Quarterback.transform));
            Near(attempt.Ball.localPosition, held);
            Near(view.transform.position, preparedCamera);
            Assert.That(view.orthographicSize, Is.EqualTo(preparedSize).Within(0.00001f));
            Assert.That(attempt.StartAttempt(), Is.False);
            Assert.That(attempt.RestartAttempt(), Is.True);
            attempt.StartAttempt();
            for (var frame = 0; frame < 150; frame++) yield return null;
            Assert.That(attempt.IsActive, Is.True, "Discarded old flight cannot complete the new attempt.");
            Assert.That(attempt.FlightOutcome, Is.EqualTo(FlightEnd.None));
            Assert.That(attempt.LaunchPass(attempt.Receiver, 1f), Is.True);
            yield return null;
            Assert.That(attempt.RestartAttempt(), Is.True);
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Preparation));
            Near(attempt.Ball.localPosition, held);
            attempt.StartAttempt();
            var expected = Expected(1f);
            Assert.That(attempt.LaunchPass(attempt.Receiver, 1f), Is.True);
            yield return ObserveFlight(expected);
        }

        [UnityTest]
        public IEnumerator ActualFlightsAcrossQbBoundsAndLandscapeAspects_StayFramedThroughGroundAndFieldExit()
        {
            var sawExit = false;
            foreach (var aspect in new[] { 16f / 9f, 20f / 9f, 2.5f })
            foreach (var destination in new[] { new Vector2(-8f, 0f), new Vector2(-8f, -8f), new Vector2(0f, -8f),
                new Vector2(8f, -8f), new Vector2(8f, 0f), new Vector2(8f, 8f), new Vector2(0f, 8f), new Vector2(-8f, 8f), Vector2.zero })
            {
                view.aspect = aspect;
                attempt.RestartAttempt();
                attempt.StartAttempt();
                for (var frame = 0; frame < 240; frame++)
                {
                    var p = attempt.Quarterback.transform.position;
                    var delta = destination - new Vector2(p.x, p.z);
                    if (delta.magnitude < 0.001f) break;
                    attempt.Quarterback.SetMoveInput(Vector2.ClampMagnitude(delta / (5f * Time.deltaTime), 1f));
                    yield return null;
                    AssertFrame();
                }
                attempt.Quarterback.SetMoveInput(Vector2.zero);
                Near(attempt.Quarterback.transform.position, new Vector3(destination.x, 0.5f, destination.y));
                var expected = Expected(1f);
                Assert.That(attempt.LaunchPass(attempt.Receiver, 1f), Is.True);
                yield return ObserveFlight(expected);
                sawExit |= attempt.FlightOutcome == FlightEnd.FieldExit;
            }
            Assert.That(sawExit, Is.True, "Moving-target lead from a permitted QB edge must exercise actual field exit.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = previousDelta;
            var scene = SceneManager.GetSceneByName("Bootstrap");
            SceneManager.SetActiveScene(SceneManager.CreateScene("PassLaunchCleanup"));
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}

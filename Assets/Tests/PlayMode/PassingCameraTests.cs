using System.Collections;
using System.Linq;
using FlagFootball.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlagFootball.Tests.PlayMode
{
    public sealed class PassingCameraTests
    {
        private SceneBootstrap attempt;
        private Camera view;
        private RunningCamera driver;
        private float previousDelta, minimumBodyWidth, minimumBodyHeight;
        private Rect safe = new Rect(0f, 0f, 1f, 1f);

        [UnitySetUp]
        public IEnumerator Prepare()
        {
            safe = new Rect(0f, 0f, 1f, 1f);
            minimumBodyWidth = minimumBodyHeight = float.PositiveInfinity;
            previousDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            attempt = Object.FindFirstObjectByType<SceneBootstrap>();
            Object.FindFirstObjectByType<TouchJoystick>().enabled = false;
            view = Camera.main;
            driver = view.GetComponent<RunningCamera>();
        }

        private void AssertVisible()
        {
            var bottom = Mathf.Max(safe.height * 0.36f, 384f / 1080f);
            var region = new Rect(safe.xMin + safe.width * 0.04f, safe.yMin + bottom,
                safe.width * 0.92f, safe.height * 0.96f - bottom);
            foreach (var visual in attempt.Quarterback.GetComponentsInChildren<Renderer>()
                .Concat(new[] { attempt.Receiver.GetComponent<Renderer>(), attempt.Ball.GetComponent<Renderer>() }))
            for (var corner = 0; corner < 8; corner++)
            {
                var bounds = visual.bounds;
                AssertPoint(new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z), region);
            }
            var low = Vector3.Min(Vector3.Min(attempt.Quarterback.transform.position, attempt.Receiver.position),
                attempt.Ball.position) - Vector3.one * 0.2f;
            var high = Vector3.Max(Vector3.Max(attempt.Quarterback.transform.position, attempt.Receiver.position),
                attempt.Ball.position) + Vector3.one * 0.2f;
            for (var corner = 0; corner < 8; corner++)
                AssertPoint(new Vector3((corner & 1) == 0 ? low.x : high.x,
                    (corner & 2) == 0 ? 0f : 4.2f, (corner & 4) == 0 ? low.z : high.z), region);
            var body = attempt.Quarterback.GetComponent<Renderer>().bounds;
            var bodySize = view.WorldToViewportPoint(body.max) - view.WorldToViewportPoint(body.min);
            minimumBodyWidth = Mathf.Min(minimumBodyWidth, bodySize.x * 1080f * view.aspect);
            minimumBodyHeight = Mathf.Min(minimumBodyHeight, bodySize.y * 1080f);
            var anchor = view.WorldToViewportPoint(attempt.Receiver.position + Vector3.up * 1.2f);
            var radius = new Vector2(96f / (1080f * view.aspect), 96f / 1080f);
            Assert.That(anchor.x - radius.x, Is.GreaterThanOrEqualTo(region.xMin - 0.0001f));
            Assert.That(anchor.x + radius.x, Is.LessThanOrEqualTo(region.xMax + 0.0001f));
            Assert.That(anchor.y - radius.y, Is.GreaterThanOrEqualTo(region.yMin - 0.0001f));
            Assert.That(anchor.y + radius.y, Is.LessThanOrEqualTo(region.yMax + 0.0001f));
            Assert.That(Quaternion.Angle(view.transform.rotation, Quaternion.Euler(60f, 0f, 0f)),
                Is.LessThan(0.001f));
        }

        private void AssertPoint(Vector3 world, Rect region)
        {
            var point = view.WorldToViewportPoint(world);
            Assert.That(point.z, Is.InRange(view.nearClipPlane, view.farClipPlane));
            Assert.That(point.x, Is.InRange(region.xMin - 0.0001f, region.xMax + 0.0001f));
            Assert.That(point.y, Is.InRange(region.yMin - 0.0001f, region.yMax + 0.0001f));
        }

        private IEnumerator MoveTo(Vector2 destination)
        {
            var qb = attempt.Quarterback;
            for (var frame = 0; frame < 240; frame++)
            {
                var delta = destination - new Vector2(qb.transform.position.x, qb.transform.position.z);
                if (delta.magnitude < 0.001f) break;
                var before = qb.transform.position;
                var cameraBefore = view.transform.position;
                var sizeBefore = view.orthographicSize;
                qb.SetMoveInput(Vector2.ClampMagnitude(delta / (5f * Time.deltaTime), 1f));
                yield return null;
                AssertVisible();
                Assert.That(Vector2.Dot(new Vector2(qb.transform.position.x - before.x,
                    qb.transform.position.z - before.z).normalized, delta.normalized), Is.GreaterThan(0.999f));
                Assert.That(Vector3.Distance(view.transform.position, cameraBefore), Is.LessThan(0.8f));
                Assert.That(Mathf.Abs(view.orthographicSize - sizeBefore), Is.LessThan(0.8f));
            }
            qb.SetMoveInput(Vector2.zero);
            Assert.That(Vector2.Distance(destination, new Vector2(qb.transform.position.x,
                qb.transform.position.z)), Is.LessThan(0.002f));
        }

        [UnityTest]
        public IEnumerator PreparationRouteAndAllQbExtremes_ReserveHaloAcrossLandscapeAspects()
        {
            foreach (var aspect in new[] { 16f / 9f, 20f / 9f, 2.5f })
            {
                view.aspect = aspect;
                attempt.RestartAttempt();
                AssertVisible();
                Assert.That(attempt.StartAttempt(), Is.True);
                for (var frame = 0; frame < 75; frame++)
                {
                    attempt.Quarterback.SetMoveInput(Vector2.left);
                    yield return null;
                    AssertVisible();
                    var receiver = attempt.Receiver.position;
                    Assert.That(receiver.z, Is.InRange(1f, 4.0001f));
                    Assert.That(receiver.x, Is.InRange(1.5f, 4.5001f));
                }
                Assert.That(Vector3.Distance(attempt.Receiver.position, new Vector3(4.5f, 0.5f, 4f)),
                    Is.LessThan(0.001f), "Actual route must reach its endpoint.");
                var min = attempt.Quarterback.MinimumPosition;
                var max = attempt.Quarterback.MaximumPosition;
                Assert.That(min, Is.EqualTo(new Vector2(-8f, -8f)));
                Assert.That(max, Is.EqualTo(new Vector2(8f, 8f)));
                foreach (var point in new[] { min, new Vector2(0f, min.y), new Vector2(max.x, min.y),
                    new Vector2(max.x, 0f), max, new Vector2(0f, max.y), new Vector2(min.x, max.y),
                    new Vector2(min.x, 0f), Vector2.zero }) yield return MoveTo(point);
            }
            Assert.That(minimumBodyWidth, Is.GreaterThan(20f), "Projected QB width at reference height 1080.");
            Assert.That(minimumBodyHeight, Is.GreaterThan(32f));
            TestContext.WriteLine($"Minimum QB footprint at 1080px height: {minimumBodyWidth:F2} x {minimumBodyHeight:F2}px.");
        }

        [UnityTest]
        public IEnumerator ElevatedBallHarnessAndInsetSafeArea_RestartRestoresPreparedFrame()
        {
            safe = new Rect(0.04f, 0.03f, 0.92f, 0.94f);
            foreach (var aspect in new[] { 16f / 9f, 20f / 9f, 2.5f })
            {
                view.aspect = aspect;
                attempt.RestartAttempt();
                var preparedPosition = view.transform.position;
                var preparedSize = view.orthographicSize;
                attempt.StartAttempt();
                foreach (var point in new[] { new Vector3(-8f, 0.2f, -8f), new Vector3(8f, 4f, 8f),
                    new Vector3(-8f, 4f, 8f), new Vector3(8f, 4f, -8f) })
                {
                    driver.SetBallFramingTestInput(point, safe);
                    yield return null;
                    Assert.That(attempt.Ball.position, Is.EqualTo(point));
                    AssertVisible();
                }
                attempt.RestartAttempt();
                Assert.That(attempt.BallOwner, Is.EqualTo(attempt.Quarterback.transform));
                Assert.That(Vector3.Distance(view.transform.position, preparedPosition), Is.LessThan(0.0001f));
                Assert.That(view.orthographicSize, Is.EqualTo(preparedSize).Within(0.0001f));
            }
        }

        [UnityTest]
        public IEnumerator InvalidHarnessInput_DoesNotDetachOrMoveTheBall()
        {
            var position = attempt.Ball.position;
            Assert.Throws<System.ArgumentException>(() => driver.SetBallFramingTestInput(
                new Vector3(float.NaN, 4f, 0f), safe));
            Assert.Throws<System.ArgumentException>(() => driver.SetBallFramingTestInput(
                Vector3.zero, new Rect(0f, 0f, 0.1f, 1f)));
            yield return null;
            Assert.That(attempt.BallOwner, Is.EqualTo(attempt.Quarterback.transform));
            Assert.That(attempt.Ball.position, Is.EqualTo(position));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = previousDelta;
            var scene = SceneManager.GetSceneByName("Bootstrap");
            SceneManager.SetActiveScene(SceneManager.CreateScene("PassingCameraCleanup"));
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}

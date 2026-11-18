using System.Collections;
using FlagFootball.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlagFootball.Tests.PlayMode
{
    public sealed class RunningCameraTests
    {
        private PlayerMovement player;
        private Camera view;
        private Renderer field;
        private Renderer[] visuals;
        private float previousCaptureDelta;

        [UnitySetUp]
        public IEnumerator LoadRunningView()
        {
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            Assert.That(Object.FindFirstObjectByType<RunningCamera>(), Is.Not.Null);
            Object.FindFirstObjectByType<TouchJoystick>().enabled = false;
            var attempt = Object.FindFirstObjectByType<SceneBootstrap>();
            player = attempt.Quarterback;
            Assert.That(attempt.StartAttempt(), Is.True);
            view = Camera.main;
            view.GetComponent<RunningCamera>().UseRunningFrame();
            field = GameObject.Find("Ground").GetComponent<Renderer>();
            visuals = player.GetComponentsInChildren<Renderer>();
        }

        private void AssertFraming()
        {
            var ground = field.bounds;
            for (var x = 0; x <= 1; x++)
            for (var y = 0; y <= 1; y++)
            {
                var ray = view.ViewportPointToRay(new Vector3(x, y, 0f));
                Assert.That(ray.direction.y, Is.LessThan(0f));
                var point = ray.GetPoint((ground.center.y - ray.origin.y) / ray.direction.y);
                Assert.That(point.x, Is.InRange(ground.min.x - 0.002f, ground.max.x + 0.002f));
                Assert.That(point.z, Is.InRange(ground.min.z - 0.002f, ground.max.z + 0.002f));
            }
            foreach (var visual in visuals)
            for (var corner = 0; corner < 8; corner++)
            {
                var bounds = visual.bounds;
                var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                var projected = view.WorldToViewportPoint(point);
                Assert.That(projected.z, Is.InRange(view.nearClipPlane, view.farClipPlane));
                Assert.That(projected.x, Is.InRange(-0.0001f, 1.0001f), visual.name);
                Assert.That(projected.y, Is.InRange(-0.0001f, 1.0001f), visual.name);
            }
        }

        private IEnumerator MoveTo(Vector2 destination)
        {
            for (var frame = 0; frame < 600; frame++)
            {
                var position = new Vector2(player.transform.position.x, player.transform.position.z);
                var delta = destination - position;
                if (delta.magnitude < 0.001f) break;
                player.SetMoveInput(Vector2.ClampMagnitude(delta / (5f * Time.deltaTime), 1f));
                yield return null;
                AssertFraming();
            }
            player.SetMoveInput(Vector2.zero);
            yield return null;
            var actual = new Vector2(player.transform.position.x, player.transform.position.z);
            Assert.That(Vector2.Distance(actual, destination), Is.LessThan(0.002f));
            AssertFraming();
        }

        [UnityTest]
        public IEnumerator Tracking_IsSmoothAndConvergesAfterMovementStops()
        {
            var rotation = view.transform.rotation;
            var start = view.transform.position;
            player.SetMoveInput(Vector2.right);
            for (var frame = 0; frame < 12; frame++) yield return null;
            player.SetMoveInput(Vector2.zero);
            yield return null;
            Assert.That(view.orthographic, Is.True);
            Assert.That(field.sharedMaterial.GetTexture("_BaseMap").name, Is.EqualTo("PrototypeFieldGrid"));
            Assert.That(view.transform.position.x, Is.GreaterThan(start.x).And.LessThan(player.transform.position.x));
            var lag = player.transform.position.x - view.transform.position.x;
            for (var frame = 0; frame < 60; frame++) yield return null;
            Assert.That(Mathf.Abs(player.transform.position.x - view.transform.position.x), Is.LessThan(0.01f));
            Assert.That(player.transform.position.x - view.transform.position.x, Is.LessThan(lag));
            Assert.That(Quaternion.Angle(rotation, view.transform.rotation), Is.LessThan(0.001f));
            Assert.That(view.transform.position.y, Is.EqualTo(start.y).Within(0.0001f));
            AssertFraming();
        }

        [UnityTest]
        public IEnumerator CenterEdgesAndCorners_KeepWholePlayerAndGroundFramedAcrossLandscapeAspects()
        {
            var rotation = view.transform.rotation;
            foreach (var aspect in new[] { 16f / 9f, 20f / 9f, 2.5f })
            {
                view.aspect = aspect;
                yield return null;
                yield return null;
                yield return MoveTo(Vector2.zero);
                var min = player.MinimumPosition;
                var max = player.MaximumPosition;
                foreach (var destination in new[] {
                    new Vector2(min.x, 0f), min, new Vector2(0f, min.y), new Vector2(max.x, min.y),
                    new Vector2(max.x, 0f), max, new Vector2(0f, max.y), new Vector2(min.x, max.y), Vector2.zero })
                    yield return MoveTo(destination);
                Assert.That(Quaternion.Angle(rotation, view.transform.rotation), Is.LessThan(0.001f));
            }
        }

        [UnityTest]
        public IEnumerator DirectionChanges_PreserveFixedViewAndScreenRelativeMovement()
        {
            var rotation = view.transform.rotation;
            foreach (var direction in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
            {
                var start = player.transform.position;
                player.SetMoveInput(direction);
                for (var frame = 0; frame < 6; frame++) yield return null;
                var delta = player.transform.position - start;
                Assert.That(Vector2.Dot(new Vector2(delta.x, delta.z).normalized, direction), Is.GreaterThan(0.999f));
                Assert.That(Quaternion.Angle(rotation, view.transform.rotation), Is.LessThan(0.001f));
                AssertFraming();
            }
        }

        [UnityTest]
        public IEnumerator SuddenRelocation_UsesVisibilitySafetyInsteadOfLosingThePlayer()
        {
            var max = player.MaximumPosition;
            player.transform.position = new Vector3(max.x, player.transform.position.y, max.y);
            player.SetMoveInput(Vector2.zero);
            yield return null;
            yield return null;
            AssertFraming();
        }

        [UnityTearDown]
        public IEnumerator UnloadRunningView()
        {
            Time.captureDeltaTime = previousCaptureDelta;
            var scene = SceneManager.GetSceneByName("Bootstrap");
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("CameraTestCleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}

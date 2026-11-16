using System.Collections;
using FlagFootball.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlagFootball.Tests.PlayMode
{
    public sealed class PlayerMovementTests
    {
        private PlayerMovement player;

        [UnitySetUp]
        public IEnumerator LoadCharacter()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            var joystick = Object.FindFirstObjectByType<TouchJoystick>();
            if (joystick) joystick.enabled = false;
            player = Object.FindFirstObjectByType<PlayerMovement>();
            Assert.That(player, Is.Not.Null, "Bootstrap must contain the movement adapter.");
        }

        [UnityTest]
        public IEnumerator PublicInput_MovesAtAnalogAndCappedSpeedThenStops()
        {
            foreach (var magnitude in new[] { 0.25f, 1f, 3f })
            {
                var before = player.transform.position;
                player.SetMoveInput(Vector2.up * magnitude);
                yield return null;
                var distance = Vector3.Distance(before, player.transform.position);
                Assert.That(distance, Is.EqualTo(5f * Mathf.Min(1f, magnitude) * Time.deltaTime)
                    .Within(0.0001f));
                Assert.That(Vector3.Dot(player.transform.forward, Vector3.forward), Is.GreaterThan(0.999f));
            }
            player.SetMoveInput(Vector2.zero);
            var stopped = player.transform.position;
            var facing = player.transform.rotation;
            yield return null;
            yield return null;
            Assert.That(player.transform.position, Is.EqualTo(stopped));
            Assert.That(player.transform.rotation, Is.EqualTo(facing));
        }

        [UnityTest]
        public IEnumerator CameraRotationAndDirectionChange_ApplyOnNextFrame()
        {
            var runningCamera = Camera.main.GetComponent<RunningCamera>();
            if (runningCamera) runningCamera.enabled = false;
            Camera.main.transform.rotation = Quaternion.Euler(50f, 90f, 0f);
            var start = player.transform.position;
            player.SetMoveInput(Vector2.up);
            yield return null;
            Assert.That(player.transform.position.x, Is.GreaterThan(start.x));
            Assert.That(player.transform.position.z, Is.EqualTo(start.z).Within(0.0001f));
            player.SetMoveInput(Vector2.right);
            var beforeTurn = player.transform.position;
            yield return null;
            Assert.That(player.transform.position.z, Is.LessThan(beforeTurn.z));
            Assert.That(player.transform.position.x, Is.EqualTo(beforeTurn.x).Within(0.0001f));
            Assert.That(Vector3.Dot(player.transform.forward, Vector3.back), Is.GreaterThan(0.999f));
        }

        [UnityTest]
        public IEnumerator FieldEdgesAndCorners_ContainWholePlaceholderAndPermitReturn()
        {
            var field = GameObject.Find("Ground").GetComponent<Renderer>().bounds;
            var marker = player.transform.Find("FacingMarker");
            Assert.That(marker, Is.Not.Null, "The placeholder needs a visible forward cue.");
            for (var x = -1; x <= 1; x++)
            for (var z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;
                var minimum = player.MinimumPosition;
                var maximum = player.MaximumPosition;
                var position = new Vector3(Mathf.Lerp(minimum.x, maximum.x, (x + 1f) * 0.5f),
                    0.5f, Mathf.Lerp(minimum.y, maximum.y, (z + 1f) * 0.5f));
                player.transform.position = position;
                player.SetMoveInput(new Vector2(x, z));
                yield return null;
                var edge = player.transform.position;
                Assert.That(edge.x, Is.EqualTo(position.x).Within(0.0001f));
                Assert.That(edge.z, Is.EqualTo(position.z).Within(0.0001f));
                foreach (var visual in player.GetComponentsInChildren<Renderer>())
                {
                    Assert.That(visual.bounds.min.x, Is.GreaterThanOrEqualTo(field.min.x));
                    Assert.That(visual.bounds.max.x, Is.LessThanOrEqualTo(field.max.x));
                    Assert.That(visual.bounds.min.z, Is.GreaterThanOrEqualTo(field.min.z));
                    Assert.That(visual.bounds.max.z, Is.LessThanOrEqualTo(field.max.z));
                }
                player.SetMoveInput(-new Vector2(x, z));
                yield return null;
                Assert.That(player.transform.position.sqrMagnitude, Is.LessThan(edge.sqrMagnitude));
                Assert.That(player.transform.position.y, Is.EqualTo(0.5f));
            }
        }

        [UnityTearDown]
        public IEnumerator UnloadCharacter()
        {
            var scene = SceneManager.GetSceneByName("Bootstrap");
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("MovementTestCleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}

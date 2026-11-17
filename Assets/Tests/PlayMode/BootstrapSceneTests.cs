using System.Collections;
using System.Linq;
using FlagFootball.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlagFootball.Tests.PlayMode
{
    public sealed class BootstrapSceneTests
    {
        [UnityTest]
        public IEnumerator BootstrapScene_InitializesWithCameraAndUrp()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            var bootstrap = Object.FindFirstObjectByType<SceneBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.IsInitialized, Is.True, "Start must finish scene composition.");
            Assert.That(bootstrap.Quarterback.gameObject, Is.EqualTo(GameObject.Find("Placeholder")));
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(GameObject.Find("Placeholder"), Is.Not.Null);
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.Not.Null);
            Assert.That(GraphicsSettings.defaultRenderPipeline.GetType().Name,
                Is.EqualTo("UniversalRenderPipelineAsset"));
        }

        [UnityTest]
        public IEnumerator PreparedActors_ExposeDistinctRolesHeldBallAndInitialFraming()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            var attempt = Object.FindFirstObjectByType<SceneBootstrap>();
            var qb = attempt.Quarterback.transform;
            Assert.That(qb.gameObject, Is.EqualTo(GameObject.Find("Placeholder")));
            Assert.That(attempt.Receiver, Is.Not.EqualTo(qb));
            Assert.That(attempt.Receiver.name, Is.EqualTo("Receiver"));
            Assert.That(attempt.Receiver.position, Is.EqualTo(new Vector3(1.5f, 0.5f, 1f)));
            Assert.That(attempt.Receiver.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(attempt.Receiver.GetComponent<PlayerMovement>(), Is.Null);
            Assert.That(attempt.BallOwner, Is.EqualTo(qb));
            Assert.That(attempt.Ball.parent, Is.EqualTo(qb));
            Assert.That(attempt.Ball.localPosition, Is.EqualTo(new Vector3(0.8f, 0.1f, 0.2f)));
            Assert.That(attempt.Ball.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(Vector3.Distance(attempt.Ball.lossyScale, new Vector3(0.22f, 0.18f, 0.36f)),
                Is.LessThan(0.0001f), "Held scale compensates the QB's nonuniform X scale.");
            var receiverVisual = attempt.Receiver.GetComponent<Renderer>();
            Assert.That(receiverVisual.sharedMaterial.GetColor("_BaseColor"),
                Is.Not.EqualTo(qb.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor")));
            Assert.That(attempt.Receiver.GetComponent<Collider>(), Is.Null);
            Assert.That(attempt.Ball.GetComponent<Collider>(), Is.Null);
            Assert.That(attempt.Receiver.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(attempt.Ball.GetComponent<Rigidbody>(), Is.Null);
            var view = Camera.main;
            foreach (var aspect in new[] { 16f / 9f, 20f / 9f, 2.5f })
            {
                view.aspect = aspect;
                yield return null;
                foreach (var visual in qb.GetComponentsInChildren<Renderer>().Concat(new[] { receiverVisual }))
                {
                    Assert.That(visual.enabled && visual.gameObject.activeInHierarchy, Is.True);
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var bounds = visual.bounds;
                        var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                            (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                            (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                        var projected = view.WorldToViewportPoint(point);
                        Assert.That(projected.z, Is.InRange(view.nearClipPlane, view.farClipPlane));
                        Assert.That(projected.x, Is.InRange(0f, 1f), visual.name);
                        Assert.That(projected.y, Is.InRange(0f, 1f), visual.name);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator PreparedActors_RepeatedSceneUnloadDestroysOwnedVisualsAndMaterials()
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
                yield return null;
                var attempt = Object.FindFirstObjectByType<SceneBootstrap>();
                var receiver = attempt.Receiver;
                var ball = attempt.Ball;
                var materials = new[] { receiver.GetComponent<Renderer>().sharedMaterial,
                    ball.GetComponent<Renderer>().sharedMaterial };
                var visuals = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                Assert.That(visuals.Count(visual => visual.name == "Receiver"), Is.EqualTo(1));
                Assert.That(visuals.Count(visual => visual.name == "Ball"), Is.EqualTo(1));
                for (var reset = 0; reset < 3; reset++)
                {
                    Assert.That(attempt.RestartAttempt(), Is.True);
                    Assert.That(attempt.Receiver, Is.SameAs(receiver));
                    Assert.That(attempt.Ball, Is.SameAs(ball));
                }
                ball.SetParent(null);
                if (cycle == 2) Object.Destroy(attempt.gameObject);
                yield return UnloadBootstrapScene();
                yield return null;
                Assert.That(receiver == null && ball == null, Is.True);
                foreach (var material in materials) Assert.That(material == null, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator PreparedActors_OwnerOnlyDestructionKeepsCameraUpdating()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            var attempt = Object.FindFirstObjectByType<SceneBootstrap>();
            var qb = attempt.Quarterback.transform;
            var view = Camera.main;
            var driver = view.GetComponent<RunningCamera>();
            var controls = Object.FindFirstObjectByType<AttemptControls>();
            var touchRoot = controls.transform.root.gameObject;
            var field = GameObject.Find("Ground").GetComponent<Renderer>();
            var receiver = attempt.Receiver;
            var ball = attempt.Ball;
            var materials = new[] { receiver.GetComponent<Renderer>().sharedMaterial,
                ball.GetComponent<Renderer>().sharedMaterial };
            Object.Destroy(attempt.gameObject);
            yield return null;
            yield return null;
            Assert.That(attempt == null && receiver == null && ball == null, Is.True);
            Assert.That(controls == null && touchRoot == null, Is.True, "Owner must dispose all touch UI.");
            foreach (var material in materials) Assert.That(material == null, Is.True);
            Assert.That(qb && field && SceneManager.GetSceneByName("Bootstrap").isLoaded, Is.True);
            var previousPosition = view.transform.position;
            qb.position = new Vector3(6f, qb.position.y, 0f);
            for (var frame = 0; frame < 4; frame++)
            {
                yield return null;
                Assert.That(view.isActiveAndEnabled && driver.isActiveAndEnabled, Is.True);
                foreach (var value in new[] { view.transform.position.x, view.transform.position.y,
                    view.transform.position.z, view.orthographicSize })
                    Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
                foreach (var visual in qb.GetComponentsInChildren<Renderer>())
                for (var corner = 0; corner < 8; corner++)
                {
                    var bounds = visual.bounds;
                    var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    var projected = view.WorldToViewportPoint(point);
                    Assert.That(projected.z, Is.InRange(view.nearClipPlane, view.farClipPlane));
                    Assert.That(projected.x, Is.InRange(0f, 1f), visual.name);
                    Assert.That(projected.y, Is.InRange(0f, 1f), visual.name);
                }
            }
            Assert.That(view.transform.position.x, Is.GreaterThan(previousPosition.x));
        }

        [UnityTearDown]
        public IEnumerator UnloadBootstrapScene()
        {
            var scene = SceneManager.GetSceneByName("Bootstrap");
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("TestCleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}

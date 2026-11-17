using System.Collections;
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

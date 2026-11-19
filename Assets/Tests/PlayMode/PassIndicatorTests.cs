using System;
using System.Collections;
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
    public sealed class PassIndicatorTests
    {
        private SceneBootstrap attempt;
        private PassIndicator indicator;
        private Camera view;
        private float previousDelta;

        [UnitySetUp]
        public IEnumerator Prepare()
        {
            previousDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            attempt = Object.FindFirstObjectByType<SceneBootstrap>();
            indicator = Object.FindFirstObjectByType<PassIndicator>();
            Object.FindFirstObjectByType<TouchJoystick>().enabled = false;
            view = Camera.main;
            Assert.That(indicator, Is.Not.Null);
        }

        private static Rect Rectangle(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private void AssertPresentation(Color color, float diameter)
        {
            Assert.That(indicator.IsAvailable && indicator.Button.gameObject.activeInHierarchy, Is.True);
            Assert.That(indicator.Halo.GetComponent<Image>().color, Is.EqualTo(new Color(color.r, color.g, color.b, 0.5f)));
            Assert.That(indicator.Button.GetComponent<Image>().color, Is.EqualTo(color));
            Assert.That(indicator.Halo.rect.width, Is.EqualTo(diameter).Within(0.01f));
            Assert.That(indicator.Button.rect.width, Is.EqualTo(96f));
        }

        [UnityTest]
        public IEnumerator CoreProgress_ProducesRedOrangeGreenAndSustainedReadyWithoutLaunching()
        {
            Assert.That(indicator.IsAvailable, Is.False);
            var marker = indicator.transform.Find("ReceiverMarker");
            Assert.That(marker.gameObject.activeInHierarchy, Is.True);
            var receiverLabel = marker.GetComponentInChildren<Text>();
            Assert.That(receiverLabel.text, Is.EqualTo("L1"));
            Assert.That(receiverLabel.preferredWidth, Is.LessThanOrEqualTo(marker.GetComponent<RectTransform>().rect.width));
            Assert.That(receiverLabel.preferredHeight, Is.LessThanOrEqualTo(marker.GetComponent<RectTransform>().rect.height));
            attempt.StartAttempt();
            var charge = new PassCharge();
            charge.Begin(1L, System.Numerics.Vector2.Zero);
            indicator.SetProgress(charge.Quality);
            AssertPresentation(Color.red, 192f);
            charge.Advance(1L, 0.5f);
            indicator.SetProgress(charge.Quality);
            AssertPresentation(new Color(1f, 0.5f, 0f), 148.8f);
            charge.Advance(1L, 0.5f);
            for (var frame = 0; frame < 90; frame++)
            {
                charge.Advance(1L, Time.deltaTime);
                indicator.SetProgress(charge.Quality);
                yield return null;
                AssertPresentation(Color.green, 105.6f);
                Assert.That(attempt.BallOwner, Is.EqualTo(attempt.Quarterback.transform));
                Assert.That(attempt.IsActive, Is.True);
            }
            foreach (var invalid in new[] { -1f, 2f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => indicator.SetProgress(invalid));
            AssertPresentation(Color.green, 105.6f);
            var label = indicator.Button.GetComponentInChildren<Text>();
            Assert.That(label.text, Is.EqualTo("PASS"));
            Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(indicator.Button.rect.width));
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(indicator.Button.rect.height));
            Assert.That(label.color, Is.EqualTo(Color.black));
            Assert.That(indicator.transform.Find("ChargeBorder").gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator PhaseChangesRestartAndOwnerDisposal_HideResetAndReleaseSharedUi()
        {
            indicator.SetProgress(1f);
            Assert.That(indicator.Button.gameObject.activeInHierarchy, Is.False);
            attempt.StartAttempt();
            indicator.SetProgress(0f);
            AssertPresentation(Color.red, 192f);
            indicator.SetProgress(1f);
            attempt.RestartAttempt();
            Assert.That(indicator.Button.gameObject.activeInHierarchy, Is.False);
            attempt.StartAttempt();
            yield return null;
            AssertPresentation(Color.red, 192f);
            attempt.Ball.SetParent(attempt.Receiver, true); // Possession observation, not a catch implementation.
            indicator.SetProgress(1f);
            Assert.That(indicator.IsAvailable, Is.False);
            attempt.RestartAttempt();
            attempt.StartAttempt();
            Assert.That(attempt.LaunchPass(attempt.Receiver, 1f), Is.True);
            indicator.SetProgress(1f);
            Assert.That(indicator.IsAvailable || indicator.Button.gameObject.activeInHierarchy, Is.False);
            for (var frame = 0; frame < 180 && attempt.IsActive; frame++) yield return null;
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Incomplete));
            Assert.That(indicator.IsAvailable, Is.False);
            attempt.RestartAttempt();
            attempt.StartAttempt();
            view.GetComponent<RunningCamera>().UseRunningFrame();
            indicator.SetProgress(1f);
            Assert.That(indicator.IsAvailable, Is.False);
            attempt.RestartAttempt();
            attempt.StartAttempt();
            attempt.enabled = false;
            indicator.SetProgress(1f);
            Assert.That(indicator.IsAvailable, Is.False);
            attempt.enabled = true;
            indicator.SetProgress(0f);
            var button = indicator.Button;
            var sprite = button.GetComponent<Image>().sprite;
            var texture = sprite.texture;
            Assert.That(sprite, Is.SameAs(GameObject.Find("MovementJoystick").GetComponent<Image>().sprite));
            Assert.That(Object.FindObjectsByType<PassIndicator>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Object.Destroy(attempt.gameObject);
            yield return null;
            yield return null;
            Assert.That(indicator == null && button == null && sprite == null && texture == null, Is.True);
        }

        [UnityTest]
        public IEnumerator MovingRouteAndAllQbEdges_FullHaloFitsInsetSafeAreaAndAvoidsExistingControls()
        {
            var canvas = indicator.GetComponentInParent<Canvas>();
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace; // Real UI rects in reference pixels, not batch Screen dimensions.
            var root = canvas.GetComponent<RectTransform>();
            root.localScale = Vector3.one;
            var safe = indicator.transform.parent.GetComponent<RectTransform>();
            var controls = Object.FindFirstObjectByType<AttemptControls>();
            var joystick = GameObject.Find("MovementJoystick").GetComponent<RectTransform>();
            var marker = indicator.transform.Find("ReceiverMarker").GetComponent<RectTransform>();
            foreach (var width in new[] { 1920f, 2400f, 2700f })
            {
                root.sizeDelta = new Vector2(width, 1080f);
                root.position = new Vector3(width * 0.5f, 540f);
                var area = new Rect(64f, 32f, width - 128f, 1016f);
                safe.anchorMin = new Vector2(area.xMin / width, area.yMin / 1080f);
                safe.anchorMax = new Vector2(area.xMax / width, area.yMax / 1080f);
                view.aspect = width / 1080f;
                attempt.RestartAttempt();
                attempt.StartAttempt();
                indicator.SetProgress(0.5f, true);
                Canvas.ForceUpdateCanvases();
                var border = indicator.transform.Find("ChargeBorder").GetComponent<RectTransform>();
                var borderRect = Rectangle(border);
                Assert.That(border.gameObject.activeInHierarchy, Is.True);
                Assert.That(Vector2.Distance(borderRect.min, Vector2.zero), Is.LessThan(0.05f));
                Assert.That(Vector2.Distance(borderRect.max, new Vector2(width, 1080f)), Is.LessThan(0.05f));
                foreach (var light in border.GetComponentsInChildren<Image>())
                {
                    Assert.That(Rectangle(light.rectTransform).Contains(borderRect.center), Is.False);
                    Assert.That(light.raycastTarget, Is.False);
                }
                foreach (var destination in new[] { new Vector2(-8f, -8f), new Vector2(0f, -8f), new Vector2(8f, -8f),
                    new Vector2(8f, 0f), new Vector2(8f, 8f), new Vector2(0f, 8f), new Vector2(-8f, 8f), new Vector2(-8f, 0f), Vector2.zero })
                {
                    for (var frame = 0; frame < 300; frame++)
                    {
                        var position = attempt.Quarterback.transform.position;
                        var delta = destination - new Vector2(position.x, position.z);
                        attempt.Quarterback.SetMoveInput(Vector2.ClampMagnitude(delta / (5f * Time.deltaTime), 1f));
                        yield return null;
                        indicator.SetProgress(0f);
                        Canvas.ForceUpdateCanvases();
                        var halo = Rectangle(indicator.Halo);
                        Assert.That(area.Contains(halo.min) && area.Contains(halo.max), Is.True);
                        foreach (var control in new[] { controls.StartRegion, controls.RestartRegion, joystick })
                            Assert.That(halo.Overlaps(Rectangle(control)), Is.False);
                        var expected = view.WorldToViewportPoint(attempt.Receiver.position + Vector3.up * 1.2f);
                        Assert.That(Vector2.Distance(Rectangle(marker).center,
                            new Vector2(expected.x * width, expected.y * 1080f)), Is.LessThan(0.05f));
                        Assert.That(area.Contains(Rectangle(marker).min) && area.Contains(Rectangle(marker).max), Is.True);
                        Assert.That(Vector2.Distance(Rectangle(indicator.Button).center,
                            new Vector2(area.xMax - 560f, area.yMin + 208f)), Is.LessThan(0.05f));
                        if (delta.magnitude < 0.001f) break;
                    }
                    attempt.Quarterback.SetMoveInput(Vector2.zero);
                    Assert.That(Vector2.Distance(destination, new Vector2(attempt.Quarterback.transform.position.x,
                        attempt.Quarterback.transform.position.z)), Is.LessThan(0.002f));
                }
                Assert.That(attempt.Receiver.position, Is.EqualTo(new Vector3(4.5f, 0.5f, 4f)));
                Assert.That(attempt.Quarterback.MinimumPosition, Is.EqualTo(new Vector2(-8f, -8f)));
                Assert.That(attempt.Quarterback.MaximumPosition, Is.EqualTo(new Vector2(8f, 8f)));
            }
        }

        [UnityTest]
        public IEnumerator UnfitSafeArea_ReportsCalibrationConflictWithoutClampingActors()
        {
            attempt.StartAttempt();
            var safe = indicator.transform.parent.GetComponent<RectTransform>();
            var min = safe.anchorMin;
            var max = safe.anchorMax;
            var receiver = attempt.Receiver.position;
            safe.anchorMax = min + Vector2.one * 0.01f;
            Canvas.ForceUpdateCanvases();
            try { Assert.Throws<InvalidOperationException>(() => indicator.SetProgress(0f)); }
            finally { safe.anchorMin = min; safe.anchorMax = max; Canvas.ForceUpdateCanvases(); }
            Assert.That(attempt.Receiver.position, Is.EqualTo(receiver));
            indicator.SetProgress(0f);
            Assert.That(indicator.IsAvailable, Is.True);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = previousDelta;
            var scene = SceneManager.GetSceneByName("Bootstrap");
            SceneManager.SetActiveScene(SceneManager.CreateScene("PassIndicatorCleanup"));
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}

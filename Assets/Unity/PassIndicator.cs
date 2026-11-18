using System;
using UnityEngine;
using UnityEngine.UI;

namespace FlagFootball.Unity
{
    [DefaultExecutionOrder(100)] // Project after the camera's LateUpdate.
    public sealed class PassIndicator : MonoBehaviour
    {
        private SceneBootstrap owner;
        private RunningCamera driver;
        private Camera view;
        private RectTransform safeArea, presentation;
        private Image button, halo;
        private Vector3 offset;
        private float progress, diameter, maximumHalo, orangeThreshold, readyScale;
        public RectTransform Button => button.rectTransform;
        public RectTransform Halo => halo.rectTransform;
        private bool Eligible => isActiveAndEnabled && owner && owner.CanLaunchPass &&
            driver && driver.IsPassingFrame && view && view.isActiveAndEnabled;
        public bool IsAvailable => Eligible && presentation && presentation.gameObject.activeInHierarchy;

        internal static PassIndicator Create(SceneBootstrap owner, RunningCamera driver, RectTransform safeArea,
            Sprite circle, float diameter, float maximumHalo, Vector3 offset, float orangeThreshold, float readyScale)
        {
            if (!float.IsFinite(diameter) || diameter <= 0f || !float.IsFinite(maximumHalo) ||
                !float.IsFinite(readyScale) || readyScale < 1f || maximumHalo < diameter * readyScale ||
                !float.IsFinite(orangeThreshold) || orangeThreshold <= 0f || orangeThreshold >= 1f ||
                !float.IsFinite(offset.x) || !float.IsFinite(offset.y) || !float.IsFinite(offset.z))
                throw new ArgumentException("Use finite, ordered indicator dimensions and a normalized orange threshold.");
            var root = new GameObject("PassIndicator", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(safeArea, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var indicator = root.AddComponent<PassIndicator>();
            indicator.owner = owner;
            indicator.driver = driver;
            indicator.view = driver.GetComponent<Camera>();
            indicator.safeArea = safeArea;
            indicator.diameter = diameter;
            indicator.maximumHalo = maximumHalo;
            indicator.offset = offset;
            indicator.orangeThreshold = orangeThreshold;
            indicator.readyScale = readyScale;
            indicator.presentation = new GameObject("PassPresentation", typeof(RectTransform)).GetComponent<RectTransform>();
            indicator.presentation.SetParent(root.transform, false);
            indicator.presentation.sizeDelta = Vector2.zero;
            indicator.halo = indicator.Disc("PassHalo", maximumHalo, circle);
            indicator.button = indicator.Disc("PassButton", diameter, circle);
            var text = new GameObject("PassLabel", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(indicator.button.transform, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.raycastTarget = false;
            text.text = "PASS";
            root.SetActive(true);
            return indicator;
        }

        private Image Disc(string name, float size, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(presentation, false);
            image.sprite = sprite;
            image.raycastTarget = false; // EnhancedTouch hit testing is added by the next slice.
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * 0.5f;
            image.rectTransform.sizeDelta = Vector2.one * size;
            return image;
        }

        public void SetProgress(float quality)
        {
            if (!float.IsFinite(quality) || quality < 0f || quality > 1f)
                throw new ArgumentOutOfRangeException(nameof(quality));
            progress = Eligible ? quality : 0f;
            Refresh();
        }

        private void OnEnable() { owner.Resetting += ResetProgress; ResetProgress(); }
        private void OnDisable() { if (owner) owner.Resetting -= ResetProgress; ResetProgress(); }
        private void ResetProgress() => SetProgress(0f);
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            var available = Eligible;
            if (!available) progress = 0f;
            var orange = new Color(1f, 0.5f, 0f);
            var color = progress <= orangeThreshold ? Color.Lerp(Color.red, orange, progress / orangeThreshold) :
                Color.Lerp(orange, Color.green, (progress - orangeThreshold) / (1f - orangeThreshold));
            button.color = color;
            color.a = 0.5f;
            halo.color = color;
            Halo.sizeDelta = Vector2.one * Mathf.Lerp(maximumHalo, diameter * readyScale, progress);
            presentation.gameObject.SetActive(available);
            if (!available) return;
            var viewport = view.WorldToViewportPoint(owner.Receiver.position + offset);
            var span = safeArea.anchorMax - safeArea.anchorMin;
            if (span.x <= 0f || span.y <= 0f) CalibrationConflict();
            var anchor = new Vector2((viewport.x - safeArea.anchorMin.x) / span.x,
                (viewport.y - safeArea.anchorMin.y) / span.y);
            var center = safeArea.rect.min + Vector2.Scale(safeArea.rect.size, anchor);
            var radius = maximumHalo * 0.5f;
            if (viewport.z < view.nearClipPlane || viewport.z > view.farClipPlane ||
                center.x - radius < safeArea.rect.xMin - 0.01f || center.x + radius > safeArea.rect.xMax + 0.01f ||
                center.y - radius < safeArea.rect.yMin - 0.01f || center.y + radius > safeArea.rect.yMax + 0.01f)
                CalibrationConflict();
            presentation.anchorMin = presentation.anchorMax = anchor;
            presentation.anchoredPosition = Vector2.zero;
        }

        private void CalibrationConflict()
        {
            presentation.gameObject.SetActive(false);
            throw new InvalidOperationException("Receiver halo does not fit the safe area; calibrate field, frame and indicator together.");
        }
    }
}

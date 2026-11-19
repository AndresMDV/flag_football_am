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
        private RectTransform safeArea, presentation, marker, border;
        private Image button, halo, receiverBadge;
        private Text receiverLabel;
        private Image[] edgeLights;
        private Vector3 offset;
        private Vector2 buttonPosition;
        private bool charging;
        private float progress, diameter, maximumHalo, orangeThreshold, readyScale, borderOpacity;
        public RectTransform Button => button.rectTransform;
        public RectTransform Halo => halo.rectTransform;
        private bool Eligible => isActiveAndEnabled && owner && owner.CanLaunchPass &&
            driver && driver.IsPassingFrame && view && view.isActiveAndEnabled;
        public bool IsAvailable => Eligible && presentation && presentation.gameObject.activeInHierarchy;

        internal static PassIndicator Create(SceneBootstrap owner, RunningCamera driver, RectTransform safeArea,
            Sprite circle, float diameter, float maximumHalo, Vector3 offset, float orangeThreshold, float readyScale,
            Vector2 buttonPosition, float borderWidth, float borderOpacity)
        {
            if (!float.IsFinite(diameter) || diameter <= 0f || !float.IsFinite(maximumHalo) ||
                !float.IsFinite(readyScale) || readyScale < 1f || maximumHalo < diameter * readyScale ||
                !float.IsFinite(orangeThreshold) || orangeThreshold <= 0f || orangeThreshold >= 1f ||
                !float.IsFinite(offset.x) || !float.IsFinite(offset.y) || !float.IsFinite(offset.z) ||
                !float.IsFinite(buttonPosition.x) || !float.IsFinite(buttonPosition.y) ||
                !float.IsFinite(borderWidth) || borderWidth <= 0f || borderWidth > 64f ||
                !float.IsFinite(borderOpacity) || borderOpacity <= 0f || borderOpacity > 1f)
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
            indicator.buttonPosition = buttonPosition;
            indicator.borderOpacity = borderOpacity;
            indicator.CreateBorder(borderWidth);
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
            text.color = Color.white;
            indicator.button.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            text.raycastTarget = false;
            text.text = "PASS";
            var markerImage = indicator.Disc("ReceiverMarker", 84f, circle);
            indicator.receiverBadge = markerImage;
            markerImage.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            indicator.marker = markerImage.rectTransform;
            indicator.marker.SetParent(root.transform, false);
            indicator.marker.sizeDelta = new Vector2(84f, 64f);
            indicator.halo.transform.SetParent(indicator.marker, false);
            indicator.halo.transform.SetAsFirstSibling();
            var markerLabel = Instantiate(text, indicator.marker, false);
            markerLabel.name = "ReceiverLabel";
            markerLabel.text = "L1";
            markerLabel.color = Color.white;
            indicator.receiverLabel = markerLabel;
            root.SetActive(true);
            return indicator;
        }

        private void CreateBorder(float width)
        {
            border = new GameObject("ChargeBorder", typeof(RectTransform)).GetComponent<RectTransform>();
            border.SetParent(transform, false);
            edgeLights = new Image[8];
            var min = new[] { Vector2.zero, Vector2.right, Vector2.zero, Vector2.up };
            var max = new[] { Vector2.up, Vector2.one, Vector2.right, Vector2.one };
            for (var i = 0; i < edgeLights.Length; i++)
            {
                var image = new GameObject("EdgeLight" + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(border, false);
                image.raycastTarget = false;
                var rect = image.rectTransform;
                var side = i % 4;
                rect.anchorMin = min[side];
                rect.anchorMax = max[side];
                rect.pivot = (min[side] + max[side]) * 0.5f;
                var thickness = i < 4 ? width : width / 3f;
                rect.sizeDelta = side < 2 ? new Vector2(thickness, 0f) : new Vector2(0f, thickness);
                edgeLights[i] = image;
            }
        }

        private Image Disc(string name, float size, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(presentation, false);
            image.sprite = sprite;
            image.raycastTarget = false; // EnhancedTouch captures only the fixed circular PASS control.
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * 0.5f;
            image.rectTransform.sizeDelta = Vector2.one * size;
            return image;
        }

        public void SetProgress(float quality, bool isCharging = false)
        {
            if (!float.IsFinite(quality) || quality < 0f || quality > 1f)
                throw new ArgumentOutOfRangeException(nameof(quality));
            progress = Eligible ? quality : 0f;
            charging = Eligible && isCharging;
            Refresh();
        }

        private void OnEnable() { owner.Resetting += ResetProgress; ResetProgress(); }
        private void OnDisable() { if (owner) owner.Resetting -= ResetProgress; ResetProgress(); }
        private void ResetProgress() => SetProgress(0f);
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            var available = Eligible;
            if (!available) { progress = 0f; charging = false; }
            var frozen = owner ? owner.ReceptionProbability : null;
            var displayed = charging ? progress : frozen.GetValueOrDefault();
            var feedback = charging || frozen.HasValue;
            var orange = new Color(1f, 0.5f, 0f);
            var color = displayed <= orangeThreshold ? Color.Lerp(Color.red, orange, displayed / orangeThreshold) :
                Color.Lerp(orange, Color.green, (displayed - orangeThreshold) / (1f - orangeThreshold));
            receiverBadge.color = feedback ? color : new Color(0.08f, 0.12f, 0.18f, 0.95f);
            receiverLabel.color = feedback ? Color.black : Color.white;
            receiverLabel.text = feedback ? $"L1\n{Mathf.RoundToInt(displayed * 100f)}%" : "L1";
            halo.gameObject.SetActive(feedback);
            for (var i = 0; i < edgeLights.Length; i++)
            {
                color.a = borderOpacity * (i < 4 ? 0.35f : 1f);
                edgeLights[i].color = color;
            }
            border.gameObject.SetActive(available && charging);
            color.a = 0.5f;
            halo.color = color;
            Halo.sizeDelta = Vector2.one * Mathf.Lerp(maximumHalo, diameter * readyScale, displayed);
            presentation.gameObject.SetActive(available);
            var showMarker = isActiveAndEnabled && owner && owner.IsInitialized && owner.Receiver &&
                driver && driver.IsPassingFrame && view && view.isActiveAndEnabled;
            marker.gameObject.SetActive(showMarker);
            if (!available && !showMarker) return;
            var span = safeArea.anchorMax - safeArea.anchorMin;
            if (span.x <= 0f || span.y <= 0f) CalibrationConflict();
            border.anchorMin = -safeArea.anchorMin / span;
            border.anchorMax = (Vector2.one - safeArea.anchorMin) / span;
            border.offsetMin = border.offsetMax = Vector2.zero;
            presentation.anchorMin = presentation.anchorMax = Vector2.right;
            presentation.anchoredPosition = buttonPosition;
            var center = safeArea.rect.max + new Vector2(buttonPosition.x, buttonPosition.y - safeArea.rect.height);
            var radius = Vector2.one * (diameter * 0.5f);
            if (available && (!safeArea.rect.Contains(center - radius) || !safeArea.rect.Contains(center + radius)))
                CalibrationConflict();
            if (!showMarker) return;
            var viewport = view.WorldToViewportPoint(owner.Receiver.position + offset);
            var anchor = (new Vector2(viewport.x, viewport.y) - safeArea.anchorMin) / span;
            center = safeArea.rect.min + Vector2.Scale(safeArea.rect.size, anchor);
            var halfMarker = feedback ? Vector2.one * (maximumHalo * 0.5f) : marker.sizeDelta * 0.5f;
            if (viewport.z < view.nearClipPlane || viewport.z > view.farClipPlane ||
                !safeArea.rect.Contains(center - halfMarker) || !safeArea.rect.Contains(center + halfMarker))
                CalibrationConflict();
            marker.anchorMin = marker.anchorMax = anchor;
            marker.anchoredPosition = Vector2.zero;
        }

        private void CalibrationConflict()
        {
            presentation.gameObject.SetActive(false);
            marker.gameObject.SetActive(false);
            border.gameObject.SetActive(false);
            throw new InvalidOperationException("PASS halo or L1 marker does not fit; calibrate HUD, field and frame together.");
        }
    }
}

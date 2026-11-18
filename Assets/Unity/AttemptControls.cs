using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace FlagFootball.Unity
{
    [DefaultExecutionOrder(-110)]
    public sealed class AttemptControls : MonoBehaviour
    {
        private SceneBootstrap owner;
        private Text startLabel;
        private bool hasFocus = true, paused;
        private double acceptTouchesAfter;
        public RectTransform StartRegion { get; private set; }
        public RectTransform RestartRegion { get; private set; }

        public static AttemptControls Create(SceneBootstrap owner, RectTransform safeArea)
        {
            var root = new GameObject("AttemptControls", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(safeArea, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var controls = root.AddComponent<AttemptControls>();
            controls.owner = owner;
            controls.StartRegion = controls.CreateRegion("Start", 128f);
            controls.RestartRegion = controls.CreateRegion("Restart", 288f);
            controls.startLabel = controls.StartRegion.GetComponent<Text>();
            root.SetActive(true);
            return controls;
        }

        private RectTransform CreateRegion(string label, float height)
        {
            var background = new GameObject(label + "Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(transform, false);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-244f, height);
            rect.sizeDelta = new Vector2(360f, 128f);
            var image = background.GetComponent<Image>();
            image.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            image.raycastTarget = false;
            var text = new GameObject(label, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(rect, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = label;
            return text.rectTransform;
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            if (owner) owner.Resetting += ResetInput;
            ResetInput();
            RefreshState();
        }

        private void OnDisable()
        {
            if (owner) owner.Resetting -= ResetInput;
            ResetInput();
            RefreshState();
            EnhancedTouchSupport.Disable();
        }

        private void ResetInput() => acceptTouchesAfter = InputState.currentTime;

        internal void RefreshState()
        {
            if (startLabel) startLabel.text = "Start | " +
                (!isActiveAndEnabled || !hasFocus || paused || !owner || !owner.IsInitialized ? "Unavailable" :
                    owner.State.ToString());
        }

        private bool FreshInside(EnhancedTouch touch, RectTransform region) =>
            touch.phase == TouchPhase.Began && touch.startTime > acceptTouchesAfter &&
            RectTransformUtility.RectangleContainsScreenPoint(region, touch.screenPosition, null);

        private void Update()
        {
            RefreshState();
            if (!hasFocus || paused || !owner || !owner.IsInitialized) return;
            // Reset wins the entire batch, regardless of finger enumeration order.
            foreach (var touch in EnhancedTouch.activeTouches)
            {
                if (!FreshInside(touch, RestartRegion)) continue;
                owner.RestartAttempt();
                return;
            }
            foreach (var touch in EnhancedTouch.activeTouches)
            {
                if (!FreshInside(touch, StartRegion)) continue;
                owner.StartAttempt();
                return;
            }
        }

        private void OnApplicationFocus(bool focus)
        {
            hasFocus = focus;
            ResetInput();
            RefreshState();
        }

        private void OnApplicationPause(bool pause)
        {
            paused = pause;
            ResetInput();
            RefreshState();
        }
    }
}

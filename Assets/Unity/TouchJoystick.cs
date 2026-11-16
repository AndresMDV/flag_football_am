using FlagFootball.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using PlanarVector = System.Numerics.Vector2;

namespace FlagFootball.Unity
{
    [DefaultExecutionOrder(-100)]
    public sealed class TouchJoystick : MonoBehaviour
    {
        private PlayerMovement player;
        private JoystickGesture gesture;
        private RectTransform pad, knob, safeArea;
        private Texture2D circleTexture;
        private Sprite circleSprite;
        private bool hasFocus = true, paused;
        private double acceptTouchesAfter;

        public static TouchJoystick Create(PlayerMovement player, float diameter, float padding, float sensitivity)
        {
            var root = new GameObject("TouchControls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.SetActive(false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var controls = root.AddComponent<TouchJoystick>();
            controls.player = player;
            controls.gesture = new JoystickGesture(sensitivity);
            controls.safeArea = new GameObject("TouchSafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            controls.safeArea.SetParent(root.transform, false);
            controls.safeArea.offsetMin = controls.safeArea.offsetMax = Vector2.zero;
            controls.CreateCircleSprite();
            controls.pad = controls.CreateDisc("MovementJoystick", controls.safeArea, diameter,
                new Color(0.08f, 0.12f, 0.18f, 0.7f));
            controls.pad.anchorMin = controls.pad.anchorMax = Vector2.zero;
            controls.pad.anchoredPosition = Vector2.one * (padding + diameter * 0.5f);
            controls.knob = controls.CreateDisc("JoystickHandle", controls.pad, diameter * 0.4f,
                new Color(1f, 1f, 1f, 0.85f));
            controls.knob.anchorMin = controls.knob.anchorMax = Vector2.one * 0.5f;
            controls.UpdateSafeArea();
            root.SetActive(true);
            Canvas.ForceUpdateCanvases();
            return controls;
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            ResetInput();
        }

        private void OnDisable()
        {
            ResetInput();
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            UpdateSafeArea();
            if (!hasFocus || paused || !player) return;
            var foundOwner = false;
            foreach (var touch in EnhancedTouch.activeTouches)
            {
                var key = ((long)touch.screen.deviceId << 32) | (uint)touch.touchId;
                if (gesture.Owns(key))
                {
                    foundOwner = true;
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) gesture.End(key);
                    else gesture.Move(key, Offset(touch.screenPosition));
                }
                else if (!gesture.IsCaptured && touch.phase == TouchPhase.Began && touch.startTime > acceptTouchesAfter)
                {
                    var offset = Offset(touch.screenPosition);
                    if (offset.LengthSquared() <= 1f) foundOwner = gesture.Begin(key, offset);
                }
            }
            if (gesture.IsCaptured && !foundOwner) ResetInput();
            var value = new Vector2(gesture.Value.X, gesture.Value.Y);
            player.SetMoveInput(value);
            knob.anchoredPosition = value * pad.rect.width * 0.3f;
        }

        private PlanarVector Offset(Vector2 position)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(pad, position, null, out var local);
            return new PlanarVector(local.x, local.y) / (pad.rect.width * 0.5f);
        }

        private void OnApplicationFocus(bool focus)
        {
            if (hasFocus == focus) return;
            hasFocus = focus;
            ResetInput();
        }

        private void OnApplicationPause(bool pause)
        {
            if (paused == pause) return;
            paused = pause;
            ResetInput();
        }

        private void ResetInput()
        {
            gesture?.Cancel();
            // Existing touches retain their original start time, including after re-enabling EnhancedTouch.
            acceptTouchesAfter = InputState.currentTime;
            if (player) player.SetMoveInput(Vector2.zero);
            if (knob) knob.anchoredPosition = Vector2.zero;
        }

        private void UpdateSafeArea()
        {
            if (!safeArea || Screen.width == 0 || Screen.height == 0) return;
            var area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        }

        private RectTransform CreateDisc(string name, Transform parent, float diameter, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = circleSprite;
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = Vector2.one * diameter;
            return image.rectTransform;
        }

        private void CreateCircleSprite()
        {
            circleTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * 32f);
                circleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(32f - distance)));
            }
            circleTexture.Apply();
            circleSprite = Sprite.Create(circleTexture, new Rect(0f, 0f, 64f, 64f), Vector2.one * 0.5f);
        }

        private void OnDestroy()
        {
            if (circleSprite) Destroy(circleSprite);
            if (circleTexture) Destroy(circleTexture);
        }
    }
}

using FlagFootball.Core;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace FlagFootball.Unity
{
    [DefaultExecutionOrder(-90)]
    public sealed class ChargedPassInput : MonoBehaviour
    {
        private SceneBootstrap owner;
        private PassIndicator indicator;
        private RectTransform canvas;
        private PassCharge charge;
        private double acceptTouchesAfter, ownerStartTime;
        private bool hasFocus = true, paused;

        internal static ChargedPassInput Create(SceneBootstrap owner, PassIndicator indicator, float duration, float cancelDistance)
        {
            var model = new PassCharge(duration, cancelDistance);
            var root = new GameObject("ChargedPassInput");
            root.SetActive(false);
            root.transform.SetParent(indicator.transform, false);
            var input = root.AddComponent<ChargedPassInput>();
            input.owner = owner;
            input.indicator = indicator;
            input.canvas = indicator.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            input.charge = model;
            root.SetActive(true);
            return input;
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            owner.Resetting += ResetInput;
            ResetInput();
        }

        private void OnDisable()
        {
            if (owner) owner.Resetting -= ResetInput;
            ResetInput();
            EnhancedTouchSupport.Disable();
        }

        private void ResetInput()
        {
            charge?.Reset();
            acceptTouchesAfter = InputState.currentTime;
            if (indicator) indicator.SetProgress(0f);
        }

        private void Update()
        {
            if (!owner || !indicator || !hasFocus || paused || !indicator.IsAvailable) { ResetInput(); return; }
            var foundOwner = false;
            foreach (var touch in EnhancedTouch.activeTouches)
            {
                var key = ((long)touch.screen.deviceId << 32) | (uint)touch.touchId;
                if (charge.Owns(key) && touch.startTime == ownerStartTime)
                {
                    foundOwner = true;
                    if (touch.phase == TouchPhase.Canceled) { ResetInput(); continue; }
                    charge.Advance(key, Time.deltaTime);
                    var position = FingerPosition(touch.screenPosition);
                    if (touch.phase == TouchPhase.Ended)
                    {
                        var quality = charge.Release(key, position);
                        ResetInput();
                        if (quality.HasValue) owner.LaunchPass(owner.Receiver, quality.Value);
                    }
                    else
                    {
                        charge.Move(key, position);
                        if (!charge.IsCaptured) ResetInput();
                        else indicator.SetProgress(charge.Quality, true);
                    }
                }
                else if (!charge.IsCaptured && indicator.IsAvailable && touch.phase == TouchPhase.Began &&
                    touch.startTime > acceptTouchesAfter && InsideButton(touch.screenPosition))
                {
                    foundOwner = charge.Begin(key, FingerPosition(touch.screenPosition));
                    ownerStartTime = touch.startTime;
                    indicator.SetProgress(0f, true);
                }
            }
            if (charge.IsCaptured && !foundOwner) ResetInput();
        }

        private System.Numerics.Vector2 FingerPosition(Vector2 screenPosition)
        {
            // Full-canvas reference coordinates do not move with the receiver/indicator.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenPosition, null, out var local);
            return new System.Numerics.Vector2(local.x, local.y);
        }

        private bool InsideButton(Vector2 position)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(indicator.Button, position, null, out var local);
            var radius = indicator.Button.rect.width * 0.5f;
            return local.sqrMagnitude <= radius * radius;
        }

        private void OnApplicationFocus(bool focus) { hasFocus = focus; ResetInput(); }
        private void OnApplicationPause(bool pause) { paused = pause; ResetInput(); }
    }
}

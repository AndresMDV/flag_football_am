using System;
using FlagFootball.Core;
using UnityEngine;

namespace FlagFootball.Unity
{
    public sealed class SceneBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerMovement quarterback;
        [SerializeField, Min(80f)] private float joystickDiameter = 240f;
        [SerializeField, Min(0f)] private float joystickPadding = 64f;
        [SerializeField, Min(0.1f)] private float joystickSensitivity = 1f;

        private Renderer ground;
        private Material originalGroundMaterial, gridMaterial;
        private Texture2D gridTexture;

        private readonly AttemptLifecycle lifecycle = new AttemptLifecycle();
        private TouchJoystick joystick;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool resetting, resetFailed;

        public bool IsInitialized { get; private set; }
        public PlayerMovement Quarterback => quarterback;
        public bool IsActive => lifecycle.State == AttemptState.Active;
        // Synchronous, registration-ordered, fail-fast callbacks while prepared and movement-blocked.
        public event Action Resetting;

        private void Awake()
        {
            if (!quarterback)
            {
                Debug.LogError("SceneBootstrap requires an explicit quarterback.", this);
                enabled = false;
                return;
            }
            quarterback.SetMovementAllowed(false);
            initialPosition = quarterback.transform.position;
            initialRotation = quarterback.transform.rotation;
        }

        private void Start()
        {
            joystick = TouchJoystick.Create(quarterback, joystickDiameter, joystickPadding, joystickSensitivity);
            AddFieldGrid();
            IsInitialized = true;
            Debug.Log("BASELINE_SCENE_READY");
        }

        // False for duplicate activation, incomplete composition, reset callbacks or a failed reset.
        public bool StartAttempt()
        {
            if (!IsInitialized || resetting || resetFailed || IsActive) return false;
            joystick.ResetInput();
            lifecycle.StartAttempt();
            quarterback.SetMovementAllowed(true);
            return true;
        }

        // True on completion, even when already prepared. Failures propagate and latch Start closed
        // until a successful retry; finally still restores the original QB pose and clears reentrancy.
        public bool RestartAttempt()
        {
            if (!IsInitialized || resetting) return false;
            resetting = resetFailed = true;
            quarterback.SetMovementAllowed(false);
            lifecycle.RestartAttempt();
            joystick.ResetInput();
            try
            {
                Resetting?.Invoke();
                resetFailed = false;
            }
            finally
            {
                quarterback.transform.SetPositionAndRotation(initialPosition, initialRotation);
                resetting = false;
            }
            return true;
        }

        private void AddFieldGrid()
        {
            var field = GameObject.Find("Ground");
            if (!field) return;
            ground = field.GetComponent<Renderer>();
            originalGroundMaterial = ground.sharedMaterial;
            gridMaterial = new Material(originalGroundMaterial);
            gridTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
                { name = "PrototypeFieldGrid", wrapMode = TextureWrapMode.Repeat };
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
                gridTexture.SetPixel(x, y, x < 2 || y < 2
                    ? new Color(0.26f, 0.34f, 0.28f) : new Color(0.15f, 0.2f, 0.17f));
            gridTexture.Apply(false, true);
            gridMaterial.SetColor("_BaseColor", Color.white);
            gridMaterial.SetTexture("_BaseMap", gridTexture);
            gridMaterial.SetTextureScale("_BaseMap", Vector2.one * 10f);
            ground.sharedMaterial = gridMaterial;
        }

        private void OnDestroy()
        {
            if (ground) ground.sharedMaterial = originalGroundMaterial;
            if (gridMaterial) Destroy(gridMaterial);
            if (gridTexture) Destroy(gridTexture);
        }
    }
}

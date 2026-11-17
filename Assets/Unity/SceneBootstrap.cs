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
        private Transform receiver, ball;
        private Material receiverMaterial, ballMaterial;
        private Vector3 initialReceiverPosition, heldPosition, heldScale;
        private Quaternion initialReceiverRotation, heldRotation;
        private bool resetting, resetFailed;

        public bool IsInitialized { get; private set; }
        public PlayerMovement Quarterback => quarterback;
        public Transform Receiver => receiver;
        public Transform Ball => ball;
        // Possession observes direct QB attachment; detached visuals have no owner.
        public Transform BallOwner => ball && ball.parent == quarterback.transform ? quarterback.transform : null;
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
            CreatePreparedActors();
            Camera.main.GetComponent<RunningCamera>().RefreshSubjectVisuals();
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
        // until a successful retry; finally restores both poses/possession and clears reentrancy.
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
                receiver.SetPositionAndRotation(initialReceiverPosition, initialReceiverRotation);
                ball.SetParent(quarterback.transform, false);
                ball.localPosition = heldPosition;
                ball.localRotation = heldRotation;
                ball.localScale = heldScale;
                resetting = false;
            }
            return true;
        }

        private void CreatePreparedActors()
        {
            receiver = CreateVisual("Receiver", PrimitiveType.Cube,
                new Color(0.1f, 0.55f, 0.9f), out receiverMaterial);
            receiver.SetParent(transform, false);
            receiver.SetPositionAndRotation(new Vector3(1.5f, 0.5f, 1f), Quaternion.identity);
            receiver.localScale = new Vector3(0.6f, 1f, 1f);
            initialReceiverPosition = receiver.position;
            initialReceiverRotation = receiver.rotation;

            ball = CreateVisual("Ball", PrimitiveType.Sphere,
                new Color(0.35f, 0.12f, 0.04f), out ballMaterial);
            ball.SetParent(quarterback.transform, false);
            ball.localPosition = new Vector3(0.8f, 0.1f, 0.2f);
            ball.localRotation = Quaternion.identity;
            var parentScale = quarterback.transform.lossyScale;
            ball.localScale = new Vector3(0.22f / parentScale.x, 0.18f / parentScale.y, 0.36f / parentScale.z);
            heldPosition = ball.localPosition;
            heldRotation = ball.localRotation;
            heldScale = ball.localScale;
        }

        private Transform CreateVisual(string role, PrimitiveType shape, Color color, out Material material)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = role;
            Destroy(visual.GetComponent<Collider>());
            material = new Material(quarterback.GetComponent<Renderer>().sharedMaterial) { name = role + "Material" };
            material.SetColor("_BaseColor", color);
            visual.GetComponent<Renderer>().sharedMaterial = material;
            return visual.transform;
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
            if (receiver) Destroy(receiver.gameObject);
            if (ball) Destroy(ball.gameObject);
            if (receiverMaterial) Destroy(receiverMaterial);
            if (ballMaterial) Destroy(ballMaterial);
            if (ground) ground.sharedMaterial = originalGroundMaterial;
            if (gridMaterial) Destroy(gridMaterial);
            if (gridTexture) Destroy(gridTexture);
        }
    }
}

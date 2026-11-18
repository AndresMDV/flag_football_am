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
        [SerializeField] private Vector2 receiverCutOffset = new Vector2(0f, 3f);
        [SerializeField] private Vector2 receiverEndOffset = new Vector2(3f, 3f);
        [SerializeField, Min(0.1f)] private float receiverSpeed = 3f;
        [SerializeField, Min(0.1f)] private float passSpeed = 8f;
        [SerializeField, Min(0.1f)] private float passArcHeight = 2.5f;
        [SerializeField, Min(0.01f)] private float minimumPassTime = 0.2f;
        [SerializeField, Min(1f)] private float passButtonDiameter = 96f;
        [SerializeField, Min(1f)] private float passHaloDiameter = 192f;
        [SerializeField] private Vector3 passIndicatorOffset = Vector3.up * 1.2f;
        [SerializeField, Range(0.01f, 0.99f)] private float orangeChargeThreshold = 0.5f;
        [SerializeField, Min(1f)] private float readyHaloScale = 1.1f;

        private Renderer ground;
        private Material originalGroundMaterial, gridMaterial;
        private Texture2D gridTexture;

        private readonly AttemptLifecycle lifecycle = new AttemptLifecycle();
        private TouchJoystick joystick;
        private AttemptControls actionControls;
        private RunningCamera cameraDriver;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Transform receiver, ball;
        private Material receiverMaterial, ballMaterial;
        private Vector3 initialReceiverPosition, heldPosition, heldScale;
        private Quaternion initialReceiverRotation, heldRotation;
        private bool resetting, resetFailed;
        private OneCutRoute receiverRoute;
        private PassFlight flight;

        public bool IsInitialized { get; private set; }
        public PlayerMovement Quarterback => quarterback;
        public Transform Receiver => receiver;
        public Transform Ball => ball;
        // Possession observes direct QB attachment; detached visuals have no owner.
        public Transform BallOwner => ball && ball.parent == quarterback.transform ? quarterback.transform : null;
        public AttemptState State => lifecycle.State;
        public bool IsActive => State == AttemptState.Active;
        public FlightEnd FlightOutcome => flight?.End ?? FlightEnd.None;
        public bool CanLaunchPass => IsInitialized && isActiveAndEnabled && !resetting && !resetFailed &&
            IsActive && ground && receiver && quarterback && flight == null && BallOwner == quarterback.transform;
        public Vector3 ReceiverVelocity
        {
            get
            {
                var v = IsActive && receiverRoute != null ? receiverRoute.Velocity : System.Numerics.Vector2.Zero;
                return new Vector3(v.X, 0f, v.Y);
            }
        }
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
            cameraDriver = Camera.main.GetComponent<RunningCamera>();
            cameraDriver.RefreshSubjectVisuals();
            cameraDriver.ConfigurePassing(this);
            IsInitialized = true;
            actionControls = AttemptControls.Create(this, joystick.SafeArea);
            PassIndicator.Create(this, cameraDriver, joystick.SafeArea, joystick.CircleSprite,
                passButtonDiameter, passHaloDiameter, passIndicatorOffset, orangeChargeThreshold, readyHaloScale);
            Debug.Log("BASELINE_SCENE_READY");
        }

        private void Update()
        {
            if (!IsActive || !receiver) return;
            var step = Time.deltaTime;
            if (flight != null)
            {
                var before = flight.Elapsed;
                flight.Step(step);
                step = (float)(flight.Elapsed - before);
                var p = flight.Position;
                ball.position = new Vector3(p.X, p.Y, p.Z);
            }
            receiverRoute.Step(step);
            var position = receiverRoute.Position;
            var facing = receiverRoute.Facing;
            receiver.SetPositionAndRotation(new Vector3(position.X, initialReceiverPosition.y, position.Y),
                Quaternion.LookRotation(new Vector3(facing.X, 0f, facing.Y)));
            if (FlightOutcome == FlightEnd.None) return;
            lifecycle.MarkIncomplete();
            quarterback.SetMovementAllowed(false);
            joystick.ResetInput();
            actionControls.RefreshState();
        }

        // Public release boundary; invalid calls never detach the held ball.
        public bool LaunchPass(Transform selectedReceiver, float quality)
        {
            if (!CanLaunchPass || !selectedReceiver || selectedReceiver != receiver ||
                !float.IsFinite(quality) || quality < 0f || quality > 1f) return false;
            var origin = ball.position;
            var target = receiver.position;
            var velocity = ReceiverVelocity;
            var bounds = ground.bounds;
            PassFlight candidate;
            try
            {
                candidate = new PassFlight(new System.Numerics.Vector3(origin.x, origin.y, origin.z),
                    new System.Numerics.Vector3(target.x, target.y, target.z),
                    new System.Numerics.Vector2(velocity.x, velocity.z), quality,
                    new System.Numerics.Vector2(bounds.min.x, bounds.min.z), new System.Numerics.Vector2(bounds.max.x, bounds.max.z),
                    passSpeed, passArcHeight, bounds.max.y + ball.GetComponent<Renderer>().bounds.extents.y, minimumPassTime);
            }
            catch (ArgumentException) { return false; }
            ball.SetParent(null, true);
            flight = candidate;
            return true;
        }

        // False for duplicate activation, incomplete composition, reset callbacks or a failed reset.
        public bool StartAttempt()
        {
            if (!IsInitialized || resetting || resetFailed || State != AttemptState.Preparation) return false;
            joystick.ResetInput();
            lifecycle.StartAttempt();
            receiverRoute.Start();
            quarterback.SetMovementAllowed(true);
            actionControls.RefreshState();
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
            flight = null;
            receiverRoute.Reset();
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
                if (cameraDriver) cameraDriver.RestorePreparedFrame();
                resetting = false;
                actionControls.RefreshState();
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
            var routeStart = new System.Numerics.Vector2(initialReceiverPosition.x, initialReceiverPosition.z);
            receiverRoute = new OneCutRoute(routeStart,
                routeStart + new System.Numerics.Vector2(receiverCutOffset.x, receiverCutOffset.y),
                routeStart + new System.Numerics.Vector2(receiverEndOffset.x, receiverEndOffset.y), receiverSpeed);

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
            IsInitialized = false;
            if (quarterback) quarterback.SetMovementAllowed(false);
            if (joystick)
            {
                joystick.gameObject.SetActive(false);
                Destroy(joystick.gameObject);
            }
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

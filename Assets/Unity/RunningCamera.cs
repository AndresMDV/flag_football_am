using FlagFootball.Core;
using UnityEngine;
using PlanarVector = System.Numerics.Vector2;

namespace FlagFootball.Unity
{
    [RequireComponent(typeof(Camera))]
    public sealed class RunningCamera : MonoBehaviour
    {
        [SerializeField] private PlayerMovement target;
        [SerializeField] private Renderer field;
        [SerializeField, Min(1f)] private float height = 7f;
        [SerializeField, Range(30f, 85f)] private float pitch = 60f;
        [SerializeField, Min(0.1f)] private float halfHeight = 2.2f;
        [SerializeField, Min(0.1f)] private float response = 8f;
        [SerializeField, Range(0f, 0.1f)] private float viewportMargin = 0.05f;

        private Camera view;
        private Renderer[] visuals;
        private Quaternion fixedRotation;
        private SceneBootstrap passingOwner;
        private Renderer[] passingVisuals;
        private bool passingFrame;
        public bool IsPassingFrame => isActiveAndEnabled && passingFrame && passingOwner;
        private Rect? testSafeViewport;
        [SerializeField, Min(0.1f)] private float passingHalfHeight = 3.5f;
        [SerializeField, Min(0f)] private float flightHeight = 4f;
        [SerializeField, Min(0f)] private float haloRadius = 96f;

        internal void ConfigurePassing(SceneBootstrap owner)
        {
            passingOwner = owner;
            passingVisuals = new[] { owner.Receiver.GetComponent<Renderer>(), owner.Ball.GetComponent<Renderer>() };
            RestorePreparedFrame();
        }

        // Explicit selection only; automatic catch-to-running blending belongs to a later slice.
        public void UseRunningFrame()
        {
            passingFrame = false;
            transform.position = DesiredPosition();
            Frame(0f);
        }

        internal void RestorePreparedFrame()
        {
            passingFrame = true;
            testSafeViewport = null;
            FramePassing(0f, true);
        }

        // Verification scaffolding: positions the visible ball, never simulates or launches flight.
        public void SetBallFramingTestInput(Vector3 position, Rect safeViewport)
        {
            if (!passingOwner || !float.IsFinite(position.x) || !float.IsFinite(position.y) ||
                !float.IsFinite(position.z) || !float.IsFinite(safeViewport.x) ||
                !float.IsFinite(safeViewport.y) || !float.IsFinite(safeViewport.width) ||
                !float.IsFinite(safeViewport.height) || safeViewport.xMin < 0f || safeViewport.yMin < 0f ||
                safeViewport.xMax > 1f || safeViewport.yMax > 1f ||
                safeViewport.width < 0.5f || safeViewport.height < 0.7f)
                throw new System.ArgumentException("A live attempt, finite ball and usable normalized safe area are required.");
            testSafeViewport = safeViewport;
            passingOwner.Ball.SetParent(null, true);
            passingOwner.Ball.position = position;
        }

        private void FramePassing(float deltaTime, bool snap = false)
        {
            var area = testSafeViewport ?? new Rect(Screen.safeArea.x / Screen.width,
                Screen.safeArea.y / Screen.height, Screen.safeArea.width / Screen.width,
                Screen.safeArea.height / Screen.height);
            var padding = new Vector2(haloRadius / (1080f * view.aspect), haloRadius / 1080f);
            var bottom = Mathf.Max(area.height * 0.36f, 384f / 1080f);
            var region = new Rect(area.x + area.width * 0.04f + padding.x,
                area.y + bottom + padding.y,
                area.width * 0.92f - 2f * padding.x, area.height * 0.96f - bottom - 2f * padding.y);
            if (region.width <= 0f || region.height <= 0f)
                throw new System.InvalidOperationException("Passing frame cannot reserve the configured halo in this safe area.");
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            void Include(Vector3 point)
            {
                var local = Quaternion.Inverse(fixedRotation) * point;
                minimum = Vector2.Min(minimum, new Vector2(local.x, local.y));
                maximum = Vector2.Max(maximum, new Vector2(local.x, local.y));
            }
            void IncludeBounds(Bounds bounds)
            {
                for (var corner = 0; corner < 8; corner++)
                    Include(new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z));
            }
            foreach (var visual in visuals) if (visual) IncludeBounds(visual.bounds);
            foreach (var visual in passingVisuals) if (visual) IncludeBounds(visual.bounds);
            var low = Vector3.Min(Vector3.Min(target.transform.position, passingOwner.Receiver.position),
                passingOwner.Ball.position) - Vector3.one * 0.2f;
            var high = Vector3.Max(Vector3.Max(target.transform.position, passingOwner.Receiver.position),
                passingOwner.Ball.position) + Vector3.one * 0.2f;
            low.y = 0f;
            high.y = flightHeight + 0.2f;
            IncludeBounds(new Bounds((low + high) * 0.5f, high - low));
            Include(passingOwner.Receiver.position + Vector3.up * 1.2f);
            var needed = Mathf.Max(passingHalfHeight, (maximum.x - minimum.x) / (2f * view.aspect * region.width),
                (maximum.y - minimum.y) / (2f * region.height));
            var blend = snap ? 1f : 1f - Mathf.Exp(-response * deltaTime);
            view.orthographic = true;
            view.orthographicSize = Mathf.Max(needed, Mathf.Lerp(view.orthographicSize, needed, blend));
            var scale = new Vector2(2f * view.orthographicSize * view.aspect, 2f * view.orthographicSize);
            var desired = (minimum + maximum) * 0.5f - Vector2.Scale(region.center - Vector2.one * 0.5f, scale);
            var previous = Quaternion.Inverse(fixedRotation) * transform.position;
            var center = Vector2.Lerp(new Vector2(previous.x, previous.y), desired, blend);
            var lower = maximum - Vector2.Scale(region.max - Vector2.one * 0.5f, scale);
            var upper = minimum - Vector2.Scale(region.min - Vector2.one * 0.5f, scale);
            center = Vector2.Max(lower, Vector2.Min(upper, center));
            transform.SetPositionAndRotation(fixedRotation * new Vector3(center.x, center.y, -30f), fixedRotation);
        }

        private void Awake()
        {
            if (!target || !field)
            {
                Debug.LogError("RunningCamera requires a player and a flat, axis-aligned field.", this);
                enabled = false;
                return;
            }
            view = GetComponent<Camera>();
            RefreshSubjectVisuals();
            fixedRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Bootstrap adds the held ball after Awake; refresh once after that composition.
        internal void RefreshSubjectVisuals() => visuals = target.GetComponentsInChildren<Renderer>();

        private void Start()
        {
            if (passingFrame) FramePassing(0f, true);
            else { transform.position = DesiredPosition(); Frame(0f); }
        }

        private void LateUpdate()
        {
            if (!target || !field) return;
            if (passingFrame && passingOwner) FramePassing(Time.deltaTime);
            else Frame(Time.deltaTime);
        }

        private Vector3 DesiredPosition() => target.transform.position +
            new Vector3(0f, height, -height / Mathf.Tan(pitch * Mathf.Deg2Rad));

        private void Frame(float deltaTime)
        {
            transform.rotation = fixedRotation;
            var desired = DesiredPosition();
            transform.position = new Vector3(transform.position.x, desired.y, transform.position.z);
            view.orthographic = true;
            var bounds = field.bounds;
            var sizeLimit = Mathf.Min(bounds.size.x / (2f * view.aspect),
                bounds.size.z * Mathf.Sin(pitch * Mathf.Deg2Rad) * 0.5f);
            view.orthographicSize = Mathf.Min(halfHeight, sizeLimit);
            ViewFootprint(bounds.center.y, out var viewMinimum, out var viewMaximum);
            SubjectFootprint(bounds.center.y, out var subjectMinimum, out var subjectMaximum);
            var visibleMinimum = PlanarVector.Lerp(viewMinimum, viewMaximum, viewportMargin);
            var visibleMaximum = PlanarVector.Lerp(viewMinimum, viewMaximum, 1f - viewportMargin);
            var position = BoundedCameraFollow.Step(Planar(transform.position), Planar(desired),
                Planar(bounds.min) - viewMinimum, Planar(bounds.max) - viewMaximum,
                subjectMaximum - visibleMaximum, subjectMinimum - visibleMinimum, response, deltaTime);
            transform.position = new Vector3(position.X, desired.y, position.Y);
        }

        private void ViewFootprint(float groundY, out PlanarVector minimum, out PlanarVector maximum)
        {
            minimum = new PlanarVector(float.PositiveInfinity);
            maximum = new PlanarVector(float.NegativeInfinity);
            for (var x = 0; x <= 1; x++)
            for (var y = 0; y <= 1; y++)
            {
                var ray = view.ViewportPointToRay(new Vector3(x, y, 0f));
                var point = ray.GetPoint((groundY - ray.origin.y) / ray.direction.y);
                var offset = Planar(point - transform.position);
                minimum = PlanarVector.Min(minimum, offset);
                maximum = PlanarVector.Max(maximum, offset);
            }
        }

        private void SubjectFootprint(float groundY, out PlanarVector minimum, out PlanarVector maximum)
        {
            minimum = new PlanarVector(float.PositiveInfinity);
            maximum = new PlanarVector(float.NegativeInfinity);
            var forward = transform.forward;
            var slope = Planar(forward) / -forward.y;
            foreach (var visual in visuals)
            {
                if (!visual) continue;
                var bounds = visual.bounds;
                // Project the entire renderer AABB onto the ground along the orthographic view rays.
                var center = Planar(bounds.center) + slope * (bounds.center.y - groundY);
                var extent = Planar(bounds.extents) + PlanarVector.Abs(slope) * bounds.extents.y;
                minimum = PlanarVector.Min(minimum, center - extent);
                maximum = PlanarVector.Max(maximum, center + extent);
            }
        }

        private static PlanarVector Planar(Vector3 value) => new PlanarVector(value.x, value.z);
    }
}

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

        private void Awake()
        {
            if (!target || !field)
            {
                Debug.LogError("RunningCamera requires a player and a flat, axis-aligned field.", this);
                enabled = false;
                return;
            }
            view = GetComponent<Camera>();
            visuals = target.GetComponentsInChildren<Renderer>();
            fixedRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Start()
        {
            transform.position = DesiredPosition();
            Frame(0f);
        }

        private void LateUpdate()
        {
            if (target && field) Frame(Time.deltaTime);
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

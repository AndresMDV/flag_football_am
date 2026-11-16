using FlagFootball.Core;
using UnityEngine;
using PlanarVector = System.Numerics.Vector2;

namespace FlagFootball.Unity
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private Camera movementView;
        [SerializeField, Min(0f)] private float maximumSpeed = 5f;
        [SerializeField] private Vector2 fieldMinimum = new Vector2(-10f, -10f);
        [SerializeField] private Vector2 fieldMaximum = new Vector2(10f, 10f);
        [SerializeField, Min(0f)] private float footprintRadius = 2f;

        private BoundedMovement movement;
        private Vector2 moveInput;

        public Vector2 MinimumPosition => fieldMinimum + Vector2.one * footprintRadius;
        public Vector2 MaximumPosition => fieldMaximum - Vector2.one * footprintRadius;

        // Screen right/up, with radial magnitude controlling speed. Zero releases movement.
        public void SetMoveInput(Vector2 input) => moveInput = input;

        private void Awake()
        {
            if (!movementView)
            {
                Debug.LogError("PlayerMovement requires a movement camera.", this);
                enabled = false;
                return;
            }
            var inset = Vector2.one * footprintRadius;
            movement = new BoundedMovement(ToPlanar(transform.position),
                new PlanarVector(fieldMinimum.x + inset.x, fieldMinimum.y + inset.y),
                new PlanarVector(fieldMaximum.x - inset.x, fieldMaximum.y - inset.y), maximumSpeed);
        }

        private void Update()
        {
            if (!movementView) { moveInput = Vector2.zero; return; }
            var forward = Vector3.ProjectOnPlane(movementView.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(movementView.transform.up, Vector3.up);
            movement.PlaceAt(ToPlanar(transform.position));
            movement.Step(new PlanarVector(moveInput.x, moveInput.y), ToPlanar(forward), Time.deltaTime);
            var next = new Vector3(movement.Position.X, transform.position.y, movement.Position.Y);
            if ((next - transform.position).sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(new Vector3(movement.Facing.X, 0f, movement.Facing.Y));
            transform.position = next;
        }

        private void OnDisable() => moveInput = Vector2.zero;

        private static PlanarVector ToPlanar(Vector3 value) => new PlanarVector(value.x, value.z);
    }
}

using System;
using System.Numerics;

namespace FlagFootball.Core
{
    public sealed class BoundedMovement
    {
        private readonly Vector2 minimum;
        private readonly Vector2 maximum;
        private readonly float maximumSpeed;

        public Vector2 Position { get; private set; }
        public Vector2 Facing { get; private set; } = Vector2.UnitY;

        public BoundedMovement(Vector2 position, Vector2 minimum, Vector2 maximum, float maximumSpeed)
        {
            RequireFinite(minimum);
            RequireFinite(maximum);
            if (minimum.X >= maximum.X || minimum.Y >= maximum.Y ||
                !IsFinite(maximumSpeed) || maximumSpeed < 0f)
                throw new ArgumentOutOfRangeException(nameof(maximumSpeed), "Use ordered bounds and a finite nonnegative speed.");
            this.minimum = minimum;
            this.maximum = maximum;
            this.maximumSpeed = maximumSpeed;
            PlaceAt(position);
        }

        public void PlaceAt(Vector2 position)
        {
            RequireFinite(position);
            Position = Vector2.Min(maximum, Vector2.Max(minimum, position));
        }

        public void Step(Vector2 input, Vector2 viewForward, float deltaTime)
        {
            RequireFinite(input);
            RequireFinite(viewForward);
            if (!IsFinite(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            var magnitude = Math.Sqrt((double)input.X * input.X + (double)input.Y * input.Y);
            input *= (float)(1d / Math.Max(1d, magnitude));
            viewForward = viewForward.LengthSquared() > 0f ? Vector2.Normalize(viewForward) : Vector2.UnitY;
            var right = new Vector2(viewForward.Y, -viewForward.X);
            var requested = (right * input.X + viewForward * input.Y) * maximumSpeed * deltaTime;
            var next = Vector2.Min(maximum, Vector2.Max(minimum, Position + requested));
            var travel = next - Position;
            if (travel.LengthSquared() > 0f) Facing = Vector2.Normalize(travel);
            Position = next;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void RequireFinite(Vector2 value)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y))
                throw new ArgumentOutOfRangeException(nameof(value), "Use finite planar coordinates.");
        }
    }
}

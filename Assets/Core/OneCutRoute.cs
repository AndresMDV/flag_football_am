using System;
using System.Numerics;

namespace FlagFootball.Core
{
    public sealed class OneCutRoute
    {
        private readonly Vector2 start, cut, end, firstDirection, lastDirection;
        private readonly double firstLength, lastLength, totalLength, speed;
        private double distance;
        private bool started;

        public Vector2 Position { get; private set; }
        public Vector2 Facing { get; private set; } = Vector2.UnitY;

        public OneCutRoute(Vector2 start, Vector2 cut, Vector2 end, float speed)
        {
            RequireFinite(start);
            RequireFinite(cut);
            RequireFinite(end);
            if (!IsFinite(speed) || speed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(speed));
            firstLength = Length(start, cut);
            lastLength = Length(cut, end);
            if (firstLength == 0d || lastLength == 0d)
                throw new ArgumentOutOfRangeException(nameof(cut), "Use two nonzero route legs.");
            firstDirection = Direction(start, cut, firstLength);
            lastDirection = Direction(cut, end, lastLength);
            if ((double)firstDirection.X * lastDirection.Y == (double)firstDirection.Y * lastDirection.X)
                throw new ArgumentOutOfRangeException(nameof(cut), "Use one noncollinear cut.");
            this.start = start;
            this.cut = cut;
            this.end = end;
            this.speed = speed;
            totalLength = firstLength + lastLength;
            Position = start;
        }

        public bool Start()
        {
            if (started) return false;
            started = true;
            return true;
        }

        public void Reset()
        {
            started = false;
            distance = 0d;
            Position = start;
            Facing = Vector2.UnitY;
        }

        public void Step(float deltaTime)
        {
            if (!IsFinite(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!started || deltaTime == 0f || distance == totalLength) return;
            distance = Math.Min(totalLength, distance + speed * deltaTime);
            // Distance, rather than per-leg frame time, preserves overshoot at the cut.
            if (distance == totalLength)
            {
                Position = end;
                Facing = lastDirection;
            }
            else if (distance <= firstLength)
            {
                Position = Interpolate(start, cut, distance / firstLength);
                Facing = firstDirection;
            }
            else
            {
                Position = Interpolate(cut, end, (distance - firstLength) / lastLength);
                Facing = lastDirection;
            }
        }

        private static double Length(Vector2 a, Vector2 b) =>
            Math.Sqrt(Math.Pow((double)b.X - a.X, 2d) + Math.Pow((double)b.Y - a.Y, 2d));

        private static Vector2 Direction(Vector2 a, Vector2 b, double length) =>
            new Vector2((float)(((double)b.X - a.X) / length), (float)(((double)b.Y - a.Y) / length));

        private static Vector2 Interpolate(Vector2 a, Vector2 b, double fraction) =>
            new Vector2((float)(a.X + ((double)b.X - a.X) * fraction),
                (float)(a.Y + ((double)b.Y - a.Y) * fraction));

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void RequireFinite(Vector2 value)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y))
                throw new ArgumentOutOfRangeException(nameof(value), "Use finite planar coordinates.");
        }
    }
}

using System;
using System.Numerics;

namespace FlagFootball.Core
{
    public enum FlightEnd { None, Ground, FieldExit }

    // A release-time snapshot. Sampling never consults a route or a live target.
    public sealed class PassFlight
    {
        private readonly Vector3 release;
        private readonly double dx, dz, dy, arc;
        private readonly float groundHeight;
        private readonly FlightEnd endReason;

        public double AimTime { get; }
        public double EndTime { get; }
        public double Elapsed { get; private set; }
        public FlightEnd End => Elapsed >= EndTime ? endReason : FlightEnd.None;
        public Vector3 Position => Sample(Elapsed);

        public PassFlight(Vector3 release, Vector3 receiver, Vector2 velocity, float quality,
            Vector2 minimum, Vector2 maximum, float speed = 8f, float arcHeight = 2.5f,
            float groundHeight = 0.09f, float minimumTime = 0.2f)
        {
            if (!Finite(release.X, release.Y, release.Z, receiver.X, receiver.Y, receiver.Z,
                velocity.X, velocity.Y, quality, minimum.X, minimum.Y, maximum.X, maximum.Y,
                speed, arcHeight, groundHeight, minimumTime))
                throw new ArgumentException("Use finite release, motion, quality, field and flight values.");
            if (quality < 0f || quality > 1f || speed <= 0f || arcHeight <= 0f || minimumTime <= 0f)
                throw new ArgumentException("Quality must be normalized; speed, arc height and minimum time must be positive.");
            if (minimum.X >= maximum.X || minimum.Y >= maximum.Y ||
                release.X < minimum.X || release.X > maximum.X || release.Z < minimum.Y || release.Z > maximum.Y ||
                release.Y <= groundHeight || receiver.Y <= groundHeight)
                throw new ArgumentException("Release must be inside the field and both sampled positions above contact height.");
            if (Math.Max((double)release.Y, receiver.Y) + arcHeight > float.MaxValue)
                throw new ArgumentException("The configured arc exceeds finite world coordinates.");

            this.release = release;
            this.groundHeight = groundHeight;
            arc = 4d * arcHeight;
            dy = (double)receiver.Y - release.Y;
            var lead = Intercept((double)receiver.X - release.X, (double)receiver.Z - release.Z,
                velocity, speed, minimumTime);
            var range = 0.5d + 0.5d * quality;
            dx = (receiver.X + velocity.X * lead - release.X) * range;
            dz = (receiver.Z + velocity.Y * lead - release.Z) * range;
            AimTime = Math.Max(minimumTime, Math.Sqrt(dx * dx + dz * dz) / speed);

            // y(u) = release.y + (dy + arc)u - arc*u². Continue past the aim until contact.
            var b = dy + arc;
            var c = (double)release.Y - groundHeight;
            var root = Math.Sqrt(b * b + 4d * arc * c);
            var groundTime = AimTime * (b >= 0d ? (b + root) / (2d * arc) : 2d * c / (root - b));
            var exitTime = Math.Min(ExitTime(release.X, dx, minimum.X, maximum.X),
                ExitTime(release.Z, dz, minimum.Y, maximum.Y));
            EndTime = Math.Min(groundTime, exitTime);
            // Ground wins exact ties; later catch integration can compare against this event time.
            endReason = exitTime < groundTime ? FlightEnd.FieldExit : FlightEnd.Ground;
        }

        public void Step(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            Elapsed = Math.Min(EndTime, Elapsed + deltaTime);
        }

        public Vector3 Sample(double seconds)
        {
            if (!double.IsFinite(seconds) || seconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            var time = Math.Min(seconds, EndTime);
            var u = time / AimTime;
            var y = time == EndTime && endReason == FlightEnd.Ground
                ? groundHeight : (float)(release.Y + dy * u + arc * u * (1d - u));
            return new Vector3((float)(release.X + dx * u), y, (float)(release.Z + dz * u));
        }

        private double ExitTime(double start, double displacement, double minimum, double maximum) =>
            displacement == 0d ? double.PositiveInfinity : AimTime *
                ((displacement > 0d ? maximum : minimum) - start) / displacement;

        private static double Intercept(double x, double z, Vector2 velocity, double speed, double minimumTime)
        {
            var atMinimumX = x + velocity.X * minimumTime;
            var atMinimumZ = z + velocity.Y * minimumTime;
            if (atMinimumX * atMinimumX + atMinimumZ * atMinimumZ <= speed * speed * minimumTime * minimumTime)
                return minimumTime;
            var a = (double)velocity.X * velocity.X + (double)velocity.Y * velocity.Y - speed * speed;
            var b = 2d * (x * velocity.X + z * velocity.Y);
            var c = x * x + z * z;
            if (a == 0d)
            {
                if (b < 0d && -c / b >= minimumTime) return -c / b;
            }
            else
            {
                var discriminant = b * b - 4d * a * c;
                if (discriminant >= 0d)
                {
                    // Stable quadratic roots, including fast approaching targets with two forward roots.
                    var q = -0.5d * (b + (b >= 0d ? 1d : -1d) * Math.Sqrt(discriminant));
                    var first = q / a;
                    var second = q == 0d ? double.PositiveInfinity : c / q;
                    var time = Math.Min(first >= minimumTime ? first : double.PositiveInfinity,
                        second >= minimumTime ? second : double.PositiveInfinity);
                    if (double.IsFinite(time)) return time;
                }
            }
            throw new ArgumentException("The sampled motion has no reachable forward intercept at these flight settings.");
        }

        private static bool Finite(params float[] values)
        {
            foreach (var value in values) if (!float.IsFinite(value)) return false;
            return true;
        }
    }
}

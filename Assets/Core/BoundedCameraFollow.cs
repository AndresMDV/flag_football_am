using System;
using System.Numerics;

namespace FlagFootball.Core
{
    public static class BoundedCameraFollow
    {
        // Both intervals describe legal camera positions, not world-space field/subject extents.
        public static Vector2 Step(Vector2 current, Vector2 desired,
            Vector2 legalMinimum, Vector2 legalMaximum, Vector2 visibleMinimum, Vector2 visibleMaximum,
            float response, float deltaTime)
        {
            if (float.IsNaN(response) || float.IsInfinity(response) || response < 0f ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            var minimum = Vector2.Max(legalMinimum, visibleMinimum);
            var maximum = Vector2.Min(legalMaximum, visibleMaximum);
            if (minimum.X > maximum.X || minimum.Y > maximum.Y)
                throw new InvalidOperationException("Camera framing cannot fit: increase the movement inset or reduce the view/margin.");
            var blend = (float)(1d - Math.Exp(-response * deltaTime));
            var smoothed = Vector2.Lerp(current, desired, blend);
            return Vector2.Min(maximum, Vector2.Max(minimum, smoothed));
        }
    }
}

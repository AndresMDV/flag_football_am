using System;
using System.Numerics;

namespace FlagFootball.Core
{
    public sealed class PassCharge
    {
        private readonly double duration, readyDuration, period;
        private readonly float cancelDistance;
        private long? owner;
        private Vector2 origin;
        private double elapsed;

        public bool IsCaptured => owner.HasValue;
        public float Quality
        {
            get
            {
                var value = elapsed <= duration ? elapsed / duration :
                    elapsed <= duration + readyDuration ? 1d : (period - elapsed) / duration;
                return (float)(Math.Round(value * 100d, MidpointRounding.AwayFromZero) / 100d);
            }
        }
        public bool Owns(long pointer) => owner == pointer;

        public PassCharge(float duration = 1f, float cancelDistance = 80f, float readyDuration = 0.2f)
        {
            if (!float.IsFinite(duration) || duration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));
            if (!float.IsFinite(cancelDistance) || cancelDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cancelDistance));
            if (!float.IsFinite(readyDuration) || readyDuration < 0f)
                throw new ArgumentOutOfRangeException(nameof(readyDuration));
            this.duration = duration;
            this.readyDuration = readyDuration;
            period = 2d * duration + readyDuration;
            this.cancelDistance = cancelDistance;
        }

        public bool Begin(long pointer, Vector2 position)
        {
            RequirePosition(position);
            if (IsCaptured) return false;
            owner = pointer;
            origin = position;
            elapsed = 0d;
            return true;
        }

        public void Advance(long pointer, float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (Owns(pointer)) elapsed = (elapsed + deltaTime) % period;
        }

        // Coordinates are stable finger-space values, never indicator-relative positions.
        public void Move(long pointer, Vector2 position)
        {
            RequirePosition(position);
            if (Owns(pointer) && (double)origin.Y - position.Y >= cancelDistance) Reset();
        }

        // A zero quality is a valid release; null means no release should be committed.
        public float? Release(long pointer, Vector2 position)
        {
            Move(pointer, position); // A final downward displacement can cancel even without a Move event.
            if (!Owns(pointer)) return null;
            var quality = Quality;
            Reset();
            return quality;
        }

        public bool Cancel(long pointer)
        {
            if (!Owns(pointer)) return false;
            Reset();
            return true;
        }

        // Lifecycle authority, independent of any pointer event; the adapter owns freshness gating.
        public void Reset()
        {
            owner = null;
            origin = default;
            elapsed = 0d;
        }

        private static void RequirePosition(Vector2 position)
        {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
                throw new ArgumentOutOfRangeException(nameof(position));
        }
    }
}

using System;
using System.Numerics;

namespace FlagFootball.Core
{
    public sealed class JoystickGesture
    {
        private readonly float sensitivity;
        private long? owner;
        public bool IsCaptured => owner.HasValue;
        public Vector2 Value { get; private set; }

        public JoystickGesture(float sensitivity)
        {
            if (float.IsNaN(sensitivity) || float.IsInfinity(sensitivity) || sensitivity <= 0f)
                throw new ArgumentOutOfRangeException(nameof(sensitivity));
            this.sensitivity = sensitivity;
        }

        public bool Owns(long pointer) => owner == pointer;

        public bool Begin(long pointer, Vector2 offsetInRadii)
        {
            if (IsCaptured) return false;
            owner = pointer;
            Move(pointer, offsetInRadii);
            return true;
        }

        public void Move(long pointer, Vector2 offsetInRadii)
        {
            if (!Owns(pointer)) return;
            var scaled = offsetInRadii * sensitivity;
            Value = scaled / Math.Max(1f, scaled.Length());
        }

        public void End(long pointer)
        {
            if (Owns(pointer)) Cancel();
        }

        public void Cancel()
        {
            owner = null;
            Value = Vector2.Zero;
        }
    }
}

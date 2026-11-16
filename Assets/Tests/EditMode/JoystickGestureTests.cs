using System.Numerics;
using FlagFootball.Core;
using NUnit.Framework;

namespace FlagFootball.Tests.EditMode
{
    public sealed class JoystickGestureTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.25f, 0.25f)]
        [TestCase(1f, 1f)]
        [TestCase(3f, 1f)]
        public void Distance_ScalesOutputAndCapsOverdrag(float distance, float expected)
        {
            var gesture = new JoystickGesture(1f);
            Assert.That(gesture.Begin(1, Vector2.UnitX * distance), Is.True);
            Assert.That(gesture.Value.X, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Overdrag_StillChangesDirectionAndCapsDiagonals()
        {
            var gesture = new JoystickGesture(1f);
            gesture.Begin(1, new Vector2(3f, 0f));
            gesture.Move(1, new Vector2(0f, -3f));
            Assert.That(gesture.Value, Is.EqualTo(-Vector2.UnitY));
            gesture.Move(1, new Vector2(3f, 3f));
            Assert.That(gesture.Value.Length(), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void OtherTouches_CannotStealMoveOrReleaseTheOwner()
        {
            var gesture = new JoystickGesture(1f);
            gesture.Begin(1, Vector2.UnitX);
            Assert.That(gesture.Begin(2, Vector2.UnitY), Is.False);
            gesture.Move(2, Vector2.UnitY);
            gesture.End(2);
            Assert.That(gesture.Value, Is.EqualTo(Vector2.UnitX));
            gesture.End(1);
            Assert.That(gesture.Value, Is.EqualTo(Vector2.Zero));
            Assert.That(gesture.IsCaptured, Is.False);
        }

        [Test]
        public void Cancel_RequiresANewBeginAndSensitivityIsConfigurable()
        {
            var gesture = new JoystickGesture(2f);
            gesture.Begin(1, Vector2.UnitX * 0.25f);
            Assert.That(gesture.Value.X, Is.EqualTo(0.5f));
            gesture.Cancel();
            gesture.Move(1, Vector2.UnitX);
            Assert.That(gesture.Value, Is.EqualTo(Vector2.Zero));
            Assert.That(gesture.Begin(2, Vector2.UnitY), Is.True);
            Assert.That(gesture.Value, Is.EqualTo(Vector2.UnitY));
        }
    }
}

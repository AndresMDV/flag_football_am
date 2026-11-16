using System.Numerics;
using FlagFootball.Core;
using NUnit.Framework;

namespace FlagFootball.Tests.EditMode
{
    public sealed class BoundedMovementTests
    {
        private static BoundedMovement Create() => new BoundedMovement(
            Vector2.Zero, new Vector2(-4.25f), new Vector2(4.25f), 5f);

        [TestCase(0.25f, 1.25f)]
        [TestCase(1f, 5f)]
        [TestCase(3f, 5f)]
        public void InputMagnitude_ScalesSpeedAndCapsAtMaximum(float input, float distance)
        {
            var movement = Create();
            movement.Step(new Vector2(0f, input), Vector2.UnitY, 0.5f);
            Assert.That(movement.Position.Y, Is.EqualTo(distance * 0.5f).Within(0.0001f));
        }

        [Test]
        public void DiagonalInput_DoesNotExceedMaximumSpeed()
        {
            var movement = Create();
            movement.Step(Vector2.One, Vector2.UnitY, 0.5f);
            Assert.That(movement.Position.Length(), Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void Input_UsesViewAxesRatherThanPreviousFacing()
        {
            var movement = Create();
            movement.Step(Vector2.UnitY, Vector2.UnitX * 2f, 0.2f);
            Assert.That(movement.Position, Is.EqualTo(Vector2.UnitX));
            movement.Step(Vector2.UnitX, Vector2.UnitX, 0.2f);
            Assert.That(movement.Position, Is.EqualTo(new Vector2(1f, -1f)));
            Assert.That(movement.Facing, Is.EqualTo(-Vector2.UnitY));
        }

        [Test]
        public void DirectionChangeAndNeutral_AreImmediateAndKeepLastFacing()
        {
            var movement = Create();
            movement.Step(Vector2.UnitY, Vector2.UnitY, 0.2f);
            movement.Step(-Vector2.UnitY, Vector2.UnitY, 0.2f);
            Assert.That(movement.Position, Is.EqualTo(Vector2.Zero));
            Assert.That(movement.Facing, Is.EqualTo(-Vector2.UnitY));
            movement.Step(Vector2.Zero, Vector2.UnitX, 1f);
            Assert.That(movement.Position, Is.EqualTo(Vector2.Zero));
            Assert.That(movement.Facing, Is.EqualTo(-Vector2.UnitY));
        }

        [TestCase(1f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(0f, -1f)]
        [TestCase(1f, 1f)]
        [TestCase(1f, -1f)]
        [TestCase(-1f, 1f)]
        [TestCase(-1f, -1f)]
        public void EdgesAndCorners_BlockOutwardMotionAndAllowReturn(float x, float y)
        {
            var movement = Create();
            var input = new Vector2(x, y);
            movement.Step(input, Vector2.UnitY, 10f);
            Assert.That(movement.Position, Is.EqualTo(input * 4.25f));
            var edge = movement.Position;
            var facing = movement.Facing;
            movement.Step(input, Vector2.UnitY, 1f);
            Assert.That(movement.Position, Is.EqualTo(edge));
            Assert.That(movement.Facing, Is.EqualTo(facing));
            movement.Step(-input, Vector2.UnitY, 0.1f);
            Assert.That(movement.Position.Length(), Is.LessThan(edge.Length()));
        }

        [Test]
        public void TangentialMotion_FacesActualTravelAlongBoundary()
        {
            var movement = Create();
            movement.Step(Vector2.UnitX, Vector2.UnitY, 10f);
            movement.Step(Vector2.One, Vector2.UnitY, 0.1f);
            Assert.That(movement.Facing.X, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(movement.Facing.Y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ConfiguredSpeedAndBounds_AreIndependentOfFrameSize()
        {
            var minimum = new Vector2(10f, 20f);
            var maximum = new Vector2(20f, 30f);
            var largeStep = new BoundedMovement(new Vector2(15f, 25f), minimum, maximum, 2f);
            var smallSteps = new BoundedMovement(largeStep.Position, minimum, maximum, 2f);
            largeStep.Step(Vector2.UnitX, Vector2.UnitY, 1f);
            for (var i = 0; i < 10; i++) smallSteps.Step(Vector2.UnitX, Vector2.UnitY, 0.1f);
            Assert.That(largeStep.Position.X, Is.EqualTo(17f));
            Assert.That(smallSteps.Position.X, Is.EqualTo(largeStep.Position.X).Within(0.0001f));
        }
    }
}

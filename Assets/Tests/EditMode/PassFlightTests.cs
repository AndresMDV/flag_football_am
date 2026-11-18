using System;
using System.Numerics;
using FlagFootball.Core;
using NUnit.Framework;

namespace FlagFootball.Tests.EditMode
{
    public sealed class PassFlightTests
    {
        private static readonly Vector3 Release = new Vector3(0f, 0.6f, 0f);
        private static readonly Vector2 Minimum = new Vector2(-10f);
        private static readonly Vector2 Maximum = new Vector2(10f);
        private static PassFlight Create(float quality = 1f, Vector2 velocity = default) =>
            new PassFlight(Release, new Vector3(4f, 0.5f, 0f), velocity, quality, Minimum, Maximum);

        private static void Near(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00001f));

        [TestCase(4f)]
        [TestCase(8f)]
        public void StationaryIntercept_ArcReachesTargetBeforeGround(float distance)
        {
            var target = new Vector3(distance, 0.5f, 0f);
            var flight = new PassFlight(Release, target, Vector2.Zero, 1f, Minimum, Maximum);
            Near(flight.Position, Release);
            Assert.That(flight.AimTime, Is.EqualTo(distance / 8d));
            Near(flight.Sample(flight.AimTime), target);
            Near(flight.Sample(flight.AimTime / 2d), new Vector3(distance / 2f, 3.05f, 0f));
            Assert.That(flight.EndTime, Is.GreaterThan(flight.AimTime));
            Assert.That(flight.Elapsed, Is.Zero);
            Assert.That(flight.End, Is.EqualTo(FlightEnd.None));
            flight.Step(100f);
            Assert.That(flight.End, Is.EqualTo(FlightEnd.Ground));
            Assert.That(flight.Position.Y, Is.EqualTo(0.09f));
        }

        [TestCase(2f, 0f)]
        [TestCase(-2f, 1f)]
        [TestCase(0f, 3f)]
        public void MovingIntercept_UsesOnlySampledCurrentMotion(float vx, float vz)
        {
            var velocity = new Vector2(vx, vz);
            var flight = Create(1f, velocity);
            var expected = new Vector3(4f + vx * (float)flight.AimTime, 0.5f, vz * (float)flight.AimTime);
            Near(flight.Sample(flight.AimTime), expected);
            Assert.That(Vector2.Distance(Vector2.Zero, new Vector2(expected.X, expected.Z)) / 8d,
                Is.EqualTo(flight.AimTime).Within(0.000001d));
        }

        [Test]
        public void Quality_ProgressivelyUnderthrowsWithoutRandomness()
        {
            double previousTime = 0d;
            float previousDistance = 0f;
            var green = Create(1f, new Vector2(2f, 0f));
            foreach (var quality in new[] { 0f, 0.5f, 1f })
            {
                var flight = Create(quality, new Vector2(2f, 0f));
                var atAim = flight.Sample(flight.AimTime);
                Assert.That(atAim.X, Is.GreaterThan(previousDistance));
                Assert.That(flight.AimTime, Is.GreaterThan(previousTime));
                Assert.That(atAim.X, Is.EqualTo(green.Sample(green.AimTime).X * (0.5f + 0.5f * quality)).Within(0.00001f));
                Near(flight.Sample(0.1d), Create(quality, new Vector2(2f, 0f)).Sample(0.1d));
                previousTime = flight.AimTime;
                previousDistance = atAim.X;
            }
        }

        [Test]
        public void TranslatedReleaseTargetAndField_PreserveRelativeQualityAndFlight()
        {
            var offset = new Vector3(2f, 0f, -3f);
            var planarOffset = new Vector2(offset.X, offset.Z);
            foreach (var quality in new[] { 0f, 0.5f, 1f })
            {
                var original = Create(quality, new Vector2(2f, 0f));
                var translated = new PassFlight(Release + offset, new Vector3(4f, 0.5f, 0f) + offset,
                    new Vector2(2f, 0f), quality, Minimum + planarOffset, Maximum + planarOffset);
                Assert.That(translated.AimTime, Is.EqualTo(original.AimTime));
                Assert.That(translated.EndTime, Is.EqualTo(original.EndTime));
                Near(translated.Sample(0.1d), original.Sample(0.1d) + offset);
                Near(translated.Sample(100d), original.Sample(100d) + offset);
            }
        }

        [TestCase(0.8f)]
        [TestCase(1.8f)]
        public void LaterRouteCutOrStop_CannotHomeTheReleasedPass(float releaseTime)
        {
            var route = new OneCutRoute(new Vector2(1.5f, 1f), new Vector2(1.5f, 4f), new Vector2(4.5f, 4f), 3f);
            route.Start();
            route.Step(releaseTime);
            var sampled = new Vector3(route.Position.X, 0.5f, route.Position.Y);
            var velocity = route.Facing * 3f;
            var flight = new PassFlight(Release, sampled, velocity, 1f, Minimum, Maximum);
            var planned = flight.Sample(flight.AimTime);
            route.Step((float)flight.AimTime);
            Near(flight.Sample(flight.AimTime), planned);
            Near(planned, sampled + new Vector3(velocity.X, 0f, velocity.Y) * (float)flight.AimTime);
            Assert.That(Vector2.Distance(new Vector2(planned.X, planned.Z), route.Position), Is.GreaterThan(0.5f));
        }

        [TestCase(1, 0.3f)]
        [TestCase(30, 0.01f)]
        [TestCase(1, 10f)]
        [TestCase(100, 0.1f)]
        public void TimePartitionsAndLargeSteps_MatchSamplingAndFreezeAtFirstEvent(int count, float dt)
        {
            var flight = Create();
            var expected = flight.Sample((double)count * dt);
            for (var i = 0; i < count; i++) flight.Step(dt);
            Near(flight.Position, expected);
            flight.Step(100f);
            var terminal = flight.Position;
            var elapsed = flight.Elapsed;
            flight.Step(100f);
            Near(flight.Position, terminal);
            Near(flight.Sample(1000d), terminal);
            Assert.That(flight.Elapsed, Is.EqualTo(elapsed).And.EqualTo(flight.EndTime));
        }

        [TestCase(1f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(0f, -1f)]
        public void FieldCrossingBeforeGround_StopsOnFirstBoundary(float x, float z)
        {
            var origin = new Vector3(x * 9.6f, 0.6f, z * 9.6f);
            var target = new Vector3(x * 9.9f, 0.5f, z * 9.9f);
            var flight = new PassFlight(origin, target, new Vector2(x, z) * 2f, 1f, Minimum, Maximum);
            Assert.That(flight.EndTime, Is.LessThan(flight.AimTime));
            flight.Step(100f);
            Assert.That(flight.End, Is.EqualTo(FlightEnd.FieldExit));
            Assert.That(flight.Position.X, Is.EqualTo(x * 10f).Within(0.000001f));
            Assert.That(flight.Position.Z, Is.EqualTo(z * 10f).Within(0.000001f));
            Assert.That(flight.Position.Y, Is.GreaterThan(0.09f));
            Near(flight.Sample(flight.EndTime + 100d), flight.Position);
        }

        [Test]
        public void GroundBoundaryTie_UsesDeterministicGroundPrecedence()
        {
            var flight = new PassFlight(new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f), Vector2.Zero,
                1f, Minimum, new Vector2(2f, 10f), speed: 1f, arcHeight: 0.125f, groundHeight: 0f);
            Assert.That(flight.EndTime, Is.EqualTo(2d));
            flight.Step(10f);
            Assert.That(flight.End, Is.EqualTo(FlightEnd.Ground));
            Near(flight.Position, new Vector3(2f, 0f, 0f));
        }

        [Test]
        public void CoincidentTargetAndBoundaryRelease_DoNotInventHorizontalMotion()
        {
            var flight = new PassFlight(Release, new Vector3(0f, 0.5f, 0f), Vector2.Zero, 1f, Minimum, Maximum);
            Assert.That(flight.AimTime, Is.EqualTo((double)0.2f));
            Near(flight.Sample(flight.AimTime), new Vector3(0f, 0.5f, 0f));
            var outward = new PassFlight(new Vector3(10f, 0.6f, 0f), new Vector3(10f, 0.5f, 0f),
                new Vector2(2f, 0f), 1f, Minimum, Maximum);
            Assert.That(outward.EndTime, Is.Zero);
            Assert.That(outward.End, Is.EqualTo(FlightEnd.FieldExit));
            Near(outward.Sample(100d), new Vector3(10f, 0.6f, 0f));
        }

        [TestCase(-20f)]
        [TestCase(-8f)]
        public void ApproachingMotion_CanInterceptButUnreachableMotionIsRejected(float velocity)
        {
            var target = new Vector3(10f, 0.5f, 0f);
            var flight = new PassFlight(Release, target, new Vector2(velocity, 0f), 1f, Minimum, Maximum);
            Near(flight.Sample(flight.AimTime), target + new Vector3(velocity, 0f, 0f) * (float)flight.AimTime);
            Assert.Throws<ArgumentException>(() => Create(1f, new Vector2(20f, 0f)));
            Assert.Throws<ArgumentException>(() => Create(1f, new Vector2(8f, 0f)));
            Assert.Throws<ArgumentException>(() => Create(1f, new Vector2(0f, 20f)));
            Assert.Throws<ArgumentException>(() => new PassFlight(Release, new Vector3(1f, 0.5f, 0f),
                new Vector2(-20f, 0f), 1f, Minimum, Maximum));
        }

        [Test]
        public void InvalidInputsAndTime_AreRejectedWithoutChangingProgress()
        {
            foreach (var q in new[] { -0.1f, 1.1f, float.NaN, float.PositiveInfinity })
                Assert.That(() => Create(q), Throws.InstanceOf<ArgumentException>());
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentException>(() => new PassFlight(new Vector3(bad, 1f, 0f), Release, Vector2.Zero, 1f, Minimum, Maximum));
                Assert.Throws<ArgumentException>(() => new PassFlight(Release, new Vector3(0f, bad, 0f), Vector2.Zero, 1f, Minimum, Maximum));
                Assert.Throws<ArgumentException>(() => Create(1f, new Vector2(0f, bad)));
            }
            Assert.Throws<ArgumentException>(() => new PassFlight(Release, Vector3.One, Vector2.Zero, 1f, Minimum, Minimum));
            Assert.Throws<ArgumentException>(() => new PassFlight(Release, Vector3.One, Vector2.Zero, 1f, Vector2.One, Maximum));
            Assert.Throws<ArgumentException>(() => new PassFlight(Vector3.Zero, Vector3.One, Vector2.Zero, 1f, Minimum, Maximum));
            Assert.Throws<ArgumentException>(() => new PassFlight(Release, Vector3.Zero, Vector2.Zero, 1f, Minimum, Maximum));
            Assert.Throws<ArgumentException>(() => new PassFlight(Release, Vector3.One, Vector2.Zero, 1f,
                new Vector2(float.NaN), Maximum));
            Assert.Throws<ArgumentException>(() => new PassFlight(new Vector3(0f, float.MaxValue, 0f),
                Vector3.One, Vector2.Zero, 1f, Minimum, Maximum, arcHeight: float.MaxValue));
            foreach (var bad in new[] { 0f, -1f, float.NaN })
            {
                Assert.Throws<ArgumentException>(() => new PassFlight(Release, Vector3.One, Vector2.Zero, 1f, Minimum, Maximum, speed: bad));
                Assert.Throws<ArgumentException>(() => new PassFlight(Release, Vector3.One, Vector2.Zero, 1f, Minimum, Maximum, arcHeight: bad));
                Assert.Throws<ArgumentException>(() => new PassFlight(Release, Vector3.One, Vector2.Zero, 1f, Minimum, Maximum, minimumTime: bad));
            }
            var flight = Create();
            flight.Step(0.1f);
            var before = flight.Position;
            foreach (var bad in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => flight.Step(bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => flight.Sample(bad));
            }
            Near(flight.Position, before);
            Assert.That(flight.Elapsed, Is.EqualTo((double)0.1f));
        }
    }
}

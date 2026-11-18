using System;
using System.Numerics;
using FlagFootball.Core;
using NUnit.Framework;

namespace FlagFootball.Tests.EditMode
{
    public sealed class PassChargeTests
    {
        private const long Owner = (10L << 32) | 7L;
        private const long Other = (11L << 32) | 7L;
        private static readonly Vector2 Origin = new Vector2(100f, 200f);

        [TestCase(0f, 0f)]
        [TestCase(0.25f, 0.25f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(5f, 1f)]
        public void EarlyMidReadyRelease_EmitsCurrentQualityExactlyOnce(float seconds, float expected)
        {
            var charge = new PassCharge();
            Assert.That(charge.Begin(Owner, Origin), Is.True);
            charge.Advance(Owner, seconds);
            Assert.That(charge.IsCaptured, Is.True, "Advancement never automatically releases.");
            Assert.That(charge.Quality, Is.EqualTo(expected));
            var released = charge.Release(Owner, Origin);
            Assert.That(released.HasValue, Is.True, "Zero-quality release is still a valid release.");
            Assert.That(released.Value, Is.EqualTo(expected));
            Assert.That(charge.IsCaptured, Is.False);
            Assert.That(charge.Quality, Is.Zero);
            Assert.That(charge.Release(Owner, Origin), Is.Null);
            charge.Advance(Owner, 10f);
            Assert.That(charge.Quality, Is.Zero);
        }

        [TestCase(1, 0.6f)]
        [TestCase(6, 0.1f)]
        [TestCase(60, 0.01f)]
        [TestCase(600, 0.001f)]
        public void TimePartitions_UseConfiguredDurationRatherThanFrameCount(int count, float dt)
        {
            var charge = new PassCharge(duration: 2f);
            charge.Begin(Owner, Origin);
            for (var i = 0; i < count; i++) charge.Advance(Owner, dt);
            Assert.That(charge.Quality, Is.EqualTo(0.3f).Within(0.000001f));
            Assert.That(charge.Release(Owner, Origin).Value, Is.EqualTo(0.3f).Within(0.000001f));
        }

        [Test]
        public void SustainedReadyAndStationaryFinger_DoNotCycleCancelOrAutoRelease()
        {
            var charge = new PassCharge();
            charge.Begin(Owner, Origin);
            charge.Advance(Owner, 1f);
            for (var frame = 0; frame < 200; frame++)
            {
                charge.Move(Owner, Origin);
                charge.Advance(Owner, 0.033f);
                Assert.That(charge.Quality, Is.EqualTo(1f));
                Assert.That(charge.Owns(Owner), Is.True);
            }
            charge.Advance(Owner, float.MaxValue);
            Assert.That(charge.Release(Owner, Origin).Value, Is.EqualTo(1f));
        }

        [Test]
        public void OtherPointerAndRepeatedBegin_CannotStealAdvanceCancelOrRelease()
        {
            var charge = new PassCharge();
            charge.Begin(Owner, Origin);
            charge.Advance(Owner, 0.25f);
            Assert.That(charge.Begin(Other, Origin), Is.False);
            Assert.That(charge.Begin(Owner, Origin + Vector2.One * 1000f), Is.False);
            charge.Advance(Other, 100f);
            charge.Move(Other, Origin - Vector2.UnitY * 1000f);
            Assert.That(charge.Cancel(Other), Is.False);
            Assert.That(charge.Release(Other, Origin), Is.Null);
            Assert.That(charge.Owns(Owner), Is.True);
            Assert.That(charge.Owns(Other), Is.False);
            Assert.That(charge.Quality, Is.EqualTo(0.25f));
            Assert.That(charge.Release(Owner, Origin - Vector2.UnitY * 79f).Value, Is.EqualTo(0.25f));
        }

        [Test]
        public void DownwardSwipe_UsesOriginalFingerPositionAndCancelsAtThreshold()
        {
            var charge = new PassCharge();
            charge.Begin(Owner, Origin);
            charge.Advance(Owner, 0.5f);
            charge.Move(Owner, Origin + Vector2.UnitY * 60f);
            charge.Move(Owner, Origin - Vector2.UnitY * 20f); // 80 down from last sample is not 80 from press.
            Assert.That(charge.IsCaptured, Is.True);
            charge.Move(Owner, Origin + Vector2.UnitX * 800f);
            Assert.That(charge.IsCaptured, Is.True);
            charge.Move(Owner, Origin - Vector2.UnitY * 79.99f);
            Assert.That(charge.IsCaptured, Is.True);
            charge.Move(Owner, Origin - Vector2.UnitY * 80f);
            Assert.That(charge.IsCaptured, Is.False);
            Assert.That(charge.Quality, Is.Zero);
            charge.Move(Owner, Origin);
            charge.Advance(Owner, 10f);
            Assert.That(charge.Release(Owner, Origin), Is.Null);
            Assert.That(charge.IsCaptured, Is.False);
            Assert.That(charge.Begin(Owner, Origin), Is.True, "Only another explicit Begin starts a new charge.");
            charge.Advance(Owner, 0.25f);
            Assert.That(charge.Release(Owner, Origin).Value, Is.EqualTo(0.25f));
        }

        [TestCase(19f, true)]
        [TestCase(20f, false)]
        [TestCase(40f, false)]
        public void ReleaseFinalPosition_UsesConfiguredSwipeThreshold(float down, bool releases)
        {
            var charge = new PassCharge(cancelDistance: 20f);
            charge.Begin(Owner, Origin);
            charge.Advance(Owner, 0.75f);
            var quality = charge.Release(Owner, Origin - Vector2.UnitY * down);
            Assert.That(quality.HasValue, Is.EqualTo(releases));
            if (releases) Assert.That(quality.Value, Is.EqualTo(0.75f));
            Assert.That(charge.IsCaptured, Is.False);
            Assert.That(charge.Quality, Is.Zero);
            Assert.That(charge.Release(Owner, Origin), Is.Null);
        }

        [Test]
        public void PointerCancelAndGlobalReset_AreIdempotentAndDoNotPrechargeNextBegin()
        {
            var charge = new PassCharge();
            Assert.That(charge.Cancel(Owner), Is.False);
            charge.Advance(Owner, 100f);
            charge.Begin(Owner, Origin);
            Assert.That(charge.Quality, Is.Zero);
            charge.Advance(Owner, 0.5f);
            Assert.That(charge.Cancel(Owner), Is.True);
            Assert.That(charge.Cancel(Owner), Is.False);
            charge.Reset();
            charge.Reset();
            Assert.That(charge.Release(Owner, Origin), Is.Null);
            charge.Begin(Other, Origin);
            charge.Advance(Other, 0.5f);
            charge.Reset();
            Assert.That(charge.Release(Other, Origin), Is.Null);
            charge.Advance(Other, 100f);
            Assert.That(charge.Begin(Owner, Origin), Is.True);
            Assert.That(charge.Release(Owner, Origin).Value, Is.Zero);
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        [TestCase(long.MinValue)]
        [TestCase(long.MaxValue)]
        public void OpaquePointerKeys_HaveNoReservedSentinelValue(long pointer)
        {
            var charge = new PassCharge();
            Assert.That(charge.Begin(pointer, Origin), Is.True);
            Assert.That(charge.Owns(pointer), Is.True);
            charge.Advance(pointer, 0.5f);
            Assert.That(charge.Release(pointer, Origin).Value, Is.EqualTo(0.5f));
            Assert.That(charge.Owns(pointer), Is.False);
        }

        [Test]
        public void InvalidConfigurationPositionsAndTime_DoNotCorruptCapturedCharge()
        {
            foreach (var invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new PassCharge(duration: invalid));
                Assert.Throws<ArgumentOutOfRangeException>(() => new PassCharge(cancelDistance: invalid));
            }
            var charge = new PassCharge();
            var invalidPositions = new[] { new Vector2(float.NaN, 0f), new Vector2(0f, float.PositiveInfinity),
                new Vector2(float.NegativeInfinity, 0f) };
            foreach (var p in invalidPositions) Assert.Throws<ArgumentOutOfRangeException>(() => charge.Begin(Owner, p));
            Assert.That(charge.IsCaptured, Is.False);
            charge.Begin(Owner, Origin);
            charge.Advance(Owner, 0.25f);
            foreach (var p in invalidPositions)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => charge.Begin(Other, p));
                Assert.Throws<ArgumentOutOfRangeException>(() => charge.Move(Owner, p));
                Assert.Throws<ArgumentOutOfRangeException>(() => charge.Release(Owner, p));
            }
            foreach (var dt in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => charge.Advance(Owner, dt));
                Assert.Throws<ArgumentOutOfRangeException>(() => charge.Advance(Other, dt));
            }
            Assert.That(charge.Owns(Owner), Is.True);
            Assert.That(charge.Quality, Is.EqualTo(0.25f));
            Assert.That(charge.Release(Owner, Origin).Value, Is.EqualTo(0.25f));
        }
    }
}

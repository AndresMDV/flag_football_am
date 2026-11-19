using System;
using System.Numerics;
using FlagFootball.Core;
using NUnit.Framework;

namespace FlagFootball.Tests.EditMode
{
    public sealed class ReceptionTrialTests
    {
        private static readonly Vector3 Left = new Vector3(-2f, 0f, 0f);
        private static readonly Vector3 Right = new Vector3(2f, 0f, 0f);
        private static bool Cross(ReceptionTrial trial) => trial.TryCatch(Left, Vector3.Zero, Right, Vector3.Zero, Vector3.Zero);

        [TestCase(0f, 0d, false)]
        [TestCase(1f, 0.999d, true)]
        [TestCase(0.5f, 0d, true)]
        [TestCase(0.5f, 0.499d, true)]
        [TestCase(0.5f, 0.5d, false)]
        [TestCase(0.5f, 0.999d, false)]
        public void FirstReach_ResolvesOnceWithFrozenProbability(float probability, double draw, bool catches)
        {
            var calls = 0;
            var trial = new ReceptionTrial(probability, 1f, () => { calls++; return draw; });
            Assert.That(Cross(trial), Is.EqualTo(catches));
            Assert.That(trial.Result, Is.EqualTo(catches ? ReceptionResult.Caught : ReceptionResult.Missed));
            Assert.That(trial.ContactFraction.Value, Is.EqualTo(0.25d).Within(1e-8));
            for (var frame = 0; frame < 5; frame++) Assert.That(Cross(trial), Is.False);
            Assert.That(trial.Result, Is.EqualTo(catches ? ReceptionResult.Caught : ReceptionResult.Missed));
            Assert.That(calls, Is.EqualTo(probability == 0f || probability == 1f ? 0 : 1));
        }

        [Test]
        public void MissedGeometry_DoesNotSpendTheTrialOrDraw()
        {
            var calls = 0;
            var trial = new ReceptionTrial(0.5f, 1f, () => { calls++; return 0.1d; });
            Assert.That(trial.TryCatch(Right, Right, Right, Vector3.Zero, Vector3.Zero), Is.False);
            Assert.That(trial.Result, Is.EqualTo(ReceptionResult.Pending));
            Assert.That(trial.ContactFraction, Is.Null);
            Assert.That(calls, Is.Zero);
            Assert.That(Cross(trial), Is.True);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Tangency_IsDetectedWithoutAnEndpointSignChange()
        {
            var trial = new ReceptionTrial(1f, 1f, () => throw new Exception("Endpoint probability needs no draw."));
            Assert.That(trial.TryCatch(Left + Vector3.UnitY, Vector3.UnitY, Right + Vector3.UnitY,
                Vector3.Zero, Vector3.Zero), Is.True);
            Assert.That(trial.ContactFraction.Value, Is.EqualTo(0.5d).Within(1e-8));
        }

        [TestCase(0.1d, true)]
        [TestCase(0.9d, false)]
        public void TwoSeparateArcContacts_UseFirstEntryAndOneTrial(double draw, bool catches)
        {
            Vector3 Sample(double t) => new Vector3((float)(4d * t - 2d), (float)(8d * t * (1d - t)), 0f);
            var calls = 0;
            var trial = new ReceptionTrial(0.5f, 1.9f, () => { calls++; return draw; });
            Assert.That(trial.TryCatch(Sample(0), Sample(0.5), Sample(1), Vector3.Zero, Vector3.Zero), Is.EqualTo(catches));
            Assert.That(trial.ContactFraction.Value, Is.InRange(0.02d, 0.04d));
            var first = trial.ContactFraction;
            Assert.That(trial.TryCatch(Sample(0.6), Sample(0.8), Sample(1), Vector3.Zero, Vector3.Zero), Is.False);
            Assert.That(trial.ContactFraction, Is.EqualTo(first));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ReceiverCut_MustBeSuppliedAsSeparateLinearLegs()
        {
            var trial = new ReceptionTrial(1f, 0.5f, () => 0d);
            var before = new Vector3(-2f, 2f, 0f);
            var after = new Vector3(2f, 2f, 0f);
            Assert.That(trial.TryCatch(Vector3.Zero, Vector3.Zero, Vector3.Zero, before, after), Is.False);
            Assert.That(trial.TryCatch(Vector3.Zero, Vector3.Zero, Vector3.Zero, before, Vector3.Zero), Is.True);
            Assert.That(trial.ContactFraction.Value, Is.EqualTo(1d - 0.5d / Math.Sqrt(8d)).Within(1e-8));
        }

        [Test]
        public void ReceiverMotion_UsesRelativePositions()
        {
            var trial = new ReceptionTrial(1f, 0.5f, () => 0d);
            Assert.That(trial.TryCatch(Vector3.Zero, Vector3.Zero, Vector3.Zero, Left, Right), Is.True);
            Assert.That(trial.ContactFraction.Value, Is.EqualTo(0.375d).Within(1e-8));
        }

        [TestCase(2f, 0.2f, true)]
        [TestCase(0f, 0.5f, false)]
        public void CurvedFlight_IsNotReplacedByItsEndpointChord(float receiverY, float reach, bool catches)
        {
            var trial = new ReceptionTrial(1f, reach, () => 0d);
            var receiver = Vector3.UnitY * receiverY;
            Assert.That(trial.TryCatch(Left, Vector3.UnitY * 2f, Right, receiver, receiver), Is.EqualTo(catches));
            if (!catches) { Assert.That(trial.ContactFraction, Is.Null); return; }
            var t = trial.ContactFraction.Value;
            Vector3 Sample(double u) => new Vector3((float)(4d * u - 2d), (float)(8d * u * (1d - u)), 0f);
            Assert.That(t, Is.InRange(0d, 0.5d));
            Assert.That(Vector3.Distance(Sample(t), receiver), Is.EqualTo(reach).Within(1e-5));
            Assert.That(Vector3.Distance(Sample(t - 1e-4), receiver), Is.GreaterThan(reach));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TerminalEndpoint_CanBeExcludedSoFlightEndWins(bool includeEnd)
        {
            var trial = new ReceptionTrial(1f, 1f, () => 0d);
            Assert.That(trial.TryCatch(Left, Left * 0.75f, Left * 0.5f,
                Vector3.Zero, Vector3.Zero, includeEnd), Is.EqualTo(includeEnd));
            if (includeEnd) Assert.That(trial.ContactFraction.Value, Is.EqualTo(1d).Within(1e-8));
            else Assert.That(trial.Result, Is.EqualTo(ReceptionResult.Pending));
            var earlier = new ReceptionTrial(1f, 1f, () => 0d);
            Assert.That(earlier.TryCatch(Left, Vector3.Zero, Right, Vector3.Zero, Vector3.Zero, false), Is.True);
        }

        [Test]
        public void InitialOverlap_ZeroContactFractionIsValidEvenWithExclusiveEnd()
        {
            var trial = new ReceptionTrial(1f, 1f, () => 0d);
            Assert.That(trial.TryCatch(Vector3.Zero, Vector3.Zero, Vector3.Zero,
                Vector3.Zero, Vector3.Zero, false), Is.True);
            Assert.That(trial.ContactFraction.Value, Is.Zero);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(20)]
        [TestCase(100)]
        public void HighSpeedCurvedCrossing_PartitionsKeepTheFirstGlobalContact(int count)
        {
            Vector3 Sample(double t) => new Vector3((float)(40d * t - 20d), (float)(8d * t * (1d - t)), 0f);
            var receiver = Vector3.UnitY * 2f;
            var whole = new ReceptionTrial(1f, 0.25f, () => 0d);
            Assert.That(whole.TryCatch(Sample(0), Sample(0.5), Sample(1), receiver, receiver), Is.True);
            var calls = 0;
            var split = new ReceptionTrial(0.5f, 0.25f, () => { calls++; return 0.1d; });
            double? global = null;
            for (var i = 0; i < count; i++)
            {
                var start = (double)i / count;
                var end = (double)(i + 1) / count;
                if (split.TryCatch(Sample(start), Sample((start + end) * 0.5d), Sample(end), receiver, receiver))
                    global = start + split.ContactFraction.Value * (end - start);
            }
            Assert.That(global.HasValue, Is.True);
            Assert.That(global.Value, Is.EqualTo(whole.ContactFraction.Value).Within(1e-6));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void InvalidConfigurationAndGeometry_DoNotConsumeAValidTrial()
        {
            foreach (var value in new[] { -1f, 2f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => new ReceptionTrial(value, 1f, () => 0d));
            foreach (var value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => new ReceptionTrial(1f, value, () => 0d));
            Assert.Throws<ArgumentNullException>(() => new ReceptionTrial(1f, 1f, null));
            var trial = new ReceptionTrial(1f, 1f, () => 0d);
            var points = new[] { Left, Vector3.Zero, Right, Vector3.Zero, Vector3.Zero };
            for (var i = 0; i < points.Length; i++)
            {
                var original = points[i];
                points[i] = new Vector3(float.NaN, 0f, 0f);
                Assert.Throws<ArgumentOutOfRangeException>(() => trial.TryCatch(points[0], points[1], points[2], points[3], points[4]));
                points[i] = original;
                Assert.That(trial.Result, Is.EqualTo(ReceptionResult.Pending));
                Assert.That(trial.ContactFraction, Is.Null);
            }
            Assert.That(Cross(trial), Is.True);
        }

        [TestCase(-0.1d)]
        [TestCase(1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidDraw_IsTerminalAndCannotBeRetried(double value)
        {
            var calls = 0;
            var trial = new ReceptionTrial(0.5f, 1f, () => { calls++; return value; });
            Assert.Throws<InvalidOperationException>(() => Cross(trial));
            Assert.That(trial.Result, Is.EqualTo(ReceptionResult.Missed));
            Assert.That(Cross(trial), Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingDraw_FailsClosedWithoutAnotherAttempt()
        {
            var calls = 0;
            var trial = new ReceptionTrial(0.5f, 1f, () => { calls++; throw new InvalidOperationException("Unavailable draw."); });
            Assert.Throws<InvalidOperationException>(() => Cross(trial));
            Assert.That(trial.Result, Is.EqualTo(ReceptionResult.Missed));
            Assert.That(Cross(trial), Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}

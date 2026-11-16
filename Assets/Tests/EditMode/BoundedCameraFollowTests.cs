using System;
using System.Numerics;
using FlagFootball.Core;
using NUnit.Framework;

namespace FlagFootball.Tests.EditMode
{
    public sealed class BoundedCameraFollowTests
    {
        private static Vector2 Step(Vector2 current, float deltaTime) => BoundedCameraFollow.Step(
            current, new Vector2(2f, 1f), new Vector2(-10f), new Vector2(10f),
            new Vector2(-10f), new Vector2(10f), 8f, deltaTime);

        [Test]
        public void Follow_ApproachesWithoutSnappingOrOvershooting()
        {
            var first = Step(Vector2.Zero, 0.1f);
            Assert.That(first.X, Is.GreaterThan(0f).And.LessThan(2f));
            var next = first;
            for (var i = 0; i < 20; i++) next = Step(next, 0.1f);
            Assert.That(Vector2.Distance(next, new Vector2(2f, 1f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void Follow_IsIndependentOfTimeStepPartition()
        {
            var once = Step(Vector2.Zero, 1f);
            var partitioned = Vector2.Zero;
            for (var i = 0; i < 10; i++) partitioned = Step(partitioned, 0.1f);
            Assert.That(Vector2.Distance(once, partitioned), Is.LessThan(0.0001f));
        }

        [Test]
        public void LegalFieldAndSubjectVisibility_OverrideLagEvenAtZeroTime()
        {
            var position = BoundedCameraFollow.Step(new Vector2(-20f, 20f), Vector2.Zero,
                new Vector2(-4f), new Vector2(4f), new Vector2(1f, -3f), new Vector2(3f, -1f), 8f, 0f);
            Assert.That(position, Is.EqualTo(new Vector2(1f, -1f)));
        }

        [Test]
        public void ImpossibleFieldAndVisibilityIntersection_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => BoundedCameraFollow.Step(
                Vector2.Zero, Vector2.One, -Vector2.One, Vector2.One,
                new Vector2(2f), new Vector2(3f), 8f, 0.1f));
        }
    }
}

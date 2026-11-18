using System;
using System.Numerics;
using FlagFootball.Core;
using NUnit.Framework;

namespace FlagFootball.Tests.EditMode
{
    public sealed class OneCutRouteTests
    {
        private static OneCutRoute Create() => new OneCutRoute(
            new Vector2(1.5f, 1f), new Vector2(1.5f, 4f), new Vector2(4.5f, 4f), 3f);

        [Test]
        public void StartCutEndWaitResetRepeat_ConsumesDistanceAndFacesTravel()
        {
            var route = Create();
            route.Step(10f);
            Assert.That(route.Position, Is.EqualTo(new Vector2(1.5f, 1f)));
            for (var run = 0; run < 2; run++)
            {
                Assert.That(route.Facing, Is.EqualTo(Vector2.UnitY));
                Assert.That(route.Start(), Is.True);
                route.Step(0.5f);
                Assert.That(route.Position, Is.EqualTo(new Vector2(1.5f, 2.5f)));
                Assert.That(route.Start(), Is.False);
                route.Step(0.5f);
                Assert.That(route.Position, Is.EqualTo(new Vector2(1.5f, 4f)));
                Assert.That(route.Facing, Is.EqualTo(Vector2.UnitY));
                route.Step(0.25f);
                Assert.That(route.Position, Is.EqualTo(new Vector2(2.25f, 4f)));
                Assert.That(route.Facing, Is.EqualTo(Vector2.UnitX));
                route.Step(100f);
                Assert.That(route.Position, Is.EqualTo(new Vector2(4.5f, 4f)));
                Assert.That(route.Start(), Is.False);
                route.Step(100f);
                Assert.That(route.Position, Is.EqualTo(new Vector2(4.5f, 4f)));
                Assert.That(route.Facing, Is.EqualTo(Vector2.UnitX));
                route.Reset();
                route.Step(10f);
                Assert.That(route.Position, Is.EqualTo(new Vector2(1.5f, 1f)));
            }
        }

        [TestCase(1, 1.25f, 2.25f)]
        [TestCase(5, 0.25f, 2.25f)]
        [TestCase(125, 0.01f, 2.25f)]
        [TestCase(1, 10f, 4.5f)]
        public void PartitionsAndLargeFrames_CrossCutWithoutLosingDistance(int count, float dt, float x)
        {
            var route = Create();
            route.Start();
            for (var i = 0; i < count; i++) route.Step(dt);
            Assert.That(route.Position.X, Is.EqualTo(x).Within(0.0001f));
            Assert.That(route.Position.Y, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(route.Facing, Is.EqualTo(Vector2.UnitX));
            route.Step(0f);
            Assert.That(route.Position.X, Is.EqualTo(x).Within(0.0001f));
            route.Reset(); // Mid-route reset must also stop, not only completed-route reset.
            route.Step(dt);
            Assert.That(route.Position, Is.EqualTo(new Vector2(1.5f, 1f)));
        }

        [Test]
        public void CustomGeometryAndSpeed_UseWorldTravelRatherThanDefaultAxes()
        {
            var route = new OneCutRoute(new Vector2(2f, -1f), new Vector2(4f, 1f),
                new Vector2(2f, 3f), (float)Math.Sqrt(2d));
            route.Start();
            route.Step(1f);
            Assert.That(Vector2.Distance(route.Position, new Vector2(3f, 0f)), Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(route.Facing, Vector2.Normalize(Vector2.One)), Is.LessThan(0.0001f));
            route.Step(1.5f);
            Assert.That(Vector2.Distance(route.Position, new Vector2(3.5f, 1.5f)), Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(route.Facing, Vector2.Normalize(new Vector2(-1f, 1f))), Is.LessThan(0.0001f));
        }

        [Test]
        public void InvalidConfigurationAndTime_AreRejectedWithoutChangingProgress()
        {
            foreach (var speed in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new OneCutRoute(Vector2.Zero, Vector2.UnitY, Vector2.One, speed));
            foreach (var cut in new[] { Vector2.Zero, Vector2.One, new Vector2(float.NaN, 1f) })
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new OneCutRoute(Vector2.Zero, cut, Vector2.One, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new OneCutRoute(Vector2.Zero, Vector2.UnitY, Vector2.UnitY * 2f, 1f));
            foreach (var point in new[] { new Vector2(float.NaN, 0f), new Vector2(0f, float.PositiveInfinity) })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new OneCutRoute(point, Vector2.UnitY, Vector2.One, 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new OneCutRoute(Vector2.Zero, Vector2.UnitY, point, 1f));
            }
            var route = Create();
            route.Start();
            foreach (var dt in new[] { -1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => route.Step(dt));
            route.Step(0.5f);
            Assert.That(route.Position, Is.EqualTo(new Vector2(1.5f, 2.5f)));
        }
    }
}

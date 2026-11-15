using System.Linq;
using FlagFootball.Core;
using NUnit.Framework;

namespace FlagFootball.Tests.EditMode
{
    public sealed class CoreBoundaryTests
    {
        [Test]
        public void CoreAssembly_UsesExpectedName()
        {
            Assert.That(typeof(CoreAssemblyMarker).Assembly.GetName().Name,
                Is.EqualTo("FlagFootball.Core"));
        }

        [Test]
        public void CoreAssembly_DoesNotReferenceUnity()
        {
            var dependencies = typeof(CoreAssemblyMarker).Assembly.GetReferencedAssemblies()
                .Select(assembly => assembly.Name)
                .Where(name => name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor"));
            Assert.That(dependencies, Is.Empty);
        }
    }
}

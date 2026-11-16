using NUnit.Framework;
using UnityEditor;

namespace FlagFootball.Tests.EditMode
{
    public sealed class AndroidOrientationTests
    {
        [Test]
        public void PlayerSettings_AllowOnlyBothLandscapeOrientations()
        {
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.AutoRotation));
            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.True);
        }
    }
}

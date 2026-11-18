using FlagFootball.Core;
using NUnit.Framework;

namespace FlagFootball.Tests.EditMode
{
    public sealed class AttemptLifecycleTests
    {
        [Test]
        public void Start_TransitionsOnlyOnceFromPreparation()
        {
            var attempt = new AttemptLifecycle();
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Preparation));
            Assert.That(attempt.StartAttempt(), Is.True);
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Active));
            Assert.That(attempt.StartAttempt(), Is.False);
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Active));
        }

        [Test]
        public void Incomplete_OnlyEndsActiveAndRequiresRestartBeforeStart()
        {
            var attempt = new AttemptLifecycle();
            Assert.That(attempt.MarkIncomplete(), Is.False);
            attempt.StartAttempt();
            Assert.That(attempt.MarkIncomplete(), Is.True);
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Incomplete));
            Assert.That(attempt.MarkIncomplete(), Is.False);
            Assert.That(attempt.StartAttempt(), Is.False);
            Assert.That(attempt.RestartAttempt(), Is.True);
            Assert.That(attempt.StartAttempt(), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Restart_IsRepeatableAndRequiresExplicitStart(bool initiallyActive)
        {
            var attempt = new AttemptLifecycle();
            if (initiallyActive) attempt.StartAttempt();
            Assert.That(attempt.RestartAttempt(), Is.EqualTo(initiallyActive));
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Preparation));
            Assert.That(attempt.RestartAttempt(), Is.False);
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Preparation));
            Assert.That(attempt.StartAttempt(), Is.True);
            Assert.That(attempt.State, Is.EqualTo(AttemptState.Active));
        }
    }
}

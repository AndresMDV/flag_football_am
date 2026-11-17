namespace FlagFootball.Core
{
    public enum AttemptState { Preparation, Active }

    public sealed class AttemptLifecycle
    {
        public AttemptState State { get; private set; } = AttemptState.Preparation;

        public bool StartAttempt()
        {
            if (State == AttemptState.Active) return false;
            State = AttemptState.Active;
            return true;
        }

        public bool RestartAttempt()
        {
            var wasActive = State == AttemptState.Active;
            State = AttemptState.Preparation;
            return wasActive;
        }
    }
}

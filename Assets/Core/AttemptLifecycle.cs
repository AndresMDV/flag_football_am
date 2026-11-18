namespace FlagFootball.Core
{
    public enum AttemptState { Preparation, Active, Incomplete }

    public sealed class AttemptLifecycle
    {
        public AttemptState State { get; private set; } = AttemptState.Preparation;

        public bool StartAttempt()
        {
            if (State != AttemptState.Preparation) return false;
            State = AttemptState.Active;
            return true;
        }

        public bool MarkIncomplete()
        {
            if (State != AttemptState.Active) return false;
            State = AttemptState.Incomplete;
            return true;
        }

        public bool RestartAttempt()
        {
            var changed = State != AttemptState.Preparation;
            State = AttemptState.Preparation;
            return changed;
        }
    }
}

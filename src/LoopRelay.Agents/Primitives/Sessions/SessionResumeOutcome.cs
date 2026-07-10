namespace LoopRelay.Agents.Primitives.Sessions;

public enum SessionResumeOutcome
{
    SuccessfulResume = 0,
    RetryableFailure = 1,
    DeterministicProtocolFailure = 2,
    UnavailableSession = 3,
    CorruptedState = 4,
    UnknownOutcome = 5,
    AuthenticationFailure = 6,
    ConfigurationFailure = 7,
    PermissionFailure = 8,
    PersistenceFailure = 9,
    Cancelled = 10,
    PostTurnFailure = 11,
    ProgrammingFailure = 12,
}

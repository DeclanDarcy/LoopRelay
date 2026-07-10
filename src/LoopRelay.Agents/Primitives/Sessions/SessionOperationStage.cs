namespace LoopRelay.Agents.Primitives.Sessions;

public enum SessionOperationStage
{
    NotStarted = 0,
    ProfileGate = 1,
    ProcessLaunch = 2,
    InitializeWrite = 3,
    InitializeResponse = 4,
    OperationWrite = 5,
    OperationResponse = 6,
    Validation = 7,
    Completed = 8,
}

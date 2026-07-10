namespace LoopRelay.Agents.Primitives.Sessions;

public enum SessionContinuityOperation
{
    Create,
    Resume,
    Fork,
    ConversationRead,
    ConversationWrite,
    ConversationImport,
    ConversationExport,
    PartialRead,
    DeterministicIdentifiers,
}

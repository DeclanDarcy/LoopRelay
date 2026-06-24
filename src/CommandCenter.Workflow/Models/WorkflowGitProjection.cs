using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowGitProjection(
    Guid RepositoryId,
    WorkflowGitStatus CommitStatus,
    WorkflowGitStatus PushStatus,
    string? CommitId,
    string Branch,
    DateTimeOffset? CommitTimestamp,
    DateTimeOffset? PushTimestamp,
    bool HasPendingChanges,
    bool HasUnpushedChanges,
    bool IsCommitRequired,
    bool IsPushRequired,
    bool IsCommitGateOpen,
    bool IsPushGateOpen,
    WorkflowCompletionEvaluation Completion,
    WorkflowGitDiagnostics Diagnostics);

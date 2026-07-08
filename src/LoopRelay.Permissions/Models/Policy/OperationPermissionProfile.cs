namespace LoopRelay.Permissions.Models.Policy;

public sealed record OperationPermissionProfile(
    string Label,
    string RepositoryRoot,
    IReadOnlyList<string> AllowedReads,
    IReadOnlyList<OperationPathGlob> AllowedReadGlobs,
    IReadOnlyList<string> AllowedWrites,
    IReadOnlyList<OperationPathGlob> AllowedWriteGlobs);

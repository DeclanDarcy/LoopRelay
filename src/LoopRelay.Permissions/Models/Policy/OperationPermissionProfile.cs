namespace LoopRelay.Permissions.Models;

public sealed record OperationPermissionProfile(
    string Label,
    string RepositoryRoot,
    IReadOnlyList<string> AllowedReads,
    IReadOnlyList<OperationPathGlob> AllowedReadGlobs,
    IReadOnlyList<string> AllowedWrites,
    IReadOnlyList<OperationPathGlob> AllowedWriteGlobs);

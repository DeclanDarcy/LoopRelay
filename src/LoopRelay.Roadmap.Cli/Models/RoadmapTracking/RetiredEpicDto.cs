namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record RetiredEpicDto(
    string EpicId,
    string EpicName,
    string PrimaryReason,
    string AuditEvidencePath,
    DateTimeOffset RetiredAt)
{
    public static RetiredEpicDto FromDomain(RetiredEpic retired) =>
        new(retired.EpicId, retired.EpicName, retired.PrimaryReason, retired.AuditEvidencePath, retired.RetiredAt);

    public RetiredEpic ToDomain() => new(EpicId, EpicName, PrimaryReason, AuditEvidencePath, RetiredAt);
}

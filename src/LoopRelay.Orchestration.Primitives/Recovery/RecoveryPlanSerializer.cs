using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoopRelay.Orchestration.Recovery;

public static class RecoveryPlanSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize(RecoveryPlan plan) => JsonSerializer.Serialize(Canonical(plan), Options);

    public static RecoveryPlan Deserialize(string json)
    {
        RecoveryPlan? plan = JsonSerializer.Deserialize<RecoveryPlan>(json, new JsonSerializerOptions(Options)
        {
            PropertyNameCaseInsensitive = true,
        });
        if (plan is null || plan.Digest != ComputeDigest(plan))
        {
            throw new InvalidDataException("The persisted recovery plan is empty or failed canonical digest verification.");
        }

        return plan;
    }

    public static string ComputeDigest(RecoveryPlan plan) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Canonical(plan), Options))).ToLowerInvariant();

    private static object Canonical(RecoveryPlan plan) => new
    {
        plan.PlanId,
        plan.SchemaVersion,
        plan.PlannerVersion,
        plan.PolicyVersion,
        Mechanism = new { plan.Mechanism.Identity, plan.Mechanism.Version },
        EligibilityAndRankingEvidence = plan.EligibilityAndRankingEvidence.Order(StringComparer.Ordinal),
        Sources = plan.Sources.OrderBy(source => source.Order).Select(source => new
        {
            source.Order,
            source.Kind,
            source.Location,
            source.Digest,
            source.VerifiedBoundary,
            source.NormalizerVersion,
            source.Completeness,
            Omissions = source.Omissions.Order(StringComparer.Ordinal),
            Evidence = SortedMap(source.Evidence),
        }),
        plan.EnvelopeDigest,
        EnvelopeDescriptor = SortedMap(plan.EnvelopeDescriptor),
        plan.ActivationStrategy,
        plan.ValidationStrategy,
        plan.ReconciliationStrategy,
        plan.ExpectedCompleteness,
        AllowedOmissions = plan.AllowedOmissions.Order(StringComparer.Ordinal),
        plan.ContinuityProfileDigest,
        OperationConstraints = SortedMap(plan.OperationConstraints),
        plan.IdempotencyIdentity,
        plan.RetryCeiling,
        plan.FailureBehavior,
    };

    private static IReadOnlyDictionary<string, string> SortedMap(IReadOnlyDictionary<string, string> source) =>
        source.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
}

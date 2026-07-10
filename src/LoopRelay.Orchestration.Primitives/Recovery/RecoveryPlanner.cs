using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Orchestration.Recovery;

public sealed class RecoveryPlanningException(
    string message,
    IReadOnlyList<string> evidence) : InvalidOperationException(message)
{
    public IReadOnlyList<string> Evidence { get; } = evidence;
}

/// <summary>Pure deterministic policy. It evaluates and ranks; it never invokes a provider or store.</summary>
public sealed class RecoveryPlanner : IRecoveryPlanner
{
    public const string SchemaVersion = "recovery-plan.v1";
    public const string PlannerVersion = "recovery-planner.v1";

    private static readonly HashSet<string> ReplacementEligibleFailures = new(StringComparer.Ordinal)
    {
        "UnavailableSession",
        "CorruptedState",
    };

    public RecoveryPlan Plan(RecoveryPlanningInput input, IReadOnlyList<IRecoveryMechanism> mechanisms)
    {
        if (input.Failure.TurnSubmitted || !ReplacementEligibleFailures.Contains(input.Failure.Classification))
        {
            throw new RecoveryPlanningException(
                $"Failure {input.Failure.Classification} is not eligible for replacement recovery.",
                [$"failure={input.Failure.Classification}", $"turn-submitted={input.Failure.TurnSubmitted}"]);
        }

        string policyVersion = Value(input.Policy, "policy-version", "recovery-ranking.v1");
        var candidates = mechanisms
            .OrderBy(mechanism => mechanism.Key.Identity, StringComparer.Ordinal)
            .ThenBy(mechanism => mechanism.Key.Version, StringComparer.Ordinal)
            .Select(mechanism => new
            {
                Mechanism = mechanism,
                Eligibility = mechanism.EvaluateEligibility(input),
                Rank = Rank(input.Policy, mechanism.Key),
            })
            .ToArray();

        var evidence = candidates.SelectMany(candidate =>
                candidate.Eligibility.Evidence.Select(item =>
                    $"{candidate.Mechanism.Key.Identity}@{candidate.Mechanism.Key.Version}:{item}"))
            .Append($"policy={policyVersion}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var selected = candidates
            .Where(candidate => candidate.Eligibility.Eligible)
            .OrderBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Mechanism.Key.Identity, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Mechanism.Key.Version, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected is null)
        {
            throw new RecoveryPlanningException("No registered recovery mechanism is eligible.", evidence);
        }

        RecoverySourceDescriptor[] sources = input.Sources
            .OrderBy(source => source.Order)
            .ThenBy(source => source.Kind, StringComparer.Ordinal)
            .ThenBy(source => source.Digest, StringComparer.Ordinal)
            .ToArray();
        string identityMaterial = JsonSerializer.Serialize(new
        {
            input.ScopeId,
            input.Failure.Classification,
            Profile = input.Profile.Digest,
            Mechanism = selected.Mechanism.Key,
            Sources = sources.Select(source => new { source.Kind, source.Digest, source.VerifiedBoundary }),
            input.EnvelopeDigest,
            EnvelopeDescriptor = input.EnvelopeDescriptor?.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            Policy = input.Policy.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            input.ContextBudget,
        });
        string identity = Sha256(identityMaterial);
        string[] allowedOmissions = sources.SelectMany(source => source.Omissions)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new RecoveryPlan(
            $"plan-{identity[..24]}",
            SchemaVersion,
            PlannerVersion,
            policyVersion,
            selected.Mechanism.Key,
            evidence.Append($"selected-rank={selected.Rank}").Order(StringComparer.Ordinal).ToArray(),
            sources,
            envelopeDigest: input.EnvelopeDigest,
            envelopeDescriptor: input.EnvelopeDescriptor ?? new Dictionary<string, string>(),
            selected.Mechanism.ActivationStrategy,
            selected.Mechanism.ValidationStrategy,
            selected.Mechanism.ReconciliationStrategy,
            selected.Eligibility.Completeness,
            allowedOmissions,
            input.Profile.Digest,
            new Dictionary<string, string>
            {
                ["conversation-read"] = input.Profile.Operation(SessionContinuityOperation.ConversationRead).Status.ToString(),
                ["conversation-write"] = input.Profile.Operation(SessionContinuityOperation.ConversationWrite).Status.ToString(),
                ["maximum-recoverable-context"] = input.Profile.MaximumRecoverableContext?.ToString() ?? "unknown",
            },
            $"recover-{identity}",
            retryCeiling: ParseNonNegative(input.Policy, "retry-ceiling", 1),
            failureBehavior: "fail-closed-preserve-original.v1");
    }

    private static int Rank(IReadOnlyDictionary<string, string> policy, RecoveryMechanismKey key) =>
        ParseNonNegative(policy, $"rank:{key.Identity}@{key.Version}", int.MaxValue / 2);

    private static int ParseNonNegative(IReadOnlyDictionary<string, string> policy, string key, int fallback) =>
        policy.TryGetValue(key, out string? value) && int.TryParse(value, out int parsed) && parsed >= 0
            ? parsed
            : fallback;

    private static string Value(IReadOnlyDictionary<string, string> policy, string key, string fallback) =>
        policy.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

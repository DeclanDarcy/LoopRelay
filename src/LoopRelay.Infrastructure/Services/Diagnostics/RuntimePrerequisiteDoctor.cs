using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Primitives.Diagnostics;

namespace LoopRelay.Infrastructure.Services.Diagnostics;

/// <summary>
/// Routes one resolved host-runtime profile to its provider-specific prerequisite inspector and
/// produces immutable typed evidence. Policy configuration and policy-owned environment inputs
/// never enter this authority.
/// </summary>
public sealed class RuntimePrerequisiteDoctor
{
    private readonly IReadOnlyDictionary<string, IRuntimePrerequisiteInspector> _inspectors;

    public RuntimePrerequisiteDoctor(
        Func<string, string?>? getEnvironmentVariable = null,
        Func<string, bool>? fileExists = null)
        : this([new CodexRuntimePrerequisiteInspector(getEnvironmentVariable, fileExists)])
    {
    }

    public RuntimePrerequisiteDoctor(IEnumerable<IRuntimePrerequisiteInspector> inspectors)
    {
        ArgumentNullException.ThrowIfNull(inspectors);
        IRuntimePrerequisiteInspector[] configured = inspectors.ToArray();
        if (configured.Any(inspector => string.IsNullOrWhiteSpace(inspector.Provider)))
        {
            throw new ArgumentException("Runtime prerequisite inspector provider must not be empty.", nameof(inspectors));
        }

        string[] duplicates = configured
            .GroupBy(inspector => inspector.Provider, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new ArgumentException(
                $"Multiple runtime prerequisite inspectors were registered for: {string.Join(", ", duplicates)}.",
                nameof(inspectors));
        }

        _inspectors = configured.ToDictionary(
            inspector => inspector.Provider,
            StringComparer.OrdinalIgnoreCase);
    }

    public RuntimePrerequisiteInspection Inspect(
        ResolvedRuntimeHostProfile profile,
        DateTimeOffset inspectedAt)
    {
        ArgumentNullException.ThrowIfNull(profile);
        IReadOnlyList<RuntimePrerequisiteFinding> findings;
        if (_inspectors.TryGetValue(profile.Provider, out IRuntimePrerequisiteInspector? inspector))
        {
            findings = inspector.Inspect(profile);
        }
        else
        {
            findings =
            [
                new RuntimePrerequisiteFinding(
                    RuntimePrerequisiteFindingCode.UnsupportedProvider,
                    "runtime.provider.unsupported",
                    RuntimePrerequisiteFindingSeverity.Error,
                    $"No runtime prerequisite inspector is registered for provider '{profile.Provider}'."),
            ];
        }

        RuntimePrerequisiteOverallStatus status = findings.Any(
            finding => finding.Severity == RuntimePrerequisiteFindingSeverity.Error)
            ? RuntimePrerequisiteOverallStatus.Unsatisfied
            : findings.Count > 0
                ? RuntimePrerequisiteOverallStatus.Degraded
                : RuntimePrerequisiteOverallStatus.Satisfied;
        return new RuntimePrerequisiteInspection(
            profile.Identity,
            profile.Provider,
            inspectedAt,
            findings,
            status);
    }
}

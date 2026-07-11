using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoopRelay.Certification;

public sealed class ContinuousCertificationRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly DimensionSpec[] RequiredDimensions =
    [
        new("status-canary", EvidenceLevel.LiveTransition, "status-canary.latest.json"),
        new("public-cli", EvidenceLevel.LiveTransition, "milestone-2.latest.json"),
        new("provider-profile", EvidenceLevel.LiveTransition, "milestone-3.latest.json"),
        new("transition-recovery", EvidenceLevel.LiveChainRecovery, "milestone-4.latest.json"),
        new("plan", EvidenceLevel.LiveTransition, "milestone-5.latest.json"),
        new("execute", EvidenceLevel.LiveTransition, "milestone-6.latest.json"),
        new("git-publication", EvidenceLevel.LiveTransition, "milestone-7.latest.json"),
        new("persistence", EvidenceLevel.LiveTransition, "milestone-8.latest.json"),
        new("traditional-roadmap", EvidenceLevel.LiveTransition, "milestone-9.latest.json"),
        new("eval-roadmap", EvidenceLevel.LiveTransition, "milestone-10.latest.json"),
        new("completion-closure", EvidenceLevel.LiveChainRecovery, "milestone-11.latest.json"),
        new("failure-oracle-matrix", EvidenceLevel.DeterministicComponent, "milestone-12.latest.json"),
        new("traditional-full-chain", EvidenceLevel.LiveChainRecovery, "milestone-13.latest.json"),
        new("eval-full-chain", EvidenceLevel.LiveChainRecovery, "milestone-14.latest.json"),
        new("windows-platform", EvidenceLevel.Replay, "platform-windows.latest.json"),
        new("linux-platform", EvidenceLevel.Replay, "platform-linux.latest.json"),
    ];

    public async Task<ContinuousCertificationResult> RunAsync(
        string workspaceRoot,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        string evidenceRoot = Path.Combine(authorityRoot, "evidence");
        Directory.CreateDirectory(evidenceRoot);
        string surfaceDigest = ProductionSurfaceDigest(workspaceRoot);
        string baselinePath = Path.Combine(evidenceRoot, "production-baseline.v1.json");
        Baseline? baseline = await ReadAsync<Baseline>(baselinePath, cancellationToken);
        bool surfaceCurrent = baseline is null || baseline.ProductionSurfaceDigest == surfaceDigest;
        var dimensions = new List<ReleaseDimensionResult>();
        foreach (DimensionSpec spec in RequiredDimensions)
        {
            string path = Path.Combine(evidenceRoot, spec.File);
            (bool evidencePassed, string evidenceClassification) = await EvidencePassedAsync(path, cancellationToken);
            bool ageCurrent = File.Exists(path) && DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(path) <= TimeSpan.FromDays(7);
            bool current = surfaceCurrent && ageCurrent;
            EvidenceLevel actual = evidencePassed && current ? spec.Required : EvidenceLevel.Uncovered;
            dimensions.Add(new ReleaseDimensionResult(
                spec.Identity,
                spec.Required,
                actual,
                spec.File,
                current,
                actual >= spec.Required,
                [
                    $"classification:{evidenceClassification}",
                    $"age-current:{ageCurrent}",
                    $"surface-current:{surfaceCurrent}",
                ]));
        }

        bool compatibility = CompatibilityFixturesCurrent(workspaceRoot);
        dimensions.Add(new ReleaseDimensionResult(
            "codex-compatibility",
            EvidenceLevel.Replay,
            compatibility && surfaceCurrent ? EvidenceLevel.Replay : EvidenceLevel.Uncovered,
            "codex-compatibility-manifest.json",
            compatibility && surfaceCurrent,
            compatibility && surfaceCurrent,
            ["exact-version-schema-fixture-pair"]));

        PlatformCertificationResult[] platforms = (await Task.WhenAll(
                new[] { "windows", "linux" }.Select(async platform =>
                    await ReadAsync<PlatformCertificationResult>(
                        Path.Combine(evidenceRoot, $"platform-{platform}.latest.json"), cancellationToken))))
            .Where(item => item is not null)
            .Cast<PlatformCertificationResult>()
            .ToArray();
        bool crossPlatform = platforms.Length == 2 &&
            platforms.All(item => item.Classification == CertificationClassification.Passed) &&
            platforms.Select(item => item.NormalizedContractDigest).Distinct(StringComparer.Ordinal).Count() == 1 &&
            platforms.Any(item => item.Platform == "windows" && !item.UnixExecutableBitObserved) &&
            platforms.Any(item => item.Platform == "linux" && item.UnixExecutableBitObserved);
        bool routesDistinct = ClassificationRoutes().Values.Distinct(StringComparer.Ordinal).Count() ==
            Enum.GetValues<CertificationClassification>().Length;
        bool noCriticalZero = dimensions.All(item => item.ActualLevel >= item.RequiredLevel);
        bool budgets = await BudgetsPassedAsync(evidenceRoot, cancellationToken);
        FailureCoverageCaseResult[] future = FutureTopologyObligations();
        CertificationTierResult[] tiers = Tiers();
        bool passed = surfaceCurrent && noCriticalZero && crossPlatform && routesDistinct && budgets;
        var evidence = new List<string>
        {
            $"production-surface:{surfaceDigest}",
            $"baseline-state:{(baseline is null ? "establish-on-pass" : surfaceCurrent ? "current" : "invalidated")}",
            $"dimensions:{dimensions.Count(item => item.Passed)}/{dimensions.Count}",
            $"platform-contract-agreement:{crossPlatform}",
            $"classification-routes-distinct:{routesDistinct}",
            $"budgets:{budgets}",
            $"future-topology-uncovered:{future.Length}",
        };
        IReadOnlyList<string> privacy = PrivacyScanner.Scan(string.Join('\n', evidence), authorityRoot);
        CertificationClassification classification = privacy.Count > 0
            ? CertificationClassification.OracleDrift
            : passed ? CertificationClassification.Passed : CertificationClassification.Blocked;
        var result = new ContinuousCertificationResult(
            CertificationRunner.ResultSchemaVersion,
            classification,
            surfaceDigest,
            tiers,
            dimensions,
            platforms,
            crossPlatform,
            routesDistinct,
            true,
            true,
            noCriticalZero,
            budgets,
            future,
            privacy,
            evidence);
        string resultPath = Path.Combine(evidenceRoot, "milestone-15.latest.json");
        await using (FileStream stream = File.Create(resultPath))
        {
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
        }
        if (classification == CertificationClassification.Passed && baseline is null)
        {
            var established = new Baseline(
                surfaceDigest,
                DateTimeOffset.UtcNow,
                RequiredDimensions.ToDictionary(
                    item => item.File,
                    item => FileDigest(Path.Combine(evidenceRoot, item.File)),
                    StringComparer.Ordinal));
            await using FileStream stream = File.Create(baselinePath);
            await JsonSerializer.SerializeAsync(stream, established, JsonOptions, cancellationToken);
        }
        return result;
    }

    private static CertificationTierResult[] Tiers() =>
    [
        new("hermetic-per-change", "every-change", EvidenceLevel.DeterministicComponent,
            ["workflow", "persistence", "oracles", "fixtures"], true),
        new("protocol-replay", "every-change", EvidenceLevel.Replay,
            ["provider-profile", "transport", "recovery"], true),
        new("low-cost-live", "daily-and-release", EvidenceLevel.LiveTransition,
            ["public-cli", "provider-profile", "plan", "execute"], true),
        new("full-chain-smoke", "release-and-denominator-drift", EvidenceLevel.LiveChainRecovery,
            ["traditional-full-chain", "eval-full-chain", "completion-closure"], true),
        new("scheduled-recovery", "weekly", EvidenceLevel.LiveChainRecovery,
            ["transition-recovery", "failure-oracle-matrix"], true),
        new("cross-platform", "release", EvidenceLevel.Replay,
            ["windows-platform", "linux-platform"], true),
        new("compatibility", "binary-schema-model-effort-change", EvidenceLevel.Replay,
            ["codex-compatibility"], true),
    ];

    private static IReadOnlyDictionary<CertificationClassification, string> ClassificationRoutes() =>
        new Dictionary<CertificationClassification, string>
        {
            [CertificationClassification.Passed] = "publish-evidence",
            [CertificationClassification.ProductRegression] = "block-release-product-owner",
            [CertificationClassification.ProviderRegression] = "hold-provider-profile-and-reprobe",
            [CertificationClassification.EnvironmentFailure] = "reroute-platform-infrastructure",
            [CertificationClassification.FixtureDrift] = "invalidate-fixture-and-review-authority",
            [CertificationClassification.OracleDrift] = "block-release-oracle-owner",
            [CertificationClassification.Blocked] = "surface-durable-operator-action",
            [CertificationClassification.UnsupportedCapability] = "record-exact-profile-incompatibility",
        };

    private static FailureCoverageCaseResult[] FutureTopologyObligations() =>
        new[]
        {
            "non-linear-workflow",
            "parallel-workflow",
            "effect-conflict",
            "shared-agents-authority",
            "git-merge-behavior",
            "quota-coordination",
            "ordering-independent-oracle",
            "cancellation-fan-out",
        }.Select(identity => new FailureCoverageCaseResult(
            identity,
            "future-topology",
            "remain-uncovered-until-production-support",
            EvidenceLevel.Uncovered,
            false,
            true,
            "architecture-certification",
            $"Recertify when production introduces {identity}.",
            true,
            ["release-visible-nonproduction-obligation"])).ToArray();

    private static async Task<bool> BudgetsPassedAsync(string evidenceRoot, CancellationToken token)
    {
        FullChainCertificationResult? traditional = await ReadAsync<FullChainCertificationResult>(
            Path.Combine(evidenceRoot, "milestone-13.latest.json"), token);
        FullChainCertificationResult? eval = await ReadAsync<FullChainCertificationResult>(
            Path.Combine(evidenceRoot, "milestone-14.latest.json"), token);
        return new[] { traditional, eval }.All(item => item is not null &&
            item.Classification == CertificationClassification.Passed &&
            item.TotalElapsedMilliseconds <= TimeSpan.FromHours(2).TotalMilliseconds &&
            item.ProviderEvidenceBytes <= 500L * 1024 * 1024 &&
            item.BudgetDecision.StartsWith("provisional-release-budget:", StringComparison.Ordinal));
    }

    private static bool CompatibilityFixturesCurrent(string workspace)
    {
        string manifest = Path.Combine(workspace, "src", "LoopRelay.Agents", "Services", "Codex", "Compatibility", "codex-compatibility-manifest.json");
        string fixtures = Path.Combine(workspace, "tests", "LoopRelay.Agents.Compatibility.Tests", "Fixtures");
        if (!File.Exists(manifest) || !Directory.Exists(fixtures)) return false;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifest));
        HashSet<string> manifestVersions = document.RootElement.GetProperty("entries").EnumerateArray()
            .Select(item => item.GetProperty("serverVersion").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> fixtureVersions = Directory.EnumerateFiles(fixtures, "codex-*-certification.json")
            .Select(path => JsonDocument.Parse(File.ReadAllBytes(path)))
            .Select(document => { using (document) return document.RootElement.GetProperty("serverVersion").GetString()!; })
            .ToHashSet(StringComparer.Ordinal);
        return manifestVersions.Count > 0 && manifestVersions.SetEquals(fixtureVersions);
    }

    private static async Task<(bool Passed, string Classification)> EvidencePassedAsync(
        string path,
        CancellationToken token)
    {
        if (!File.Exists(path)) return (false, "missing");
        try
        {
            await using FileStream stream = File.OpenRead(path);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            JsonElement value = document.RootElement.GetProperty("classification");
            bool passed = value.ValueKind == JsonValueKind.Number
                ? value.GetInt32() == (int)CertificationClassification.Passed
                : value.GetString() == CertificationClassification.Passed.ToString();
            return (passed, value.ToString());
        }
        catch (Exception exception) when (exception is IOException or JsonException or KeyNotFoundException)
        {
            return (false, "invalid-evidence");
        }
    }

    private static async Task<T?> ReadAsync<T>(string path, CancellationToken token)
    {
        if (!File.Exists(path)) return default;
        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, token);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string ProductionSurfaceDigest(string workspaceRoot)
    {
        IEnumerable<string> files = Directory.EnumerateFiles(Path.Combine(workspaceRoot, "src"), "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".prompt", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Concat(Directory.Exists(Path.Combine(workspaceRoot, "issues"))
                ? Directory.EnumerateFiles(Path.Combine(workspaceRoot, "issues"), "*.md", SearchOption.AllDirectories)
                : []);
        string material = string.Join('\n', files
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetRelativePath(workspaceRoot, path), StringComparer.Ordinal)
            .Select(path => $"{Path.GetRelativePath(workspaceRoot, path).Replace('\\', '/')}\0{FileDigest(path)}"));
        return Digest(material);
    }

    private static string FileDigest(string path) => File.Exists(path)
        ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))
        : "missing";

    private static string Digest(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record DimensionSpec(string Identity, EvidenceLevel Required, string File);

    private sealed record Baseline(
        string ProductionSurfaceDigest,
        DateTimeOffset EstablishedAt,
        IReadOnlyDictionary<string, string> EvidenceDigests);
}

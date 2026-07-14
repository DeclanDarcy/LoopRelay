using System.Text.RegularExpressions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Tests.Architecture;

public sealed partial class BaselineArchitectureContractTests
{
    [Fact]
    public void BlockingOwnerDecisionsAreAcceptedAndIndexed()
    {
        string root = RepositoryRoot();
        IReadOnlyDictionary<string, string[]> decisions = new Dictionary<string, string[]>
        {
            ["0001-logical-schema-v9.md"] = ["Status: Accepted", "CanonicalWorkspace v9", "physical-shape fingerprint"],
            ["0002-configuration-and-policy-authorities.md"] = ["Status: Accepted", "Runtime Authority receives only the resolved session policy"],
            ["0004-canonical-prompt-composition.md"] = ["Status: Accepted", "before hashing and persistence"],
            ["0009-canonical-prompt-dispatch-gateway.md"] = ["Status: Accepted", "identity-only Runtime dispatch", "`Planned`/`Authorized`"],
            ["0012-specific-reason-bearing-outcomes.md"] = ["Status: Accepted", "not canonical outcome values"],
        };

        string index = File.ReadAllText(Path.Combine(root, "docs", "architecture", "decisions", "README.md"));
        foreach ((string file, string[] requiredText) in decisions)
        {
            string path = Path.Combine(root, "docs", "architecture", "decisions", file);
            Assert.True(File.Exists(path), $"Missing blocking ADR: {file}");
            string content = File.ReadAllText(path);
            Assert.All(requiredText, fragment => Assert.Contains(fragment, content, StringComparison.Ordinal));
            Assert.Contains(file, index, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CanonicalRuntimeConsumesAuthorizationAndResolvedProfileContracts()
    {
        Type resolver = typeof(IExecutionAuthorizationResolver);
        var method = Assert.Single(resolver.GetMethods());

        Assert.Equal(typeof(ExecutionAuthorization), method.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(Task<ResolvedRuntimeProfile>), method.ReturnType);
        Assert.DoesNotContain(
            CanonicalRuntimeTypes().SelectMany(type =>
                type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                    .Concat(type.GetMethods().SelectMany(candidate => candidate.GetParameters()))),
            parameter => parameter.ParameterType == typeof(BrainConfiguration));
    }

    [Fact]
    public void CanonicalOutcomeVocabularyContainsNoGenericLatch()
    {
        string[] names = Enum.GetNames<RuntimeOutcomeKind>();

        Assert.DoesNotContain("Blocked", names);
        Assert.DoesNotContain(names, name => name.Contains("Unblock", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nameof(RuntimeOutcomeKind.MissingRequiredInput), names);
        Assert.Contains(nameof(RuntimeOutcomeKind.DirtyInputSurface), names);
        Assert.Contains(nameof(RuntimeOutcomeKind.EffectsPending), names);
        Assert.Contains(nameof(RuntimeOutcomeKind.RecoveryRequired), names);
        Assert.Contains(nameof(RuntimeOutcomeKind.HumanDecisionRequired), names);
        Assert.Contains(nameof(RuntimeOutcomeKind.UnsupportedProviderCapability), names);
        Assert.Contains(nameof(RuntimeOutcomeKind.CompatibilityImportRequired), names);
    }

    [Fact]
    public void GeneratedSpecificationSetHasOneExistingSourceAndNoBrokenNormativeLinks()
    {
        string root = RepositoryRoot();
        string specs = Path.Combine(root, ".agents", "specs");
        string source = Path.Combine(specs, "epic.md");
        string indexPath = Path.Combine(specs, "README.md");
        Assert.True(File.Exists(source), "The durable roadmap source must be .agents/specs/epic.md.");

        string index = File.ReadAllText(indexPath);
        Assert.StartsWith("<!-- BEGIN GENERATED: source=.agents/specs/epic.md version=3.0 index -->", index);

        Match[] milestoneRows = MilestoneRowRegex().Matches(index).Cast<Match>().ToArray();
        int[] milestoneIds = milestoneRows.Select(match => int.Parse(match.Groups[1].Value)).ToArray();
        Assert.Equal(Enumerable.Range(0, 22), milestoneIds.Order());
        Assert.Equal(milestoneIds.Length, milestoneIds.Distinct().Count());

        string[] deepDives = Directory.GetFiles(specs, "m*-deep-dive.md");
        Assert.Equal(14, deepDives.Length);
        foreach (string path in deepDives)
        {
            string content = File.ReadAllText(path);
            Match header = GeneratedHeaderRegex().Match(content);
            Assert.True(header.Success, $"Generated source/version header is invalid: {Path.GetFileName(path)}");
            string milestone = header.Groups[1].Value;
            Assert.Matches($"^m{milestone}-", Path.GetFileName(path));
            Assert.InRange(int.Parse(milestone), 8, 21);
            AssertAllMarkdownLinksResolve(path, content);
        }

        AssertAllMarkdownLinksResolve(indexPath, index);
        foreach ((int milestone, string commit) in AcceptedPreservationCommits())
        {
            Assert.Contains($"| M{milestone} |", index, StringComparison.Ordinal);
            Assert.Contains(commit, index, StringComparison.Ordinal);
        }
    }

    private static IReadOnlyList<Type> CanonicalRuntimeTypes() =>
    [
        typeof(ExecutionAuthorizationResolver),
        typeof(PromptDispatchGateway),
        typeof(LoadingPromptRuntimeDispatcher),
        typeof(TransitionRuntime),
    ];

    private static IReadOnlyDictionary<int, string> AcceptedPreservationCommits() =>
        new Dictionary<int, string>
        {
            [0] = "9f6418f5",
            [1] = "8c0b11a4",
            [2] = "87c97444",
            [3] = "ab10e06b",
            [4] = "b1b9aa8a",
            [5] = "96d41f44",
            [6] = "45053775",
            [7] = "10dd9494",
        };

    private static void AssertAllMarkdownLinksResolve(string documentPath, string content)
    {
        foreach (Match match in MarkdownLinkRegex().Matches(content))
        {
            string target = match.Groups[1].Value.Split('#', 2)[0];
            if (string.IsNullOrWhiteSpace(target) || Uri.TryCreate(target, UriKind.Absolute, out _))
            {
                continue;
            }

            string resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(documentPath)!, target));
            Assert.True(File.Exists(resolved), $"Broken normative link '{target}' in {documentPath}.");
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoopRelay.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    [GeneratedRegex(@"(?m)^\| M(\d+) \|")]
    private static partial Regex MilestoneRowRegex();

    [GeneratedRegex(@"\]\(([^)]+\.md(?:#[^)]*)?)\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"\A<!-- BEGIN GENERATED: source=\.agents/specs/epic\.md version=3\.0 milestone=M(\d+) -->")]
    private static partial Regex GeneratedHeaderRegex();
}

using System.Reflection;
using System.Xml.Linq;

namespace CommandCenter.Backend.Tests;

/// <summary>
/// Enforces the refactor-plan governing invariant at the project-dependency level:
/// Operational (Execution) and Decision (DecisionSessions) are distinct session ROLES
/// that both reach Codex only through the shared, role-agnostic <c>CommandCenter.Agents</c>
/// runtime — and neither role nor the shared center may reach back up into composition layers.
///
/// Negative (isolation) invariants are asserted via reflection over the compiled assembly
/// manifest (sound: an unused reference is pruned by the compiler, so absence is real).
/// The positive "DecisionSessions is wired to Agents" invariant cannot be seen by reflection
/// until a concrete Agents type is consumed (refactor-plan Phase 3), so it is asserted
/// structurally against the .csproj build graph.
/// </summary>
public sealed class ArchitectureLayeringTests
{
    // One public anchor type per production assembly, so typeof(T).Assembly resolves the
    // compiled assembly for reflection over its metadata references.
    private static Assembly AgentsAssembly => typeof(CommandCenter.Agents.Abstractions.IAgentSession).Assembly;
    private static Assembly CoreAssembly => typeof(CommandCenter.Core.Repositories.Repository).Assembly;
    private static Assembly DecisionSessionsAssembly => typeof(CommandCenter.DecisionSessions.Models.DecisionSessionHealthStatus).Assembly;
    private static Assembly ExecutionAssembly => typeof(CommandCenter.Execution.Abstractions.IGitService).Assembly;

    private static HashSet<string> ReferencedCommandCenterAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(name => name.StartsWith("CommandCenter.", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Agents_is_role_agnostic_and_references_no_other_CommandCenter_project()
    {
        // The shared runtime must select no role: if it referenced any product project it
        // would no longer be reusable across the Operational and Decision roles.
        Assert.Empty(ReferencedCommandCenterAssemblies(AgentsAssembly));
    }

    [Fact]
    public void Core_is_the_shared_center_and_references_no_other_CommandCenter_project()
    {
        Assert.Empty(ReferencedCommandCenterAssemblies(CoreAssembly));
    }

    [Theory]
    [InlineData("CommandCenter.Execution")]
    [InlineData("CommandCenter.Workflow")]
    [InlineData("CommandCenter.Middle")]
    [InlineData("CommandCenter.Backend")]
    public void DecisionSessions_does_not_reference_operational_or_higher_layers(string forbidden)
    {
        // A Decision Session reasons over handoffs; it must not reach Execution's operational
        // orchestration (ExecutionSessionService, Git/commit/push lifecycle) or any layer above it.
        Assert.DoesNotContain(forbidden, ReferencedCommandCenterAssemblies(DecisionSessionsAssembly));
    }

    [Theory]
    [InlineData("CommandCenter.DecisionSessions")]
    [InlineData("CommandCenter.Workflow")]
    [InlineData("CommandCenter.Middle")]
    [InlineData("CommandCenter.Backend")]
    public void Execution_does_not_reference_the_decision_role_or_higher_layers(string forbidden)
    {
        Assert.DoesNotContain(forbidden, ReferencedCommandCenterAssemblies(ExecutionAssembly));
    }

    [Fact]
    public void DecisionSessions_csproj_references_the_shared_Agents_runtime()
    {
        Assert.True(
            HasActiveProjectReference("CommandCenter.DecisionSessions", "CommandCenter.Agents"),
            "CommandCenter.DecisionSessions must reference the role-agnostic CommandCenter.Agents runtime " +
            "so the Decision role reaches Codex only through Agents (refactor-plan governing invariant).");
    }

    [Fact]
    public void DecisionSessions_csproj_does_not_reference_Execution()
    {
        Assert.False(
            HasActiveProjectReference("CommandCenter.DecisionSessions", "CommandCenter.Execution"),
            "CommandCenter.DecisionSessions must not reference Execution's operational orchestration.");
    }

    // XDocument.Load discards XML comments, so a commented-out ProjectReference never appears
    // as an element — anything matched here is an active build-graph edge.
    private static bool HasActiveProjectReference(string project, string referencedProject)
    {
        var csproj = XDocument.Load(RepoLayout.ProjectFile(project));
        var needle = $"{referencedProject}\\{referencedProject}.csproj";
        return csproj.Descendants("ProjectReference")
            .Select(element => ((string?)element.Attribute("Include") ?? string.Empty).Replace('/', '\\'))
            .Any(include => include.EndsWith(needle, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Enforces Prompt Authority: production source must source all canonical prompt text from
/// the generated <c>CommandCenter.Core.Prompts</c> catalog rather than hand-composing it.
/// The live execution path (<c>ExecutionPromptBuilder</c>) previously diverged from the
/// catalog by composing literals; this locks that regression out.
/// </summary>
public sealed class PromptAuthorityTests
{
    // Distinctive verbatim fragments of canonical prompt bodies, sourced from
    // src/CommandCenter.Core/Prompts/*.prompt. If any of these appears in production source
    // outside the catalog, canonical prompt text has been duplicated.
    private static readonly string[] CanonicalPromptMarkers =
    {
        "start executing the first milestone",
        "continue executing the current milestone",
        "then write .agents/handoffs/handoff.md with:",
        "Each milestone markdown should be formatted such that progress can be tracked using checkboxes for itemized work",
        "extract every piece of information that should persist beyond the current session",
        "it evolves into the authoritative description of both the remaining work and everything learned while executing it",
        "the roadmap and specs will be deleted after certifying the plan, the plan must be self-contained",
        "write .agents/plan.md to implement the roadmap and specs in .agents/specs/ against the current codebase",
        "Provide clear directions for the next execution agent, including distinct decisions regarding all options and ambiguities raised in the session report",
        "Execution of this plan is starting. Wait for the first session report to respond.",
        "Execution of this work is continuing. Wait for the next session report to respond.",
    };

    [Fact]
    public void Production_source_does_not_duplicate_canonical_prompt_text()
    {
        // Scope is production src/. The catalog dir authors the text; generated *.g.cs in obj/
        // legitimately contains it. Test-tree assertion literals are tracked as a separate
        // follow-up (plan line 46) and are out of scope for this production-composition guard.
        var srcRoot = Path.Combine(RepoLayout.RepoRoot, "src");
        var catalogDir = Path.Combine(srcRoot, "CommandCenter.Core", "Prompts");

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.StartsWith(catalogDir, StringComparison.OrdinalIgnoreCase)) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)) continue;
            if (file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) continue;

            var text = File.ReadAllText(file);
            foreach (var marker in CanonicalPromptMarkers)
            {
                if (text.Contains(marker, StringComparison.Ordinal))
                {
                    offenders.Add($"{file} :: \"{marker}\"");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Prompt Authority violation — canonical prompt text duplicated in production source " +
            "instead of being sourced from CommandCenter.Core.Prompts:\n" + string.Join("\n", offenders));
    }
}

/// <summary>Locates the repository root from the test output directory.</summary>
internal static class RepoLayout
{
    private static readonly Lazy<string> Root = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CommandCenter.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    });

    public static string RepoRoot => Root.Value;

    public static string ProjectFile(string project) =>
        Path.Combine(RepoRoot, "src", project, $"{project}.csproj");
}

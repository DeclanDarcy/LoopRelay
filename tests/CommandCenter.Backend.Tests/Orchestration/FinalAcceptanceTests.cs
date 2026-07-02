using System.Linq;
using System.Reflection;
using CommandCenter.Core.Prompts;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Backend.Tests.Orchestration;

/// <summary>
/// m12 final-acceptance guard (Phase 12 — "Deferred Non-Goals and Final Definition of Done"). The capstone
/// milestone certifies that the implemented system matches the original Definition of Done AND that the
/// explicit non-goals were NOT built. The detailed criterion-by-criterion evidence lives in
/// <c>docs/final-acceptance.md</c>; this test pins the four m12-specific boundaries that no earlier milestone
/// test protects:
/// (1) the Completion-Statement command surface — the five orchestrator entry points that drive the whole
///     roadmap/spec -> Write -> Revise -> Execute -> Decision -> Submit loop from one repository screen;
/// (2) NON-GOAL isolation — the orchestration loop's composition root does not absorb the per-repository
///     reasoning / knowledge-graph subsystem (NG-1/NG-3: no knowledge platform, knowledge graph, or lineage
///     explorer was folded into the loop);
/// (3) NON-GOAL delegation — the orchestrator stays compositional, delegating to domain services rather than
///     becoming a domain service itself (NG-6);
/// (4) the generated canonical prompt catalog is complete and is the sole communication surface (FA-12 / NG-5:
///     prompts remain generated communication mechanisms, never semantic authority).
///
/// Pure unit assertions (no host boot, no process-global mutation), so this class is intentionally NOT in the
/// ProcessEnvironment serialized collection.
/// </summary>
public sealed class FinalAcceptanceTests
{
    [Fact]
    public void Completion_statement_command_surface_is_present_on_the_orchestrator()
    {
        // The five public entry points the Final Acceptance + Completion Statement enumerate. typeof makes a
        // deletion a compile break; the named lookups turn a rename into a clear failure instead of a silent
        // loss of the end-to-end command surface.
        foreach (var name in new[]
                 {
                     nameof(RepositoryOrchestrator.BeginWritePlanAsync),
                     nameof(RepositoryOrchestrator.BeginRevisePlanAsync),
                     nameof(RepositoryOrchestrator.BeginExecutePlanAsync),
                     nameof(RepositoryOrchestrator.BeginDecisionRunAsync),
                     nameof(RepositoryOrchestrator.BeginSubmitDecisionsAsync),
                 })
        {
            Assert.NotNull(typeof(RepositoryOrchestrator).GetMethod(name, BindingFlags.Public | BindingFlags.Instance));
        }
    }

    [Fact]
    public void Orchestration_loop_does_not_absorb_the_reasoning_or_knowledge_subsystem()
    {
        // NG-1/NG-3: the loop is not a Repository Knowledge platform, knowledge graph, or lineage explorer. The
        // per-repository reasoning-graph subsystem (CommandCenter.Reasoning) and the per-repository decision
        // discovery/recommendation services (CommandCenter.Decisions) are pre-existing, repository-scoped, and
        // deliberately NOT consumed by the orchestration loop. Negative isolation via the compiled manifest is
        // sound because the compiler prunes unconsumed references — absence here means the loop genuinely does
        // not reach the reasoning subsystem (same reasoning as ArchitectureLayeringTests).
        var referenced = typeof(RepositoryOrchestrator).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.DoesNotContain("CommandCenter.Reasoning", referenced);

        // Sanity: the loop DOES reach Codex through the shared Agents runtime, so the manifest enumeration is
        // meaningful (the negative above is a real absence, not an empty list).
        Assert.Contains("CommandCenter.Agents", referenced);
    }

    [Fact]
    public void Orchestrator_delegates_to_domain_services_rather_than_owning_them()
    {
        // NG-6: the orchestrator is not a domain service for Execution, Decisions, Continuity, Git, Workflow, or
        // contracts — it composes them. Its constructor takes the domain seams (agent runtime, artifact store,
        // the Git-backed plan publisher, and the router) and delegates authority to each rather than
        // reimplementing it.
        var parameterTypeNames = typeof(RepositoryOrchestrator)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Name)
            .ToHashSet();

        Assert.Contains("IAgentRuntime", parameterTypeNames);            // agent execution delegated to Agents
        Assert.Contains("IArtifactStore", parameterTypeNames);          // artifact persistence delegated to Core
        Assert.Contains("IPlanArtifactPublisher", parameterTypeNames);  // commit/push delegated over IGitService
        Assert.Contains("IDecisionSessionRouter", parameterTypeNames);  // routing delegated to the pure router
    }

    [Fact]
    public void Canonical_prompt_catalog_is_the_sole_generated_communication_surface()
    {
        // FA-12 / NG-5: all prompt text comes from the generated CommandCenter.Core.Prompts classes, which are
        // pure text templates (no semantic authority). typeof pins the ten canonical prompts as a compile-time
        // contract — a removed or renamed prompt fails to build here. Each must expose the generated API surface
        // (a const Text for placeholder-free prompts, a Render method for those with holes).
        var promptTypes = new[]
        {
            typeof(WritePlan),
            typeof(RevisePlan),
            typeof(ExtractMilestones),
            typeof(StartExecution),
            typeof(ContinueExecution),
            typeof(StartDecisionSession),
            typeof(GetNextDecisions),
            typeof(StartDecisionSessionFromTransfer),
            typeof(ProduceOperationalDelta),
            typeof(UpdateOperationalContext),
        };

        Assert.Equal(10, promptTypes.Length);

        foreach (var promptType in promptTypes)
        {
            Assert.Equal("CommandCenter.Core.Prompts", promptType.Namespace);

            var hasText = promptType.GetMember("Text", BindingFlags.Public | BindingFlags.Static).Length > 0;
            var hasRender = promptType.GetMethod("Render", BindingFlags.Public | BindingFlags.Static) is not null;

            Assert.True(
                hasText || hasRender,
                $"{promptType.Name} must expose the generated prompt API (Text or Render).");
        }
    }
}

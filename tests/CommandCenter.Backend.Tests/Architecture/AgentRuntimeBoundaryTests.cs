using CommandCenter.Agents.Services;

namespace CommandCenter.Backend.Tests.Architecture;

public sealed class AgentRuntimeBoundaryTests
{
    [Fact]
    public void AgentsAssemblyDoesNotReferenceDomainOrProductAssemblies()
    {
        string[] forbiddenReferences =
        [
            "CommandCenter.Execution",
            "CommandCenter.Decisions",
            "CommandCenter.DecisionSessions",
            "CommandCenter.Workflow",
            "CommandCenter.Continuity",
            "CommandCenter.Reasoning",
            "CommandCenter.Middle",
            "CommandCenter.Backend",
            "CommandCenter.UI"
        ];

        string[] actualReferences = typeof(ProcessRunner)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        foreach (string forbiddenReference in forbiddenReferences)
        {
            Assert.DoesNotContain(forbiddenReference, actualReferences);
        }
    }
}

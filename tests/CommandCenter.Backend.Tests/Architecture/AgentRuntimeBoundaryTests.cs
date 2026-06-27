using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Services;
using CommandCenter.Agents.Models;

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

    [Fact]
    public void RuntimePrimitivesStayInAgentsAssembly()
    {
        Type[] primitiveTypes =
        [
            typeof(SessionIdentity),
            typeof(SessionRole),
            typeof(AgentSessionSpec),
            typeof(SandboxProfile),
            typeof(EffortProfile),
            typeof(AgentProcessState),
            typeof(AgentTurnState),
            typeof(IAgentProcess)
        ];

        foreach (Type primitiveType in primitiveTypes)
        {
            Assert.Same(typeof(ProcessRunner).Assembly, primitiveType.Assembly);
        }
    }

    [Fact]
    public void AgentSessionSpecTakesImmutableStartupOptionSnapshot()
    {
        Dictionary<string, string> startupOptions = new(StringComparer.Ordinal)
        {
            ["provider"] = "codex"
        };

        AgentSessionSpec spec = new(
            SessionIdentity.New(),
            SessionRole.OperationalExecution,
            new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: true),
            new EffortProfile(AgentEffortLevel.Medium),
            workingDirectory: "C:\\repo",
            startupOptions);

        startupOptions["provider"] = "other";

        Assert.Equal("codex", spec.StartupOptions["provider"]);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(spec.StartupOptions);
    }

    [Fact]
    public void AgentProcessBoundaryDoesNotExposeOperatingSystemProcessTypes()
    {
        Type[] exposedTypes = typeof(IAgentProcess)
            .GetMembers()
            .SelectMany(member => member switch
            {
                System.Reflection.PropertyInfo property => [property.PropertyType],
                System.Reflection.MethodInfo method => method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .Append(method.ReturnType),
                _ => []
            })
            .ToArray();

        Assert.DoesNotContain(exposedTypes, type => type.FullName == "System.Diagnostics.Process");
    }
}

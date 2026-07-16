using System.Text.Json;
using LoopRelay.Certification;
using LoopRelay.Orchestration.Workflows;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return 0;
}

string command = args[0];
if (!CertificationCommandCatalog.IsKnown(command))
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Console.Error.WriteLine("Run with --help to list the post-epic certification commands.");
    return 2;
}

Dictionary<string, string?> options;
try
{
    options = ParseOptions(args.Skip(1).ToArray());
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

if (options.TryGetValue("--model", out string? selectedModel))
{
    try
    {
        CertificationFixtureSettings.SelectBrainModel(selectedModel ?? string.Empty);
    }
    catch (ArgumentException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
}

if (!options.TryGetValue("--workspace", out string? workspace) || string.IsNullOrWhiteSpace(workspace))
{
    Console.Error.WriteLine("--workspace <path> is required.");
    return 2;
}

workspace = Path.GetFullPath(workspace);
CertificationCommandDefinition definition = CertificationCommandCatalog.Commands.Single(item => item.Name == command);
string? cli = RequireOption(definition.RequiresCli, "--cli", options);
if (definition.RequiresCli && cli is null) return 2;
string? codex = RequireOption(definition.RequiresProvider, "--codex", options);
string? auth = RequireOption(definition.RequiresProvider, "--auth", options);
if (definition.RequiresProvider && (codex is null || auth is null)) return 2;

string authorityRoot = options.TryGetValue("--case-root", out string? configuredRoot) &&
    !string.IsNullOrWhiteSpace(configuredRoot)
        ? Path.GetFullPath(configuredRoot)
        : Path.Combine(workspace, ".tmp", "certification");
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

try
{
    switch (command)
    {
        case CertificationCommandCatalog.CoverageLedger:
            Console.WriteLine(JsonSerializer.Serialize(CoverageLedgerBuilder.Build(workspace), json));
            return 0;

        case CertificationCommandCatalog.PlatformProbe:
            PlatformCertificationResult platform = await new PlatformCertificationRunner().RunAsync(authorityRoot);
            return WriteResult(platform, platform.Classification);

        case CertificationCommandCatalog.FailureOracleMatrix:
            FailureOracleMatrixCertificationResult matrix = await new FailureOracleMatrixRunner().RunAsync(
                workspace, authorityRoot);
            return WriteResult(matrix, matrix.Classification);

        case CertificationCommandCatalog.ReleaseGate:
            ReleaseGateResult gate = await new ReleaseGateRunner().RunAsync(workspace, authorityRoot);
            return WriteResult(gate, gate.Classification);

        case CertificationCommandCatalog.ProviderProfile:
            ProviderProfileCertificationResult profile = await new ProviderProfileRunner().RunAsync(
                Path.GetFullPath(codex!), Path.GetFullPath(auth!), authorityRoot);
            return WriteResult(profile, profile.Classification);

        case CertificationCommandCatalog.TransitionRecovery:
            TransitionRecoveryCertificationResult recovery = await new TransitionRecoveryRunner().RunAsync(
                Path.GetFullPath(codex!), Path.GetFullPath(auth!), Path.GetFullPath(cli!), authorityRoot);
            return WriteResult(recovery, recovery.Classification);

        case CertificationCommandCatalog.PlanWorkflow:
            PlanWorkflowCertificationResult plan = await new PlanWorkflowRunner().RunAsync(
                Path.GetFullPath(codex!), Path.GetFullPath(auth!), Path.GetFullPath(cli!), authorityRoot);
            return WriteResult(plan, plan.Classification);

        case CertificationCommandCatalog.ExecuteWorkflow:
            ExecuteWorkflowCertificationResult execute = await new ExecuteWorkflowRunner().RunAsync(
                Path.GetFullPath(codex!), Path.GetFullPath(auth!), Path.GetFullPath(cli!), authorityRoot);
            return WriteResult(execute, execute.Classification);

        case CertificationCommandCatalog.TraditionalRoadmap:
        case CertificationCommandCatalog.EvalRoadmap:
            WorkflowIdentity roadmapWorkflow = command == CertificationCommandCatalog.TraditionalRoadmap
                ? WorkflowIdentity.TraditionalRoadmap
                : WorkflowIdentity.EvalRoadmap;
            RoadmapLiveCertificationResult roadmap = await new RoadmapLiveRunner().RunAsync(
                roadmapWorkflow, Path.GetFullPath(codex!), Path.GetFullPath(auth!),
                Path.GetFullPath(cli!), authorityRoot);
            return WriteResult(roadmap, roadmap.Classification);

        case CertificationCommandCatalog.CompletionClosure:
            CompletionClosureCertificationResult completion = await new CompletionClosureRunner().RunAsync(
                Path.GetFullPath(codex!), Path.GetFullPath(auth!), Path.GetFullPath(cli!), authorityRoot);
            return WriteResult(completion, completion.Classification);

        case CertificationCommandCatalog.TraditionalFullChain:
        case CertificationCommandCatalog.EvalFullChain:
            WorkflowIdentity fullChainWorkflow = command == CertificationCommandCatalog.TraditionalFullChain
                ? WorkflowIdentity.TraditionalRoadmap
                : WorkflowIdentity.EvalRoadmap;
            FullChainCertificationResult chain = await new FullChainLiveRunner().RunAsync(
                fullChainWorkflow, Path.GetFullPath(codex!), Path.GetFullPath(auth!),
                Path.GetFullPath(cli!), authorityRoot, options.ContainsKey("--retain-case"));
            return WriteResult(chain, chain.Classification);

        case CertificationCommandCatalog.GitPublication:
            GitPublicationCertificationResult publication = await new GitPublicationRunner().RunAsync(
                Path.GetFullPath(cli!), authorityRoot);
            return WriteResult(publication, publication.Classification);

        case CertificationCommandCatalog.PersistenceLifecycle:
            PersistenceLifecycleCertificationResult persistence = await new PersistenceLifecycleRunner().RunAsync(
                Path.GetFullPath(cli!), authorityRoot);
            return WriteResult(persistence, persistence.Classification);

        case CertificationCommandCatalog.PublicCliContracts:
            PublicCliContractsCertificationResult publicCli = await new PublicCliContractsRunner().RunAsync(
                new CertificationOptions(workspace, Path.GetFullPath(cli!), authorityRoot,
                    options.ContainsKey("--retain-case")));
            return WriteResult(publicCli, publicCli.Classification);

        case CertificationCommandCatalog.StatusCanary:
            StatusCanaryCertificationResult status = await new StatusCanaryRunner().RunStatusCanaryAsync(
                new CertificationOptions(workspace, Path.GetFullPath(cli!), authorityRoot,
                    options.ContainsKey("--retain-case")));
            return WriteResult(status, status.Classification);

        default:
            throw new InvalidOperationException($"Command routing is incomplete for `{command}`.");
    }
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

int WriteResult<T>(T result, CertificationClassification classification)
{
    Console.WriteLine(JsonSerializer.Serialize(result, json));
    return classification == CertificationClassification.Passed ? 0 : 1;
}

static string? RequireOption(
    bool required,
    string option,
    IReadOnlyDictionary<string, string?> options)
{
    if (!required) return null;
    if (options.TryGetValue(option, out string? value) && !string.IsNullOrWhiteSpace(value)) return value;
    Console.Error.WriteLine($"{option} <path> is required for this certification command.");
    return null;
}

static void PrintUsage()
{
    Console.WriteLine("Usage: looprelay-certification <command> --workspace <path> [--cli <path>] [--codex <path>] [--auth <path>] [--case-root <path>] [--model <gpt-5.3-codex-spark|gpt-5.4-mini>] [--retain-case]");
    Console.WriteLine();
    Console.WriteLine("This executable is reserved for post-epic completion hardening; it is not part of routine run-all-tests verification.");
    Console.WriteLine("Live certification defaults to gpt-5.3-codex-spark at medium effort.");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    foreach (CertificationCommandDefinition item in CertificationCommandCatalog.Commands)
    {
        string kind = item.Kind.ToString().ToLowerInvariant();
        Console.WriteLine($"  {item.Name,-28} [{kind}] {item.Purpose}");
    }
    Console.WriteLine();
    Console.WriteLine("See docs/certification.md for prerequisites, campaign order, evidence handling, and failure adjudication.");
}

static Dictionary<string, string?> ParseOptions(string[] values)
{
    var parsed = new Dictionary<string, string?>(StringComparer.Ordinal);
    for (int index = 0; index < values.Length; index++)
    {
        string option = values[index];
        if (option == "--retain-case")
        {
            parsed[option] = null;
            continue;
        }

        if (!option.StartsWith("--", StringComparison.Ordinal) || index + 1 >= values.Length)
        {
            throw new ArgumentException($"Invalid option: {option}");
        }

        parsed[option] = values[++index];
    }

    return parsed;
}

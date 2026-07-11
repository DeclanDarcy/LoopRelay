using System.Text.Json;
using LoopRelay.Certification;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: looprelay-certification <canary|ledger> --workspace <path> [--cli <path>] [--case-root <path>] [--retain-case]");
    return 0;
}

string command = args[0];
Dictionary<string, string?> options = ParseOptions(args.Skip(1).ToArray());
if (!options.TryGetValue("--workspace", out string? workspace) || string.IsNullOrWhiteSpace(workspace))
{
    Console.Error.WriteLine("--workspace <path> is required.");
    return 2;
}

workspace = Path.GetFullPath(workspace);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
if (command == "ledger")
{
    Console.WriteLine(JsonSerializer.Serialize(CoverageLedgerBuilder.Build(workspace), json));
    return 0;
}

if (command == "platform")
{
    string platformRoot = options.TryGetValue("--case-root", out string? configured) && !string.IsNullOrWhiteSpace(configured)
        ? Path.GetFullPath(configured)
        : Path.Combine(workspace, ".tmp", "certification");
    PlatformCertificationResult platform = await new PlatformCertificationRunner().RunAsync(platformRoot);
    Console.WriteLine(JsonSerializer.Serialize(platform, json));
    return platform.Classification == CertificationClassification.Passed ? 0 : 1;
}

if (command is not ("canary" or "milestone2" or "milestone3" or "milestone4" or "milestone5" or "milestone6" or "milestone7" or "milestone8" or "milestone9" or "milestone10" or "milestone11" or "milestone12" or "milestone13" or "milestone14" or "milestone15"))
{
    Console.Error.WriteLine($"Unknown command: {command}");
    return 2;
}

if (command == "milestone12")
{
    string matrixRoot = options.TryGetValue("--case-root", out string? configured) && !string.IsNullOrWhiteSpace(configured)
        ? Path.GetFullPath(configured)
        : Path.Combine(workspace, ".tmp", "certification");
    MilestoneTwelveCertificationResult matrix = await new MilestoneTwelveRunner().RunAsync(
        workspace, matrixRoot);
    Console.WriteLine(JsonSerializer.Serialize(matrix, json));
    return matrix.Classification == CertificationClassification.Passed ? 0 : 1;
}

if (command == "milestone15")
{
    string continuousRoot = options.TryGetValue("--case-root", out string? configured) && !string.IsNullOrWhiteSpace(configured)
        ? Path.GetFullPath(configured)
        : Path.Combine(workspace, ".tmp", "certification");
    ContinuousCertificationResult continuous = await new ContinuousCertificationRunner().RunAsync(
        workspace, continuousRoot);
    Console.WriteLine(JsonSerializer.Serialize(continuous, json));
    return continuous.Classification == CertificationClassification.Passed ? 0 : 1;
}

if (command is "milestone3" or "milestone4" or "milestone5" or "milestone6" or "milestone9" or "milestone10" or "milestone11" or "milestone13" or "milestone14")
{
    if (!options.TryGetValue("--codex", out string? codex) || string.IsNullOrWhiteSpace(codex) ||
        !options.TryGetValue("--auth", out string? auth) || string.IsNullOrWhiteSpace(auth))
    {
        Console.Error.WriteLine("--codex <path> and --auth <path> are required for live certification.");
        return 2;
    }

    string liveRoot = options.TryGetValue("--case-root", out string? liveConfigured) && !string.IsNullOrWhiteSpace(liveConfigured)
        ? Path.GetFullPath(liveConfigured)
        : Path.Combine(workspace, ".tmp", "certification");
    if (command == "milestone3")
    {
        MilestoneThreeCertificationResult live = await new MilestoneThreeRunner().RunAsync(
            Path.GetFullPath(codex), Path.GetFullPath(auth), liveRoot);
        Console.WriteLine(JsonSerializer.Serialize(live, json));
        return live.Classification == CertificationClassification.Passed ? 0 : 1;
    }

    if (!options.TryGetValue("--cli", out string? recoveryCli) || string.IsNullOrWhiteSpace(recoveryCli))
    {
        Console.Error.WriteLine("--cli <path> is required for milestone4, milestone5, and milestone6.");
        return 2;
    }
    if (command == "milestone5")
    {
        MilestoneFiveCertificationResult plan = await new MilestoneFiveRunner().RunAsync(
            Path.GetFullPath(codex), Path.GetFullPath(auth), Path.GetFullPath(recoveryCli), liveRoot);
        Console.WriteLine(JsonSerializer.Serialize(plan, json));
        return plan.Classification == CertificationClassification.Passed ? 0 : 1;
    }
    if (command == "milestone6")
    {
        MilestoneSixCertificationResult execute = await new MilestoneSixRunner().RunAsync(
            Path.GetFullPath(codex), Path.GetFullPath(auth), Path.GetFullPath(recoveryCli), liveRoot);
        Console.WriteLine(JsonSerializer.Serialize(execute, json));
        return execute.Classification == CertificationClassification.Passed ? 0 : 1;
    }
    if (command is "milestone9" or "milestone10")
    {
        if (!options.TryGetValue("--cli", out string? roadmapCli) || string.IsNullOrWhiteSpace(roadmapCli))
        {
            Console.Error.WriteLine("--cli <path> is required for live roadmap certification.");
            return 2;
        }
        LoopRelay.Orchestration.Workflows.WorkflowIdentity workflow = command == "milestone9"
            ? LoopRelay.Orchestration.Workflows.WorkflowIdentity.TraditionalRoadmap
            : LoopRelay.Orchestration.Workflows.WorkflowIdentity.EvalRoadmap;
        RoadmapLiveCertificationResult roadmap = await new RoadmapLiveRunner().RunAsync(
            workflow, Path.GetFullPath(codex), Path.GetFullPath(auth), Path.GetFullPath(roadmapCli), liveRoot);
        Console.WriteLine(JsonSerializer.Serialize(roadmap, json));
        return roadmap.Classification == CertificationClassification.Passed ? 0 : 1;
    }
    if (command == "milestone11")
    {
        MilestoneElevenCertificationResult completion = await new MilestoneElevenRunner().RunAsync(
            Path.GetFullPath(codex), Path.GetFullPath(auth), Path.GetFullPath(recoveryCli), liveRoot);
        Console.WriteLine(JsonSerializer.Serialize(completion, json));
        return completion.Classification == CertificationClassification.Passed ? 0 : 1;
    }
    if (command is "milestone13" or "milestone14")
    {
        LoopRelay.Orchestration.Workflows.WorkflowIdentity roadmapWorkflow = command == "milestone13"
            ? LoopRelay.Orchestration.Workflows.WorkflowIdentity.TraditionalRoadmap
            : LoopRelay.Orchestration.Workflows.WorkflowIdentity.EvalRoadmap;
        FullChainCertificationResult chain = await new FullChainLiveRunner().RunAsync(
            roadmapWorkflow, Path.GetFullPath(codex), Path.GetFullPath(auth),
            Path.GetFullPath(recoveryCli), liveRoot, options.ContainsKey("--retain-case"));
        Console.WriteLine(JsonSerializer.Serialize(chain, json));
        return chain.Classification == CertificationClassification.Passed ? 0 : 1;
    }

    MilestoneFourCertificationResult recovery = await new MilestoneFourRunner().RunAsync(
        Path.GetFullPath(codex), Path.GetFullPath(auth), Path.GetFullPath(recoveryCli), liveRoot);
    Console.WriteLine(JsonSerializer.Serialize(recovery, json));
    return recovery.Classification == CertificationClassification.Passed ? 0 : 1;
}

if (!options.TryGetValue("--cli", out string? cli) || string.IsNullOrWhiteSpace(cli))
{
    Console.Error.WriteLine("--cli <path> is required for canary execution.");
    return 2;
}

string caseRoot = options.TryGetValue("--case-root", out string? configuredRoot) && !string.IsNullOrWhiteSpace(configuredRoot)
    ? Path.GetFullPath(configuredRoot)
    : Path.Combine(workspace, ".tmp", "certification");
try
{
    if (command == "milestone7")
    {
        MilestoneSevenCertificationResult publication = await new MilestoneSevenRunner().RunAsync(
            Path.GetFullPath(cli), caseRoot);
        Console.WriteLine(JsonSerializer.Serialize(publication, json));
        return publication.Classification == CertificationClassification.Passed ? 0 : 1;
    }
    if (command == "milestone8")
    {
        MilestoneEightCertificationResult persistence = await new MilestoneEightRunner().RunAsync(
            Path.GetFullPath(cli), caseRoot);
        Console.WriteLine(JsonSerializer.Serialize(persistence, json));
        return persistence.Classification == CertificationClassification.Passed ? 0 : 1;
    }

    if (command == "milestone2")
    {
        MilestoneTwoCertificationResult milestoneTwo = await new MilestoneTwoRunner().RunAsync(
            new CertificationOptions(workspace, Path.GetFullPath(cli), caseRoot,
                options.ContainsKey("--retain-case")));
        Console.WriteLine(JsonSerializer.Serialize(milestoneTwo, json));
        return milestoneTwo.Classification == CertificationClassification.Passed ? 0 : 1;
    }

    CanaryCertificationResult result = await new CertificationRunner().RunStatusCanaryAsync(
        new CertificationOptions(
            workspace,
            Path.GetFullPath(cli),
            caseRoot,
            options.ContainsKey("--retain-case")));
    Console.WriteLine(JsonSerializer.Serialize(result, json));
    return result.Classification == CertificationClassification.Passed ? 0 : 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
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

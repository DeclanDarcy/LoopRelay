using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Certification;

public sealed class GitPublicationRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<GitPublicationCertificationResult> RunAsync(
        string cliPath,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(authorityRoot, "git-publication", Guid.NewGuid().ToString("N"));
        string outsidePath = Path.Combine(authorityRoot, "git-publication.outside-authority.sentinel");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(outsidePath, "outside-authority\n", cancellationToken);
        string outsideHash = Hash(await File.ReadAllBytesAsync(outsidePath, cancellationToken));
        var cases = new List<GitPublicationCaseResult>();
        try
        {
            cases.Add(await RunNestedSuccessAsync());
            cases.Add(await RunOrdinaryFailClosedAsync());
            IReadOnlyList<string> privacy = PrivacyScanner.Scan(
                string.Join("\n", cases.SelectMany(item => item.Evidence)), authorityRoot);
            CertificationClassification classification = cases.All(item => item.Passed) && privacy.Count == 0
                ? CertificationClassification.Passed
                : CertificationClassification.ProductRegression;
            var result = new GitPublicationCertificationResult(
                CertificationEvidenceSchema.Version, classification, cases, privacy);
            string evidencePath = Path.Combine(authorityRoot, "evidence", "git-publication.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
            await using FileStream stream = File.Create(evidencePath);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            cases.Add(new GitPublicationCaseResult(
                "suite-failure", "unknown", 1, false, false, false, false, false, false, false,
                [exception.GetType().Name, exception.Message]));
            var result = new GitPublicationCertificationResult(
                CertificationEvidenceSchema.Version,
                CertificationClassification.EnvironmentFailure,
                cases,
                PrivacyScanner.Scan(string.Join("\n", cases.SelectMany(item => item.Evidence)), authorityRoot));
            string evidencePath = Path.Combine(authorityRoot, "evidence", "git-publication.latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
            await using FileStream stream = File.Create(evidencePath);
            await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
            return result;
        }
        finally
        {
            if (File.Exists(outsidePath)) File.Delete(outsidePath);
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(root, recursive: true);
            }
        }

        async Task<GitPublicationCaseResult> RunNestedSuccessAsync()
        {
            string caseRoot = Path.Combine(root, "nested-success");
            string repositoryPath = Path.Combine(caseRoot, "repository");
            string parentRemote = Path.Combine(caseRoot, "remotes", "parent.git");
            string agentsRemote = Path.Combine(caseRoot, "remotes", "agents.git");
            Directory.CreateDirectory(repositoryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(parentRemote)!);
            await GitAsync(caseRoot, "init", "--bare", "--initial-branch=main", parentRemote);
            await GitAsync(caseRoot, "init", "--bare", "--initial-branch=main", agentsRemote);
            await GitAsync(repositoryPath, "init", "-b", "main");
            await ConfigureIdentityAsync(repositoryPath);
            await GitAsync(repositoryPath, "remote", "add", "origin", parentRemote);

            string agents = Path.Combine(repositoryPath, ".agents");
            Directory.CreateDirectory(Path.Combine(agents, "handoffs"));
            await File.WriteAllTextAsync(Path.Combine(agents, "handoffs", "handoff.md"), "# Handoff\n\nInitial.\n", cancellationToken);
            await GitAsync(agents, "init", "-b", "main");
            await ConfigureIdentityAsync(agents);
            await GitAsync(agents, "remote", "add", "origin", agentsRemote);
            await GitAsync(agents, "add", ".");
            await GitAsync(agents, "commit", "-m", "seed agents");
            await GitAsync(agents, "push", "-u", "origin", "main");

            await File.WriteAllTextAsync(Path.Combine(repositoryPath, "README.md"), "# Nested publication\n", cancellationToken);
            await GitAsync(repositoryPath, "add", "README.md", ".agents");
            await GitAsync(repositoryPath, "commit", "-m", "seed parent");
            await GitAsync(repositoryPath, "push", "-u", "origin", "main");
            string parentBefore = await GitValueAsync(repositoryPath, "rev-parse", "HEAD");
            string agentsBefore = await GitValueAsync(agents, "rev-parse", "HEAD");
            int parentCountBefore = int.Parse(await GitValueAsync(repositoryPath, "rev-list", "--count", "HEAD"));
            int agentsCountBefore = int.Parse(await GitValueAsync(agents, "rev-list", "--count", "HEAD"));
            ProcessResult init = await RunCliAsync(cliPath, repositoryPath, ["storage", "init"], cancellationToken);
            if (init.ExitCode != 0) throw new InvalidOperationException("Nested storage init failed.");
            var repository = new Repository { Id = Guid.NewGuid(), Name = "nested", Path = repositoryPath };
            await SeedPublicationStateAsync(repository, cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(agents, "handoffs", "handoff.md"),
                "# Handoff\n\nChanged for publication.\n",
                cancellationToken);
            string agentsStatusBefore = await GitValueAsync(agents, "status", "--porcelain");
            ProcessResult run = await RunCliAsync(cliPath, repositoryPath, ["execute"], cancellationToken);

            string parentAfter = await GitValueAsync(repositoryPath, "rev-parse", "HEAD");
            string agentsAfter = await GitValueAsync(agents, "rev-parse", "HEAD");
            string parentRemoteHead = await GitValueAsync(parentRemote, "rev-parse", "refs/heads/main");
            string agentsRemoteHead = await GitValueAsync(agentsRemote, "rev-parse", "refs/heads/main");
            int parentCountAfter = int.Parse(await GitValueAsync(repositoryPath, "rev-list", "--count", "HEAD"));
            int agentsCountAfter = int.Parse(await GitValueAsync(agents, "rev-list", "--count", "HEAD"));
            string tree = await GitValueAsync(repositoryPath, "ls-tree", "HEAD", ".agents");
            string agentsStatusAfter = await GitValueAsync(agents, "status", "--porcelain");
            CanonicalWorkflowPersistenceSnapshot publicationSnapshot =
                await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync(cancellationToken);
            CanonicalTransitionRunRecord publicationRun = publicationSnapshot.TransitionRuns
                .Last(item => item.Transition == new WorkflowTransitionIdentity("PublishRepositoryState"));
            IReadOnlyList<LoopRelay.Orchestration.Effects.EffectWorkItem> publicationPlan =
                await new CanonicalEffectWorkStore(repository).ReadPlanAsync(
                    new TransitionRunIdentity(publicationRun.RunId), cancellationToken);
            bool parentExpected = parentAfter != parentBefore && parentCountAfter == parentCountBefore + 2;
            bool agentsExpected = agentsAfter != agentsBefore && agentsCountAfter == agentsCountBefore + 1;
            bool parentRemoteExpected = parentRemoteHead == parentAfter;
            bool agentsRemoteExpected = agentsRemoteHead == agentsAfter;
            bool gitlinkExpected = tree.StartsWith("160000 commit ", StringComparison.Ordinal) &&
                tree.Contains(agentsAfter, StringComparison.OrdinalIgnoreCase);
            bool outside = File.Exists(outsidePath) && Hash(await File.ReadAllBytesAsync(outsidePath, cancellationToken)) == outsideHash;
            bool passed = run.ExitCode == 0 && ParseOutputValue(run.StandardOutput, "Transition") == "PublishRepositoryState" &&
                parentExpected && agentsExpected && parentRemoteExpected && agentsRemoteExpected && gitlinkExpected && outside;
            return new GitPublicationCaseResult(
                "nested-real-remotes-success",
                "nested-independent-repository",
                run.ExitCode,
                parentExpected,
                agentsExpected,
                parentRemoteExpected,
                agentsRemoteExpected,
                gitlinkExpected,
                outside,
                passed,
                [
                    $"parent-before:{Short(parentBefore)}",
                    $"parent-after:{Short(parentAfter)}",
                    $"agents-before:{Short(agentsBefore)}",
                    $"agents-after:{Short(agentsAfter)}",
                    $"parent-commits:{parentCountBefore}->{parentCountAfter}",
                    $"agents-commits:{agentsCountBefore}->{agentsCountAfter}",
                    $"agents-status-before:{agentsStatusBefore.Replace('\n', '|')}",
                    $"agents-status-after:{agentsStatusAfter.Replace('\n', '|')}",
                    $"effect-plan:{string.Join(';', publicationPlan.Select(item => $"{item.Intent.Executor.Value}:{item.State}:{item.Receipt?.PostconditionSatisfied}"))}",
                    $"gitlink-mode:{(gitlinkExpected ? "160000" : "unexpected")}",
                    $"cli-stdout:{EvidenceNormalizer.Normalize(run.StandardOutput, repositoryPath)}",
                    $"cli-stderr:{EvidenceNormalizer.Normalize(run.StandardError, repositoryPath)}",
                ]);
        }

        async Task<GitPublicationCaseResult> RunOrdinaryFailClosedAsync()
        {
            string caseRoot = Path.Combine(root, "ordinary-fail-closed");
            string repositoryPath = Path.Combine(caseRoot, "repository");
            string parentRemote = Path.Combine(caseRoot, "remotes", "parent.git");
            Directory.CreateDirectory(repositoryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(parentRemote)!);
            await GitAsync(caseRoot, "init", "--bare", "--initial-branch=main", parentRemote);
            await GitAsync(repositoryPath, "init", "-b", "main");
            await ConfigureIdentityAsync(repositoryPath);
            await GitAsync(repositoryPath, "remote", "add", "origin", parentRemote);
            Directory.CreateDirectory(Path.Combine(repositoryPath, ".agents", "handoffs"));
            await File.WriteAllTextAsync(
                Path.Combine(repositoryPath, ".agents", "handoffs", "handoff.md"), "# Handoff\n\nInitial.\n", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(repositoryPath, "README.md"), "# Ordinary publication\n", cancellationToken);
            await GitAsync(repositoryPath, "add", ".");
            await GitAsync(repositoryPath, "commit", "-m", "seed ordinary parent");
            await GitAsync(repositoryPath, "push", "-u", "origin", "main");
            string parentBefore = await GitValueAsync(repositoryPath, "rev-parse", "HEAD");
            int countBefore = int.Parse(await GitValueAsync(repositoryPath, "rev-list", "--count", "HEAD"));
            await File.WriteAllTextAsync(
                Path.Combine(repositoryPath, ".agents", "handoffs", "handoff.md"),
                "# Handoff\n\nMust remain uncommitted.\n",
                cancellationToken);
            ProcessResult init = await RunCliAsync(cliPath, repositoryPath, ["storage", "init"], cancellationToken);
            if (init.ExitCode != 0) throw new InvalidOperationException("Ordinary storage init failed.");
            var repository = new Repository { Id = Guid.NewGuid(), Name = "ordinary", Path = repositoryPath };
            await SeedPublicationStateAsync(repository, cancellationToken);
            ProcessResult run = await RunCliAsync(cliPath, repositoryPath, ["execute"], cancellationToken);
            string parentAfter = await GitValueAsync(repositoryPath, "rev-parse", "HEAD");
            string remoteAfter = await GitValueAsync(parentRemote, "rev-parse", "refs/heads/main");
            int countAfter = int.Parse(await GitValueAsync(repositoryPath, "rev-list", "--count", "HEAD"));
            bool headExpected = parentAfter == parentBefore && countAfter == countBefore;
            bool remoteExpected = remoteAfter == parentBefore;
            bool dirtyPreserved = (await GitValueAsync(repositoryPath, "status", "--porcelain"))
                .Contains(".agents/handoffs/handoff.md", StringComparison.Ordinal);
            bool explicitFailure = run.ExitCode == 1 &&
                run.StandardOutput.Contains("ordinary parent-repository directory", StringComparison.Ordinal);
            bool outside = File.Exists(outsidePath) && Hash(await File.ReadAllBytesAsync(outsidePath, cancellationToken)) == outsideHash;
            bool passed = explicitFailure && headExpected && remoteExpected && dirtyPreserved && outside;
            return new GitPublicationCaseResult(
                "ordinary-directory-fails-before-mutation",
                "ordinary-parent-directory",
                run.ExitCode,
                headExpected,
                AgentsHeadExpected: true,
                remoteExpected,
                AgentsRemoteExpected: true,
                GitlinkExpected: true,
                outside,
                passed,
                [
                    $"head-unchanged:{headExpected}",
                    $"remote-unchanged:{remoteExpected}",
                    $"dirty-preserved:{dirtyPreserved}",
                    $"explicit-topology-failure:{explicitFailure}",
                    $"cli-stdout:{EvidenceNormalizer.Normalize(run.StandardOutput, repositoryPath)}",
                    $"cli-stderr:{EvidenceNormalizer.Normalize(run.StandardError, repositoryPath)}",
                ]);
        }
    }

    private static async Task SeedPublicationStateAsync(Repository repository, CancellationToken token)
    {
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string[] evidence = ["certification:git-publication:publication-entry"];
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Execute,
            WorkflowResolutionState.Resumable,
            new WorkflowStageIdentity("Execution Continuity"),
            RuntimeOutcomeKind.Waiting,
            now,
            evidence), token);
        await store.UpsertStageStateAsync(new CanonicalStageStateRecord(
            WorkflowIdentity.Execute,
            new WorkflowStageIdentity("Execution Continuity"),
            WorkflowResolutionState.Active,
            now,
            evidence), token);
        foreach (string transition in new[] { "GenerateHandoff", "UpdateOperationalContext" })
        {
            await store.UpsertTransitionRunAsync(new CanonicalTransitionRunRecord(
                $"m7-{transition}",
                WorkflowIdentity.Execute,
                new WorkflowStageIdentity("Execution Continuity"),
                new WorkflowTransitionIdentity(transition),
                TransitionDurableState.Completed,
                RuntimeOutcomeKind.Completed,
                now,
                now,
                Hash(transition),
                "Seeded completed predecessor for publication certification.",
                evidence), token);
        }

        foreach (ProductIdentity identity in new[]
        {
            ProductIdentity.ImplementationSlice,
            ProductIdentity.RepositoryChanges,
            ProductIdentity.ExecutionHandoff,
        })
        {
            string path = identity == ProductIdentity.ExecutionHandoff
                ? ".agents/handoffs/handoff.md"
                : $".LoopRelay/evidence/certification/{identity.Value}.md";
            await store.UpsertProductAsync(new ProductRecord(
                identity,
                WorkflowIdentity.Execute,
                new WorkflowTransitionIdentity(identity == ProductIdentity.ExecutionHandoff
                    ? "GenerateHandoff"
                    : "ExecuteImplementationSlice"),
                [WorkflowIdentity.Execute],
                "repository-owned certification seed",
                "independent git-publication fixture",
                [path],
                Hash(identity.Value),
                ProductFreshness.Fresh,
                ProductValidationState.Valid,
                ProductLifecycle.Active,
                [path]), token);
        }
    }

    private static async Task ConfigureIdentityAsync(string repository)
    {
        await GitAsync(repository, "config", "user.email", "certification@looprelay.invalid");
        await GitAsync(repository, "config", "user.name", "LoopRelay Certification");
    }

    private static async Task GitAsync(string workingDirectory, params string[] arguments)
    {
        ProcessResult result = await RunProcessAsync("git", arguments, workingDirectory, CancellationToken.None);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments[0]} failed: {result.StandardError}");
        }
    }

    private static async Task<string> GitValueAsync(string workingDirectory, params string[] arguments)
    {
        ProcessResult result = await RunProcessAsync("git", arguments, workingDirectory, CancellationToken.None);
        if (result.ExitCode != 0) throw new InvalidOperationException($"git {arguments[0]} failed: {result.StandardError}");
        return result.StandardOutput.Trim();
    }

    private static Task<ProcessResult> RunCliAsync(
        string cliPath, string repository, IReadOnlyList<string> arguments, CancellationToken token)
    {
        var all = new List<string>();
        string file = cliPath;
        if (cliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            file = "dotnet";
            all.Add(cliPath);
        }
        all.AddRange(["--repo", repository]);
        all.AddRange(arguments);
        return RunProcessAsync(file, all, repository, token);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string file, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken token)
    {
        var start = new ProcessStartInfo
        {
            FileName = file,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"{file} did not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static string? ParseOutputValue(string output, string label) => output
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .Where(line => line.StartsWith(label + ": ", StringComparison.Ordinal))
        .Select(line => line[(label.Length + 2)..])
        .LastOrDefault();

    private static string Short(string value) => value.Length <= 12 ? value : value[..12];
    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

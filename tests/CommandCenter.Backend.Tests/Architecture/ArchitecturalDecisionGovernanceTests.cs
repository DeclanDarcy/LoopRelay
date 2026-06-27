using System.Text.RegularExpressions;

namespace CommandCenter.Backend.Tests.Architecture;

public sealed class ArchitecturalDecisionGovernanceTests
{
    private static readonly string[] RequiredDecisionClasses =
    [
        "New authority",
        "New projection",
        "Contract change",
        "Compatibility exception",
        "Regression weakening",
        "Generated artifact exception",
        "Transport exception",
        "State ownership change",
        "Controller/workspace boundary change",
        "Runtime failure scope change",
        "Reference architecture change"
    ];

    private static readonly string[] RequiredDecisionClassColumns =
    [
        "Decision class",
        "Applies when",
        "Minimum evidence",
        "Required regression or guard",
        "Durable docs update"
    ];

    private static readonly string[] RequiredMechanismLifecycleChanges =
    [
        "Add mechanism",
        "Strengthen mechanism",
        "Weaken mechanism",
        "Quarantine mechanism",
        "Replace mechanism",
        "Retire mechanism"
    ];

    private static readonly string[] RequiredEvidencePackageFields =
    [
        "Evidence id",
        "Capability",
        "Invariant",
        "Slice or milestone",
        "Decision records",
        "Files and modules observed",
        "Commands run",
        "Results",
        "Consumers affected",
        "Known limits",
        "Rollback path",
        "Retention location",
        "Reviewer or certifier"
    ];

    private static readonly string[] RequiredEvidenceTypes =
    [
        "Inventory evidence",
        "Contract evidence",
        "Authority evidence",
        "Projection evidence",
        "Transport evidence",
        "State evidence",
        "Runtime evidence",
        "Mechanism evidence",
        "Compatibility evidence",
        "Certification evidence",
        "Acceptance evidence",
        "Rollback evidence"
    ];

    private static readonly string[] RequiredTemplateHeadings =
    [
        "## Metadata",
        "## Decision",
        "## Context",
        "## Evidence",
        "## Alternatives",
        "## Compatibility Impact",
        "## Regression Impact",
        "## Rollback Path",
        "## Baseline Updates",
        "## Follow-Up"
    ];

    private static readonly string[] RequiredDecisionCheckpointHeadings =
    [
        "## Authorized Decisions",
        "## Next Authorized Sequence"
    ];

    private static readonly string[] RequiredGovernanceEvidenceHeadings =
    [
        "## Objective",
        "## Capability",
        "## Invariant",
        "## Changes",
        "## Verification",
        "## Known Limits",
        "## Rollback"
    ];

    private static readonly string[] RequiredAuthorityProjectionWatchlistHeadings =
    [
        "## Detection Scope",
        "## Exclusions",
        "## Accepted Exceptions",
        "## Authority-Like File Inventory",
        "## Projection-Like File Inventory",
        "## Non-Claims"
    ];

    private static readonly string[] RequiredCompatibilityStructureHeadings =
    [
        "## Detection Scope",
        "## Compatibility Kinds",
        "## Compatibility Field Inventory",
        "## Compatibility Route Inventory",
        "## Compatibility Command Inventory",
        "## Compatibility Mirror Inventory",
        "## Exclusions",
        "## Non-Claims"
    ];

    private static readonly string[] RequiredCompatibilityInventoryColumns =
    [
        "Compatibility structure",
        "Kind",
        "Owner",
        "Consumers",
        "Replacement path",
        "Retirement condition",
        "Evidence"
    ];

    private static readonly ArchitectureRegressionBypassPattern[] ArchitectureRegressionBypassPatterns =
    [
        new(
            "xUnit skip",
            @"\[(Fact|Theory)\s*\([^\)]*\bSkip\s*=",
            "Backend architecture regressions cannot be disabled with xUnit Skip without an explicit regression-weakening decision and mechanism evidence."),
        new(
            "Vitest skipped suite or test",
            @"\b(describe|it|test)\.skip\s*\(",
            "Frontend architecture regressions cannot be disabled with Vitest .skip without an explicit regression-weakening decision and mechanism evidence."),
        new(
            "Vitest focused suite or test",
            @"\b(describe|it|test)\.only\s*\(",
            "Frontend architecture regressions cannot be focused with Vitest .only because it can silently exclude other architecture regressions.")
    ];

    [Fact]
    public void DecisionGovernanceDocumentDefinesRequiredDecisionClasses()
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> decisionClasses = ReadTable(
            "docs/architecture-decision-governance.md",
            "## Decision Classes",
            "Decision class",
            RequiredDecisionClassColumns);

        foreach (string decisionClass in RequiredDecisionClasses)
        {
            Assert.Contains(decisionClasses, row => row["Decision class"] == decisionClass);
        }

        foreach (IReadOnlyDictionary<string, string> row in decisionClasses)
        {
            foreach (string column in RequiredDecisionClassColumns)
            {
                Assert.True(
                    row.TryGetValue(column, out string? value) && HasAcceptedCatalogValue(value),
                    $"Decision class '{row["Decision class"]}' must populate '{column}'. M0.4 governance needs explicit applicability, evidence, regression, and documentation obligations before architecture-changing migrations resume.");
            }
        }
    }

    [Fact]
    public void DecisionGovernanceDocumentDefinesMechanismLifecycleApproval()
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> lifecycleRows = ReadTable(
            "docs/architecture-decision-governance.md",
            "## Mechanism Lifecycle Governance",
            "Lifecycle change",
            ["Lifecycle change", "Approval requirement", "Evidence requirement"]);

        foreach (string lifecycleChange in RequiredMechanismLifecycleChanges)
        {
            Assert.Contains(lifecycleRows, row => row["Lifecycle change"] == lifecycleChange);
        }

        foreach (IReadOnlyDictionary<string, string> row in lifecycleRows)
        {
            Assert.True(HasAcceptedCatalogValue(row["Approval requirement"]));
            Assert.True(HasAcceptedCatalogValue(row["Evidence requirement"]));
        }
    }

    [Fact]
    public void EvidenceModelDefinesPackageSchemaAndEvidenceTypes()
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> schemaRows = ReadTable(
            "docs/architectural-evidence.md",
            "## Evidence Package Schema",
            "Field",
            ["Field", "Required content", "Used by"]);

        foreach (string field in RequiredEvidencePackageFields)
        {
            Assert.Contains(schemaRows, row => row["Field"] == field);
        }

        IReadOnlyList<IReadOnlyDictionary<string, string>> evidenceTypes = ReadTable(
            "docs/architectural-evidence.md",
            "## Evidence Types",
            "Evidence type",
            ["Evidence type", "Required proof", "Typical location"]);

        foreach (string evidenceType in RequiredEvidenceTypes)
        {
            Assert.Contains(evidenceTypes, row => row["Evidence type"] == evidenceType);
        }
    }

    [Fact]
    public void DecisionRecordTemplateCapturesGovernanceMetadata()
    {
        string templatePath = Path.Combine(
            FindRepositoryRoot().FullName,
            ".agents",
            "decisions",
            "decision-record-template.md");

        Assert.True(
            File.Exists(templatePath),
            "M0.4 requires a decision record template so architecture-affecting changes capture decision class, invariant, capability, evidence, compatibility, regressions, rollback, and baseline updates.");

        string template = File.ReadAllText(templatePath);

        foreach (string heading in RequiredTemplateHeadings)
        {
            Assert.Contains(heading, template);
        }

        foreach (string requiredSignal in new[]
        {
            "Decision class:",
            "Capability:",
            "Invariant:",
            "Authority owner:",
            "Mechanism owner:",
            "Compatibility owner:",
            "Evidence package:",
            "Supersedes:",
            "Superseded by:"
        })
        {
            Assert.Contains(requiredSignal, template);
        }
    }

    [Fact]
    public void ArchitectureRegressionTestsAreNotDisabledOrFocused()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string[] architectureRegressionFiles =
        [
            .. Directory.GetFiles(
                Path.Combine(repositoryRoot.FullName, "tests", "CommandCenter.Backend.Tests", "Architecture"),
                "*.cs",
                SearchOption.TopDirectoryOnly),
            .. Directory.GetFiles(
                Path.Combine(repositoryRoot.FullName, "src", "CommandCenter.UI", "src", "test", "architecture"),
                "*.ts",
                SearchOption.TopDirectoryOnly),
            .. Directory.GetFiles(
                Path.Combine(repositoryRoot.FullName, "src", "CommandCenter.UI", "src", "test", "architecture"),
                "*.tsx",
                SearchOption.TopDirectoryOnly)
        ];

        foreach (string architectureRegressionFile in architectureRegressionFiles)
        {
            string source = File.ReadAllText(architectureRegressionFile);

            foreach (ArchitectureRegressionBypassPattern bypassPattern in ArchitectureRegressionBypassPatterns)
            {
                Assert.False(
                    System.Text.RegularExpressions.Regex.IsMatch(source, bypassPattern.Pattern),
                    $"{bypassPattern.Description} File: {Path.GetRelativePath(repositoryRoot.FullName, architectureRegressionFile)}. M0.4 treats disabled, focused, or bypassed architecture regressions as regression weakening; add governance evidence before weakening the guard.");
            }
        }
    }

    [Fact]
    public void ShellRustStructsRemainClassifiedInTransportInventory()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string shellSourcePath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "CommandCenter.Shell",
            "src",
            "main.rs");
        string classificationPath = Path.Combine(
            repositoryRoot.FullName,
            "docs",
            "shell-transport-classification.md");

        string shellSource = File.ReadAllText(shellSourcePath);
        string classification = File.ReadAllText(classificationPath);

        string[] rustStructs = Regex.Matches(
                shellSource,
                @"(?:^|\n)struct\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:\{|;|\()",
                RegexOptions.Singleline)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] classifiedStructs = ReadRustMirrorInventoryStructNames(classification)
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] unclassified = rustStructs.Except(classifiedStructs, StringComparer.Ordinal).ToArray();
        string[] staleClassification = classifiedStructs.Except(rustStructs, StringComparer.Ordinal).ToArray();

        Assert.True(
            unclassified.Length == 0,
            $"Shell Rust structs must be classified in docs/shell-transport-classification.md before they can become accepted transport, compatibility, request, or shell-owned surfaces. Unclassified structs: {string.Join(", ", unclassified)}. M0.4 treats new shell response mirrors as transport responsibility growth requiring governance evidence.");
        Assert.True(
            staleClassification.Length == 0,
            $"docs/shell-transport-classification.md must stay aligned with src/CommandCenter.Shell/src/main.rs so mirror retirement and compatibility obligations remain traceable. Stale classifications: {string.Join(", ", staleClassification)}.");
    }

    [Fact]
    public void ActiveGovernanceArtifactsKeepRequiredStructureAndEvidenceLinks()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string activeDecisionPath = Path.Combine(
            repositoryRoot.FullName,
            ".agents",
            "decisions",
            "decisions.md");

        Assert.True(
            File.Exists(activeDecisionPath),
            ".agents/decisions/decisions.md must exist so newly authorized architectural decisions remain visible before rotation.");

        string activeDecision = File.ReadAllText(activeDecisionPath);
        Assert.StartsWith("# Decisions:", activeDecision, StringComparison.Ordinal);
        foreach (string heading in RequiredDecisionCheckpointHeadings)
        {
            Assert.Contains(heading, activeDecision);
        }

        string milestoneDirectory = Path.Combine(repositoryRoot.FullName, ".agents", "milestones");
        string[] governanceEvidenceFiles = Directory.GetFiles(
                milestoneDirectory,
                "m0.4-*-slice-*.md",
                SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(governanceEvidenceFiles);

        foreach (string evidenceFile in governanceEvidenceFiles)
        {
            string evidence = File.ReadAllText(evidenceFile);
            foreach (string heading in RequiredGovernanceEvidenceHeadings)
            {
                Assert.Contains(
                    heading,
                    evidence,
                    StringComparison.Ordinal);
            }

            Assert.Contains(
                "dotnet test",
                evidence,
                StringComparison.Ordinal);
        }

        string mechanisms = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docs", "architectural-mechanisms.md"));
        string[] linkedGovernanceEvidence = Regex.Matches(
                mechanisms,
                @"`(?<path>\.agents/milestones/m0\.4-[^`]+\.md)`")
            .Select(match => match.Groups["path"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (string evidenceFile in governanceEvidenceFiles)
        {
            string relativePath = Path.GetRelativePath(repositoryRoot.FullName, evidenceFile).Replace('\\', '/');
            Assert.Contains(
                relativePath,
                linkedGovernanceEvidence);
        }

        foreach (string linkedPath in linkedGovernanceEvidence)
        {
            Assert.True(
                File.Exists(Path.Combine(repositoryRoot.FullName, linkedPath)),
                $"Decision governance mechanism evidence link must resolve to a file: {linkedPath}");
        }
    }

    [Fact]
    public void ReferentialGovernanceClaimsRemainReachable()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string activeDecision = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            ".agents",
            "decisions",
            "decisions.md"));
        string capabilities = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docs", "architectural-capabilities.md"));
        string mechanisms = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docs", "architectural-mechanisms.md"));

        string[] governanceEvidenceFiles = Directory.GetFiles(
                Path.Combine(repositoryRoot.FullName, ".agents", "milestones"),
                "m0.4-*-slice-*.md",
                SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(governanceEvidenceFiles);

        string[] activeDecisionEvidenceLinks = ExtractRepositoryRelativeLinks(
                activeDecision,
                @".agents/milestones/m0\.4-[^`\s)]+\.md")
            .ToArray();

        Assert.NotEmpty(activeDecisionEvidenceLinks);
        foreach (string linkedPath in activeDecisionEvidenceLinks)
        {
            Assert.True(
                File.Exists(Path.Combine(repositoryRoot.FullName, linkedPath)),
                $"Active decision checkpoint must cite reachable governance evidence. Missing: {linkedPath}");
        }

        foreach (string evidenceFile in governanceEvidenceFiles)
        {
            string relativePath = Path.GetRelativePath(repositoryRoot.FullName, evidenceFile).Replace('\\', '/');
            string evidence = File.ReadAllText(evidenceFile);

            Assert.True(
                EvidenceReferencesGovernedArtifact(evidence),
                $"{relativePath} must reference the decision, capability, or mechanism artifact it supports so governance evidence remains traceable.");
            Assert.Contains(
                relativePath,
                capabilities,
                StringComparison.Ordinal);
            Assert.Contains(
                relativePath,
                mechanisms,
                StringComparison.Ordinal);
        }

        AssertReachableGovernanceEvidenceLinks(repositoryRoot, capabilities, "docs/architectural-capabilities.md");
        AssertReachableGovernanceEvidenceLinks(repositoryRoot, mechanisms, "docs/architectural-mechanisms.md");
    }

    [Fact]
    public void AuthorityAndProjectionLikeFileNamesRemainGoverned()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string watchlistPath = Path.Combine(
            repositoryRoot.FullName,
            "docs",
            "authority-projection-governance-watchlist.md");

        Assert.True(
            File.Exists(watchlistPath),
            "M0.4 requires a governance watchlist before new authority-like or projection-like source files can be accepted.");

        string watchlist = File.ReadAllText(watchlistPath);
        foreach (string heading in RequiredAuthorityProjectionWatchlistHeadings)
        {
            Assert.Contains(heading, watchlist);
        }

        string[] actualWatchedFiles = EnumerateAuthorityProjectionLikeSourceFiles(repositoryRoot)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] governedFiles = ExtractRepositoryRelativeLinks(
                watchlist,
                @"(?:src|tests/CommandCenter\.Backend\.Tests)/[^`\s|)]+\.(?:cs|ts|tsx|rs)")
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] ungovernedFiles = actualWatchedFiles.Except(governedFiles, StringComparer.Ordinal).ToArray();
        string[] staleGovernedFiles = governedFiles.Except(actualWatchedFiles, StringComparer.Ordinal).ToArray();

        Assert.True(
            ungovernedFiles.Length == 0,
            $"New authority-like or projection-like source file names require governance before acceptance. Add an explicit authority/projection watchlist entry, scope, owner rationale, and evidence before landing new named artifacts. Ungoverned files: {string.Join(", ", ungovernedFiles)}");
        Assert.True(
            staleGovernedFiles.Length == 0,
            $"docs/authority-projection-governance-watchlist.md contains authority/projection watchlist entries that no longer exist. Retire or update the governed entry with evidence. Stale files: {string.Join(", ", staleGovernedFiles)}");
    }

    [Fact]
    public void CompatibilityStructuresRemainGoverned()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string inventoryPath = Path.Combine(
            repositoryRoot.FullName,
            "docs",
            "compatibility-structure-governance.md");

        Assert.True(
            File.Exists(inventoryPath),
            "M0.4 requires a compatibility-structure inventory before compatibility fields, routes, commands, or mirrors can be accepted as transitional architecture.");

        string inventory = File.ReadAllText(inventoryPath);
        foreach (string heading in RequiredCompatibilityStructureHeadings)
        {
            Assert.Contains(heading, inventory);
        }

        IReadOnlyList<IReadOnlyDictionary<string, string>> fieldRows = ReadCompatibilityInventory("## Compatibility Field Inventory");
        IReadOnlyList<IReadOnlyDictionary<string, string>> routeRows = ReadCompatibilityInventory("## Compatibility Route Inventory");
        IReadOnlyList<IReadOnlyDictionary<string, string>> commandRows = ReadCompatibilityInventory("## Compatibility Command Inventory");
        IReadOnlyList<IReadOnlyDictionary<string, string>> mirrorRows = ReadCompatibilityInventory("## Compatibility Mirror Inventory");

        AssertCompatibilityRowsHaveGovernanceMetadata(repositoryRoot, fieldRows, "Compatibility field");
        AssertCompatibilityRowsHaveGovernanceMetadata(repositoryRoot, routeRows, "Compatibility route");
        AssertCompatibilityRowsHaveGovernanceMetadata(repositoryRoot, commandRows, "Compatibility command");
        AssertCompatibilityRowsHaveGovernanceMetadata(repositoryRoot, mirrorRows, "Compatibility mirror");

        string[] governedRoutes = routeRows
            .Select(row => row["Compatibility structure"].Trim('`'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                "GET /api/ping",
                "GET /api/repositories/{repositoryId:guid}/planning"
            ],
            governedRoutes);

        string shellClassification = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "docs", "shell-transport-classification.md"));
        string[] transitionalCommandFamilies = ReadTableRows(
                shellClassification,
                "## Command-Family Inventory",
                "Family",
                ["Family", "Representative commands", "Current category", "Target category", "Evidence", "Known gap"])
            .Where(row => row["Current category"] == "Transitional compatibility")
            .Select(row => $"{row["Family"]} shell command family")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] governedCommandFamilies = commandRows
            .Select(row => row["Compatibility structure"])
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(transitionalCommandFamilies, governedCommandFamilies);

        string[] shellCompatibilityMirrors = ReadTableRows(
                shellClassification,
                "## Rust Mirror Inventory",
                "Rust struct or group",
                ["Rust struct or group", "Current state", "Target state", "Reason", "Retirement or quarantine condition"])
            .Where(row => row["Current state"] is "Mirror" or "Compatibility")
            .Select(row => row["Rust struct or group"])
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] governedMirrors = mirrorRows
            .Select(row => row["Compatibility structure"])
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(shellCompatibilityMirrors, governedMirrors);

        IReadOnlyList<IReadOnlyDictionary<string, string>> ReadCompatibilityInventory(string heading)
        {
            return ReadTable(
                "docs/compatibility-structure-governance.md",
                heading,
                "Compatibility structure",
                RequiredCompatibilityInventoryColumns);
        }
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadTable(
        string relativePath,
        string heading,
        string firstColumnName,
        IReadOnlyList<string> requiredColumns)
    {
        string documentPath = Path.Combine(FindRepositoryRoot().FullName, relativePath);

        Assert.True(
            File.Exists(documentPath),
            $"{relativePath} must exist for M0.4 governance metadata verification.");

        string[] lines = File.ReadAllLines(documentPath);
        int headingIndex = Array.FindIndex(lines, line => line.Trim() == heading);

        Assert.True(
            headingIndex >= 0,
            $"{relativePath} must define '{heading}'.");

        int headerIndex = Array.FindIndex(lines, headingIndex, line => line.StartsWith($"| {firstColumnName} |", StringComparison.Ordinal));

        Assert.True(
            headerIndex > headingIndex,
            $"{heading} must use a markdown table whose first column is '{firstColumnName}'.");

        string[] columns = SplitMarkdownTableRow(lines[headerIndex]);

        Assert.Equal(requiredColumns, columns);

        List<IReadOnlyDictionary<string, string>> rows = [];

        for (int i = headerIndex + 2; i < lines.Length; i++)
        {
            string line = lines[i];

            if (!line.StartsWith("|", StringComparison.Ordinal))
            {
                break;
            }

            string[] values = SplitMarkdownTableRow(line);

            Assert.True(
                values.Length == columns.Length,
                $"Governance table row has {values.Length} columns but expected {columns.Length}: {line}");

            rows.Add(columns
                .Zip(values, static (column, value) => new { column, value })
                .ToDictionary(pair => pair.column, pair => pair.value));
        }

        Assert.NotEmpty(rows);

        return rows;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadTableRows(
        string source,
        string heading,
        string firstColumnName,
        IReadOnlyList<string> requiredColumns)
    {
        string[] lines = source.Split('\n');
        int headingIndex = Array.FindIndex(lines, line => line.Trim() == heading);

        Assert.True(
            headingIndex >= 0,
            $"Source must define '{heading}'.");

        int headerIndex = Array.FindIndex(lines, headingIndex, line => line.StartsWith($"| {firstColumnName} |", StringComparison.Ordinal));

        Assert.True(
            headerIndex > headingIndex,
            $"{heading} must use a markdown table whose first column is '{firstColumnName}'.");

        string[] columns = SplitMarkdownTableRow(lines[headerIndex]);

        Assert.Equal(requiredColumns, columns);

        List<IReadOnlyDictionary<string, string>> rows = [];

        for (int i = headerIndex + 2; i < lines.Length; i++)
        {
            string line = lines[i];

            if (!line.StartsWith("|", StringComparison.Ordinal))
            {
                break;
            }

            string[] values = SplitMarkdownTableRow(line);

            Assert.True(
                values.Length == columns.Length,
                $"Governance table row has {values.Length} columns but expected {columns.Length}: {line}");

            rows.Add(columns
                .Zip(values, static (column, value) => new { column, value })
                .ToDictionary(pair => pair.column, pair => pair.value));
        }

        Assert.NotEmpty(rows);

        return rows;
    }

    private static void AssertCompatibilityRowsHaveGovernanceMetadata(
        DirectoryInfo repositoryRoot,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        string expectedKind)
    {
        foreach (IReadOnlyDictionary<string, string> row in rows)
        {
            Assert.Equal(expectedKind, row["Kind"]);

            foreach (string column in RequiredCompatibilityInventoryColumns)
            {
                Assert.True(
                    HasAcceptedCatalogValue(row[column]),
                    $"Ungoverned compatibility structure detected. '{row["Compatibility structure"]}' must populate '{column}' so compatibility remains transitional and traceable.");
            }

            string[] evidenceLinks = ExtractRepositoryRelativeLinks(
                    row["Evidence"],
                    @"(?:\.agents/milestones/m0\.[0-9]-[^`\s)]+\.md|docs/[^`\s)]+\.md|tests/[^`\s)]+\.cs)")
                .ToArray();

            Assert.NotEmpty(evidenceLinks);
            foreach (string evidenceLink in evidenceLinks)
            {
                Assert.True(
                    File.Exists(Path.Combine(repositoryRoot.FullName, evidenceLink)),
                    $"Compatibility structure '{row["Compatibility structure"]}' cites missing evidence: {evidenceLink}");
            }
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CommandCenter.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory;
    }

    private static string[] SplitMarkdownTableRow(string line)
    {
        return line
            .Trim()
            .Trim('|')
            .Split('|')
            .Select(value => value.Trim())
            .ToArray();
    }

    private static IReadOnlyList<string> ReadRustMirrorInventoryStructNames(string classification)
    {
        string[] lines = classification.Split('\n');
        int headingIndex = Array.FindIndex(lines, line => line.Trim() == "## Rust Mirror Inventory");

        Assert.True(
            headingIndex >= 0,
            "docs/shell-transport-classification.md must define '## Rust Mirror Inventory'.");

        int headerIndex = Array.FindIndex(
            lines,
            headingIndex,
            line => line.StartsWith("| Rust struct or group |", StringComparison.Ordinal));

        Assert.True(
            headerIndex > headingIndex,
            "The Rust Mirror Inventory must use a markdown table whose first column is 'Rust struct or group'.");

        List<string> names = [];
        for (int i = headerIndex + 2; i < lines.Length; i++)
        {
            string line = lines[i];
            if (!line.StartsWith("|", StringComparison.Ordinal))
            {
                break;
            }

            string firstColumn = SplitMarkdownTableRow(line)[0];
            foreach (Match match in Regex.Matches(firstColumn, "`(?<name>[A-Za-z_][A-Za-z0-9_]*)`"))
            {
                names.Add(match.Groups["name"].Value);
            }
        }

        Assert.NotEmpty(names);

        return names;
    }

    private static IEnumerable<string> ExtractRepositoryRelativeLinks(string source, string relativePathPattern)
    {
        return Regex.Matches(source, $"`(?<path>{relativePathPattern})`|(?<path>{relativePathPattern})")
            .Select(match => match.Groups["path"].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal);
    }

    private static void AssertReachableGovernanceEvidenceLinks(
        DirectoryInfo repositoryRoot,
        string source,
        string sourceName)
    {
        string[] links = ExtractRepositoryRelativeLinks(
                source,
                @".agents/milestones/m0\.4-[^`\s)]+\.md")
            .ToArray();

        Assert.NotEmpty(links);
        foreach (string linkedPath in links)
        {
            Assert.True(
                File.Exists(Path.Combine(repositoryRoot.FullName, linkedPath)),
                $"{sourceName} claims M0.4 governance evidence that does not exist: {linkedPath}");
        }
    }

    private static IEnumerable<string> EnumerateAuthorityProjectionLikeSourceFiles(DirectoryInfo repositoryRoot)
    {
        string[] roots =
        [
            Path.Combine(repositoryRoot.FullName, "src"),
            Path.Combine(repositoryRoot.FullName, "tests", "CommandCenter.Backend.Tests")
        ];
        HashSet<string> acceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".ts",
            ".tsx",
            ".rs"
        };

        foreach (string root in roots)
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file);
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (!acceptedExtensions.Contains(extension)
                    || (fileName.IndexOf("Authority", StringComparison.OrdinalIgnoreCase) < 0
                        && fileName.IndexOf("Projection", StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                yield return Path.GetRelativePath(repositoryRoot.FullName, file).Replace('\\', '/');
            }
        }
    }

    private static bool EvidenceReferencesGovernedArtifact(string evidence)
    {
        return evidence.Contains(".agents/decisions/", StringComparison.Ordinal)
            || evidence.Contains("docs/architectural-capabilities.md", StringComparison.Ordinal)
            || evidence.Contains("docs/architectural-mechanisms.md", StringComparison.Ordinal)
            || evidence.Contains("docs/authority-projection-governance-watchlist.md", StringComparison.Ordinal)
            || evidence.Contains("docs/architecture-decision-governance.md", StringComparison.Ordinal)
            || evidence.Contains("docs/architectural-evidence.md", StringComparison.Ordinal);
    }

    private static bool HasAcceptedCatalogValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, "TBD", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value, "TODO", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value, "None", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ArchitectureRegressionBypassPattern(
        string Name,
        string Pattern,
        string Description);
}

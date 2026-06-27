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

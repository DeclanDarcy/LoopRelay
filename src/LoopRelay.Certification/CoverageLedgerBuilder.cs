using System.Security.Cryptography;
using System.Text;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Certification;

public static class CoverageLedgerBuilder
{
    public const string Version = "2";

    public static CoverageLedger Build(string workspaceRoot, bool creditCanary = false)
    {
        CanonicalWorkflowCatalogSnapshot catalog = CanonicalWorkflowCatalog.Current;
        IReadOnlyList<WorkflowDefinition> workflows = catalog.Workflows;
        IReadOnlyList<WorkflowChainDefinition> chains = catalog.Chains;
        var obligations = new List<CoverageObligation>();

        foreach (CatalogObligation obligation in catalog.Obligations)
            Add("catalog-obligation", obligation.Key, contentHash: obligation.ContentHash);

        foreach (WorkflowDefinition workflow in workflows.OrderBy(item => item.Identity.Value, StringComparer.Ordinal))
        {
            Add("workflow", workflow.Identity.Value);
            foreach (WorkflowStageDefinition stage in workflow.Stages.OrderBy(item => item.Identity.Value, StringComparer.Ordinal))
            {
                Add("stage", $"{workflow.Identity}/{stage.Identity}");
                Add("gate", stage.EntryGate.Identity.Value);
                Add("gate", stage.CompletionGate.Identity.Value);
            }

            Add("gate", workflow.EntryGate.Identity.Value);
            Add("gate", workflow.ExitGate.Identity.Value);
            Add("recovery", workflow.Recovery.Identity);
            foreach (WorkflowTransitionDefinition transition in workflow.Transitions.OrderBy(item => item.Identity.Value, StringComparer.Ordinal))
            {
                string transitionId = $"{workflow.Identity}/{transition.Identity}";
                Add("transition", transitionId);
                Add("prompt", transition.PromptIdentity);
                Add("posture", transition.ExecutionPosture.Kind.ToString());
                Add("recovery", transition.Recovery.Identity);
                Add("gate", transition.InputGate.Identity.Value);
                Add("gate", transition.OutputGate.Identity.Value);
                foreach (EffectDefinition effect in transition.Effects)
                {
                    Add("effect", $"{transitionId}/{effect.Identity}");
                    Add("effect-category", effect.Category.ToString());
                }

                foreach (ProductDefinition product in transition.ProducedProducts)
                {
                    Add("product", product.Identity.Value);
                }
            }
        }

        foreach (WorkflowChainDefinition chain in chains.OrderBy(item => item.Identity, StringComparer.Ordinal))
        {
            Add("chain", chain.Identity);
            for (int index = 0; index < chain.Workflows.Count - 1; index++)
            {
                Add("chain-boundary", $"{chain.Identity}/{chain.Workflows[index].Identity}->{chain.Workflows[index + 1].Identity}");
            }
        }

        foreach (string prompt in EnumerateFiles(workspaceRoot, "src", "*.prompt"))
        {
            Add("prompt-asset", Path.GetRelativePath(workspaceRoot, prompt).Replace('\\', '/'));
        }

        foreach (string issue in EnumerateFiles(workspaceRoot, "issues", "*.md"))
        {
            Add("known-risk", Path.GetRelativePath(workspaceRoot, issue).Replace('\\', '/'));
        }

        string schemaSource = Path.Combine(workspaceRoot, "src", "LoopRelay.Core", "Services", "Persistence", "LoopRelayWorkspaceDatabase.cs");
        if (File.Exists(schemaSource))
        {
            Add("persistence-schema", "LoopRelayWorkspaceDatabase");
        }

        Add("public-cli-contracts", "status", creditCanary ? EvidenceLevel.LiveTransition : EvidenceLevel.Uncovered,
            creditCanary ? ["status-canary/status-canary"] : []);
        Add("fixture-lifecycle", "materialize-reset-repeat", creditCanary ? EvidenceLevel.DeterministicComponent : EvidenceLevel.Uncovered,
            creditCanary ? ["status-canary/repeated-cycle"] : []);
        Add("oracle", "status-exact-structural-invariant", creditCanary ? EvidenceLevel.LiveTransition : EvidenceLevel.Uncovered,
            creditCanary ? ["status-canary/status-canary"] : []);

        CoverageObligation[] distinct = obligations
            .GroupBy(item => (item.Dimension, item.Identity))
            .Select(group => group.OrderByDescending(item => item.Level).First())
            .OrderBy(item => item.Dimension, StringComparer.Ordinal)
            .ThenBy(item => item.Identity, StringComparer.Ordinal)
            .ToArray();
        string digest = Hash(string.Join("\n", distinct.Select(item =>
            $"{item.Dimension}\0{item.Identity}\0{item.ContentHash}")));
        return new CoverageLedger(Version, digest, distinct);

        void Add(string dimension, string identity, EvidenceLevel level = EvidenceLevel.Uncovered,
            IReadOnlyList<string>? evidence = null, string contentHash = "")
        {
            if (!string.IsNullOrWhiteSpace(identity))
            {
                obligations.Add(new CoverageObligation(dimension, identity, level, evidence ?? [], contentHash));
            }
        }
    }

    public static string HashFiles(IEnumerable<string> files, string root)
    {
        string material = string.Join("\n", files.Where(File.Exists)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => $"{Path.GetRelativePath(root, path).Replace('\\', '/')}\0{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))}"));
        return Hash(material);
    }

    private static IEnumerable<string> EnumerateFiles(string workspaceRoot, string relativeRoot, string pattern)
    {
        string root = Path.Combine(workspaceRoot, relativeRoot);
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            : [];
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

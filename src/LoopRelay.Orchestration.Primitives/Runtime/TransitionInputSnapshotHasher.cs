using System.Security.Cryptography;
using System.Text;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Runtime;

public static class TransitionInputSnapshotHasher
{
    public static TransitionInputSnapshot Create(
        WorkflowTransitionDefinition transition,
        IReadOnlyList<ProductRecord> products,
        IReadOnlyDictionary<string, string> metadata) =>
        Create(transition, products, metadata, []);

    public static TransitionInputSnapshot Create(
        WorkflowTransitionDefinition transition,
        IReadOnlyList<ProductRecord> products,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyList<PromptContextSection> sections)
    {
        var builder = new StringBuilder();
        Append(builder, "transition", transition.Identity.Value);
        Append(builder, "prompt", transition.PromptIdentity);
        Append(builder, "posture", transition.ExecutionPosture.Kind.ToString());

        foreach (ProductRecord product in products.OrderBy(product => product.Identity.Value, StringComparer.Ordinal))
        {
            Append(builder, "product.identity", product.Identity.Value);
            Append(builder, "product.producerWorkflow", product.ProducerWorkflow.Value);
            Append(builder, "product.producerTransition", product.ProducerTransition.Value);
            Append(builder, "product.authority", product.Authority);
            Append(builder, "product.causalIdentity", product.CausalIdentity);
            Append(builder, "product.freshness", product.Freshness.ToString());
            Append(builder, "product.validation", product.ValidationState.ToString());
            foreach (string representation in product.StorageRepresentations.Order(StringComparer.Ordinal))
            {
                Append(builder, "product.storage", representation);
            }
        }

        foreach ((string key, string value) in metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Append(builder, $"metadata.{key}", value);
        }

        foreach (PromptContextSection section in sections.OrderBy(section => section.Title, StringComparer.Ordinal))
        {
            Append(builder, "section.title", section.Title);
            Append(builder, "section.source", section.SourcePath);
            Append(builder, "section.content", section.Content);
            foreach (string evidence in section.Evidence.Order(StringComparer.Ordinal))
            {
                Append(builder, "section.evidence", evidence);
            }
        }

        return new TransitionInputSnapshot(
            Hash(builder.ToString()),
            products,
            new Dictionary<string, string>(metadata, StringComparer.Ordinal),
            sections.ToArray());
    }

    private static void Append(StringBuilder builder, string name, string value)
    {
        builder
            .Append(name.Length)
            .Append(':')
            .Append(name)
            .Append('=')
            .Append(value.Length)
            .Append(':')
            .Append(value)
            .AppendLine();
    }

    private static string Hash(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

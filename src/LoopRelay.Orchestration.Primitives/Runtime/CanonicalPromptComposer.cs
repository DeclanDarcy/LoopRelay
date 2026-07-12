using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Orchestration.Runtime;

/// <summary>
/// Canonical template/profile composition. Nothing may be appended at the send site after this
/// output is hashed and persisted as a rendered-prompt fact.
/// </summary>
public sealed class CanonicalPromptComposer : IPromptComposer
{
    public PromptComposition Compose(
        PromptTemplateIdentity template,
        string? templateSourceHash,
        PolicyIdentity policy,
        PromptPolicyProfile policyProfile,
        ConsumedInputManifestIdentity consumedInputManifest,
        IReadOnlyList<ConsumedInputFile> consumedInputs,
        IReadOnlyDictionary<string, string> renderingVariables,
        string renderedTemplate)
    {
        ArgumentNullException.ThrowIfNull(policyProfile);
        if (string.IsNullOrWhiteSpace(renderedTemplate))
        {
            throw new ArgumentException("Rendered template content must not be empty.", nameof(renderedTemplate));
        }

        string renderedContent = string.Join(
            "\n\n",
            renderedTemplate.TrimEnd(),
            "---",
            policyProfile.Content);
        return new PromptComposition(
            PromptCompositionIdentity.New(),
            template,
            templateSourceHash,
            policy,
            policyProfile.Identity,
            consumedInputManifest,
            consumedInputs,
            renderingVariables,
            renderedContent);
    }
}

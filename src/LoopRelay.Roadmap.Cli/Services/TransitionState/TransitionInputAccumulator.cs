using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;

namespace LoopRelay.Roadmap.Cli.Services.TransitionState;

internal sealed class TransitionInputAccumulator
{
    private readonly Dictionary<string, PendingTransitionInput> inputs = new(StringComparer.Ordinal);

    public void AddRequired(string path, string role) => Add(path, role, required: true);

    public void AddOptional(string path, string role) => Add(path, role, required: false);

    public async Task<IReadOnlyList<TransitionArtifactInput>> SnapshotAsync(ICanonicalArtifactHasher canonicalHasher)
    {
        var snapshot = new List<TransitionArtifactInput>();
        foreach (PendingTransitionInput input in inputs.Values.OrderBy(input => input.Path, StringComparer.Ordinal))
        {
            CanonicalArtifactHash? hash = await canonicalHasher.HashIfPresentAsync(input.Path);
            if (hash is null)
            {
                if (input.Required)
                {
                    throw new RoadmapStepException($"Required transition input is missing: {input.Path}");
                }

                snapshot.Add(new TransitionArtifactInput(
                    input.Path,
                    input.JoinedRoles(),
                    Required: false,
                    TransitionInputPresence.MissingOptional,
                    Hash: null));
                continue;
            }

            snapshot.Add(new TransitionArtifactInput(
                input.Path,
                input.JoinedRoles(),
                input.Required,
                TransitionInputPresence.Present,
                hash.Value));
        }

        return snapshot;
    }

    private void Add(string path, string role, bool required)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Transition input path cannot be empty.", nameof(path));
        }

        if (!inputs.TryGetValue(path, out PendingTransitionInput? input))
        {
            input = new PendingTransitionInput(path);
            inputs.Add(path, input);
        }

        input.Required |= required;
        input.Roles.Add(role);
    }

    private sealed class PendingTransitionInput(string _path)
    {
        public string Path { get; } = _path;
        public bool Required { get; set; }
        public SortedSet<string> Roles { get; } = new(StringComparer.Ordinal);
        public string JoinedRoles() => string.Join("+", Roles);
    }
}

using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ArtifactPromotionRequest(
    string TargetPath,
    string CandidateContent,
    string EvidenceDirectory,
    string EvidenceStem,
    string ArtifactName,
    IArtifactOutputClassifier Classifier,
    IArtifactValidator Validator,
    ArtifactLifecycleState PromotedLifecycleState,
    string LifecycleNotes);

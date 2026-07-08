using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;

namespace LoopRelay.Roadmap.Cli.Models.ArtifactRecords;

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

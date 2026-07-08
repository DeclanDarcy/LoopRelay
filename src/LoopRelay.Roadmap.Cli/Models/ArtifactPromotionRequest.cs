namespace LoopRelay.Roadmap.Cli;

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

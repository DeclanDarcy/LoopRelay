using CommandCenter.Core.Repositories;

namespace CommandCenter.Core.Artifacts;

public interface IArtifactRotationService
{
    Task<Artifact> RotateCurrentHandoffAsync(Repository repository);

    Task<Artifact> RotateCurrentDecisionsAsync(Repository repository);

    Task<Artifact> RotateCurrentOperationalContextAsync(Repository repository);

    Task<Artifact> RotateAsync(Repository repository, ArtifactFamily family);
}

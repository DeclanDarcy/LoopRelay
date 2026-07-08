namespace LoopRelay.Orchestration.Models.NonImplementationCompletion;

public sealed class NonImplementationPostExecutionReviewException : InvalidOperationException
{
    public NonImplementationPostExecutionReviewException(
        string message,
        IReadOnlyList<string> evidencePaths,
        Exception? innerException = null)
        : base(message, innerException)
    {
        EvidencePaths = evidencePaths;
    }

    public IReadOnlyList<string> EvidencePaths { get; }
}

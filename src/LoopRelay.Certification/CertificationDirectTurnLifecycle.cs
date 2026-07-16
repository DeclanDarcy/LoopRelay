namespace LoopRelay.Certification;

internal static class CertificationDirectTurnLifecycle
{
    public static async Task<T> RunAndRecordAsync<T>(
        IAsyncDisposable session,
        Func<CancellationToken, Task<T>> run,
        Func<T, CancellationToken, Task> record,
        CancellationToken cancellationToken)
    {
        T result;
        await using (session)
        {
            result = await run(cancellationToken);
        }

        await record(result, cancellationToken);
        return result;
    }
}

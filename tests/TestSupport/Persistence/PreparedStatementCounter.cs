using Microsoft.Data.Sqlite;
using SQLitePCL;
using Xunit;

namespace LoopRelay.Core.Services.Persistence;

/// <summary>
/// Counts the SQL statements a store connection actually compiles, by installing a SQLite
/// authorizer on it. SQLite consults the authorizer while preparing a statement and raises
/// <c>SQLITE_SELECT</c> exactly once for each SELECT it compiles, so the tally is the read
/// path's real statement count — nothing here models what the implementation ought to cost, and
/// a per-row implementation reports its per-row tally.
/// <para>
/// The authorizer is the only statement-level hook SQLitePCLRaw exposes and it is per
/// connection, which is why a store hands its connections to a testing-only observer seam (see
/// e.g. <c>CanonicalEffectWorkStore.ConnectionObserverForTesting</c> and
/// <c>LedgerLoopHistoryStore.ConnectionObserverForTesting</c>): the store opens connections
/// itself, with pooling off, so a test cannot otherwise reach the handle a read ran on.
/// </para>
/// <para>
/// Shared test utility: promoted out of a private nested position inside
/// <c>DurableEffectPlanHydrationTests</c> (where it originated) so it can be linked into any test
/// project that needs to measure a store's real statement cost, without duplicating the
/// authorizer-plumbing by hand per project. Linked via <c>&lt;Compile Include&gt;</c>, following
/// the convention already established by <c>MemoryArtifactStore</c> in this same
/// <c>tests/TestSupport</c> folder, rather than promoted to its own project - this repo has no
/// shared test project, and a linked file is the smallest move that avoids a second copy.
/// </para>
/// </summary>
public sealed class PreparedStatementCounter
{
    // Held for the lifetime of the counter: SQLite keeps calling this for as long as the
    // connection lives, so it must not be collected once `Watch` returns.
    private readonly delegate_authorizer _authorizer;
    private int _statements;

    public PreparedStatementCounter() => _authorizer = Authorize;

    public int Statements => Volatile.Read(ref _statements);

    public void Watch(SqliteConnection connection) => Assert.Equal(
        raw.SQLITE_OK, raw.sqlite3_set_authorizer(connection.Handle, _authorizer, null));

    private int Authorize(
        object userData, int action, utf8z first, utf8z second, utf8z database, utf8z trigger)
    {
        if (action == raw.SQLITE_SELECT) Interlocked.Increment(ref _statements);
        return raw.SQLITE_OK;
    }
}

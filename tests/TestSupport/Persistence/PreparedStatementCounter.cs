using System.Collections.Concurrent;
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
/// <para>
/// <b>Additive read-attribution channel:</b> <see cref="Statements"/> only ever observes
/// <c>SQLITE_SELECT</c>, whose authorizer arguments are always NULL, so it cannot say which
/// table a compiled SELECT touched. <c>SQLITE_READ</c> fires once per column reference during
/// preparation and carries the table name as its first callback argument, so
/// <see cref="ReadsOfTable"/> tallies those separately, per table. This channel is purely
/// additive: it does not change what <see cref="Statements"/> counts or when it increments, so
/// existing exact-count assertions against <see cref="Statements"/> keep the same value.
/// </para>
/// <para>
/// <b>Additive column-attribution channel (fix pass 1, finding 1):</b> the same
/// <c>SQLITE_READ</c> callback also carries the column name as its second argument, which
/// <see cref="ReadsOfTable"/> discards. <see cref="ReadsOfColumn"/> tallies
/// <c>(table, column)</c> pairs from that same argument instead, so a test can assert a specific
/// column was never authorized for read while a statement compiled - e.g. that a per-row document
/// column never gets selected by a path that is only supposed to read row locations, even if a
/// future regression selects the column without mapping it into the result type (which a
/// type-level or returned-data assertion cannot catch, since the column would compile into the
/// statement but never reach any field). Like <see cref="ReadsOfTable"/>, this is purely additive:
/// it reads the same callback invocations <see cref="Statements"/> and <see cref="ReadsOfTable"/>
/// already observe, adds a second tally alongside theirs, and changes neither what nor when they
/// count.
/// </para>
/// </summary>
public sealed class PreparedStatementCounter
{
    // Held for the lifetime of the counter: SQLite keeps calling this for as long as the
    // connection lives, so it must not be collected once `Watch` returns.
    private readonly delegate_authorizer _authorizer;
    private int _statements;
    private readonly ConcurrentDictionary<string, int> _tableReads = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _columnReads = new(StringComparer.Ordinal);

    public PreparedStatementCounter() => _authorizer = Authorize;

    public int Statements => Volatile.Read(ref _statements);

    /// <summary>
    /// How many <c>SQLITE_READ</c> authorizer callbacks named <paramref name="table"/> as the
    /// table being read, across every statement this counter has observed since construction.
    /// Zero if the table was never read.
    /// </summary>
    public int ReadsOfTable(string table) =>
        _tableReads.TryGetValue(table, out int count) ? count : 0;

    /// <summary>
    /// How many <c>SQLITE_READ</c> authorizer callbacks named <paramref name="column"/> on
    /// <paramref name="table"/> as the column being read, across every statement this counter has
    /// observed since construction. Zero if that column was never read - the direct signal for
    /// "this statement never authorized reading this column at all", independent of whether the
    /// column would have been mapped into any result.
    /// </summary>
    public int ReadsOfColumn(string table, string column) =>
        _columnReads.TryGetValue(ColumnKey(table, column), out int count) ? count : 0;

    public void Watch(SqliteConnection connection) => Assert.Equal(
        raw.SQLITE_OK, raw.sqlite3_set_authorizer(connection.Handle, _authorizer, null));

    private int Authorize(
        object userData, int action, utf8z first, utf8z second, utf8z database, utf8z trigger)
    {
        if (action == raw.SQLITE_SELECT) Interlocked.Increment(ref _statements);
        if (action == raw.SQLITE_READ)
        {
            string? table = first.utf8_to_string();
            if (!string.IsNullOrEmpty(table))
            {
                _tableReads.AddOrUpdate(table, 1, static (_, count) => count + 1);

                string? column = second.utf8_to_string();
                if (!string.IsNullOrEmpty(column))
                {
                    _columnReads.AddOrUpdate(ColumnKey(table, column), 1, static (_, count) => count + 1);
                }
            }
        }

        return raw.SQLITE_OK;
    }

    private static string ColumnKey(string table, string column) => table + "." + column;
}

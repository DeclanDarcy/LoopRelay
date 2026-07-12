using LoopRelay.Core.Services.Persistence;

namespace LoopRelay.Core.Tests.Services;

public sealed class LoopRelayWorkspaceDatabaseTests
{
    [Fact]
    public async Task WorkspaceIdentityIsStableAcrossProcessShapedReopen()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"looprelay-workspace-id-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "looprelay.sqlite3");
        try
        {
            string first;
            await using (Microsoft.Data.Sqlite.SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(database))
            {
                await connection.OpenAsync();
                first = await LoopRelayWorkspaceDatabase.EnsureSchemaAndReadWorkspaceIdAsync(connection);
            }

            await using (Microsoft.Data.Sqlite.SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(database))
            {
                await connection.OpenAsync();
                string second = await LoopRelayWorkspaceDatabase.EnsureSchemaAndReadWorkspaceIdAsync(connection);
                Assert.Equal(first, second);
                Assert.Matches("^(ws_[0-9A-Z]{26}|[a-f0-9]{32})$", second);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DifferentWorkspaceDatabasesHaveDifferentIdentities()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"looprelay-workspace-isolation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string[] ids = new string[2];
            for (int index = 0; index < ids.Length; index++)
            {
                await using Microsoft.Data.Sqlite.SqliteConnection connection =
                    LoopRelayWorkspaceDatabase.OpenReadWriteCreate(Path.Combine(directory, $"{index}.sqlite3"));
                await connection.OpenAsync();
                ids[index] = await LoopRelayWorkspaceDatabase.EnsureSchemaAndReadWorkspaceIdAsync(connection);
            }

            Assert.NotEqual(ids[0], ids[1]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

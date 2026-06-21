using CommandCenter.Core.Configuration;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Backend.Tests;

public sealed class ApplicationConfigurationStoreTests
{
    [Fact]
    public async Task SaveAndLoadRoundTripsRepositories()
    {
        string path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"), "configuration.json");
        var store = new ApplicationConfigurationStore(path);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "CommandCenter",
            Path = @"C:\kernritsu\CommandCenter"
        };

        await store.SaveAsync(new ApplicationConfiguration { Repositories = [repository] });

        ApplicationConfiguration loaded = await new ApplicationConfigurationStore(path).LoadAsync();

        Assert.Single(loaded.Repositories);
        Assert.Equal(repository.Id, loaded.Repositories[0].Id);
        Assert.Equal(repository.Name, loaded.Repositories[0].Name);
        Assert.Equal(repository.Path, loaded.Repositories[0].Path);
    }

    [Fact]
    public async Task MissingConfigurationLoadsEmptyRepositoryList()
    {
        string path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"), "configuration.json");

        ApplicationConfiguration loaded = await new ApplicationConfigurationStore(path).LoadAsync();

        Assert.Empty(loaded.Repositories);
    }
}

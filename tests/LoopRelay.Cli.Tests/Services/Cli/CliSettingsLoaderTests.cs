using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Services.Configuration;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class CliSettingsLoaderTests
{
    [Fact]
    public void Loader_reads_settings_json_from_controlled_base_directory()
    {
        string directory = Directory.CreateTempSubdirectory("cc-cli-settings").FullName;
        File.Copy(DefaultSettingsPath(), Path.Combine(directory, CliSettingsLoader.ConsumerSettingsFileName));

        CliSettingsLoadResult result = CliSettingsLoader.Load(
            baseDirectory: directory,
            getEnvironmentVariable: _ => null);

        Assert.Equal(Path.Combine(directory, CliSettingsLoader.ConsumerSettingsFileName), result.Path);
        Assert.False(result.IsDefaultTemplate);
        Assert.Contains("git", result.PermissionInputs.CommandsWithSubcommands);
    }

    private static string DefaultSettingsPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "config", "settings.default.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate config/settings.default.json.");
    }
}

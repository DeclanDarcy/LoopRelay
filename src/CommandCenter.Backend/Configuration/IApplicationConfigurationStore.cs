namespace CommandCenter.Backend.Configuration;

public interface IApplicationConfigurationStore
{
    Task<ApplicationConfiguration> LoadAsync();

    Task SaveAsync(ApplicationConfiguration configuration);
}

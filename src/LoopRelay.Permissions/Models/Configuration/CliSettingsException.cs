namespace LoopRelay.Permissions.Models.Configuration;

public sealed class CliSettingsException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

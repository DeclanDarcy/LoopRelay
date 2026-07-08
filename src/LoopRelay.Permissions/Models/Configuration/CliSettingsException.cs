using System.Collections.Frozen;
using System.Text.Json;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Configuration;

public sealed class CliSettingsException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

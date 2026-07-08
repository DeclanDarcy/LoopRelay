using System.Collections.Frozen;

namespace LoopRelay.Permissions.Models;

public sealed class PermissionPolicyValidationException(string message) : InvalidOperationException(message);

namespace LoopRelay.Permissions.Models.Policy;

public sealed class PermissionPolicyValidationException(string message) : InvalidOperationException(message);

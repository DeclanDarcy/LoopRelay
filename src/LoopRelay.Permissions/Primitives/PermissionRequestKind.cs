namespace LoopRelay.Permissions.Models;

public enum PermissionRequestKind
{
    Unknown,
    CommandExecution,
    FileChange,
    ToolCall,
    UserInput,
    McpElicitation,
    Permissions,
}

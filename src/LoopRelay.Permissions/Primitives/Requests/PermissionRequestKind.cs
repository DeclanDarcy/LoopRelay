namespace LoopRelay.Permissions.Primitives;

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

namespace LoopRelay.Permissions.Primitives.Requests;

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

namespace CommandCenter.Backend.Execution;

public interface IExecutionPromptBuilder
{
    ExecutionPrompt Build(ExecutionContext context);
}

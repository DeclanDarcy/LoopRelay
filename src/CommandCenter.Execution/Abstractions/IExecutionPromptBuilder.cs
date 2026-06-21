
using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Abstractions;

public interface IExecutionPromptBuilder
{
    ExecutionPrompt Build(ExecutionContext context);
}

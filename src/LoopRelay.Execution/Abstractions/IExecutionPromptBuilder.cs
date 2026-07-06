using LoopRelay.Execution.Models;

namespace LoopRelay.Execution.Abstractions;

public interface IExecutionPromptBuilder
{
    ExecutionPrompt Build(ImplementationExecutionContext context);
}

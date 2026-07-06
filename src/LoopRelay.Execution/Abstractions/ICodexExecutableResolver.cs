using LoopRelay.Execution.Models;

namespace LoopRelay.Execution.Abstractions;

public interface ICodexExecutableResolver
{
    CodexExecutable Resolve();
}

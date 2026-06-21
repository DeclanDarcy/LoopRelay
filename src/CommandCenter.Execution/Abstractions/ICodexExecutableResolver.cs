using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Abstractions;

public interface ICodexExecutableResolver
{
    CodexExecutable Resolve();
}

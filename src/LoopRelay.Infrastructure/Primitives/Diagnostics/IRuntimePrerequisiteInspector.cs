using LoopRelay.Infrastructure.Models.Diagnostics;

namespace LoopRelay.Infrastructure.Primitives.Diagnostics;

public interface IRuntimePrerequisiteInspector
{
    string Provider { get; }

    IReadOnlyList<RuntimePrerequisiteFinding> Inspect(ResolvedRuntimeHostProfile profile);
}

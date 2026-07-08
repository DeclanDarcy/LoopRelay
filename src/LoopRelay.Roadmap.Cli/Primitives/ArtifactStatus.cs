using System.Text.RegularExpressions;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Roadmap.Cli;

internal enum ArtifactStatus
{
    Missing,
    Empty,
    Present,
}

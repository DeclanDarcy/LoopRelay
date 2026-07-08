using System.Globalization;
using System.Text.RegularExpressions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Cli;

/// <summary>Waits out a delay. Abstracted so tests assert the requested duration without actually sleeping.</summary>
internal interface IUsageDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

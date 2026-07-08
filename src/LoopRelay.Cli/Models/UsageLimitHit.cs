using System.Globalization;
using System.Text.RegularExpressions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Cli;

/// <summary>A detected codex usage-limit failure: how long to wait before retrying, and the advertised
/// retry time when it could be parsed from the error (null when the fallback wait is in effect).</summary>
internal sealed record UsageLimitHit(TimeSpan Wait, DateTimeOffset? RetryAt);

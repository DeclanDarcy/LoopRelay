using System.Globalization;
using System.Text.RegularExpressions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Cli;

/// <summary>
/// Always-on replacement for the old watermark usage gate: codex reports quota exhaustion only by failing
/// the turn with "You've hit your usage limit ... try again at &lt;time&gt;", so instead of probing quota
/// before each turn this detector inspects each FAILED turn's diagnostics (protocol failure message or
/// stderr tail), parses the advertised retry time, and tells the seam how long to wait before rerunning
/// the turn. The advertised time is a wall-clock local time with no zone marker, so it is anchored in
/// <paramref name="timeZone"/> (production: the machine's local zone). Degradation is deliberate and
/// bounded: an unparseable or long-past ("stale") time falls back to a conservative fixed wait that rides
/// out short outages, and the seam's retry cap surfaces the failure when the limit persists beyond that.
/// </summary>
internal sealed class UsageLimitDetector(
    IClock clock, IUsageDelay delay, ILoopConsole console, TimeZoneInfo? timeZone = null) : IUsageLimitDetector
{
    internal static readonly TimeSpan FallbackWait = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan MinimumWait = TimeSpan.FromMinutes(1);

    /// <summary>How far past an advertised time still counts as a reset-boundary race / small clock skew
    /// (floored to <see cref="MinimumWait"/>). Anything older is stale or mis-anchored — a wrong zone is
    /// whole hours off — and trusting it would burn every retry in minutes, so it degrades to
    /// <see cref="FallbackWait"/> instead.</summary>
    private static readonly TimeSpan StaleRetryTimeTolerance = TimeSpan.FromMinutes(10);

    // Matched against the sentence "...purchase more credits or try again at Jul 7th, 2026 9:13 AM."
    // (captured verbatim from codex 0.142.5, both the stderr and the app-server protocol variant); the
    // phrase check deliberately skips the "You've" contraction so the apostrophe's encoding cannot break
    // detection. Two capture shapes: sentence-terminated (stops at the first period) and rest-of-line
    // (survives periods inside the time, e.g. "9:13 a.m."). Diagnostics can be the codex process's
    // CUMULATIVE stderr tail holding several messages, oldest first — the NEWEST advertised time is the
    // operative one, so parsing always takes the last occurrence.
    private static readonly Regex SentenceCapture = new(
        @"try again at\s+(?<when>[^.\r\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LineCapture = new(
        @"try again at\s+(?<when>[^\r\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OrdinalSuffixPattern = new(
        @"(?<=\d)(st|nd|rd|th)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "a.m." / "p. m." → "aM" / "pM" so the period-styled meridiem survives sentence trimming and parses
    // (AM/PM designator matching is case-insensitive).
    private static readonly Regex PeriodMeridiemPattern = new(
        @"\b(?<half>[ap])\.\s?m\.?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] FullFormats = ["MMM d, yyyy h:mm tt", "MMMM d, yyyy h:mm tt"];
    private static readonly string[] TimeOnlyFormats = ["h:mm tt", "h tt"];

    public UsageLimitHit? Detect(AgentTurnResult result)
    {
        if (result.State != AgentTurnState.Failed
            || result.Diagnostics is not { } diagnostics
            || !diagnostics.Contains("hit your usage limit", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!TryParseRetryAt(diagnostics, out DateTimeOffset retryAt))
        {
            return new UsageLimitHit(FallbackWait, RetryAt: null);
        }

        TimeSpan wait = retryAt - clock.UtcNow;
        if (wait >= MinimumWait)
        {
            return new UsageLimitHit(wait, retryAt);
        }

        return wait >= -StaleRetryTimeTolerance
            ? new UsageLimitHit(MinimumWait, retryAt)
            : new UsageLimitHit(FallbackWait, RetryAt: null);
    }

    public async Task WaitOutAsync(UsageLimitHit hit, CancellationToken cancellationToken)
    {
        console.Warn(hit.RetryAt is { } retryAt
            ? $"Codex usage limit hit — waiting {Format(hit.Wait)} (until {retryAt:MMM d, yyyy h:mm tt zzz}) before retrying."
            : $"Codex usage limit hit and no usable retry time could be parsed from the error — waiting {Format(hit.Wait)} before retrying.");
        await delay.DelayAsync(hit.Wait, cancellationToken);
    }

    public void WarnRetriesExhausted(int retries) =>
        console.Warn($"Codex usage limit still hit after {retries} waited retries — giving up and surfacing the failure.");

    private bool TryParseRetryAt(string diagnostics, out DateTimeOffset retryAt)
    {
        foreach (Regex capture in new[] { SentenceCapture, LineCapture })
        {
            MatchCollection matches = capture.Matches(diagnostics);
            if (matches.Count == 0)
            {
                continue;
            }

            string when = Normalize(matches[^1].Groups["when"].Value);
            if (TryParseWhen(when, out retryAt))
            {
                return true;
            }
        }

        retryAt = default;
        return false;
    }

    private static string Normalize(string when)
    {
        when = when.Replace('\u00A0', ' ').Replace('\u202F', ' ');
        when = PeriodMeridiemPattern.Replace(when, "${half}M");
        when = OrdinalSuffixPattern.Replace(when, string.Empty);
        return when.TrimEnd('.', ' ', '\t').Trim();
    }

    private bool TryParseWhen(string when, out DateTimeOffset retryAt)
    {
        if (DateTime.TryParseExact(when, FullFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            retryAt = AnchorFull(parsed);
            return true;
        }

        if (DateTime.TryParseExact(when, TimeOnlyFormats, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out parsed))
        {
            retryAt = AnchorTimeOnly(parsed.TimeOfDay);
            return true;
        }

        // Lenient last resort for unforeseen shapes ("2026-07-07 09:13", "7 July 2026 9:13 AM", ...).
        // NoCurrentDateDefault keeps this deterministic: a dateless input yields the year-1 sentinel date
        // instead of silently borrowing the MACHINE's today, which for a time already past would collapse
        // the wait to the minimum floor and burn the retries hours before the real reset.
        if (DateTime.TryParse(when, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out parsed))
        {
            retryAt = parsed.Date == DateTime.MinValue.Date ? AnchorTimeOnly(parsed.TimeOfDay) : AnchorFull(parsed);
            return true;
        }

        retryAt = default;
        return false;
    }

    // codex prints a wall-clock time; anchor it in the configured zone regardless of what Kind the
    // lenient fallback parse produced (a Utc/Local Kind would make the DateTimeOffset ctor throw).
    private DateTimeOffset AnchorFull(DateTime parsed)
    {
        parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        TimeZoneInfo zone = Zone();
        return new DateTimeOffset(parsed, zone.GetUtcOffset(parsed));
    }

    // A bare time of day means the NEXT upcoming instant with that wall-clock time: "3:47 AM" printed at
    // 23:00 is tomorrow, not a past instant earlier today.
    private DateTimeOffset AnchorTimeOnly(TimeSpan timeOfDay)
    {
        TimeZoneInfo zone = Zone();
        DateTime localNow = TimeZoneInfo.ConvertTime(clock.UtcNow, zone).DateTime;
        DateTime wall = localNow.Date + timeOfDay;
        if (wall <= localNow)
        {
            wall = wall.AddDays(1);
        }

        return new DateTimeOffset(wall, zone.GetUtcOffset(wall));
    }

    private TimeZoneInfo Zone() => timeZone ?? TimeZoneInfo.Local;

    private static string Format(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "0m";
        }

        int days = (int)duration.TotalDays;
        return days > 0
            ? $"{days}d {duration.Hours}h {duration.Minutes}m"
            : $"{duration.Hours}h {duration.Minutes}m";
    }
}

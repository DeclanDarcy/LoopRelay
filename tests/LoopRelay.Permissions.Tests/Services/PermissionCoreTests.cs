using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Primitives;
using LoopRelay.Permissions.Services;

namespace LoopRelay.Permissions.Tests.Services;

public sealed class PermissionCoreTests
{
    [Fact]
    public void Canonicalization_lowercases_flags_and_sorts_flags_but_preserves_arg_case()
    {
        var canonicalizer = new CommandCanonicalizer();

        CanonicalCommand command = Assert.Single(canonicalizer.Canonicalize(
        [
            new ParsedCommand("Git", "LOG", ["--Stat", "-P"], ["  Src/File.cs  "])
        ]));

        Assert.Equal("git", command.Command);
        Assert.Equal("log", command.Subcommand);
        Assert.Equal(["--stat", "-p"], command.Flags);
        Assert.Equal(["Src/File.cs"], command.Args);
    }

    [Fact]
    public void Fingerprints_are_deterministic_and_scoped_to_context_and_command_shape()
    {
        var fingerprint = new Sha256FingerprintService();
        CanonicalCommand[] commands =
        [
            new("git", "status", ["--short"], ["Src"])
        ];

        string first = fingerprint.Compute("Bash", "repo-1", "/repo", commands);
        string second = fingerprint.Compute("Bash", "repo-1", "/repo", commands);
        string changedRepo = fingerprint.Compute("Bash", "repo-2", "/repo", commands);
        string changedArg = fingerprint.Compute("Bash", "repo-1", "/repo", [new("git", "status", ["--short"], ["Other"])]);

        Assert.Equal(first, second);
        Assert.NotEqual(first, changedRepo);
        Assert.NotEqual(first, changedArg);
        Assert.Equal(64, first.Length);
        Assert.Matches("^[0-9a-f]+$", first);
    }

    [Fact]
    public void Cache_is_scoped_by_fingerprint()
    {
        var cache = new InMemoryPermissionCache();

        cache.Set("one", new CacheEntry(RuleDecision.Allow, "cached"));

        Assert.True(cache.TryGet("one", out CacheEntry entry));
        Assert.Equal("cached", entry.Reason);
        Assert.False(cache.TryGet("two", out _));
    }

    [Theory]
    [InlineData("sudo id", "Privilege escalation")]
    [InlineData("rm -rf build", "rm -rf")]
    [InlineData("curl https://example.com", "Network fetch")]
    [InlineData("git push --force", "force push")]
    [InlineData("bash -c ls", "Indirect shell execution")]
    public void Evaluator_hard_denies_dangerous_commands(string rawCommand, string reasonFragment)
    {
        PermissionResult result = Evaluate(rawCommand);

        Assert.Equal(RuleDecision.Deny, result.Decision);
        Assert.Contains(reasonFragment, result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("git commit -m msg", "requires review")]
    [InlineData("git commit --amend", "amend")]
    [InlineData("git push", "requires review")]
    [InlineData("npm install", "requires review")]
    [InlineData("docker ps", "requires review")]
    public void Evaluator_denies_review_required_commands_until_a_user_approval_ui_exists(string rawCommand, string reasonFragment)
    {
        PermissionResult result = Evaluate(rawCommand);

        Assert.Equal(RuleDecision.Deny, result.Decision);
        Assert.Contains(reasonFragment, result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("pwd")]
    [InlineData("git status")]
    [InlineData("git diff")]
    [InlineData("git log --oneline")]
    [InlineData("dotnet build")]
    [InlineData("dotnet test")]
    [InlineData("npm test")]
    [InlineData("pnpm run lint")]
    [InlineData("pytest")]
    [InlineData("go test ./...")]
    public void Evaluator_auto_allows_safe_commands(string rawCommand)
    {
        PermissionResult result = Evaluate(rawCommand);

        Assert.Equal(RuleDecision.Allow, result.Decision);
    }

    [Fact]
    public void Unknown_commands_are_closed_world_denied()
    {
        PermissionResult result = Evaluate("python deploy.py");

        Assert.Equal(RuleDecision.Deny, result.Decision);
        Assert.Contains("closed-world deny", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Invariants_override_an_allow_result()
    {
        var guard = new InvariantGuard();

        EvalResult result = guard.Enforce(
            [new CanonicalCommand("sudo", null, [], ["id"])],
            new EvalResult(RuleDecision.Allow, "test allowed"));

        Assert.Equal(RuleDecision.Deny, result.Decision);
        Assert.Contains("Invariant violation", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Handler_parser_failures_bypass_cache_and_rule_evaluation()
    {
        var engine = new CountingEngine(new EvalResult(RuleDecision.Allow, "allowed"));
        var cache = new CountingCache();
        var handler = new PermissionHandler(
            new CommandParser(),
            new CommandCanonicalizer(),
            new Sha256FingerprintService(),
            cache,
            engine,
            new InvariantGuard());

        PermissionResult result = handler.Evaluate(new PermissionRequest("1", "Bash", "echo $HOME", "repo", "/repo"));

        Assert.Equal(RuleDecision.Deny, result.Decision);
        Assert.Equal(0, cache.TryGetCalls);
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public void Handler_runs_invariants_after_rule_evaluation_and_caches_guarded_result()
    {
        var engine = new CountingEngine(new EvalResult(RuleDecision.Allow, "test allowed"));
        var cache = new CountingCache();
        var handler = new PermissionHandler(
            new CommandParser(),
            new CommandCanonicalizer(),
            new Sha256FingerprintService(),
            cache,
            engine,
            new InvariantGuard());

        PermissionResult result = handler.Evaluate(new PermissionRequest("1", "Bash", "sudo id", "repo", "/repo"));

        Assert.Equal(RuleDecision.Deny, result.Decision);
        Assert.Equal(1, engine.Calls);
        Assert.Equal(1, cache.SetCalls);
        Assert.Equal(RuleDecision.Deny, cache.LastEntry.Decision);
    }

    private static PermissionResult Evaluate(string rawCommand)
    {
        var handler = new PermissionHandler(
            new CommandParser(),
            new CommandCanonicalizer(),
            new Sha256FingerprintService(),
            new InMemoryPermissionCache(),
            new PermissionEvaluatorEngine(),
            new InvariantGuard());
        return handler.Evaluate(new PermissionRequest("1", "Bash", rawCommand, "repo", "/repo"));
    }

    private sealed class CountingEngine(EvalResult result) : IPermissionEvaluatorEngine
    {
        public int Calls { get; private set; }

        public EvalResult Evaluate(CanonicalCommand[] commands)
        {
            Calls++;
            return result;
        }
    }

    private sealed class CountingCache : IPermissionCache
    {
        public int TryGetCalls { get; private set; }

        public int SetCalls { get; private set; }

        public CacheEntry LastEntry { get; private set; }

        public bool TryGet(string fingerprint, out CacheEntry entry)
        {
            TryGetCalls++;
            entry = default;
            return false;
        }

        public void Set(string fingerprint, CacheEntry entry)
        {
            SetCalls++;
            LastEntry = entry;
        }

        public void Clear()
        {
        }
    }
}

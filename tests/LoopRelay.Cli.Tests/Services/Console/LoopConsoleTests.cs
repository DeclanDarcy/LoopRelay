using LoopRelay.Cli.Services;
using Xunit;

namespace LoopRelay.Cli.Tests.Services;

public class LoopConsoleTests
{
    private static (ConsoleLoopConsole Console, StringWriter Out, StringWriter Err) New()
    {
        // Force "\n" so assertions are deterministic regardless of the host's Environment.NewLine.
        var outw = new StringWriter { NewLine = "\n" };
        var errw = new StringWriter { NewLine = "\n" };
        return (new ConsoleLoopConsole(outw, errw), outw, errw);
    }

    // The core bug: a streamed codex turn arrives as deltas with no guaranteed trailing newline, so a status
    // line printed right after would glue onto the tail of the streamed output. It must start on its own line.
    [Fact]
    public void Info_AfterAnUnterminatedDelta_StartsOnItsOwnLine()
    {
        var (console, outw, _) = New();

        console.Delta("streamed codex output with no trailing newline");
        console.Info("New decisions.md verified.");

        Assert.Equal(
            "streamed codex output with no trailing newline\n[ok] New decisions.md verified.\n",
            outw.ToString());
    }

    // A delta that already ended in a newline must NOT get a spurious blank line inserted after it.
    [Fact]
    public void Info_AfterADeltaThatEndsInNewline_DoesNotInsertABlankLine()
    {
        var (console, outw, _) = New();

        console.Delta("line\n");
        console.Info("done");

        Assert.Equal("line\n[ok] done\n", outw.ToString());
    }

    [Fact]
    public void Message_AfterUnterminatedDelta_StartsOnItsOwnLine()
    {
        var (console, outw, _) = New();

        console.Delta("partial");
        console.Message("full output");

        Assert.Equal("partial\nfull output\n", outw.ToString());
    }

    [Fact]
    public void Phase_AfterUnterminatedDelta_ClosesTheLineThenSeparates()
    {
        var (console, outw, _) = New();

        console.Delta("partial");
        console.Phase("Execution: GenerateHandoff");

        // Close the streamed line, then the phase's own blank-line separator + banner.
        Assert.Equal("partial\n\n=== Execution: GenerateHandoff ===\n", outw.ToString());
    }

    [Fact]
    public void Error_AfterUnterminatedDelta_ClosesStdoutLine_AndWritesToStderr()
    {
        var (console, outw, errw) = New();

        console.Delta("partial");
        console.Error("boom");

        Assert.Equal("partial\n", outw.ToString());       // the half-written stdout line is closed
        Assert.Equal("[error] boom\n", errw.ToString());  // the error itself goes to stderr
    }

    [Fact]
    public void Progress_WritesToStderrWithoutTouchingStdout()
    {
        var (console, outw, errw) = New();

        console.Progress("[codex] submitted turn: promptTokensEstimated=2");
        console.ProgressComplete("[codex] first output: elapsed=1s");

        Assert.Equal(string.Empty, outw.ToString());
        Assert.Equal(
            "[codex] submitted turn: promptTokensEstimated=2\n[codex] first output: elapsed=1s\n",
            errw.ToString());
    }

    // Consecutive structured messages already end in newlines, so each lands on its own line.
    [Fact]
    public void StructuredMessages_EachLandOnTheirOwnLine()
    {
        var (console, outw, _) = New();

        console.Info("a");
        console.Warn("b");
        console.Message("c");

        Assert.Equal("[ok] a\n[warn] b\nc\n", outw.ToString());
    }

    // A tool call renders as a compact, indented line of its own.
    [Fact]
    public void Tool_RendersACompactIndentedLine()
    {
        var (console, outw, _) = New();

        console.Tool("$ git status");

        Assert.Equal("  $ git status\n", outw.ToString());
    }

    // A tool call that lands while a delta line is still open closes that line first.
    [Fact]
    public void Tool_AfterUnterminatedDelta_StartsOnItsOwnLine()
    {
        var (console, outw, _) = New();

        console.Delta("partial reply");
        console.Tool("$ dotnet build");

        Assert.Equal("partial reply\n  $ dotnet build\n", outw.ToString());
    }
}

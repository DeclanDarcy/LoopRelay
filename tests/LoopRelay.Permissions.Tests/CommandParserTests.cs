using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Services;

namespace LoopRelay.Permissions.Tests;

public sealed class CommandParserTests
{
    private readonly CommandParser parser = new();

    [Fact]
    public void Parses_simple_commands_flags_args_and_chains()
    {
        ParseResult result = parser.Parse("Bash", "git status --short && dotnet test tests/LoopRelay.Permissions.Tests");

        Assert.False(result.HasUnknownSyntax);
        Assert.Equal(2, result.Commands.Length);
        Assert.Equal("git", result.Commands[0].Command);
        Assert.Equal("status", result.Commands[0].Subcommand);
        Assert.Equal(["--short"], result.Commands[0].Flags);
        Assert.Equal("dotnet", result.Commands[1].Command);
        Assert.Equal("test", result.Commands[1].Subcommand);
        Assert.Equal(["tests/LoopRelay.Permissions.Tests"], result.Commands[1].Args);
    }

    [Fact]
    public void Parses_quoted_arguments()
    {
        ParseResult result = parser.Parse("Bash", "echo \"Hello World\" 'Again'");

        Assert.False(result.HasUnknownSyntax);
        Assert.Equal(["Hello World", "Again"], result.Commands.Single().Args);
    }

    [Theory]
    [InlineData("echo $(whoami)", "subshell expansion")]
    [InlineData("echo `whoami`", "backtick execution")]
    [InlineData("cat <(echo hi)", "process substitution")]
    [InlineData("echo $HOME", "environment variable")]
    [InlineData("cat <<EOF", "here-document")]
    [InlineData("echo \"unterminated", "unbalanced quotes")]
    [InlineData("   ", "empty command")]
    public void Unsupported_shell_constructs_are_terminal_parse_failures(string command, string expectedReason)
    {
        ParseResult result = parser.Parse("Bash", command);

        Assert.True(result.HasUnknownSyntax);
        Assert.Contains(expectedReason, result.UnknownSyntaxReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Null_raw_command_maps_the_tool_name_as_the_command()
    {
        ParseResult result = parser.Parse("Read", rawCommand: null);

        ParsedCommand command = Assert.Single(result.Commands);
        Assert.Equal("Read", command.Command);
        Assert.False(result.HasUnknownSyntax);
    }
}

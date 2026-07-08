using LoopRelay.Roadmap.Cli.Abstractions;

namespace LoopRelay.Roadmap.Cli.Services.Cli;

internal sealed class ConsoleLoopConsole(TextWriter? output = null, TextWriter? error = null)
    : Infrastructure.Services.Console.ConsoleLoopConsole(output, error), ILoopConsole;

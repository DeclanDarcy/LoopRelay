using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Services.Console;

internal sealed class ConsoleLoopConsole(TextWriter? output = null, TextWriter? error = null)
    : Infrastructure.Services.Console.ConsoleLoopConsole(output, error), ILoopConsole;

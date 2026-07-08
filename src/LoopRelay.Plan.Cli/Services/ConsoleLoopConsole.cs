using LoopRelay.Plan.Cli.Abstractions;

namespace LoopRelay.Plan.Cli.Services;

internal sealed class ConsoleLoopConsole(TextWriter? output = null, TextWriter? error = null)
    : Infrastructure.Services.Console.ConsoleLoopConsole(output, error), ILoopConsole;

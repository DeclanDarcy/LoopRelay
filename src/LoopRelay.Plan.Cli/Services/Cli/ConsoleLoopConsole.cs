using LoopRelay.Plan.Cli.Abstractions;

namespace LoopRelay.Plan.Cli.Services.Cli;

internal sealed class ConsoleLoopConsole(TextWriter? _output = null, TextWriter? _error = null)
    : Infrastructure.Services.Console.ConsoleLoopConsole(_output, _error), ILoopConsole;

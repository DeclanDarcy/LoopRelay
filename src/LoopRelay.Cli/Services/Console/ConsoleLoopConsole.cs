using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Services.Console;

internal sealed class ConsoleLoopConsole(TextWriter? _output = null, TextWriter? _error = null)
    : Infrastructure.Services.Console.ConsoleLoopConsole(_output, _error), ILoopConsole;

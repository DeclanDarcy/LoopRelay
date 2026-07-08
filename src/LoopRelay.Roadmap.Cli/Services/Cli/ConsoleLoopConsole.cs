using LoopRelay.Roadmap.Cli.Abstractions;

namespace LoopRelay.Roadmap.Cli.Services.Cli;

internal sealed class ConsoleLoopConsole(TextWriter? _output = null, TextWriter? _error = null)
    : Infrastructure.Services.Console.ConsoleLoopConsole(_output, _error), ILoopConsole;

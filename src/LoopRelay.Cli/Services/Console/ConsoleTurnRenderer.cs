using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Services.Console;

internal sealed class ConsoleTurnRenderer(ILoopConsole _console)
    : Infrastructure.Services.Console.ConsoleTurnRenderer(_console);

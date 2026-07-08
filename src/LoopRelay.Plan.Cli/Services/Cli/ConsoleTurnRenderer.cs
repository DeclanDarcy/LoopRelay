using LoopRelay.Plan.Cli.Abstractions;

namespace LoopRelay.Plan.Cli.Services.Cli;

internal sealed class ConsoleTurnRenderer(ILoopConsole _console)
    : Infrastructure.Services.Console.ConsoleTurnRenderer(_console);

using LoopRelay.Roadmap.Cli.Abstractions;

namespace LoopRelay.Roadmap.Cli.Services.Cli;

internal sealed class ConsoleTurnRenderer(ILoopConsole _console)
    : Infrastructure.Services.Console.ConsoleTurnRenderer(_console);

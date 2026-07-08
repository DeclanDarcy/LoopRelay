using LoopRelay.Roadmap.Cli.Abstractions;

namespace LoopRelay.Roadmap.Cli.Services.Cli;

internal sealed class ConsoleTurnRenderer(ILoopConsole console)
    : Infrastructure.Services.Console.ConsoleTurnRenderer(console);

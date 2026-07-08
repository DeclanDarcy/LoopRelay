using LoopRelay.Plan.Cli.Abstractions;

namespace LoopRelay.Plan.Cli.Services;

internal sealed class ConsoleTurnRenderer(ILoopConsole console)
    : Infrastructure.Services.Console.ConsoleTurnRenderer(console);

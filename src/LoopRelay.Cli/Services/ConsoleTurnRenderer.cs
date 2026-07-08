using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Services;

internal sealed class ConsoleTurnRenderer(ILoopConsole console)
    : Infrastructure.Services.Console.ConsoleTurnRenderer(console);

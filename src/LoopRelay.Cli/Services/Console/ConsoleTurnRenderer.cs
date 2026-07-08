using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Services.Console;

internal sealed class ConsoleTurnRenderer(ILoopConsole console)
    : Infrastructure.Services.Console.ConsoleTurnRenderer(console);

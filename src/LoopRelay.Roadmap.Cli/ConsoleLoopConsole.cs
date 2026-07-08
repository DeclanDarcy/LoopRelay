namespace LoopRelay.Roadmap.Cli;

internal sealed class ConsoleLoopConsole(TextWriter? output = null, TextWriter? error = null)
    : Infrastructure.Console.ConsoleLoopConsole(output, error), ILoopConsole;

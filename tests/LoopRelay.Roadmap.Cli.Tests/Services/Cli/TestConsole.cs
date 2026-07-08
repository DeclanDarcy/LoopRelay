using LoopRelay.Roadmap.Cli.Abstractions;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

internal sealed class TestConsole : ILoopConsole
{
    public List<string> Phases { get; } = [];
    public List<string> Messages { get; } = [];
    public List<string> Deltas { get; } = [];
    public List<string> Tools { get; } = [];
    public List<string> Infos { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];

    public void Phase(string phase) => Phases.Add(phase);
    public void Message(string content) => Messages.Add(content);
    public void Delta(string text) => Deltas.Add(text);
    public void Tool(string summary) => Tools.Add(summary);
    public void Info(string text) => Infos.Add(text);
    public void Warn(string text) => Warnings.Add(text);
    public void Error(string text) => Errors.Add(text);
}

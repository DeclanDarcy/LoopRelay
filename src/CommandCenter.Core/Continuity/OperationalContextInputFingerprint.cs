namespace CommandCenter.Core.Continuity;

public sealed class OperationalContextInputFingerprint
{
    public string Name { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public bool Present { get; init; }

    public string Hash { get; init; } = string.Empty;

    public int CharacterCount { get; init; }

    public int ByteCount { get; init; }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LoopRelay.Permissions.Abstractions.Security;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Primitives.Parsing;
using LoopRelay.Permissions.Services.Evaluation;

namespace LoopRelay.Permissions.Services.Security;

public sealed class Sha256FingerprintService : IFingerprintService
{
    private readonly PermissionPolicyOptions _policy;

    public Sha256FingerprintService()
        : this(PermissionPolicyOptions.Default)
    {
    }

    public Sha256FingerprintService(PermissionPolicyOptions policy)
    {
        _policy = PermissionPolicyFactory.MergeWithMinimum(policy);
    }

    public string Compute(
        string toolName,
        string repoIdentity,
        string workingDirectory,
        CanonicalCommand[] commands)
    {
        using var stream = new MemoryStream();
        WriteField(stream, "fingerprintVersion", _policy.FingerprintVersion);
        WriteField(stream, "repoIdentity", repoIdentity);
        WriteField(stream, "workingDirectory", workingDirectory);
        WriteField(stream, "toolName", toolName.ToLowerInvariant());
        WriteField(stream, "commands.count", commands.Length.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < commands.Length; i++)
        {
            CanonicalCommand command = commands[i];
            string prefix = $"commands.{i.ToString(CultureInfo.InvariantCulture)}";
            WriteField(stream, $"{prefix}.command", command.Command);
            WriteField(stream, $"{prefix}.subcommand", command.Subcommand);
            WriteStringList(stream, $"{prefix}.flags", command.Flags);
            WriteStringList(stream, $"{prefix}.args", command.Args);
        }

        byte[] hash = SHA256.HashData(stream.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void WriteField(Stream stream, string name, string? value)
    {
        WriteToken(stream, "field");
        WriteString(stream, name);
        WriteString(stream, value);
    }

    private static void WriteStringList(Stream stream, string name, IReadOnlyList<string> values)
    {
        WriteField(stream, $"{name}.count", values.Count.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < values.Count; i++)
        {
            WriteField(stream, $"{name}.{i.ToString(CultureInfo.InvariantCulture)}", values[i]);
        }
    }

    private static void WriteString(Stream stream, string? value)
    {
        if (value is null)
        {
            WriteAscii(stream, "null;");
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteAscii(stream, bytes.Length.ToString(CultureInfo.InvariantCulture));
        WriteAscii(stream, ":");
        stream.Write(bytes, 0, bytes.Length);
        WriteAscii(stream, ";");
    }

    private static void WriteToken(Stream stream, string token)
    {
        WriteAscii(stream, token);
        WriteAscii(stream, ":");
    }

    private static void WriteAscii(Stream stream, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }
}

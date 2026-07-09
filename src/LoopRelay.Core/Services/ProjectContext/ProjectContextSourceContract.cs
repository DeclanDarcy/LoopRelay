using System.Text;

namespace LoopRelay.Core.Services.ProjectContext;

public static class ProjectContextSourceContract
{
    public const string DirectoryPath = ".agents/ctx";

    public static readonly IReadOnlyList<string> SourceFiles = Array.AsReadOnly(
    [
        $"{DirectoryPath}/01-purpose.md",
        $"{DirectoryPath}/02-capability-model.md",
        $"{DirectoryPath}/03-invariants.md",
        $"{DirectoryPath}/04-strategic-structure.md",
        $"{DirectoryPath}/05-authority-model.md",
        $"{DirectoryPath}/06-evaluation-model.md",
        $"{DirectoryPath}/07-drift-and-false-success.md",
        $"{DirectoryPath}/08-vocabulary.md",
        $"{DirectoryPath}/09-eval-details.md",
    ]);

    public static bool IsCanonicalSourceFile(string path) =>
        SourceFiles.Contains(path, StringComparer.Ordinal);

    public static bool IsNumberedSourceFileName(string fileName) =>
        fileName.Length > "00-.md".Length &&
        char.IsDigit(fileName[0]) &&
        char.IsDigit(fileName[1]) &&
        fileName[2] == '-' &&
        fileName.EndsWith(".md", StringComparison.Ordinal);

    public static string BuildViolationMessage(
        IReadOnlyCollection<string> missing,
        IReadOnlyCollection<string> extraNumberedFiles)
    {
        var message = new StringBuilder("Project Context source contract violation.");
        message.Append("\nThe current canonical Project Context contract requires ")
            .Append(SourceFiles.Count)
            .AppendLine(" files:");
        foreach (string path in SourceFiles)
        {
            message.Append("- ").AppendLine(path);
        }

        if (missing.Count > 0)
        {
            message.Append("Missing required files:");
            foreach (string path in missing)
            {
                message.Append("\n- ").Append(path);
            }
        }

        if (extraNumberedFiles.Count > 0)
        {
            if (missing.Count > 0)
            {
                message.AppendLine();
            }

            message.Append("Unexpected numbered Project Context source files were found:");
            foreach (string path in extraNumberedFiles)
            {
                message.Append("\n- ").Append(path);
            }
        }

        return message.ToString();
    }
}

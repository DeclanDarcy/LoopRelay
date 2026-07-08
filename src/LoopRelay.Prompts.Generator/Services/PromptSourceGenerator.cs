// <auto-authored/> Lib.Prompts - compile-time prompt template generator.
//
// Scans `*.prompt` files supplied as <AdditionalFiles>, parses {name} placeholders
// ONCE at compile time, and emits a strongly-typed `Render(...)` per file. Missing
// or malformed placeholders become BUILD ERRORS (the whole point vs embedded
// resources). Runtime cost is a single `string.Concat` - zero parse, zero file I/O.
//
// Template syntax (the resolved contract - see README "Template contract"):
//   {name}   -> a placeholder; becomes a `string name` parameter on Render.
//   {{       -> a literal '{'
//   }}       -> a literal '}'
//   names must be valid C# identifiers; reused names map to one parameter.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LoopRelay.Prompts.Generator.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace LoopRelay.Prompts.Generator.Services
{
    [Generator(LanguageNames.CSharp)]
    public sealed class PromptSourceGenerator : IIncrementalGenerator
    {
        private const string PromptExtension = ".prompt";

        // --- Diagnostics (compile-time validation surface) -------------------
        private static readonly DiagnosticDescriptor UnterminatedPlaceholder = new(
            id: "PROMPT001", title: "Unterminated placeholder",
            messageFormat: "Unterminated placeholder: '{{' is missing its closing '}}'",
            category: "LibPrompts", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor EmptyPlaceholder = new(
            id: "PROMPT002", title: "Empty placeholder",
            messageFormat: "Empty placeholder '{{}}' is not allowed",
            category: "LibPrompts", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InvalidPlaceholderName = new(
            id: "PROMPT003", title: "Invalid placeholder name",
            messageFormat: "Placeholder name '{0}' is not a valid C# identifier",
            category: "LibPrompts", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor UnescapedBrace = new(
            id: "PROMPT004", title: "Unescaped brace",
            messageFormat: "Unescaped '}}'; use '}}}}' for a literal closing brace",
            category: "LibPrompts", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Global MSBuild settings (root namespace + project dir for folder->namespace).
            IncrementalValueProvider<Settings> settings = context.AnalyzerConfigOptionsProvider
                .Select(static (provider, _) =>
                {
                    var g = provider.GlobalOptions;
                    string root =
                        TryGet(g, "build_property.PromptRootNamespace")
                        ?? TryGet(g, "build_property.RootNamespace")
                        ?? "Prompts";
                    string projectDir = TryGet(g, "build_property.ProjectDir") ?? "";
                    return new Settings(root, projectDir);
                });

            // Every *.prompt additional file.
            IncrementalValuesProvider<AdditionalText> promptFiles = context.AdditionalTextsProvider
                .Where(static f => f.Path.EndsWith(PromptExtension, StringComparison.OrdinalIgnoreCase));

            // Parse each file (compile time) into an equatable model. The model holds
            // ONLY equatable data so the pipeline caches per-file across edits.
            IncrementalValuesProvider<PromptModel> models = promptFiles
                .Combine(settings)
                .Select(static (pair, ct) => BuildModel(pair.Left, pair.Right, ct));

            context.RegisterSourceOutput(models, static (spc, model) => Produce(spc, model));
        }

        // ---------------------------------------------------------------------
        // Model building
        // ---------------------------------------------------------------------
        private static PromptModel BuildModel(AdditionalText file, Settings settings, System.Threading.CancellationToken ct)
        {
            SourceText? source = file.GetText(ct);
            string text = source?.ToString() ?? string.Empty;

            var parts = new List<PromptPart>();
            var @params = new List<string>();
            var diagnostics = new List<DiagnosticInfo>();
            Parse(text, source, file.Path, parts, @params, diagnostics, ct);

            string ns = ComputeNamespace(settings, file.Path);
            string className = ToIdentifier(System.IO.Path.GetFileNameWithoutExtension(file.Path));
            string hash = ComputeHash(text);

            return new PromptModel(
                Namespace: ns,
                ClassName: className,
                FilePath: file.Path,
                Template: text,
                SourceHash: hash,
                Parts: new EquatableArray<PromptPart>(parts.ToArray()),
                Parameters: new EquatableArray<string>(@params.ToArray()),
                Diagnostics: new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
        }

        // ---------------------------------------------------------------------
        // Parser  (single pass; {{ }} escaping; {name} holes)
        // ---------------------------------------------------------------------
        private static void Parse(
            string text, SourceText? source, string filePath,
            List<PromptPart> parts, List<string> @params, List<DiagnosticInfo> diagnostics,
            System.Threading.CancellationToken ct)
        {
            var literal = new StringBuilder();
            int i = 0, n = text.Length;

            void FlushLiteral()
            {
                if (literal.Length > 0)
                {
                    parts.Add(PromptPart.Literal(literal.ToString()));
                    literal.Clear();
                }
            }

            while (i < n)
            {
                ct.ThrowIfCancellationRequested();
                char c = text[i];

                if (c == '{')
                {
                    if (i + 1 < n && text[i + 1] == '{') { literal.Append('{'); i += 2; continue; }

                    int start = i;
                    int j = i + 1;
                    while (j < n && text[j] != '}') j++;

                    if (j >= n)
                    {
                        diagnostics.Add(MakeDiag(UnterminatedPlaceholder, source, filePath, start, n - start));
                        break;
                    }

                    string name = text.Substring(i + 1, j - (i + 1));
                    if (name.Length == 0)
                    {
                        diagnostics.Add(MakeDiag(EmptyPlaceholder, source, filePath, start, j - start + 1));
                    }
                    else if (!SyntaxFacts.IsValidIdentifier(name))
                    {
                        diagnostics.Add(MakeDiag(InvalidPlaceholderName, source, filePath, start, j - start + 1, name));
                    }
                    else
                    {
                        FlushLiteral();
                        parts.Add(PromptPart.Hole(name));
                        if (!@params.Contains(name)) @params.Add(name);
                    }
                    i = j + 1;
                }
                else if (c == '}')
                {
                    if (i + 1 < n && text[i + 1] == '}') { literal.Append('}'); i += 2; continue; }
                    diagnostics.Add(MakeDiag(UnescapedBrace, source, filePath, i, 1));
                    i++;
                }
                else
                {
                    literal.Append(c);
                    i++;
                }
            }

            FlushLiteral();
        }

        // ---------------------------------------------------------------------
        // Emitter
        // ---------------------------------------------------------------------
        private static void Produce(SourceProductionContext spc, PromptModel m)
        {
            // Malformed template: report and emit nothing (avoid cascading errors).
            if (m.Diagnostics.Count > 0)
            {
                foreach (var d in m.Diagnostics) spc.ReportDiagnostic(d.ToDiagnostic());
                return;
            }

            var sb = new StringBuilder(1024);
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.Append("namespace ").AppendLine(m.Namespace);
            sb.AppendLine("{");
            sb.Append("    /// <summary>Generated from <c>").Append(EscapeXml(System.IO.Path.GetFileName(m.FilePath))).AppendLine("</c>.</summary>");
            sb.Append("    public static partial class ").AppendLine(m.ClassName);
            sb.AppendLine("    {");

            // Provenance: content hash of the template, pinned into the assembly.
            sb.Append("        /// <summary>SHA-256 (hex) of the template content at build time.</summary>");
            sb.AppendLine();
            sb.Append("        public const string SourceHash = ").Append(Literal(m.SourceHash)).AppendLine(";");
            sb.AppendLine();

            // Raw template text (handy for logging / re-rendering through another engine).
            sb.Append("        /// <summary>The raw template text.</summary>");
            sb.AppendLine();
            sb.Append("        public const string Template = ").Append(Literal(m.Template)).AppendLine(";");
            sb.AppendLine();

            if (m.Parameters.Count == 0)
            {
                string full = string.Concat(m.Parts.Where(p => !p.IsHole).Select(p => p.Text));
                sb.Append("        /// <summary>The fully-rendered template (no placeholders).</summary>");
                sb.AppendLine();
                sb.Append("        public const string Text = ").Append(Literal(full)).AppendLine(";");
                sb.AppendLine("        /// <summary>Renders the template.</summary>");
                sb.AppendLine("        public static string Render() => Text;");
            }
            else
            {
                string sig = string.Join(", ", m.Parameters.Select(p => "string? " + Escaped(p)));
                sb.Append("        /// <summary>Renders the template. Null arguments render as the empty string.</summary>");
                sb.AppendLine();
                sb.Append("        public static string Render(").Append(sig).AppendLine(")");

                if (m.Parts.Count == 1 && m.Parts[0].IsHole)
                {
                    // Single hole, e.g. "{body}". Null -> empty, matching interpolation semantics.
                    sb.Append("            => ").Append(Escaped(m.Parts[0].Text)).AppendLine(" ?? string.Empty;");
                }
                else
                {
                    string args = string.Join(", ", m.Parts.Select(p => p.IsHole ? Escaped(p.Text) : Literal(p.Text)));
                    sb.Append("            => string.Concat(").Append(args).AppendLine(");");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            string hint = MakeHintName(m);
            spc.AddSource(hint, SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------
        private static string? TryGet(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions options, string key)
            => options.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;

        private static string ComputeNamespace(Settings settings, string filePath)
        {
            string root = string.IsNullOrEmpty(settings.RootNamespace) ? "Prompts" : settings.RootNamespace;

            if (string.IsNullOrEmpty(settings.ProjectDir))
                return root;

            string projectDir = settings.ProjectDir;
            if (projectDir.Length > 0 && projectDir[projectDir.Length - 1] != System.IO.Path.DirectorySeparatorChar
                && projectDir[projectDir.Length - 1] != System.IO.Path.AltDirectorySeparatorChar)
            {
                projectDir += System.IO.Path.DirectorySeparatorChar;
            }

            string relative = filePath;
            if (filePath.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase))
                relative = filePath.Substring(projectDir.Length);

            string? folder = System.IO.Path.GetDirectoryName(relative);
            if (string.IsNullOrEmpty(folder)) return root;

            var segments = folder!
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ToIdentifier)
                .Where(s => s.Length > 0);

            string suffix = string.Join(".", segments);
            return suffix.Length == 0 ? root : root + "." + suffix;
        }

        private static string MakeHintName(PromptModel m)
        {
            // Unique, stable file name. Path hash guards against same class name in
            // different folders colliding on the generated file name.
            string pathHash = ComputeHash(m.FilePath).Substring(0, 8);
            return (m.Namespace + "." + m.ClassName + "." + pathHash + ".g.cs").Replace("..", ".");
        }

        private static string ToIdentifier(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "_";
            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw)
                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');

            string s = sb.ToString();
            if (char.IsDigit(s[0])) s = "_" + s;
            // PascalCase the first character for a class-name-like feel.
            if (char.IsLetter(s[0]) && char.IsLower(s[0])) s = char.ToUpperInvariant(s[0]) + s.Substring(1);
            return s;
        }

        private static string Escaped(string identifier)
            => SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
               || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
               ? "@" + identifier
               : identifier;

        private static string Literal(string value)
            => SymbolDisplay.FormatLiteral(value, quote: true);

        private static string EscapeXml(string value)
            => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string ComputeHash(string text)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static DiagnosticInfo MakeDiag(
            DiagnosticDescriptor descriptor, SourceText? source, string filePath,
            int start, int length, string? arg = null)
        {
            LinePositionSpan lineSpan = default;
            if (source != null)
            {
                int safeStart = Math.Min(start, source.Length);
                int safeEnd = Math.Min(start + length, source.Length);
                lineSpan = source.Lines.GetLinePositionSpan(TextSpan.FromBounds(safeStart, safeEnd));
            }
            return new DiagnosticInfo(descriptor, filePath, new TextSpan(start, length), lineSpan, arg);
        }
    }

    // -------------------------------------------------------------------------
    // Equatable model types (must stay value-equatable for incremental caching)
    // -------------------------------------------------------------------------
    internal readonly record struct PromptPart(bool IsHole, string Text)
    {
        public static PromptPart Literal(string text) => new(false, text);
        public static PromptPart Hole(string name) => new(true, name);
    }

    internal readonly record struct Settings(string RootNamespace, string ProjectDir);

    internal sealed record PromptModel(
        string Namespace,
        string ClassName,
        string FilePath,
        string Template,
        string SourceHash,
        EquatableArray<PromptPart> Parts,
        EquatableArray<string> Parameters,
        EquatableArray<DiagnosticInfo> Diagnostics);

    internal readonly record struct DiagnosticInfo(
        DiagnosticDescriptor Descriptor, string FilePath, TextSpan Span, LinePositionSpan LineSpan, string? Arg)
    {
        public Diagnostic ToDiagnostic()
        {
            Location location = Location.Create(FilePath, Span, LineSpan);
            return Arg is null
                ? Diagnostic.Create(Descriptor, location)
                : Diagnostic.Create(Descriptor, location, Arg);
        }

        // DiagnosticDescriptor compares by reference; we key on its Id for value equality.
        public bool Equals(DiagnosticInfo other)
            => Descriptor.Id == other.Descriptor.Id
               && FilePath == other.FilePath
               && Span.Equals(other.Span)
               && Arg == other.Arg;

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (Descriptor?.Id?.GetHashCode() ?? 0);
                h = h * 31 + (FilePath?.GetHashCode() ?? 0);
                h = h * 31 + Span.GetHashCode();
                h = h * 31 + (Arg?.GetHashCode() ?? 0);
                return h;
            }
        }
    }
}

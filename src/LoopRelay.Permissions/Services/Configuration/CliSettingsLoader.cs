using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Models.Shell;
using LoopRelay.Permissions.Services.Evaluation;

namespace LoopRelay.Permissions.Services.Configuration;

public static class CliSettingsLoader
{
    public const string SettingsPathEnvironmentVariable = "LOOPRELAY_SETTINGS_PATH";
    public const string ConsumerSettingsFileName = "settings.json";
    public const string DefaultSettingsFileName = "settings.default.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static PermissionPolicyOptions LoadPermissionPolicy() => Load().Permissions;

    public static CliSettingsLoadResult Load(
        string? baseDirectory = null,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        string? overridePath = getEnvironmentVariable(SettingsPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return LoadFromFile(overridePath, isDefaultTemplate: false);
        }

        string resolvedBaseDirectory = Path.GetFullPath(baseDirectory ?? AppContext.BaseDirectory);
        string settingsPath = Path.Combine(resolvedBaseDirectory, ConsumerSettingsFileName);
        if (File.Exists(settingsPath))
        {
            return LoadFromFile(settingsPath, isDefaultTemplate: false);
        }

        string defaultSettingsPath = Path.Combine(resolvedBaseDirectory, DefaultSettingsFileName);
        if (File.Exists(defaultSettingsPath))
        {
            return LoadFromFile(defaultSettingsPath, isDefaultTemplate: true);
        }

        throw new CliSettingsException(
            $"Missing settings file. Expected '{settingsPath}' or development fallback '{defaultSettingsPath}'.");
    }

    public static CliSettingsLoadResult LoadFromFile(string path, bool isDefaultTemplate = false)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new CliSettingsException($"Settings file '{fullPath}' was not found.");
        }

        try
        {
            using FileStream stream = File.OpenRead(fullPath);
            SettingsDocument? document = JsonSerializer.Deserialize<SettingsDocument>(stream, JsonOptions);
            if (document?.Permissions is null)
            {
                throw new PermissionPolicyValidationException("permissions section is required.");
            }

            PermissionPolicyOptions policy = PermissionPolicyDocumentMapper.ToPolicy(document.Permissions);
            PermissionPolicyOptions merged = PermissionPolicyFactory.MergeWithMinimum(policy);
            CliPolicyDocument policyDocument = new(
                document.Policy?.Execution?.MaxUnboundedContinuationSteps,
                document.Policy?.Execution?.MaxNoChangesCommits,
                document.Policy?.Execution?.OperationalContextGrowthWarningStreak,
                document.Policy?.Decisions?.SessionResume);
            return new CliSettingsLoadResult(merged, policyDocument, fullPath, isDefaultTemplate);
        }
        catch (JsonException ex)
        {
            throw new CliSettingsException($"Invalid settings file '{fullPath}': {ex.Message}", ex);
        }
        catch (PermissionPolicyValidationException ex)
        {
            throw new CliSettingsException($"Invalid settings file '{fullPath}': {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new CliSettingsException($"Could not read settings file '{fullPath}': {ex.Message}", ex);
        }
    }

    // Policy-owned sections reject unknown members: a typoed or unsupported key is a configured
    // value with no production effect, which the policy authority rejects instead of ignoring.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class SettingsDocument
    {
        public PermissionPolicyDocument? Permissions { get; set; }

        public PolicyDocument? Policy { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class PolicyDocument
    {
        public PolicyExecutionDocument? Execution { get; set; }

        public PolicyDecisionsDocument? Decisions { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class PolicyExecutionDocument
    {
        public int? MaxUnboundedContinuationSteps { get; set; }

        public int? MaxNoChangesCommits { get; set; }

        public int? OperationalContextGrowthWarningStreak { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class PolicyDecisionsDocument
    {
        public bool? SessionResume { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class PermissionPolicyDocument
    {
        public string? FingerprintVersion { get; set; }

        public string[]? CommandsWithSubcommands { get; set; }

        public string[]? SafeTools { get; set; }

        public string[]? SafeBashCommands { get; set; }

        public PermissionHardDenyDocument? HardDeny { get; set; }

        public PermissionReviewRequiredDocument? ReviewRequired { get; set; }

        public PermissionAllowDocument? Allow { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class PermissionHardDenyDocument
    {
        public string[]? PrivilegeEscalationCommands { get; set; }

        public RecursiveForceDeleteDocument? RecursiveForceDelete { get; set; }

        public string[]? SystemControlCommands { get; set; }

        public string[]? NetworkFetchCommands { get; set; }

        public string[]? GitForcePushFlags { get; set; }

        public IndirectShellExecutionDocument? IndirectShellExecution { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class RecursiveForceDeleteDocument
    {
        public string? Command { get; set; }

        public string[][]? FlagSets { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class IndirectShellExecutionDocument
    {
        public string[]? Commands { get; set; }

        public string? Flag { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class PermissionReviewRequiredDocument
    {
        public bool? GitCommit { get; set; }

        public string[]? GitCommitAmendFlags { get; set; }

        public bool? GitPush { get; set; }

        public string[]? InstallCommands { get; set; }

        public string? InstallSubcommand { get; set; }

        public string[]? InfrastructureCommands { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class PermissionAllowDocument
    {
        public string[]? AlwaysAllowedCommands { get; set; }

        public string[]? GitReadOnlySubcommands { get; set; }

        public string[]? GitLogAllowedUnlessFlags { get; set; }

        public string[]? DotnetAllowedSubcommands { get; set; }

        public Dictionary<string, string[]?>? PackageManagerAllowedSubcommands { get; set; }

        public Dictionary<string, string[]?>? TestCommands { get; set; }
    }

    private static class PermissionPolicyDocumentMapper
    {
        public static PermissionPolicyOptions ToPolicy(PermissionPolicyDocument document)
        {
            PermissionHardDenyDocument hardDeny = RequireSection(document.HardDeny, "permissions.hardDeny");
            PermissionReviewRequiredDocument reviewRequired = RequireSection(
                document.ReviewRequired,
                "permissions.reviewRequired");
            PermissionAllowDocument allow = RequireSection(document.Allow, "permissions.allow");

            return new PermissionPolicyOptions(
                RequiredScalar("permissions.fingerprintVersion", document.FingerprintVersion),
                RequiredSet("permissions.commandsWithSubcommands", document.CommandsWithSubcommands),
                RequiredSet("permissions.safeTools", document.SafeTools),
                RequiredSet("permissions.safeBashCommands", document.SafeBashCommands),
                new PermissionHardDenyOptions(
                    RequiredSet(
                        "permissions.hardDeny.privilegeEscalationCommands",
                        hardDeny.PrivilegeEscalationCommands),
                    MapRecursiveForceDelete(
                        RequireSection(
                            hardDeny.RecursiveForceDelete,
                            "permissions.hardDeny.recursiveForceDelete")),
                    RequiredSet("permissions.hardDeny.systemControlCommands", hardDeny.SystemControlCommands),
                    RequiredSet("permissions.hardDeny.networkFetchCommands", hardDeny.NetworkFetchCommands),
                    RequiredSet("permissions.hardDeny.gitForcePushFlags", hardDeny.GitForcePushFlags),
                    MapIndirectShellExecution(
                        RequireSection(
                            hardDeny.IndirectShellExecution,
                            "permissions.hardDeny.indirectShellExecution"))),
                new PermissionReviewRequiredOptions(
                    RequiredBool("permissions.reviewRequired.gitCommit", reviewRequired.GitCommit),
                    RequiredSet("permissions.reviewRequired.gitCommitAmendFlags", reviewRequired.GitCommitAmendFlags),
                    RequiredBool("permissions.reviewRequired.gitPush", reviewRequired.GitPush),
                    RequiredSet("permissions.reviewRequired.installCommands", reviewRequired.InstallCommands),
                    RequiredScalar("permissions.reviewRequired.installSubcommand", reviewRequired.InstallSubcommand),
                    RequiredSet(
                        "permissions.reviewRequired.infrastructureCommands",
                        reviewRequired.InfrastructureCommands)),
                new PermissionAllowOptions(
                    RequiredSet("permissions.allow.alwaysAllowedCommands", allow.AlwaysAllowedCommands),
                    RequiredSet("permissions.allow.gitReadOnlySubcommands", allow.GitReadOnlySubcommands),
                    RequiredSet("permissions.allow.gitLogAllowedUnlessFlags", allow.GitLogAllowedUnlessFlags),
                    RequiredSet("permissions.allow.dotnetAllowedSubcommands", allow.DotnetAllowedSubcommands),
                    RequiredMap(
                        "permissions.allow.packageManagerAllowedSubcommands",
                        allow.PackageManagerAllowedSubcommands,
                        allowEmptyValues: false),
                    RequiredMap(
                        "permissions.allow.testCommands",
                        allow.TestCommands,
                        allowEmptyValues: true)));
        }

        private static RecursiveForceDeleteOptions MapRecursiveForceDelete(RecursiveForceDeleteDocument document)
        {
            if (document.FlagSets is null)
            {
                throw new PermissionPolicyValidationException(
                    "permissions.hardDeny.recursiveForceDelete.flagSets section is required.");
            }

            if (document.FlagSets.Length == 0)
            {
                throw new PermissionPolicyValidationException(
                    "permissions.hardDeny.recursiveForceDelete.flagSets must contain at least one flag set.");
            }

            var flagSets = new List<IReadOnlySet<string>>();
            for (int i = 0; i < document.FlagSets.Length; i++)
            {
                string path = $"permissions.hardDeny.recursiveForceDelete.flagSets[{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
                string[]? flagSet = document.FlagSets[i];
                if (flagSet is null || flagSet.Length == 0)
                {
                    throw new PermissionPolicyValidationException($"{path} must contain at least one flag.");
                }

                IReadOnlySet<string> normalized = RequiredSet(path, flagSet);
                if (flagSets.Any(existing => SetEquals(existing, normalized)))
                {
                    throw new PermissionPolicyValidationException($"{path} duplicates an earlier flag set.");
                }

                flagSets.Add(normalized);
            }

            return new RecursiveForceDeleteOptions(
                RequiredScalar("permissions.hardDeny.recursiveForceDelete.command", document.Command),
                flagSets.ToArray());
        }

        private static IndirectShellExecutionOptions MapIndirectShellExecution(
            IndirectShellExecutionDocument document) =>
            new(
                RequiredSet("permissions.hardDeny.indirectShellExecution.commands", document.Commands),
                RequiredScalar("permissions.hardDeny.indirectShellExecution.flag", document.Flag));

        private static IReadOnlyDictionary<string, IReadOnlySet<string>> RequiredMap(
            string path,
            Dictionary<string, string[]?>? values,
            bool allowEmptyValues)
        {
            if (values is null)
            {
                throw new PermissionPolicyValidationException($"{path} section is required.");
            }

            var dictionary = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, string[]? rawValues) in values)
            {
                string normalizedKey = RequiredScalar($"{path} key", key);
                if (dictionary.ContainsKey(normalizedKey))
                {
                    throw new PermissionPolicyValidationException(
                        $"{path} contains duplicate key '{normalizedKey}'.");
                }

                if (rawValues is null)
                {
                    throw new PermissionPolicyValidationException(
                        $"{path}.{normalizedKey} must be an array.");
                }

                if (!allowEmptyValues && rawValues.Length == 0)
                {
                    throw new PermissionPolicyValidationException(
                        $"{path}.{normalizedKey} must contain at least one value.");
                }

                dictionary.Add(normalizedKey, RequiredSet($"{path}.{normalizedKey}", rawValues));
            }

            return dictionary.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlySet<string> RequiredSet(string path, string[]? values)
        {
            if (values is null)
            {
                throw new PermissionPolicyValidationException($"{path} section is required.");
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < values.Length; i++)
            {
                string value = RequiredScalar(
                    $"{path}[{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}]",
                    values[i]);
                if (!set.Add(value))
                {
                    throw new PermissionPolicyValidationException(
                        $"{path} contains duplicate value '{value}'.");
                }
            }

            return set.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        }

        private static T RequireSection<T>(T? section, string path)
            where T : class
        {
            if (section is null)
            {
                throw new PermissionPolicyValidationException($"{path} section is required.");
            }

            return section;
        }

        private static bool RequiredBool(string path, bool? value)
        {
            if (value is null)
            {
                throw new PermissionPolicyValidationException($"{path} is required.");
            }

            return value.Value;
        }

        private static string RequiredScalar(string path, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new PermissionPolicyValidationException($"{path} must not be empty.");
            }

            return value.Trim();
        }

        private static bool SetEquals(IReadOnlySet<string> left, IReadOnlySet<string> right) =>
            left.Count == right.Count && left.All(right.Contains);
    }

}

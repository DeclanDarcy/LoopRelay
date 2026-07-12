using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Models.Shell;

namespace LoopRelay.Permissions.Services.Configuration;

public static class CliSettingsLoader
{
    public const string SettingsPathEnvironmentVariable = "LOOPRELAY_SETTINGS_PATH";
    public const string ConsumerSettingsFileName = "settings.json";
    public const string DefaultSettingsFileName = "settings.default.json";
    public const string CurrentSchemaVersion = "settings-v3";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static PermissionPolicyOptions LoadPermissionInputs() => Load().PermissionInputs;

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

            var warnings = new List<ConfigurationCompatibilityWarning>();
            bool canonicalLayout = document.SchemaVersion is not null;
            if (canonicalLayout && !string.Equals(document.SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"schemaVersion must be '{CurrentSchemaVersion}'.",
                    nameof(document.SchemaVersion));
            }

            if (canonicalLayout && document.HasLegacyRuntimeFields)
            {
                throw new ArgumentException(
                    "Canonical settings cannot mix runtime fields with the legacy brainModel/brainEffort/continuity layout.");
            }

            if (!canonicalLayout && document.Runtime is not null)
            {
                throw new ArgumentException(
                    $"runtime requires schemaVersion '{CurrentSchemaVersion}'.",
                    nameof(document.Runtime));
            }

            if (document.Policy?.Decisions?.SessionResume is not null && document.Continuity?.DecisionResume is not null)
            {
                throw new ArgumentException("Decision resume was configured in both policy and legacy continuity sections.");
            }

            if (document.Policy?.Recovery?.Strategy is not null && document.Continuity?.RecoveryPolicy is not null)
            {
                throw new ArgumentException("Recovery strategy was configured in both policy and legacy continuity sections.");
            }

            ConfiguredRuntimeFacts runtime = canonicalLayout
                ? ToRuntimeFacts(document.Runtime)
                : TranslateLegacyRuntimeFacts(document, warnings);
            PermissionPolicyOptions permissionInputs =
                PermissionPolicyDocumentMapper.ToPolicy(document.Permissions);
            CliPolicyDocument policyInputs = new(
                document.Policy?.Execution?.MaxUnboundedContinuationSteps,
                document.Policy?.Execution?.MaxNoChangesCommits,
                document.Policy?.Execution?.OperationalContextGrowthWarningStreak,
                document.Policy?.Decisions?.SessionResume ?? document.Continuity?.DecisionResume,
                NormalizeOptional(document.Policy?.Recovery?.Strategy ?? document.Continuity?.RecoveryPolicy),
                document.ArtifactPolicy is null
                    ? null
                    : new LegacyArtifactPolicyInputs(
                        document.ArtifactPolicy.AllowHitlRequestedNonImplementationFiles,
                        document.ArtifactPolicy.AllowAuxiliaryNonImplementationFiles));
            if (document.ArtifactPolicy is not null)
            {
                warnings.Add(new ConfigurationCompatibilityWarning(
                    "legacy-artifact-policy",
                    "artifactPolicy was preserved as a compatibility policy input; Policy Authority must translate or reject it."));
            }

            return new CliSettingsLoadResult(
                runtime,
                permissionInputs,
                policyInputs,
                warnings,
                new ConfigurationSourceProvenance(
                    fullPath,
                    isDefaultTemplate,
                    canonicalLayout ? CurrentSchemaVersion : "legacy-unversioned"));
        }
        catch (JsonException ex)
        {
            throw new CliSettingsException($"Invalid settings file '{fullPath}': {ex.Message}", ex);
        }
        catch (PermissionPolicyValidationException ex)
        {
            throw new CliSettingsException($"Invalid settings file '{fullPath}': {ex.Message}", ex);
        }
        catch (ArgumentException ex)
        {
            throw new CliSettingsException($"Invalid settings file '{fullPath}': {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new CliSettingsException($"Could not read settings file '{fullPath}': {ex.Message}", ex);
        }
    }

    private static ConfiguredRuntimeFacts ToRuntimeFacts(RuntimeDocument? document) =>
        new(
            new ConfiguredBrainFacts(
                ParseOptionalModel("runtime.brain.model", document?.Brain?.Model),
                ParseOptionalEffort("runtime.brain.effort", document?.Brain?.Effort)),
            NormalizeDistinctStrings(
                "runtime.providers.supportedCodexProfiles",
                document?.Providers?.SupportedCodexProfiles));

    private static ConfiguredRuntimeFacts TranslateLegacyRuntimeFacts(
        SettingsDocument document,
        List<ConfigurationCompatibilityWarning> warnings)
    {
        warnings.Add(new ConfigurationCompatibilityWarning(
            "legacy-settings-layout",
            $"Legacy unversioned settings were translated to {CurrentSchemaVersion} configuration and policy inputs."));

        return new ConfiguredRuntimeFacts(
            new ConfiguredBrainFacts(
                ParseOptionalModel("brainModel", document.BrainModel),
                ParseOptionalEffort("brainEffort", document.BrainEffort)),
            NormalizeDistinctStrings(
                "continuity.supportedCodexProfiles",
                document.Continuity?.SupportedCodexProfiles));
    }

    private static AgentModel? ParseOptionalModel(string path, string? value) =>
        value is null
            ? null
            : AgentConfigurationCatalog.ParseModel(RequiredScalar(path, value), path);

    private static AgentEffort? ParseOptionalEffort(string path, string? value) =>
        value is null
            ? null
            : AgentConfigurationCatalog.ParseEffort(RequiredScalar(path, value), path);

    private static string? NormalizeOptional(string? value) =>
        value is null ? null : RequiredScalar("policy.recovery.strategy", value);

    private static IReadOnlyList<string> NormalizeDistinctStrings(string path, string[]? values)
    {
        if (values is null)
        {
            return [];
        }

        var distinct = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<string>(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            string value = RequiredScalar($"{path}[{index}]", values[index]);
            if (!distinct.Add(value))
            {
                throw new ArgumentException($"{path} contains duplicate value '{value}'.", path);
            }

            normalized.Add(value);
        }

        return normalized;
    }

    private static string RequiredScalar(string path, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{path} must not be empty.", path);
        }

        return value.Trim();
    }

    // Configuration parsing is strict: a typo is neither configured evidence nor a policy input.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class SettingsDocument
    {
        public string? SchemaVersion { get; set; }

        public RuntimeDocument? Runtime { get; set; }

        public PolicyDocument? Policy { get; set; }

        public PermissionPolicyDocument? Permissions { get; set; }

        // Explicit compatibility inputs for the two pre-authority settings layouts.
        public string? BrainModel { get; set; }

        public string? BrainEffort { get; set; }

        public LegacyContinuityDocument? Continuity { get; set; }

        public LegacyArtifactPolicyDocument? ArtifactPolicy { get; set; }

        public bool HasLegacyRuntimeFields =>
            BrainModel is not null || BrainEffort is not null || Continuity is not null || ArtifactPolicy is not null;
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class RuntimeDocument
    {
        public RuntimeBrainDocument? Brain { get; set; }

        public RuntimeProvidersDocument? Providers { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class RuntimeBrainDocument
    {
        public string? Model { get; set; }

        public string? Effort { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class RuntimeProvidersDocument
    {
        public string[]? SupportedCodexProfiles { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class PolicyDocument
    {
        public PolicyExecutionDocument? Execution { get; set; }

        public PolicyDecisionsDocument? Decisions { get; set; }

        public PolicyRecoveryDocument? Recovery { get; set; }
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
    private sealed class PolicyRecoveryDocument
    {
        public string? Strategy { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class LegacyContinuityDocument
    {
        public bool? DecisionResume { get; set; }

        public string? RecoveryPolicy { get; set; }

        public string[]? SupportedCodexProfiles { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class LegacyArtifactPolicyDocument
    {
        public bool? AllowHitlRequestedNonImplementationFiles { get; set; }

        public bool? AllowAuxiliaryNonImplementationFiles { get; set; }
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

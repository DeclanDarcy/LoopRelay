using System.Text.Json;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex.Compatibility;

namespace LoopRelay.Agents.Tests.Services.Codex;

public sealed class CodexSessionContinuityProfileResolverTests
{
    [Fact]
    public void ExactVersionAndSchemaPromoteCertifiedSupport()
    {
        CodexSessionContinuityProfileResolver resolver = Resolver();

        SessionContinuityNegotiationResult result = resolver.Resolve(Request(
            "0.142.5", "sha256:fixture", """{"capabilities":{}}"""));

        Assert.True(result.FromCertifiedManifest);
        Assert.Equal(SessionOperationSupport.Supported,
            result.Profile.Operation(SessionContinuityOperation.Resume).Status);
        Assert.Equal(SessionOperationSupport.Supported,
            result.Profile.Parameter(SessionContinuityOperation.Resume, SessionContinuityProfile.ExcludeTurnsParameter).Status);
    }

    [Theory]
    [InlineData("0.142.6", "sha256:fixture")]
    [InlineData("0.142.5", "sha256:different")]
    public void VersionOrSchemaAloneNeverPromotesSupport(string version, string schema)
    {
        SessionContinuityProfile profile = Resolver().Resolve(Request(version, schema, """{"capabilities":{}}""")).Profile;

        Assert.Equal(SessionOperationSupport.Unknown,
            profile.Operation(SessionContinuityOperation.Resume).Status);
    }

    [Theory]
    [InlineData("other-provider", "app-server-v2")]
    [InlineData("codex", "other-protocol")]
    public void ExactFixtureCannotAuthorizeAnotherProviderOrProtocol(string provider, string protocol)
    {
        SessionContinuityNegotiationResult result = Resolver().Resolve(Request(
            "0.142.5",
            "sha256:fixture",
            """{"capabilities":{}}""",
            provider,
            protocol));

        Assert.False(result.FromCertifiedManifest);
        Assert.Equal(SessionOperationSupport.Unknown,
            result.Profile.Operation(SessionContinuityOperation.Resume).Status);
        Assert.Contains("Provider/protocol identity", result.Evidence, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, """{"capabilities":{}}""")]
    [InlineData(true, """{"capabilities":{"experimentalApi":false}}""")]
    public void CertifiedResumeIsNotBroadenedBeyondTheFixtureExperimentalShape(
        bool offerExperimentalApi,
        string initializeJson)
    {
        SessionContinuityProfile profile = Resolver().Resolve(Request(
            "0.142.5",
            "sha256:fixture",
            initializeJson,
            offerExperimentalApi: offerExperimentalApi)).Profile;

        Assert.Equal(SessionOperationSupport.Unknown,
            profile.Operation(SessionContinuityOperation.Resume).Status);
        Assert.Equal(SessionOperationSupport.Unsupported,
            profile.Parameter(
                SessionContinuityOperation.Resume,
                SessionContinuityProfile.ExcludeTurnsParameter).Status);
    }

    [Fact]
    public void ManifestRejectsDuplicateCertifiedVersionAndSchemaIdentity()
    {
        CodexCompatibilityManifestEntry first = Entry("one", "fixture-one");
        CodexCompatibilityManifestEntry second = Entry("two", "fixture-two");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new CodexCompatibilityManifest([first, second]));

        Assert.Contains("duplicate version/schema identity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsNonPositiveMaximumRecoverableContext()
    {
        CodexCompatibilityManifestEntry invalid = Entry("one", "fixture-one") with
        {
            MaximumRecoverableContext = 0,
        };

        Assert.Throws<InvalidOperationException>(() => new CodexCompatibilityManifest([invalid]));
    }

    [Fact]
    public void ExplicitServerUnsupportedNarrowsCertifiedEvidence()
    {
        SessionContinuityProfile profile = Resolver().Resolve(Request(
            "0.142.5", "sha256:fixture", """{"capabilities":{"threadResume":false}}""")).Profile;

        Assert.Equal(SessionOperationSupport.Unsupported,
            profile.Operation(SessionContinuityOperation.Resume).Status);
    }

    [Fact]
    public void StructuredParameterRejectionNarrowsOnlyThatParameter()
    {
        CodexSessionContinuityProfileResolver resolver = Resolver();
        SessionContinuityProfile original = resolver.Resolve(Request(
            "0.142.5", "sha256:fixture", """{"capabilities":{}}""")).Profile;

        SessionContinuityProfile narrowed = resolver.NarrowAfterStructuredRejection(
            original, SessionContinuityOperation.Resume, SessionContinuityProfile.ExcludeTurnsParameter,
            -32602, "invalid params");

        Assert.Equal(SessionOperationSupport.Supported, narrowed.Operation(SessionContinuityOperation.Resume).Status);
        Assert.Equal(SessionOperationSupport.Unsupported,
            narrowed.Parameter(SessionContinuityOperation.Resume, SessionContinuityProfile.ExcludeTurnsParameter).Status);
        Assert.NotEqual(original.Digest, narrowed.Digest);
    }

    [Fact]
    public void ProfileDigestIsStableAcrossDictionaryEnumerationAndNegotiationTime()
    {
        SessionContinuityProfile first = Resolver().Resolve(Request(
            "0.142.5", "sha256:fixture", """{"capabilities":{"z":true,"a":false}}""")).Profile;
        SessionContinuityProfile second = Resolver().Resolve(Request(
            "0.142.5", "sha256:fixture", """{"capabilities":{"a":false,"z":true}}""")).Profile;

        Assert.Equal(first.Digest, second.Digest);
    }

    private static CodexSessionContinuityProfileResolver Resolver() =>
        new(new CodexCompatibilityManifest(
        [
            new CodexCompatibilityManifestEntry(
                "codex-0.142.5-fixture", "0.142.5", "sha256:fixture", "fixture-1",
                SessionOperationSupport.Supported,
                SessionOperationSupport.Supported,
                SessionOperationSupport.Unknown,
                SessionOperationSupport.Supported,
                SessionOperationSupport.Supported,
                256_000,
                "evidence-digest"),
        ]));

    private static CodexCompatibilityManifestEntry Entry(string id, string fixture) =>
        new(
            id,
            "0.142.5",
            "sha256:fixture",
            fixture,
            SessionOperationSupport.Supported,
            SessionOperationSupport.Supported,
            SessionOperationSupport.Unknown,
            SessionOperationSupport.Supported,
            SessionOperationSupport.Unknown,
            null,
            "evidence-digest");

    private static SessionContinuityNegotiationRequest Request(
        string version,
        string schema,
        string json,
        string provider = "codex",
        string protocol = "app-server-v2",
        bool offerExperimentalApi = true)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new SessionContinuityNegotiationRequest(
            provider, "LoopRelay/0.1", version, "codex", protocol, schema,
            document.RootElement.Clone(), offerExperimentalApi);
    }
}

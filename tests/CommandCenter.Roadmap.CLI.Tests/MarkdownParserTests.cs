using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class MarkdownParserTests
{
    [Fact]
    public void Selection_parser_parses_valid_selection()
    {
        string markdown = """
        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select Existing Epic |
        | Recommended Initiative | Epic A |
        | Initiative Type | Existing Roadmap Epic |
        | Confidence | High |
        | Primary Reason | Best leverage |

        ## If Existing Roadmap Epic Selected

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-001 |
        | Epic Name | Epic A |
        """;

        SelectionDecision decision = new SelectionParser().Parse(markdown);

        Assert.Equal("Select Existing Epic", decision.RecommendedOutcome);
        Assert.Equal("Epic A", decision.RecommendedInitiative);
        Assert.Equal("EPIC-001", decision.ExistingEpicId);
        Assert.Equal("Epic A", decision.ExistingEpicName);
    }

    [Fact]
    public void Audit_parser_parses_all_dispositions()
    {
        foreach (string disposition in new[] { "Realign", "Reimagine", "Retire", "Insufficient Evidence" })
        {
            string markdown = $$"""
            ## Selected Epic

            | Field | Value |
            |---|---|
            | Epic ID | EPIC-001 |
            | Epic Name | Epic A |

            ## Audit Disposition

            | Field | Value |
            |---|---|
            | Disposition | {{disposition}} |
            | Confidence | Medium |
            | Primary Reason | Test reason |
            | Evidence Strength | Moderate |
            | Recommended Next Step | Gather More Evidence |
            """;

            Assert.Equal(disposition, new EpicPreparationAuditParser().Parse(markdown).Disposition);
        }
    }

    [Fact]
    public void Selection_parser_rejects_existing_epic_without_identity_section()
    {
        string markdown = """
        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select Existing Epic |
        | Recommended Initiative | Epic A |
        | Initiative Type | Existing Roadmap Epic |
        | Confidence | High |
        """;

        Assert.Throws<MarkdownParseException>(() => new SelectionParser().Parse(markdown));
    }

    [Fact]
    public void Completion_parser_parses_closure_recommendation()
    {
        string markdown = """
        ## Evaluation Summary

        | Field | Value |
        |---|---|
        | Overall Completion Status | Fully Complete |
        | Overall Drift Classification | None |
        | Closure Recommendation | Close Epic |
        """;

        CompletionEvaluationDecision decision = new CompletionEvaluationParser().Parse(markdown);

        Assert.Equal("Close Epic", decision.ClosureRecommendation);
    }

    [Fact]
    public void Parser_rejects_unknown_values()
    {
        string markdown = """
        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Maybe |
        | Recommended Initiative | Epic A |
        | Initiative Type | Existing Roadmap Epic |
        | Confidence | High |
        """;

        Assert.Throws<MarkdownParseException>(() => new SelectionParser().Parse(markdown));
    }
}

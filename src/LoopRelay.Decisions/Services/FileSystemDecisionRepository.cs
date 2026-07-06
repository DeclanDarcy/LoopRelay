using System.Text.Json;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Persistence;
using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Services;

public sealed class FileSystemDecisionRepository(IArtifactStore artifactStore) : IDecisionRepository
{
    public async Task<DecisionId> AllocateDecisionIdAsync(Repository repository)
    {
        return new DecisionId(await AllocateIdAsync(repository, DecisionArtifactKind.Decision, "DEC"));
    }

    public async Task<string> AllocateCandidateIdAsync(Repository repository)
    {
        return await AllocateIdAsync(repository, DecisionArtifactKind.Candidate, "CAND");
    }

    public async Task<string> AllocateProposalIdAsync(Repository repository)
    {
        return await AllocateIdAsync(repository, DecisionArtifactKind.Proposal, "PROP");
    }

    public async Task<string> AllocateProposalRevisionIdAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        string revisionsRoot = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.ProposalRevisionsDirectory(id));
        IReadOnlyList<string> files = await artifactStore.ListAsync(revisionsRoot, "REV-*.json");
        int next = files
            .Select(Path.GetFileNameWithoutExtension)
            .Where(revisionId => !string.IsNullOrWhiteSpace(revisionId))
            .Select(revisionId => ParseSequence(revisionId!, "REV"))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"REV-{next:0000}";
    }

    public async Task<string> AllocatePackageVersionIdAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        string versionsRoot = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.ProposalVersionsDirectory(id));
        IReadOnlyList<string> files = await artifactStore.ListAsync(versionsRoot, "PKG-*.json");
        int next = files
            .Select(Path.GetFileNameWithoutExtension)
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
            .Select(packageId => ParseSequence(packageId!, "PKG"))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"PKG-{next:0000}";
    }

    public async Task<string> AllocateRefinementArtifactIdAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        string refinementsRoot = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.ProposalRefinementsDirectory(id));
        IReadOnlyList<string> files = await artifactStore.ListAsync(refinementsRoot, "REF-*.json");
        int next = files
            .Select(Path.GetFileNameWithoutExtension)
            .Where(refinementId => !string.IsNullOrWhiteSpace(refinementId))
            .Select(refinementId => ParseSequence(refinementId!, "REF"))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"REF-{next:0000}";
    }

    public async Task<string> AllocateReviewNoteIdAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        IReadOnlyList<DecisionReviewNote> notes = await ListReviewNotesAsync(repository, id);
        int next = notes
            .Select(note => ParseSequence(note.Id, "NOTE"))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"NOTE-{next:0000}";
    }

    public async Task<IReadOnlyList<Decision>> ListDecisionsAsync(Repository repository)
    {
        IReadOnlyList<string> directories = await ListArtifactDirectoriesAsync(repository, DecisionArtifactKind.Decision);
        var decisions = new List<Decision>();
        foreach (string directory in directories)
        {
            string id = Path.GetFileName(directory);
            Decision? decision = await GetDecisionAsync(repository, new DecisionId(id));
            if (decision is not null)
            {
                decisions.Add(decision);
            }
        }

        return decisions.OrderBy(decision => decision.Id.Value, StringComparer.Ordinal).ToArray();
    }

    public async Task<Decision?> GetDecisionAsync(Repository repository, DecisionId decisionId)
    {
        string id = DecisionArtifactPaths.ValidateId(decisionId.Value, "DEC");
        return await ReadPayloadAsync<Decision>(
            repository,
            DecisionArtifactPaths.DecisionJson(id));
    }

    public async Task<Decision> SaveDecisionAsync(Repository repository, Decision decision)
    {
        string id = DecisionArtifactPaths.ValidateId(decision.Id.Value, "DEC");
        if (decision.Metadata.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision belongs to a different repository.");
        }

        string directory = DecisionArtifactPaths.DecisionDirectory(id);
        await WriteDocumentAsync(repository, DecisionArtifactPaths.DecisionJson(id), decision, decision.Metadata.CreatedAt, decision.Metadata.UpdatedAt);
        await WriteHistoryAsync(repository, directory, decision.History);
        return decision;
    }

    public async Task<IReadOnlyList<DecisionCandidate>> ListCandidatesAsync(Repository repository)
    {
        IReadOnlyList<string> directories = await ListArtifactDirectoriesAsync(repository, DecisionArtifactKind.Candidate);
        var candidates = new List<DecisionCandidate>();
        foreach (string directory in directories)
        {
            string id = Path.GetFileName(directory);
            DecisionCandidate? candidate = await GetCandidateAsync(repository, id);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        return candidates.OrderBy(candidate => candidate.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<DecisionCandidate?> GetCandidateAsync(Repository repository, string candidateId)
    {
        string id = DecisionArtifactPaths.ValidateId(candidateId, "CAND");
        return await ReadPayloadAsync<DecisionCandidate>(
            repository,
            DecisionArtifactPaths.CandidateJson(id));
    }

    public async Task<DecisionCandidate> SaveCandidateAsync(Repository repository, DecisionCandidate candidate)
    {
        string id = DecisionArtifactPaths.ValidateId(candidate.Id, "CAND");
        if (candidate.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision candidate belongs to a different repository.");
        }

        string directory = DecisionArtifactPaths.CandidateDirectory(id);
        string relativePath = DecisionArtifactPaths.CandidateJson(id);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset createdAt = await GetExistingCreatedAtAsync<DecisionCandidate>(repository, relativePath) ?? now;
        await WriteDocumentAsync(repository, relativePath, candidate, createdAt, now);
        await WriteHistoryAsync(repository, directory, candidate.History);
        return candidate;
    }

    public async Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Repository repository)
    {
        IReadOnlyList<string> directories = await ListArtifactDirectoriesAsync(repository, DecisionArtifactKind.Proposal);
        var proposals = new List<DecisionProposal>();
        foreach (string directory in directories)
        {
            string id = Path.GetFileName(directory);
            DecisionProposal? proposal = await GetProposalAsync(repository, id);
            if (proposal is not null)
            {
                proposals.Add(proposal);
            }
        }

        return proposals.OrderBy(proposal => proposal.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<DecisionProposal?> GetProposalAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        return await ReadPayloadAsync<DecisionProposal>(
            repository,
            DecisionArtifactPaths.ProposalJson(id));
    }

    public async Task<DecisionProposal> SaveProposalAsync(Repository repository, DecisionProposal proposal)
    {
        string id = DecisionArtifactPaths.ValidateId(proposal.Id, "PROP");
        if (proposal.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision proposal belongs to a different repository.");
        }

        string directory = DecisionArtifactPaths.ProposalDirectory(id);
        string relativePath = DecisionArtifactPaths.ProposalJson(id);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset createdAt = await GetExistingCreatedAtAsync<DecisionProposal>(repository, relativePath) ?? now;
        await WriteDocumentAsync(repository, relativePath, proposal, createdAt, now);
        await WriteHistoryAsync(repository, directory, proposal.History);
        return proposal;
    }

    public async Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        string revisionsRoot = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.ProposalRevisionsDirectory(id));
        IReadOnlyList<string> files = await artifactStore.ListAsync(revisionsRoot, "REV-*.json");
        var revisions = new List<DecisionProposalRevision>();

        foreach (string file in files
            .Where(file => string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.Ordinal))
        {
            string revisionId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(revisionId))
            {
                continue;
            }

            DecisionProposalRevision? revision = await ReadPayloadAsync<DecisionProposalRevision>(
                repository,
                DecisionArtifactPaths.ProposalRevisionJson(id, revisionId));
            if (revision is not null)
            {
                revisions.Add(revision);
            }
        }

        return revisions.OrderBy(revision => revision.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<DecisionProposalRevision> SaveProposalRevisionAsync(Repository repository, DecisionProposalRevision revision)
    {
        string proposalId = DecisionArtifactPaths.ValidateId(revision.ProposalId, "PROP");
        string revisionId = DecisionArtifactPaths.ValidateId(revision.Id, "REV");
        if (revision.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision proposal revision belongs to a different repository.");
        }

        await WriteDocumentAsync(
            repository,
            DecisionArtifactPaths.ProposalRevisionJson(proposalId, revisionId),
            revision,
            revision.CreatedAt,
            revision.CreatedAt);
        return revision;
    }

    public async Task<IReadOnlyList<DecisionPackageVersion>> ListPackageVersionsAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        string versionsRoot = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.ProposalVersionsDirectory(id));
        IReadOnlyList<string> files = await artifactStore.ListAsync(versionsRoot, "PKG-*.json");
        var versions = new List<DecisionPackageVersion>();

        foreach (string file in files
            .Where(file => string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.Ordinal))
        {
            string? packageId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(packageId))
            {
                continue;
            }

            DecisionPackageVersion? packageVersion = await GetPackageVersionAsync(repository, id, packageId);
            if (packageVersion is not null)
            {
                versions.Add(packageVersion);
            }
        }

        return versions
            .OrderBy(version => version.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionPackageVersion?> GetPackageVersionAsync(
        Repository repository,
        string proposalId,
        string packageId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        string versionId = DecisionArtifactPaths.ValidateId(packageId, "PKG");
        return await ReadPayloadAsync<DecisionPackageVersion>(
            repository,
            DecisionArtifactPaths.ProposalPackageJson(id, versionId));
    }

    public async Task<DecisionPackageVersion> SavePackageVersionAsync(
        Repository repository,
        DecisionPackageVersion packageVersion)
    {
        string proposalId = DecisionArtifactPaths.ValidateId(packageVersion.ProposalId, "PROP");
        string packageId = DecisionArtifactPaths.ValidateId(packageVersion.Id, "PKG");
        if (packageVersion.RepositoryId != repository.Id || packageVersion.Package.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision package version belongs to a different repository.");
        }

        string relativePath = DecisionArtifactPaths.ProposalPackageJson(proposalId, packageId);
        string path = DecisionArtifactPaths.Resolve(repository, relativePath);
        if (await artifactStore.ExistsAsync(path))
        {
            throw new InvalidOperationException($"Decision package version already exists: {packageId}.");
        }

        await WriteDocumentAsync(repository, relativePath, packageVersion, packageVersion.CreatedAt, packageVersion.CreatedAt);
        return packageVersion;
    }

    public async Task<IReadOnlyList<DecisionRefinementArtifact>> ListRefinementArtifactsAsync(
        Repository repository,
        string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        string refinementsRoot = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.ProposalRefinementsDirectory(id));
        IReadOnlyList<string> files = await artifactStore.ListAsync(refinementsRoot, "REF-*.json");
        var refinements = new List<DecisionRefinementArtifact>();

        foreach (string file in files
            .Where(file => string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.Ordinal))
        {
            string? refinementId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(refinementId))
            {
                continue;
            }

            DecisionRefinementArtifact? refinement = await ReadPayloadAsync<DecisionRefinementArtifact>(
                repository,
                DecisionArtifactPaths.ProposalRefinementJson(id, refinementId));
            if (refinement is not null)
            {
                refinements.Add(refinement);
            }
        }

        return refinements
            .OrderBy(refinement => refinement.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionRefinementArtifact> SaveRefinementArtifactAsync(
        Repository repository,
        DecisionRefinementArtifact refinementArtifact)
    {
        string proposalId = DecisionArtifactPaths.ValidateId(refinementArtifact.ProposalId, "PROP");
        string refinementId = DecisionArtifactPaths.ValidateId(refinementArtifact.Id, "REF");
        if (refinementArtifact.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision refinement artifact belongs to a different repository.");
        }

        string relativePath = DecisionArtifactPaths.ProposalRefinementJson(proposalId, refinementId);
        string path = DecisionArtifactPaths.Resolve(repository, relativePath);
        if (await artifactStore.ExistsAsync(path))
        {
            throw new InvalidOperationException($"Decision refinement artifact already exists: {refinementId}.");
        }

        await WriteDocumentAsync(repository, relativePath, refinementArtifact, refinementArtifact.CreatedAt, refinementArtifact.CreatedAt);
        return refinementArtifact;
    }

    public async Task<DecisionReviewStatus?> GetReviewStatusAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        return await ReadPayloadAsync<DecisionReviewStatus>(
            repository,
            DecisionArtifactPaths.ProposalReviewJson(id));
    }

    public async Task<DecisionReviewStatus> SaveReviewStatusAsync(Repository repository, DecisionReviewStatus reviewStatus)
    {
        string proposalId = DecisionArtifactPaths.ValidateId(reviewStatus.ProposalId, "PROP");
        if (reviewStatus.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision review status belongs to a different repository.");
        }

        string relativePath = DecisionArtifactPaths.ProposalReviewJson(proposalId);
        DateTimeOffset createdAt = await GetExistingCreatedAtAsync<DecisionReviewStatus>(repository, relativePath) ?? reviewStatus.UpdatedAt;
        await WriteDocumentAsync(repository, relativePath, reviewStatus, createdAt, reviewStatus.UpdatedAt);
        return reviewStatus;
    }

    public async Task<IReadOnlyList<DecisionReviewNote>> ListReviewNotesAsync(Repository repository, string proposalId)
    {
        string id = DecisionArtifactPaths.ValidateId(proposalId, "PROP");
        IReadOnlyList<DecisionReviewNote>? notes = await ReadPayloadAsync<IReadOnlyList<DecisionReviewNote>>(
            repository,
            DecisionArtifactPaths.ProposalReviewNotesJson(id));
        return (notes ?? [])
            .OrderBy(note => note.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionReviewNote> SaveReviewNoteAsync(Repository repository, DecisionReviewNote note)
    {
        string proposalId = DecisionArtifactPaths.ValidateId(note.ProposalId, "PROP");
        string noteId = DecisionArtifactPaths.ValidateId(note.Id, "NOTE");
        if (note.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision review note belongs to a different repository.");
        }

        List<DecisionReviewNote> notes = (await ListReviewNotesAsync(repository, proposalId)).ToList();
        int existingIndex = notes.FindIndex(existing => string.Equals(existing.Id, noteId, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            notes[existingIndex] = note;
        }
        else
        {
            notes.Add(note);
        }

        notes = notes
            .OrderBy(existing => existing.Id, StringComparer.Ordinal)
            .ToList();
        string relativePath = DecisionArtifactPaths.ProposalReviewNotesJson(proposalId);
        DateTimeOffset createdAt = await GetExistingCreatedAtAsync<IReadOnlyList<DecisionReviewNote>>(repository, relativePath) ?? note.CreatedAt;
        await WriteDocumentAsync(repository, relativePath, notes, createdAt, DateTimeOffset.UtcNow);
        return note;
    }

    public async Task<DecisionAssimilationRecommendation?> GetAssimilationRecommendationAsync(
        Repository repository,
        DecisionId decisionId)
    {
        string id = DecisionArtifactPaths.ValidateId(decisionId.Value, "DEC");
        return await ReadPayloadAsync<DecisionAssimilationRecommendation>(
            repository,
            DecisionArtifactPaths.AssimilationRecommendationJson(id));
    }

    public async Task<DecisionAssimilationRecommendation> SaveAssimilationRecommendationAsync(
        Repository repository,
        DecisionAssimilationRecommendation recommendation)
    {
        string id = DecisionArtifactPaths.ValidateId(recommendation.DecisionId, "DEC");
        if (recommendation.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision assimilation recommendation belongs to a different repository.");
        }

        string relativePath = DecisionArtifactPaths.AssimilationRecommendationJson(id);
        DateTimeOffset createdAt =
            await GetExistingCreatedAtAsync<DecisionAssimilationRecommendation>(repository, relativePath) ??
            recommendation.CreatedAt;
        await WriteDocumentAsync(repository, relativePath, recommendation, createdAt, recommendation.CreatedAt);
        return recommendation;
    }

    public async Task<IReadOnlyList<DecisionGovernanceReport>> ListGovernanceReportsAsync(Repository repository)
    {
        string root = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.GovernanceRootPath());
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "governance.*.json");
        var reports = new List<DecisionGovernanceReport>();

        foreach (string file in files.OrderBy(file => file, StringComparer.Ordinal))
        {
            string? reportId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(reportId))
            {
                continue;
            }

            DecisionGovernanceReport? report = await ReadPayloadAsync<DecisionGovernanceReport>(
                repository,
                DecisionArtifactPaths.GovernanceReportJson(reportId));
            if (report is not null)
            {
                reports.Add(report);
            }
        }

        return reports
            .OrderBy(report => report.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionGovernanceReport> SaveGovernanceReportAsync(
        Repository repository,
        DecisionGovernanceReport report)
    {
        DecisionArtifactPaths.ValidateReportId(report.Id);
        if (report.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision governance report belongs to a different repository.");
        }

        await WriteDocumentAsync(
            repository,
            DecisionArtifactPaths.GovernanceReportJson(report.Id),
            report,
            report.GeneratedAt,
            report.GeneratedAt);
        return report;
    }

    public async Task<IReadOnlyList<DecisionCertificationReport>> ListCertificationReportsAsync(Repository repository)
    {
        string root = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.CertificationRootPath());
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "certification.*.json");
        var reports = new List<DecisionCertificationReport>();

        foreach (string file in files.OrderBy(file => file, StringComparer.Ordinal))
        {
            string? reportId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(reportId))
            {
                continue;
            }

            DecisionCertificationReport? report = await ReadPayloadAsync<DecisionCertificationReport>(
                repository,
                DecisionArtifactPaths.CertificationReportJson(reportId));
            if (report is not null)
            {
                reports.Add(report);
            }
        }

        return reports
            .OrderBy(report => report.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionCertificationReport> SaveCertificationReportAsync(
        Repository repository,
        DecisionCertificationReport report)
    {
        DecisionArtifactPaths.ValidateCertificationReportId(report.Id);
        if (report.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision certification report belongs to a different repository.");
        }

        await WriteDocumentAsync(
            repository,
            DecisionArtifactPaths.CertificationReportJson(report.Id),
            report,
            report.GeneratedAt,
            report.GeneratedAt);
        return report;
    }

    public async Task<IReadOnlyList<DecisionGenerationCertificationReport>> ListGenerationCertificationReportsAsync(
        Repository repository)
    {
        string root = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.CertificationRootPath());
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "generation-certification.*.json");
        var reports = new List<DecisionGenerationCertificationReport>();

        foreach (string file in files.OrderBy(file => file, StringComparer.Ordinal))
        {
            string? reportId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(reportId))
            {
                continue;
            }

            DecisionGenerationCertificationReport? report =
                await ReadPayloadAsync<DecisionGenerationCertificationReport>(
                    repository,
                    DecisionArtifactPaths.GenerationCertificationReportJson(reportId));
            if (report is not null)
            {
                reports.Add(report);
            }
        }

        return reports
            .OrderBy(report => report.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionGenerationCertificationReport> SaveGenerationCertificationReportAsync(
        Repository repository,
        DecisionGenerationCertificationReport report)
    {
        DecisionArtifactPaths.ValidateGenerationCertificationReportId(report.Id);
        if (report.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision generation certification report belongs to a different repository.");
        }

        await WriteDocumentAsync(
            repository,
            DecisionArtifactPaths.GenerationCertificationReportJson(report.Id),
            report,
            report.GeneratedAt,
            report.GeneratedAt);
        return report;
    }

    public async Task<IReadOnlyList<DecisionQualityAssessment>> ListQualityAssessmentsAsync(Repository repository)
    {
        string root = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.QualityAssessmentsRootPath());
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "assessment.*.json");
        var assessments = new List<DecisionQualityAssessment>();

        foreach (string file in files.OrderBy(file => file, StringComparer.Ordinal))
        {
            string? assessmentId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(assessmentId))
            {
                continue;
            }

            DecisionQualityAssessment? assessment = await ReadPayloadAsync<DecisionQualityAssessment>(
                repository,
                DecisionArtifactPaths.QualityAssessmentJson(assessmentId));
            if (assessment is not null)
            {
                assessments.Add(assessment);
            }
        }

        return assessments
            .OrderBy(assessment => assessment.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionQualityAssessment> SaveQualityAssessmentAsync(
        Repository repository,
        DecisionQualityAssessment assessment)
    {
        DecisionArtifactPaths.ValidateQualityAssessmentId(assessment.Id);
        if (assessment.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision quality assessment belongs to a different repository.");
        }

        await WriteDocumentAsync(
            repository,
            DecisionArtifactPaths.QualityAssessmentJson(assessment.Id),
            assessment,
            assessment.AssessedAt,
            assessment.AssessedAt);
        return assessment;
    }

    public async Task<IReadOnlyList<DecisionQualityReport>> ListQualityReportsAsync(Repository repository)
    {
        string root = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.QualityReportsRootPath());
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "quality.*.json");
        var reports = new List<DecisionQualityReport>();

        foreach (string file in files.OrderBy(file => file, StringComparer.Ordinal))
        {
            string? reportId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(reportId))
            {
                continue;
            }

            DecisionQualityReport? report = await ReadPayloadAsync<DecisionQualityReport>(
                repository,
                DecisionArtifactPaths.QualityReportJson(reportId));
            if (report is not null)
            {
                reports.Add(report);
            }
        }

        return reports
            .OrderBy(report => report.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionQualityReport> SaveQualityReportAsync(
        Repository repository,
        DecisionQualityReport report)
    {
        DecisionArtifactPaths.ValidateQualityReportId(report.Id);
        if (report.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision quality report belongs to a different repository.");
        }

        await WriteDocumentAsync(
            repository,
            DecisionArtifactPaths.QualityReportJson(report.Id),
            report,
            report.GeneratedAt,
            report.GeneratedAt);
        return report;
    }

    public async Task<IReadOnlyList<DecisionQualityTrend>> ListQualityTrendsAsync(Repository repository)
    {
        string root = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.QualityTrendsRootPath());
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "trend.*.json");
        var trends = new List<DecisionQualityTrend>();

        foreach (string file in files.OrderBy(file => file, StringComparer.Ordinal))
        {
            string? trendId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(trendId))
            {
                continue;
            }

            DecisionQualityTrend? trend = await ReadPayloadAsync<DecisionQualityTrend>(
                repository,
                DecisionArtifactPaths.QualityTrendJson(trendId));
            if (trend is not null)
            {
                trends.Add(trend);
            }
        }

        return trends
            .OrderBy(trend => trend.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionQualityTrend> SaveQualityTrendAsync(
        Repository repository,
        DecisionQualityTrend trend)
    {
        DecisionArtifactPaths.ValidateQualityTrendId(trend.Id);
        if (trend.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision quality trend belongs to a different repository.");
        }

        await WriteDocumentAsync(
            repository,
            DecisionArtifactPaths.QualityTrendJson(trend.Id),
            trend,
            trend.GeneratedAt,
            trend.GeneratedAt);
        return trend;
    }

    private async Task<string> AllocateIdAsync(Repository repository, DecisionArtifactKind kind, string prefix)
    {
        IReadOnlyList<string> directories = await ListArtifactDirectoriesAsync(repository, kind);
        int next = directories
            .Select(Path.GetFileName)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => ParseSequence(id!, prefix))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}-{next:0000}";
    }

    private async Task<IReadOnlyList<string>> ListArtifactDirectoriesAsync(Repository repository, DecisionArtifactKind kind)
    {
        string root = DecisionArtifactPaths.ResolveRoot(repository, kind);
        return await artifactStore.ListDirectoriesAsync(root);
    }

    private async Task<T?> ReadPayloadAsync<T>(Repository repository, string relativePath)
    {
        DecisionArtifactDocument<T>? document = await ReadDocumentAsync<T>(repository, relativePath);
        if (document is null)
        {
            return default;
        }

        if (!string.Equals(document.SchemaVersion, DecisionArtifactPaths.SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported decision artifact schema version '{document.SchemaVersion}'.");
        }

        if (document.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision artifact belongs to a different repository.");
        }

        return document.Payload;
    }

    private async Task<DateTimeOffset?> GetExistingCreatedAtAsync<T>(Repository repository, string relativePath)
    {
        DecisionArtifactDocument<T>? existing = await ReadDocumentAsync<T>(repository, relativePath);
        return existing?.CreatedAt;
    }

    private async Task<DecisionArtifactDocument<T>?> ReadDocumentAsync<T>(Repository repository, string relativePath)
    {
        // ReadAs caches the deserialized document graph keyed by the file signature, so the repeated reads of the
        // same unchanged artifact within a single request (List* fanning out into per-id Get*, plus the
        // created-at probe before each Save) skip re-deserializing. DecisionArtifactDocument<T> and its record
        // payloads are immutable, so aliasing the cached instance to multiple callers is safe.
        return await artifactStore.ReadAs(
            DecisionArtifactPaths.Resolve(repository, relativePath),
            json => JsonSerializer.Deserialize<DecisionArtifactDocument<T>>(json, DecisionJson.Options));
    }

    private async Task WriteDocumentAsync<T>(
        Repository repository,
        string relativePath,
        T payload,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var document = new DecisionArtifactDocument<T>(
            DecisionArtifactPaths.SchemaVersion,
            repository.Id,
            createdAt,
            updatedAt,
            payload);
        await artifactStore.WriteAsync(
            DecisionArtifactPaths.Resolve(repository, relativePath),
            JsonSerializer.Serialize(document, DecisionJson.Options));
    }

    private async Task WriteHistoryAsync(
        Repository repository,
        string relativeDirectory,
        IReadOnlyList<DecisionHistoryEntry> history)
    {
        await WriteDocumentAsync(
            repository,
            DecisionArtifactPaths.HistoryJsonForDirectory(relativeDirectory),
            history,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static int ParseSequence(string id, string prefix)
    {
        return id.StartsWith($"{prefix}-", StringComparison.Ordinal) &&
            int.TryParse(id[(prefix.Length + 1)..], out int sequence)
            ? sequence
            : 0;
    }
}

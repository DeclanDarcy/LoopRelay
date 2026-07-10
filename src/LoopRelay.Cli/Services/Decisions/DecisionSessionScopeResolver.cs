using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Cli.Services.Decisions;

internal readonly record struct DecisionSessionScopeId(string Value)
{
    public override string ToString() => Value;
}

internal sealed record DecisionSessionScope(
    string WorkspaceId,
    DecisionSessionScopeId ScopeId,
    ProductRecord PreparedEpic,
    ProductRecord ExecutablePlan,
    string ContractVersion);

internal sealed record DecisionExecutionContext(
    DecisionSessionScope Scope,
    PromptExecutionRequest PromptExecution);

internal sealed class DecisionSessionScopeResolver(Repository _repository, RepositoryObserver? _observer = null)
{
    public const string ScopeContractVersion = "decision-session-scope.v1";

    public async Task<DecisionSessionScope> ResolveAsync(CancellationToken cancellationToken = default)
    {
        RepositoryObservation observation = await (_observer ?? new RepositoryObserver())
            .ObserveAsync(_repository.Path, cancellationToken);
        if (observation.StorageVerification.IsBlocked)
        {
            throw new InvalidOperationException("Execute continuity scope cannot be resolved from blocked storage authority.");
        }
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        await using Microsoft.Data.Sqlite.SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        string workspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdAsync(connection, cancellationToken);
        return Resolve(
            workspaceId,
            observation.Products
                .Where(product => product.GateUsable)
                .Select(product => product.Product.ValidationState == ProductValidationState.Unknown
                    ? product.Product with { ValidationState = ProductValidationState.Valid }
                    : product.Product));
    }

    internal static DecisionSessionScope Resolve(string workspaceId, IEnumerable<ProductRecord> products)
    {
        ProductRecord preparedEpic = RequireCanonical(products, ProductIdentity.PreparedEpic);
        ProductRecord executablePlan = RequireCanonical(products, ProductIdentity.ExecutablePlan);
        string canonical = string.Join('\n',
        [
            $"contract={ScopeContractVersion}",
            $"workspace={workspaceId}",
            $"workflow={WorkflowIdentity.Execute.Value}",
            ProductLine("prepared-epic", preparedEpic),
            ProductLine("executable-plan", executablePlan),
            "role=Decision",
        ]);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new DecisionSessionScope(
            workspaceId,
            new DecisionSessionScopeId(digest),
            preparedEpic,
            executablePlan,
            ScopeContractVersion);
    }

    private static ProductRecord RequireCanonical(IEnumerable<ProductRecord> products, ProductIdentity identity)
    {
        ProductRecord[] matches = products.Where(product => product.Identity == identity).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Execute continuity requires exactly one canonical {identity}; found {matches.Length}.");
        }

        ProductRecord product = matches[0];
        if (product.ValidationState != ProductValidationState.Valid
            || product.Freshness != ProductFreshness.Fresh
            || product.Lifecycle != ProductLifecycle.Active)
        {
            throw new InvalidOperationException(
                $"Execute continuity requires a fresh, valid, active {identity} product.");
        }

        if (product.ProducerWorkflow.IsEmpty
            || product.ProducerTransition.IsEmpty
            || product.CausalIdentity.Length != 64
            || product.CausalIdentity.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException(
                $"Execute continuity requires canonical content/producer identity for {identity}.");
        }

        return product;
    }

    private static string ProductLine(string label, ProductRecord product) =>
        $"{label}={product.ProducerWorkflow.Value}:{product.ProducerTransition.Value}:{product.CausalIdentity.ToLowerInvariant()}";
}

using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal sealed class SqliteTransitionJournalStore(Repository repository)
    : SqliteDomainStore(repository), ITransitionJournalStore
{
    public async Task AppendAsync(TransitionJournalRecord record)
    {
        await using SqliteConnection connection = await OpenAsync();
        await ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO transition_journal (
                correlation_id, event_name, recorded_at, from_state, to_state, transition,
                projection_path, prompt_contract, input_hashes_json, output_paths_json,
                duration_milliseconds, retry_count, result, decision, error, input_snapshot_json)
            VALUES (
                $correlation_id, $event_name, $recorded_at, $from_state, $to_state, $transition,
                $projection_path, $prompt_contract, $input_hashes_json, $output_paths_json,
                $duration_milliseconds, 0, $result, $decision, $error, $input_snapshot_json);
            """,
            parameters:
            [
                ("$correlation_id", record.CorrelationId),
                ("$event_name", record.Event),
                ("$recorded_at", Format(record.Timestamp)),
                ("$from_state", record.PreviousState.ToString()),
                ("$to_state", record.AttemptedState.ToString()),
                ("$transition", record.Prompt),
                ("$projection_path", record.Projection),
                ("$prompt_contract", record.PromptContractKey),
                ("$input_hashes_json", JsonSerializer.Serialize(record.InputArtifactHashes, JsonOptions)),
                ("$output_paths_json", JsonSerializer.Serialize(record.OutputPaths, JsonOptions)),
                ("$duration_milliseconds", record.DurationMilliseconds),
                ("$result", record.Result),
                ("$decision", record.ParserDecision),
                ("$error", record.ErrorMessage),
                ("$input_snapshot_json", record.InputSnapshot is null ? null : JsonSerializer.Serialize(record.InputSnapshot, JsonOptions)),
            ]);
    }
}

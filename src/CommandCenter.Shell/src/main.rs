use serde::{Deserialize, Serialize};
use serde_json::{Value, json};
use std::{
    env,
    path::PathBuf,
    process::{Child, Command},
    sync::{Mutex, OnceLock},
    thread,
    time::Duration,
};
use tauri::Manager;

const BACKEND_URL: &str = "http://127.0.0.1:5000";

/// Process-wide shared blocking HTTP client.
///
/// Every backend call reuses this single client so its connection pool and
/// keep-alive survive across invokes. A repo-select fans out ~24 concurrent
/// invokes to 127.0.0.1:5000; without a shared pool each one paid a fresh TCP
/// setup. There is deliberately NO overall request timeout (some commands are
/// legitimately slow and never had one) — only a short connect timeout.
static HTTP_CLIENT: OnceLock<reqwest::blocking::Client> = OnceLock::new();

fn http_client() -> &'static reqwest::blocking::Client {
    HTTP_CLIENT.get_or_init(|| {
        reqwest::blocking::Client::builder()
            .pool_max_idle_per_host(32)
            .connect_timeout(Duration::from_secs(5))
            .build()
            .unwrap_or_else(|_| reqwest::blocking::Client::new())
    })
}

/// Runs a blocking backend call on Tauri's dedicated blocking thread pool.
///
/// Every command body uses `reqwest::blocking`, which must not execute on an
/// async-runtime worker thread. `reqwest::blocking::Client::send` routes through
/// `wait::timeout`, whose debug-only sanity check builds and then drops a throwaway
/// Tokio runtime; dropping a runtime while a worker thread is *entered* (driving the
/// scheduler) panics with "Cannot drop a runtime in a context where blocking is not
/// allowed". Even in release, blocking the request on a worker parks one of the few
/// scheduler threads for the whole round-trip. `spawn_blocking` moves the work onto a
/// blocking thread, where the runtime context is not "entered" (so the drop is
/// permitted) and where parking is exactly what that pool is for — leaving the async
/// workers free to service the other invokes a repo-select fans out concurrently.
async fn offload<T, F>(task: F) -> Result<T, String>
where
    T: Send + 'static,
    F: FnOnce() -> Result<T, String> + Send + 'static,
{
    tauri::async_runtime::spawn_blocking(task)
        .await
        .map_err(|error| error.to_string())?
}

struct BackendProcess {
    child: Mutex<Option<Child>>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct Repository {
    id: String,
    name: String,
    path: String,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct RepositoryDashboardProjection {
    repository: Repository,
    availability: String,
    readiness: String,
    execution_state: String,
    active_execution_session: Option<ExecutionSessionSummary>,
    execution_summary: Option<ExecutionSessionSummary>,
    execution_history: Vec<ExecutionSessionSummary>,
    milestone_count: i32,
    has_current_handoff: bool,
    has_current_decisions: bool,
    continuity_summary: RepositoryContinuitySummary,
    reasoning_summary: RepositoryReasoningSummary,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct RepositoryContinuitySummary {
    operational_context_exists: bool,
    operational_context_revision_count: i32,
    operational_context_last_updated_at: Option<String>,
    open_question_count: i32,
    active_risk_count: i32,
    pending_proposal_exists: bool,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct RepositoryReasoningSummary {
    event_count: i32,
    thread_count: i32,
    relationship_count: i32,
    hypothesis_event_count: i32,
    alternative_event_count: i32,
    contradiction_event_count: i32,
    direction_event_count: i32,
    decision_evolution_event_count: i32,
    assumption_evolution_event_count: i32,
    constraint_evolution_event_count: i32,
    evidence_event_count: i32,
    last_event_at: Option<String>,
    last_thread_activity_at: Option<String>,
    last_relationship_at: Option<String>,
    last_activity_at: Option<String>,
    last_reconstruction_at: Option<String>,
    last_certification_at: Option<String>,
    certification_result: Option<String>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct ExecutionSessionSummary {
    session_id: String,
    state: String,
    repository_state: String,
    started_at: Option<String>,
    completed_at: Option<String>,
    duration: Option<String>,
    accepted_at: Option<String>,
    rejected_at: Option<String>,
    decision_note: Option<String>,
    last_activity_at: Option<String>,
    provider_name: String,
    provider_executable_path: Option<String>,
    provider_process_id: Option<i32>,
    provider_started_at: Option<String>,
    handoff_path: Option<String>,
    commit_sha: Option<String>,
    committed_at: Option<String>,
    commit_message: Option<String>,
    preparation_snapshot_id: Option<String>,
    push_attempted_at: Option<String>,
    pushed_at: Option<String>,
    pushed_commit_sha: Option<String>,
    push_remote_name: Option<String>,
    push_branch_name: Option<String>,
    failure_reason: Option<String>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct RepositoryDirtyState {
    staged_paths: Vec<String>,
    modified_paths: Vec<String>,
    added_paths: Vec<String>,
    deleted_paths: Vec<String>,
    renamed_paths: Vec<String>,
    untracked_paths: Vec<String>,
    is_clean: bool,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct RepositoryGitStatus {
    branch: String,
    ahead_count: i32,
    behind_count: i32,
    dirty_state: RepositoryDirtyState,
    captured_at: String,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct CommitScopeItem {
    path: String,
    change_type: String,
    origin: String,
    is_selected: bool,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct CommitStatusSnapshot {
    id: String,
    branch: String,
    ahead_count: i32,
    behind_count: i32,
    dirty_state: RepositoryDirtyState,
    captured_at: String,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct CommitPreparation {
    id: String,
    session_id: String,
    repository_id: String,
    repository_path: String,
    proposed_message: String,
    scope_items: Vec<CommitScopeItem>,
    status_snapshot: CommitStatusSnapshot,
    generated_at: String,
    has_pre_existing_changes: bool,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct CommitRequest {
    message: String,
    selected_paths: Vec<String>,
    status_snapshot_id: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct PushRequest {}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ExecutionGitActionEligibilityRequest {
    commit_message: Option<String>,
    selected_paths: Vec<String>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct PushAttemptResult {
    succeeded: bool,
    retryable: bool,
    error: Option<String>,
    attempted_at: Option<String>,
    session: Option<ExecutionSessionSummary>,
    diagnostics: Vec<String>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct Artifact {
    relative_path: String,
    name: String,
    #[serde(rename = "type")]
    artifact_type: String,
    family: String,
    version_kind: String,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct ArtifactInventory {
    plan: Option<Artifact>,
    operational_context: Option<Artifact>,
    historical_operational_contexts: Vec<Artifact>,
    milestones: Vec<Artifact>,
    current_handoff: Option<Artifact>,
    historical_handoffs: Vec<Artifact>,
    current_decisions: Option<Artifact>,
    historical_decisions: Vec<Artifact>,
    reasoning_artifacts: Vec<Artifact>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct MilestoneProgress {
    relative_path: String,
    name: String,
    completed_task_count: i32,
    total_task_count: i32,
    is_complete: bool,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct MilestoneProgressRollup {
    completed_milestone_count: i32,
    total_milestone_count: i32,
    milestones: Vec<MilestoneProgress>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct RepositoryWorkspaceProjection {
    repository: Repository,
    availability: String,
    readiness: String,
    execution_state: String,
    execution_summary: Option<ExecutionSessionSummary>,
    execution_history: Vec<ExecutionSessionSummary>,
    artifact_inventory: ArtifactInventory,
    milestone_count: i32,
    milestone_progress: MilestoneProgressRollup,
    has_plan: bool,
    has_operational_context: bool,
    has_current_handoff: bool,
    has_current_decisions: bool,
    operational_context_proposal_summary: OperationalContextProposalSummary,
    operational_context: OperationalContextProjection,
    reasoning_summary: RepositoryReasoningSummary,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct OperationalContextProposalSummary {
    pending_proposal_exists: bool,
    latest_proposal_id: Option<String>,
    generated_at: Option<String>,
    status: Option<String>,
    source_input_count: i32,
    content_byte_count: i32,
    content_character_count: i32,
    last_promoted_at: Option<String>,
    last_archived_relative_path: Option<String>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct OperationalContextProjection {
    exists: bool,
    current_relative_path: Option<String>,
    revision_count: i32,
    current_revision_number: i32,
    last_updated_at: Option<String>,
    last_promotion_at: Option<String>,
    current_understanding_summary: Vec<String>,
    architecture: Vec<OperationalContextItem>,
    authority_boundaries: Vec<OperationalContextItem>,
    constraints: Vec<OperationalContextItem>,
    stable_decisions: Vec<OperationalContextItem>,
    decision_rationale: Vec<OperationalContextItem>,
    open_questions: Vec<OperationalContextItem>,
    active_risks: Vec<OperationalContextItem>,
    recent_understanding_changes: Vec<OperationalContextItem>,
    pending_proposal_summary: OperationalContextProposalSummary,
    latest_review_state: Option<String>,
    continuity_warnings: Vec<String>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct OperationalContextItem {
    id: String,
    kind: String,
    text: String,
    rationale: Option<String>,
    source_relative_path: Option<String>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct RegisterRepositoryRequest {
    path: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct SaveArtifactContentRequest {
    relative_path: String,
    content: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct OperationalContextProposalContentRequest {
    content: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct OperationalContextProposalReviewRequest {
    review_note: Option<String>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ExecutionAcceptanceRequest {
    decision_note: Option<String>,
}

#[derive(Deserialize, Serialize)]
struct ErrorResponse {
    error: String,
    #[serde(rename = "boundaryViolation", skip_serializing_if = "Option::is_none")]
    boundary_violation: Option<Value>,
}

impl BackendProcess {
    fn stop(&self) {
        if let Ok(mut child) = self.child.lock() {
            if let Some(mut child) = child.take() {
                let _ = child.kill();
                let _ = child.wait();
            }
        }
    }
}

impl Drop for BackendProcess {
    fn drop(&mut self) {
        self.stop();
    }
}

/// Synchronous ping used both by the `ping_backend` command (via `offload`) and by
/// the startup `wait_for_backend` poll. The startup call runs on the main thread
/// during `setup`, where the runtime context is not "entered", so `reqwest::blocking`
/// is safe there without `spawn_blocking`.
fn ping_backend_request() -> Result<String, String> {
    http_client()
        .get(format!("{BACKEND_URL}/api/ping"))
        .send()
        .map_err(|error| error.to_string())?
        .text()
        .map_err(|error| error.to_string())
}

#[tauri::command]
async fn ping_backend() -> Result<String, String> {
    offload(ping_backend_request).await
}

#[tauri::command(async)]
fn get_backend_url() -> String {
    BACKEND_URL.to_string()
}

// Native folder picker must stay synchronous: rfd's modal dialog is main-thread-only.
// Every other command is an async `#[tauri::command]` that runs its blocking HTTP on
// Tauri's blocking pool via `offload`, so it never freezes the webview — and never
// drops reqwest's debug-only throwaway runtime on an async worker thread.
#[tauri::command]
fn select_repository_directory() -> Option<String> {
    rfd::FileDialog::new()
        .set_title("Select Repository")
        .pick_folder()
        .map(|path| path.display().to_string())
}

#[tauri::command]
async fn list_repositories() -> Result<Vec<RepositoryDashboardProjection>, String> {
    offload(move || {
        http_client()
            .get(format!("{BACKEND_URL}/api/repositories"))
            .send()
            .map_err(|error| error.to_string())?
            .error_for_status()
            .map_err(|error| error.to_string())?
            .json()
            .map_err(|error| error.to_string())
    })
    .await
}

#[tauri::command]
async fn register_repository(path: String) -> Result<(), String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!("{BACKEND_URL}/api/repositories"))
            .json(&RegisterRepositoryRequest { path })
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return Ok(());
        }

        let status = response.status();
        let message = response
            .json::<ErrorResponse>()
            .map(|response| response.error)
            .unwrap_or_else(|_| format!("repository registration failed with status {status}"));

        Err(message)
    })
    .await
}

#[tauri::command]
async fn remove_repository(repository_id: String) -> Result<(), String> {
    offload(move || {
        let client = http_client();
        client
            .delete(format!("{BACKEND_URL}/api/repositories/{repository_id}"))
            .send()
            .map_err(|error| error.to_string())?
            .error_for_status()
            .map_err(|error| error.to_string())?;

        Ok(())
    })
    .await
}

#[tauri::command]
async fn get_repository_workspace(
    repository_id: String,
) -> Result<RepositoryWorkspaceProjection, String> {
    offload(move || {
        http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/workspace"
            ))
            .send()
            .map_err(|error| error.to_string())?
            .error_for_status()
            .map_err(|error| error.to_string())?
            .json()
            .map_err(|error| error.to_string())
    })
    .await
}

#[tauri::command]
async fn refresh_repository_workspace(
    repository_id: String,
) -> Result<RepositoryWorkspaceProjection, String> {
    offload(move || {
        let client = http_client();
        client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/refresh"
            ))
            .send()
            .map_err(|error| error.to_string())?
            .error_for_status()
            .map_err(|error| error.to_string())?
            .json()
            .map_err(|error| error.to_string())
    })
    .await
}

#[tauri::command]
async fn load_artifact_content(
    repository_id: String,
    relative_path: String,
) -> Result<String, String> {
    offload(move || {
        let client = http_client();
        client
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/artifacts/content"
            ))
            .query(&[("relativePath", relative_path)])
            .send()
            .map_err(|error| error.to_string())?
            .error_for_status()
            .map_err(|error| error.to_string())?
            .text()
            .map_err(|error| error.to_string())
    })
    .await
}

#[tauri::command]
async fn save_artifact_content(
    repository_id: String,
    relative_path: String,
    content: String,
) -> Result<(), String> {
    offload(move || {
        let client = http_client();
        client
            .put(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/artifacts/content"
            ))
            .json(&SaveArtifactContentRequest {
                relative_path,
                content,
            })
            .send()
            .map_err(|error| error.to_string())?
            .error_for_status()
            .map_err(|error| error.to_string())?;

        Ok(())
    })
    .await
}

#[tauri::command]
async fn rotate_current_handoff(
    repository_id: String,
) -> Result<RepositoryWorkspaceProjection, String> {
    offload(move || rotate_artifact(repository_id, "rotate-current-handoff")).await
}

#[tauri::command]
async fn rotate_current_decisions(
    repository_id: String,
) -> Result<RepositoryWorkspaceProjection, String> {
    offload(move || rotate_artifact(repository_id, "rotate-current-decisions")).await
}

#[tauri::command]
async fn preview_execution_context(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        client
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/execution/context"
            ))
            .send()
            .map_err(|error| error.to_string())?
            .error_for_status()
            .map_err(|error| error.to_string())?
            .json()
            .map_err(|error| error.to_string())
    })
    .await
}

#[tauri::command]
async fn get_plan_status(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/plan/status"),
            "plan status lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn write_plan(
    repository_id: String,
    roadmap: String,
    specs: Vec<String>,
    new_codebase: bool,
) -> Result<Value, String> {
    offload(move || {
        backend_post_json_value(
            &format!("/api/repositories/{repository_id}/plan/write"),
            &json!({
                "roadmap": roadmap,
                "specs": specs,
                "newCodebase": new_codebase,
            }),
            "plan write failed",
        )
    })
    .await
}

#[tauri::command]
async fn revise_plan(repository_id: String, feedback: String) -> Result<Value, String> {
    offload(move || {
        backend_post_json_value(
            &format!("/api/repositories/{repository_id}/plan/revise"),
            &json!({ "feedback": feedback }),
            "plan revision failed",
        )
    })
    .await
}

#[tauri::command]
async fn execute_plan(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!("/api/repositories/{repository_id}/plan/execute"),
            "plan execution failed",
        )
    })
    .await
}

#[tauri::command]
async fn decision_run(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!("/api/repositories/{repository_id}/decision/run"),
            "decision run failed",
        )
    })
    .await
}

#[tauri::command]
async fn decision_submit(repository_id: String, decisions: String) -> Result<Value, String> {
    offload(move || {
        backend_post_json_value(
            &format!("/api/repositories/{repository_id}/decision/submit"),
            &json!({ "decisions": decisions }),
            "decision submission failed",
        )
    })
    .await
}

#[tauri::command]
async fn generate_operational_context_proposal(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/operational-context/generate"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "operational-context proposal generation failed")
    })
    .await
}

#[tauri::command]
async fn list_operational_context_proposals(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/operational-context/proposals"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "operational-context proposal listing failed")
    })
    .await
}

#[tauri::command]
async fn get_operational_context_proposal(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    offload(move || {
        let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/operational-context/proposals/{proposal_id}"
    )).send()
    .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "operational-context proposal lookup failed")
    })
    .await
}

#[tauri::command]
async fn edit_operational_context_proposal(
    repository_id: String,
    proposal_id: String,
    content: String,
) -> Result<Value, String> {
    offload(move || {
    let client = http_client();
    let response = client
        .put(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/operational-context/proposals/{proposal_id}/content"
        ))
        .json(&OperationalContextProposalContentRequest { content })
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "operational-context proposal edit failed")
    })
    .await
}

#[tauri::command]
async fn accept_operational_context_proposal(
    repository_id: String,
    proposal_id: String,
    review_note: Option<String>,
) -> Result<Value, String> {
    offload(move || {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/operational-context/proposals/{proposal_id}/accept"
        ))
        .json(&OperationalContextProposalReviewRequest { review_note })
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "operational-context proposal accept failed")
    })
    .await
}

#[tauri::command]
async fn reject_operational_context_proposal(
    repository_id: String,
    proposal_id: String,
    review_note: Option<String>,
) -> Result<Value, String> {
    offload(move || {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/operational-context/proposals/{proposal_id}/reject"
        ))
        .json(&OperationalContextProposalReviewRequest { review_note })
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "operational-context proposal reject failed")
    })
    .await
}

#[tauri::command]
async fn promote_operational_context_proposal(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    offload(move || {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/operational-context/proposals/{proposal_id}/promote"
        ))
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "operational-context proposal promote failed")
    })
    .await
}

#[tauri::command]
async fn get_decision_context(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/context"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision context lookup failed")
    })
    .await
}

#[tauri::command]
async fn build_decision_context(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/context"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision context build failed")
    })
    .await
}

#[tauri::command]
async fn list_decision_candidates(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/candidates"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision candidate listing failed")
    })
    .await
}

#[tauri::command]
async fn get_decision_lifecycle_eligibility(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decisions/lifecycle/eligibility"),
            "decision lifecycle eligibility lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn discover_decisions(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!("/api/repositories/{repository_id}/decisions/discover"),
            "decision discovery failed",
        )
    })
    .await
}

#[tauri::command]
async fn promote_decision_candidate(
    repository_id: String,
    candidate_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    offload(move || {
        decision_candidate_transition(
            repository_id,
            candidate_id,
            "promote",
            json!({ "reason": reason }),
            "decision candidate promotion failed",
        )
    })
    .await
}

#[tauri::command]
async fn dismiss_decision_candidate(
    repository_id: String,
    candidate_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    offload(move || {
        decision_candidate_transition(
            repository_id,
            candidate_id,
            "dismiss",
            json!({ "reason": reason }),
            "decision candidate dismissal failed",
        )
    })
    .await
}

#[tauri::command]
async fn expire_decision_candidate(
    repository_id: String,
    candidate_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    offload(move || {
        decision_candidate_transition(
            repository_id,
            candidate_id,
            "expire",
            json!({ "reason": reason }),
            "decision candidate expiration failed",
        )
    })
    .await
}

#[tauri::command]
async fn mark_decision_candidate_duplicate(
    repository_id: String,
    candidate_id: String,
    duplicate_of_candidate_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    offload(move || {
        decision_candidate_transition(
            repository_id,
            candidate_id,
            "duplicate",
            json!({
                "reason": reason,
                "duplicateOfCandidateId": duplicate_of_candidate_id
            }),
            "decision candidate duplicate marking failed",
        )
    })
    .await
}

#[tauri::command]
async fn list_decision_proposals(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision proposal listing failed")
    })
    .await
}

#[tauri::command]
async fn list_decision_proposal_browser(
    repository_id: String,
    states: Vec<String>,
) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let mut request = client.get(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/browser"
        ));

        if !states.is_empty() {
            request = request.query(&[("states", states.join(","))]);
        }

        let response = request.send().map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision proposal browser listing failed")
    })
    .await
}

#[tauri::command]
async fn get_decision_proposal(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision proposal lookup failed")
    })
    .await
}

#[tauri::command]
async fn generate_decision_proposal(
    repository_id: String,
    candidate_id: String,
) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!(
                "/api/repositories/{repository_id}/decisions/candidates/{candidate_id}/proposals"
            ),
            "decision proposal generation failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_proposal_review(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    offload(move || {
        let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/review"
    )).send()
    .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision proposal review lookup failed")
    })
    .await
}

#[tauri::command]
async fn expire_decision_proposal(
    repository_id: String,
    proposal_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    offload(move || {
        decision_proposal_transition(
            repository_id,
            proposal_id,
            "expire",
            json!({ "reason": reason }),
            "decision proposal expiration failed",
        )
    })
    .await
}

#[tauri::command]
async fn discard_decision_proposal(
    repository_id: String,
    proposal_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    offload(move || {
        decision_proposal_transition(
            repository_id,
            proposal_id,
            "discard",
            json!({ "reason": reason }),
            "decision proposal discard failed",
        )
    })
    .await
}

#[tauri::command]
async fn mark_decision_proposal_viewed(
    repository_id: String,
    proposal_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    offload(move || {
        decision_proposal_transition(
            repository_id,
            proposal_id,
            "review/viewed",
            json!({ "reason": reason }),
            "decision proposal viewed transition failed",
        )
    })
    .await
}

#[tauri::command]
async fn mark_decision_proposal_needs_refinement(
    repository_id: String,
    proposal_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    offload(move || {
        decision_proposal_transition(
            repository_id,
            proposal_id,
            "review/needs-refinement",
            json!({ "reason": reason }),
            "decision proposal needs-refinement transition failed",
        )
    })
    .await
}

#[tauri::command]
async fn mark_decision_proposal_ready_for_resolution(
    repository_id: String,
    proposal_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    offload(move || {
        decision_proposal_transition(
            repository_id,
            proposal_id,
            "review/ready-for-resolution",
            json!({ "reason": reason }),
            "decision proposal ready-for-resolution transition failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_proposal_lineage(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    offload(move || {
        let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/lineage"
    )).send()
    .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision proposal lineage lookup failed")
    })
    .await
}

#[tauri::command]
async fn refine_decision_proposal(
    repository_id: String,
    proposal_id: String,
    request: Value,
) -> Result<Value, String> {
    offload(move || {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/refinements"
        ))
        .json(&request)
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision proposal refinement failed")
    })
    .await
}

#[tauri::command]
async fn analyze_decision_refinement(
    repository_id: String,
    proposal_id: String,
    request: Value,
) -> Result<Value, String> {
    offload(move || {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/refinements/analyze"
        ))
        .json(&request)
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision refinement analysis failed")
    })
    .await
}

#[tauri::command]
async fn regenerate_decision_refinement(
    repository_id: String,
    proposal_id: String,
    request: Value,
) -> Result<Value, String> {
    offload(move || {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/refinements/regenerate"
        ))
        .json(&request)
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision refinement regeneration failed")
    })
    .await
}

#[tauri::command]
async fn resolve_decision_proposal(
    repository_id: String,
    proposal_id: String,
    request: Value,
) -> Result<Value, String> {
    offload(move || {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/resolve"
        ))
        .json(&request)
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision proposal resolution failed")
    })
    .await
}

#[tauri::command]
async fn supersede_decision(
    repository_id: String,
    decision_id: String,
    request: Value,
) -> Result<Value, String> {
    offload(move || {
        backend_post_json_value(
            &format!("/api/repositories/{repository_id}/decisions/{decision_id}/supersede"),
            &request,
            "decision supersession failed",
        )
    })
    .await
}

#[tauri::command]
async fn archive_decision(
    repository_id: String,
    decision_id: String,
    request: Value,
) -> Result<Value, String> {
    offload(move || {
        backend_post_json_value(
            &format!("/api/repositories/{repository_id}/decisions/{decision_id}/archive"),
            &request,
            "decision archival failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_assimilation_recommendation(
    repository_id: String,
    decision_id: String,
) -> Result<Value, String> {
    offload(move || {
        let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/{decision_id}/assimilation"
    )).send()
    .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(
            response,
            "decision assimilation recommendation lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn propose_decision_operational_context_assimilation(
    repository_id: String,
    decision_id: String,
    request: Value,
) -> Result<Value, String> {
    offload(move || {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/decisions/{decision_id}/assimilation/propose-operational-context"
        ))
        .json(&request)
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(
        response,
        "decision assimilation recommendation creation failed",
    )
    })
    .await
}

#[tauri::command]
async fn get_decision_option_comparison(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    offload(move || {
        let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/options"
    )).send()
    .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision option comparison lookup failed")
    })
    .await
}

#[tauri::command]
async fn get_decision_evidence_inspection(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    offload(move || {
        let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/evidence"
    )).send()
    .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision evidence inspection lookup failed")
    })
    .await
}

#[tauri::command]
async fn list_decision_source_attributions(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    offload(move || {
        let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/sources"
    )).send()
    .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision source attribution listing failed")
    })
    .await
}

#[tauri::command]
async fn get_decision_governance(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/governance"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision governance lookup failed")
    })
    .await
}

#[tauri::command]
async fn generate_decision_governance_report(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/governance/reports"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision governance report generation failed")
    })
    .await
}

#[tauri::command]
async fn list_decision_governance_reports(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/governance/reports"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision governance report listing failed")
    })
    .await
}

#[tauri::command]
async fn get_decision_certification(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/certification"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision certification lookup failed")
    })
    .await
}

#[tauri::command]
async fn run_decision_certification(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/certification"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision certification run failed")
    })
    .await
}

#[tauri::command]
async fn list_decision_certification_reports(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/certification/reports"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision certification report listing failed")
    })
    .await
}

#[tauri::command]
async fn get_decision_generation_certification(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/generation-certification/current"
    )).send()
    .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision generation certification lookup failed")
    })
    .await
}

#[tauri::command]
async fn run_decision_generation_certification(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/generation-certification"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision generation certification run failed")
    })
    .await
}

#[tauri::command]
async fn list_decision_generation_certification_reports(
    repository_id: String,
) -> Result<Value, String> {
    offload(move || {
        let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/generation-certification/reports"
    )).send()
    .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(
            response,
            "decision generation certification report listing failed",
        )
    })
    .await
}

#[tauri::command]
async fn assess_decision_quality(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    offload(move || {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/quality/assess"
        ))
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision quality assessment failed")
    })
    .await
}

#[tauri::command]
async fn list_decision_quality_assessments(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/assessments"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision quality assessment listing failed")
    })
    .await
}

#[tauri::command]
async fn get_decision_quality_report(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/reports/current"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision quality report lookup failed")
    })
    .await
}

#[tauri::command]
async fn generate_decision_quality_report(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/reports"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision quality report generation failed")
    })
    .await
}

#[tauri::command]
async fn list_decision_quality_reports(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/reports"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision quality report listing failed")
    })
    .await
}

#[tauri::command]
async fn get_decision_quality_trend(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/trends/current"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision quality trend lookup failed")
    })
    .await
}

#[tauri::command]
async fn generate_decision_quality_trend(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/trends"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision quality trend generation failed")
    })
    .await
}

#[tauri::command]
async fn list_decision_quality_trends(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/trends"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision quality trend listing failed")
    })
    .await
}

#[tauri::command]
async fn get_execution_decision_projection(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/decisions/execution-projection"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "execution decision projection lookup failed")
    })
    .await
}

#[tauri::command]
async fn get_execution_decision_influence(
    repository_id: String,
    execution_id: String,
) -> Result<Value, String> {
    offload(move || {
    let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/influence/executions/{execution_id}"
    )).send()
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "execution decision influence lookup failed")
    })
    .await
}

#[tauri::command]
async fn get_decision_influence(
    repository_id: String,
    decision_id: String,
) -> Result<Value, String> {
    offload(move || {
        let response = http_client().get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/influence/decisions/{decision_id}"
    )).send()
    .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "decision influence lookup failed")
    })
    .await
}

#[tauri::command]
async fn list_reasoning_events(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/events"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning event listing failed")
    })
    .await
}

#[tauri::command]
async fn get_reasoning_event(repository_id: String, event_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/events/{event_id}"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning event lookup failed")
    })
    .await
}

#[tauri::command]
async fn create_reasoning_event(repository_id: String, command: Value) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/events"
            ))
            .json(&command)
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning event creation failed")
    })
    .await
}

#[tauri::command]
async fn list_reasoning_manual_capture_templates(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/manual-captures/templates"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning manual-capture template listing failed")
    })
    .await
}

#[tauri::command]
async fn capture_manual_reasoning(repository_id: String, command: Value) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/manual-captures"
            ))
            .json(&command)
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning manual capture failed")
    })
    .await
}

#[tauri::command]
async fn list_reasoning_threads(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/threads"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning thread listing failed")
    })
    .await
}

#[tauri::command]
async fn get_reasoning_thread(repository_id: String, thread_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/threads/{thread_id}"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning thread lookup failed")
    })
    .await
}

#[tauri::command]
async fn create_reasoning_thread(repository_id: String, command: Value) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/threads"
            ))
            .json(&command)
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning thread creation failed")
    })
    .await
}

#[tauri::command]
async fn append_reasoning_thread_event(
    repository_id: String,
    thread_id: String,
    event_id: String,
) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/threads/{thread_id}/events"
        ))
        .json(&serde_json::json!({ "eventId": event_id }))
        .send()
        .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning thread event append failed")
    })
    .await
}

#[tauri::command]
async fn list_reasoning_relationships(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/relationships"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning relationship listing failed")
    })
    .await
}

#[tauri::command]
async fn create_reasoning_relationship(
    repository_id: String,
    command: Value,
) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/relationships"
            ))
            .json(&command)
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning relationship creation failed")
    })
    .await
}

#[tauri::command]
async fn get_reasoning_graph(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/graph"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning graph lookup failed")
    })
    .await
}

#[tauri::command]
async fn trace_reasoning_backward(
    repository_id: String,
    kind: String,
    id: String,
) -> Result<Value, String> {
    offload(move || {
        trace_reasoning(
            repository_id,
            kind,
            id,
            "backward",
            "reasoning backward trace failed",
        )
    })
    .await
}

#[tauri::command]
async fn trace_reasoning_forward(
    repository_id: String,
    kind: String,
    id: String,
) -> Result<Value, String> {
    offload(move || {
        trace_reasoning(
            repository_id,
            kind,
            id,
            "forward",
            "reasoning forward trace failed",
        )
    })
    .await
}

#[tauri::command]
async fn query_reasoning(repository_id: String, query: Value) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/queries"
            ))
            .json(&query)
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning query failed")
    })
    .await
}

#[tauri::command]
async fn reconstruct_reasoning(repository_id: String, query: Value) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/reconstructions"
            ))
            .json(&query)
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning reconstruction failed")
    })
    .await
}

#[tauri::command]
async fn run_reasoning_reconstruction(
    repository_id: String,
    query: Value,
) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/reconstructions/reports"
            ))
            .json(&query)
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning reconstruction run failed")
    })
    .await
}

#[tauri::command]
async fn list_reasoning_reconstructions(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/reconstructions"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning reconstruction report listing failed")
    })
    .await
}

#[tauri::command]
async fn get_reasoning_materialization_review(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/materialization-review"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning materialization review lookup failed")
    })
    .await
}

#[tauri::command]
async fn run_reasoning_materialization_review(
    repository_id: String,
    request: Value,
) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/materialization-review"
            ))
            .json(&request)
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning materialization review failed")
    })
    .await
}

#[tauri::command]
async fn get_reasoning_certification(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/certification"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning certification lookup failed")
    })
    .await
}

#[tauri::command]
async fn run_reasoning_certification(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/certification"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning certification run failed")
    })
    .await
}

#[tauri::command]
async fn list_reasoning_certification_reports(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/certification/reports"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "reasoning certification report listing failed")
    })
    .await
}

#[tauri::command]
async fn get_continuity_diagnostics(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/continuity/diagnostics"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "continuity diagnostics lookup failed")
    })
    .await
}

#[tauri::command]
async fn generate_continuity_report(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/continuity/reports"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "continuity report generation failed")
    })
    .await
}

#[tauri::command]
async fn list_continuity_reports(repository_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/continuity/reports"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "continuity report listing failed")
    })
    .await
}

#[tauri::command]
async fn get_workflow_projection(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow"),
            "workflow projection lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_diagnostics(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/diagnostics"),
            "workflow diagnostics lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_timeline(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/timeline"),
            "workflow timeline lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_history(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/history"),
            "workflow history lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_transitions(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/transitions"),
            "workflow transitions lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_gates(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/gates"),
            "workflow gates lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_gate_history(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/gates/history"),
            "workflow gate history lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_recovery(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/recovery"),
            "workflow recovery lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn recover_workflow(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!("/api/repositories/{repository_id}/workflow/recover"),
            "workflow recovery failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_execution(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/execution"),
            "workflow execution lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_handoff(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/handoff"),
            "workflow handoff lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_decisions(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/decisions"),
            "workflow decisions lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_operational_context(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/operational-context"),
            "workflow operational context lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_git(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/git"),
            "workflow git lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_continuation_evaluation(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/continuation/evaluation"),
            "workflow continuation evaluation lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn run_workflow_continuation(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!("/api/repositories/{repository_id}/workflow/continuation/run"),
            "workflow continuation failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_continuation_history(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/continuation/history"),
            "workflow continuation history lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_preparation_evaluation(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/preparation/evaluation"),
            "workflow preparation evaluation lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn run_workflow_preparation(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!("/api/repositories/{repository_id}/workflow/preparation/run"),
            "workflow preparation failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_preparation_history(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/preparation/history"),
            "workflow preparation history lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_health(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/health"),
            "workflow health lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_repository_workflow_report(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/reports/repository"),
            "repository workflow report lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_progression_report(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/reports/progression"),
            "workflow progression report lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_human_governance_report(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/reports/human-governance"),
            "workflow human governance report lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_readiness_report(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/reports/readiness"),
            "workflow readiness report lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_workflow_certification(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/workflow/certification"),
            "workflow certification lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn run_workflow_certification(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!("/api/repositories/{repository_id}/workflow/certification"),
            "workflow certification failed",
        )
    })
    .await
}

#[tauri::command]
async fn list_decision_sessions(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions"),
            "decision-session list lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_active_decision_session(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/active"),
            "active decision-session lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_diagnostics(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/diagnostics"),
            "decision-session diagnostics lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_metrics(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/analysis/metrics"),
            "decision-session metrics lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_statistics(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/analysis/statistics"),
            "decision-session statistics lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_economics(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/analysis/economics"),
            "decision-session economics lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_coherence(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/analysis/coherence"),
            "decision-session coherence lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_analysis_diagnostics(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/analysis/diagnostics"),
            "decision-session analysis diagnostics lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_lifecycle_policy(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/policy"),
            "decision-session lifecycle policy lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_lifecycle_policy_diagnostics(
    repository_id: String,
) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!(
                "/api/repositories/{repository_id}/decision-sessions/lifecycle/policy/diagnostics"
            ),
            "decision-session lifecycle policy diagnostics lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_transfer_eligibility(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/eligibility"),
            "decision-session transfer eligibility lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_transfer_eligibility_diagnostics(
    repository_id: String,
) -> Result<Value, String> {
    offload(move || {
    backend_get_value(
        &format!(
            "/api/repositories/{repository_id}/decision-sessions/lifecycle/eligibility/diagnostics"
        ),
        "decision-session transfer eligibility diagnostics lookup failed",
    )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_lifecycle_projection(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/projection"),
            "decision-session lifecycle projection lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_lifecycle_history(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/history"),
            "decision-session lifecycle history lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_lifecycle_influence(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/influence"),
            "decision-session lifecycle influence lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_lifecycle_health(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/health"),
            "decision-session lifecycle health lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn list_decision_session_continuity_artifacts(
    repository_id: String,
) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/continuity-artifacts"),
            "decision-session continuity artifact list lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_continuity_artifact(
    repository_id: String,
    artifact_id: String,
) -> Result<Value, String> {
    offload(move || {
    backend_get_value(
        &format!(
            "/api/repositories/{repository_id}/decision-sessions/continuity-artifacts/{artifact_id}"
        ),
        "decision-session continuity artifact lookup failed",
    )
    })
    .await
}

#[tauri::command]
async fn list_decision_session_transfers(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/transfers"),
            "decision-session transfer list lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn list_decision_session_transfer_history(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/transfers/history"),
            "decision-session transfer history lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_transfer_diagnostics(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/transfers/diagnostics"),
            "decision-session transfer diagnostics lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn execute_decision_session_transfer(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/transfers"),
            "decision-session transfer execution failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_recovery(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/recovery"),
            "decision-session recovery lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn list_decision_session_recovery_history(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/recovery/history"),
            "decision-session recovery history lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_recovery_diagnostics(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/recovery/diagnostics"),
            "decision-session recovery diagnostics lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn recover_decision_session(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/recovery"),
            "decision-session persisted recovery failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_workflow(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/workflow"),
            "decision-session workflow projection lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_workflow_summary(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/workflow/summary"),
            "decision-session workflow summary lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_workflow_health(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/workflow/health"),
            "decision-session workflow health lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_workflow_influence(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/workflow/influence"),
            "decision-session workflow influence lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_certification(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/certification"),
            "decision-session certification lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_decision_session_certification_report(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/certification/report"),
            "decision-session certification report lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn run_decision_session_certification(repository_id: String) -> Result<Value, String> {
    offload(move || {
        backend_post_value(
            &format!("/api/repositories/{repository_id}/decision-sessions/certification"),
            "decision-session certification failed",
        )
    })
    .await
}

#[tauri::command]
async fn start_execution(repository_id: String) -> Result<ExecutionSessionSummary, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/execution/start"
            ))
            .json(&serde_json::json!({}))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "execution start failed")
    })
    .await
}

#[tauri::command]
async fn cancel_execution(repository_id: String) -> Result<ExecutionSessionSummary, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/execution/cancel"
            ))
            .json(&serde_json::json!({}))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "execution cancel failed")
    })
    .await
}

#[tauri::command]
async fn get_active_execution(repository_id: String) -> Result<ExecutionSessionSummary, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/execution/active"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "active execution lookup failed")
    })
    .await
}

#[tauri::command]
async fn get_git_status(repository_id: String) -> Result<RepositoryGitStatus, String> {
    offload(move || {
        let response = http_client()
            .get(format!(
                "{BACKEND_URL}/api/repositories/{repository_id}/git/status"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "git status lookup failed")
    })
    .await
}

#[tauri::command]
async fn prepare_commit(session_id: String) -> Result<CommitPreparation, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/execution-sessions/{session_id}/git/prepare-commit"
            ))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "commit preparation failed")
    })
    .await
}

#[tauri::command]
async fn get_execution_git_eligibility(
    session_id: String,
    commit_message: Option<String>,
    selected_paths: Vec<String>,
) -> Result<Value, String> {
    offload(move || {
        backend_post_json_value(
            &format!("/api/execution-sessions/{session_id}/git/eligibility"),
            &ExecutionGitActionEligibilityRequest {
                commit_message,
                selected_paths,
            },
            "execution git eligibility lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn commit_execution(
    session_id: String,
    message: String,
    selected_paths: Vec<String>,
    status_snapshot_id: String,
) -> Result<ExecutionSessionSummary, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/execution-sessions/{session_id}/git/commit"
            ))
            .json(&CommitRequest {
                message,
                selected_paths,
                status_snapshot_id,
            })
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "commit failed")
    })
    .await
}

#[tauri::command]
async fn push_execution(session_id: String) -> Result<PushAttemptResult, String> {
    offload(move || {
        let client = http_client();
        let response = client
            .post(format!(
                "{BACKEND_URL}/api/execution-sessions/{session_id}/git/push"
            ))
            .json(&PushRequest {})
            .send()
            .map_err(|error| error.to_string())?;

        let status = response.status();
        let body = response.text().map_err(|error| error.to_string())?;
        if status.is_success() {
            return serde_json::from_str(&body).map_err(|error| error.to_string());
        }

        if status == reqwest::StatusCode::CONFLICT {
            if let Ok(result) = serde_json::from_str::<PushAttemptResult>(&body) {
                return Ok(result);
            }
        }

        let message = serde_json::from_str::<ErrorResponse>(&body)
            .map(|response| response.error)
            .unwrap_or_else(|_| format!("push failed with status {status}"));

        Err(message)
    })
    .await
}

#[tauri::command]
async fn get_execution_session(session_id: String) -> Result<Value, String> {
    offload(move || {
        let response = http_client()
            .get(format!("{BACKEND_URL}/api/execution-sessions/{session_id}"))
            .send()
            .map_err(|error| error.to_string())?;

        if response.status().is_success() {
            return response.json().map_err(|error| error.to_string());
        }

        response_error(response, "execution session lookup failed")
    })
    .await
}

#[tauri::command]
async fn get_execution_prompt_manifest(session_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/execution-sessions/{session_id}/prompt"),
            "execution prompt manifest lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn get_execution_transparency(session_id: String) -> Result<Value, String> {
    offload(move || {
        backend_get_value(
            &format!("/api/execution-sessions/{session_id}/transparency"),
            "execution transparency lookup failed",
        )
    })
    .await
}

#[tauri::command]
async fn accept_execution_handoff(session_id: String) -> Result<ExecutionSessionSummary, String> {
    offload(move || complete_handoff_decision(session_id, "accept")).await
}

#[tauri::command]
async fn reject_execution_handoff(session_id: String) -> Result<ExecutionSessionSummary, String> {
    offload(move || complete_handoff_decision(session_id, "reject")).await
}

fn rotate_artifact(
    repository_id: String,
    operation: &str,
) -> Result<RepositoryWorkspaceProjection, String> {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/artifacts/{operation}"
        ))
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    let status = response.status();
    let message = response
        .json::<ErrorResponse>()
        .map(|response| response.error)
        .unwrap_or_else(|_| format!("artifact rotation failed with status {status}"));

    Err(message)
}

fn complete_handoff_decision(
    session_id: String,
    operation: &str,
) -> Result<ExecutionSessionSummary, String> {
    let client = http_client();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/execution-sessions/{session_id}/{operation}"
        ))
        .json(&ExecutionAcceptanceRequest {
            decision_note: None,
        })
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "handoff decision failed")
}

fn trace_reasoning(
    repository_id: String,
    kind: String,
    id: String,
    direction: &str,
    fallback: &str,
) -> Result<Value, String> {
    let client = http_client();
    let response = client
        .get(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/trace/{direction}"
        ))
        .query(&[("kind", kind), ("id", id)])
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, fallback)
}

fn decision_candidate_transition(
    repository_id: String,
    candidate_id: String,
    operation: &str,
    body: Value,
    fallback: &str,
) -> Result<Value, String> {
    backend_post_json_value(
        &format!(
            "/api/repositories/{repository_id}/decisions/candidates/{candidate_id}/{operation}"
        ),
        &body,
        fallback,
    )
}

fn decision_proposal_transition(
    repository_id: String,
    proposal_id: String,
    operation: &str,
    body: Value,
    fallback: &str,
) -> Result<Value, String> {
    backend_post_json_value(
        &format!("/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/{operation}"),
        &body,
        fallback,
    )
}

fn backend_get_value(path: &str, fallback: &str) -> Result<Value, String> {
    backend_get_value_from(BACKEND_URL, path, fallback)
}

fn backend_get_value_from(base_url: &str, path: &str, fallback: &str) -> Result<Value, String> {
    let response = http_client()
        .get(format!("{base_url}{path}"))
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, fallback)
}

fn backend_post_value(path: &str, fallback: &str) -> Result<Value, String> {
    let client = http_client();
    let response = client
        .post(format!("{BACKEND_URL}{path}"))
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, fallback)
}

fn backend_post_json_value<T: Serialize>(
    path: &str,
    body: &T,
    fallback: &str,
) -> Result<Value, String> {
    let client = http_client();
    let response = client
        .post(format!("{BACKEND_URL}{path}"))
        .json(body)
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, fallback)
}

fn response_error<T>(response: reqwest::blocking::Response, fallback: &str) -> Result<T, String> {
    let status = response.status();
    let message = match response.json::<ErrorResponse>() {
        Ok(error_response) if error_response.boundary_violation.is_some() => {
            serde_json::to_string(&error_response).unwrap_or_else(|_| error_response.error)
        }
        Ok(error_response) => error_response.error,
        Err(_) => format!("{fallback} with status {status}"),
    };

    Err(message)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::{
        io::{Read, Write},
        net::TcpListener,
        thread,
    };

    #[test]
    fn backend_get_value_relays_opaque_json_without_interpretation() {
        let expected = json!({
            "id": "opaque-response",
            "unknownField": {
                "nested": [
                    { "kind": "backend-owned-enum-like-string", "value": "NeedsReview" },
                    { "kind": "null-carrier", "value": null },
                    { "kind": "empty-array-carrier", "value": [] }
                ],
                "emptyObject": {}
            },
            "items": [
                "alpha",
                null,
                { "semanticStatus": "BackendOwnsThis" }
            ],
            "emptyString": "",
            "emptyArray": [],
            "explicitNull": null
        });
        let base_url = serve_response_once("200 OK", expected.to_string());

        let actual = backend_get_value_from(&base_url, "/opaque", "opaque lookup failed")
            .expect("opaque JSON response should relay");

        assert_eq!(actual, expected);
    }

    #[test]
    fn backend_get_value_preserves_boundary_violation_error_envelope() {
        let expected = json!({
            "error": "architectural boundary violation",
            "boundaryViolation": {
                "boundary": "transport",
                "invariant": "passive-error-relay",
                "details": {
                    "unknownBackendField": "preserve-me",
                    "explicitNull": null,
                    "emptyArray": []
                }
            }
        });
        let base_url = serve_response_once("409 Conflict", expected.to_string());

        let actual = backend_get_value_from(&base_url, "/opaque", "opaque lookup failed")
            .expect_err("backend error envelope should be relayed as an error");
        let actual: Value =
            serde_json::from_str(&actual).expect("boundary violation error should remain JSON");

        assert_eq!(actual, expected);
    }

    fn serve_response_once(status: &str, body: String) -> String {
        let listener = TcpListener::bind("127.0.0.1:0").expect("test server should bind");
        let address = listener
            .local_addr()
            .expect("test server should have address");
        let status = status.to_string();

        thread::spawn(move || {
            let (mut stream, _) = listener
                .accept()
                .expect("test server should accept request");
            let mut buffer = [0; 1024];
            let _ = stream.read(&mut buffer);
            let response = format!(
                "HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
                body.len(),
                body
            );
            stream
                .write_all(response.as_bytes())
                .expect("test server should write response");
        });

        format!("http://{address}")
    }
}

fn backend_executable_path() -> Result<PathBuf, String> {
    if let Ok(path) = env::var("COMMAND_CENTER_BACKEND_PATH") {
        let path = PathBuf::from(path);
        if path.exists() {
            return Ok(path);
        }

        return Err(format!(
            "COMMAND_CENTER_BACKEND_PATH points to a missing file: {}",
            path.display()
        ));
    }

    let executable_name = if cfg!(windows) {
        "CommandCenter.Backend.exe"
    } else {
        "CommandCenter.Backend"
    };

    let current_dir = env::current_dir().map_err(|error| error.to_string())?;
    let development_path = current_dir
        .join("../CommandCenter.Backend/bin/Debug/net10.0")
        .join(executable_name);

    if development_path.exists() {
        return Ok(development_path);
    }

    Err(format!(
        "backend executable was not found at {}",
        development_path.display()
    ))
}

fn start_backend() -> Result<Child, String> {
    let backend_path = backend_executable_path()?;
    let backend_dir = backend_path
        .parent()
        .ok_or_else(|| "backend executable has no parent directory".to_string())?;

    let mut command = Command::new(&backend_path);
    command
        .arg("--urls")
        .arg(BACKEND_URL)
        .current_dir(backend_dir)
        .env("ASPNETCORE_ENVIRONMENT", "Development");

    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x08000000);
    }

    let mut child = command.spawn().map_err(|error| {
        format!(
            "failed to start backend at {}: {error}",
            backend_path.display()
        )
    })?;

    if let Err(error) = wait_for_backend(&mut child) {
        let _ = child.kill();
        let _ = child.wait();
        return Err(error);
    }

    Ok(child)
}

fn wait_for_backend(child: &mut Child) -> Result<(), String> {
    // Poll readiness on a tight 100ms interval (not a coarse 250ms one) so that a
    // backend which now binds Kestrel immediately is detected within ~100ms instead
    // of being held back by up to a full poll period. The first ping happens before
    // any sleep, so an already-up backend adds zero shell-side latency. The bounded
    // overall timeout is preserved: 100 attempts * 100ms = the same 10 seconds, and a
    // real sleep between polls keeps this from busy-spinning. ping_backend() reuses
    // the shared http_client() connection pool.
    for _ in 0..100 {
        if let Some(status) = child.try_wait().map_err(|error| error.to_string())? {
            return Err(format!("backend exited before becoming ready: {status}"));
        }

        if ping_backend_request().is_ok() {
            return Ok(());
        }

        thread::sleep(Duration::from_millis(100));
    }

    Err("backend did not become ready within 10 seconds".to_string())
}

fn main() {
    tauri::Builder::default()
        .setup(|app| {
            let backend = start_backend().map_err(|error| {
                Box::<dyn std::error::Error>::from(std::io::Error::other(error))
            })?;

            app.manage(BackendProcess {
                child: Mutex::new(Some(backend)),
            });

            Ok(())
        })
        .on_window_event(|window, event| {
            if matches!(event, tauri::WindowEvent::CloseRequested { .. }) {
                window.state::<BackendProcess>().stop();
            }
        })
        .invoke_handler(tauri::generate_handler![
            ping_backend,
            get_backend_url,
            select_repository_directory,
            list_repositories,
            register_repository,
            remove_repository,
            get_repository_workspace,
            refresh_repository_workspace,
            load_artifact_content,
            save_artifact_content,
            rotate_current_handoff,
            rotate_current_decisions,
            preview_execution_context,
            get_plan_status,
            write_plan,
            revise_plan,
            execute_plan,
            decision_run,
            decision_submit,
            generate_operational_context_proposal,
            list_operational_context_proposals,
            get_operational_context_proposal,
            edit_operational_context_proposal,
            accept_operational_context_proposal,
            reject_operational_context_proposal,
            promote_operational_context_proposal,
            get_decision_context,
            build_decision_context,
            list_decision_candidates,
            get_decision_lifecycle_eligibility,
            discover_decisions,
            promote_decision_candidate,
            dismiss_decision_candidate,
            expire_decision_candidate,
            mark_decision_candidate_duplicate,
            list_decision_proposals,
            list_decision_proposal_browser,
            get_decision_proposal,
            generate_decision_proposal,
            get_decision_proposal_review,
            expire_decision_proposal,
            discard_decision_proposal,
            mark_decision_proposal_viewed,
            mark_decision_proposal_needs_refinement,
            mark_decision_proposal_ready_for_resolution,
            get_decision_proposal_lineage,
            refine_decision_proposal,
            analyze_decision_refinement,
            regenerate_decision_refinement,
            resolve_decision_proposal,
            supersede_decision,
            archive_decision,
            get_decision_assimilation_recommendation,
            propose_decision_operational_context_assimilation,
            get_decision_option_comparison,
            get_decision_evidence_inspection,
            list_decision_source_attributions,
            get_decision_governance,
            generate_decision_governance_report,
            list_decision_governance_reports,
            get_decision_certification,
            run_decision_certification,
            list_decision_certification_reports,
            get_decision_generation_certification,
            run_decision_generation_certification,
            list_decision_generation_certification_reports,
            assess_decision_quality,
            list_decision_quality_assessments,
            get_decision_quality_report,
            generate_decision_quality_report,
            list_decision_quality_reports,
            get_decision_quality_trend,
            generate_decision_quality_trend,
            list_decision_quality_trends,
            get_execution_decision_projection,
            get_execution_decision_influence,
            get_decision_influence,
            list_reasoning_events,
            get_reasoning_event,
            create_reasoning_event,
            list_reasoning_manual_capture_templates,
            capture_manual_reasoning,
            list_reasoning_threads,
            get_reasoning_thread,
            create_reasoning_thread,
            append_reasoning_thread_event,
            list_reasoning_relationships,
            create_reasoning_relationship,
            get_reasoning_graph,
            trace_reasoning_backward,
            trace_reasoning_forward,
            query_reasoning,
            reconstruct_reasoning,
            run_reasoning_reconstruction,
            list_reasoning_reconstructions,
            get_reasoning_materialization_review,
            run_reasoning_materialization_review,
            get_reasoning_certification,
            run_reasoning_certification,
            list_reasoning_certification_reports,
            get_continuity_diagnostics,
            generate_continuity_report,
            list_continuity_reports,
            get_workflow_projection,
            get_workflow_diagnostics,
            get_workflow_timeline,
            get_workflow_history,
            get_workflow_transitions,
            get_workflow_gates,
            get_workflow_gate_history,
            get_workflow_recovery,
            recover_workflow,
            get_workflow_execution,
            get_workflow_handoff,
            get_workflow_decisions,
            get_workflow_operational_context,
            get_workflow_git,
            get_workflow_continuation_evaluation,
            run_workflow_continuation,
            get_workflow_continuation_history,
            get_workflow_preparation_evaluation,
            run_workflow_preparation,
            get_workflow_preparation_history,
            get_workflow_health,
            get_repository_workflow_report,
            get_workflow_progression_report,
            get_workflow_human_governance_report,
            get_workflow_readiness_report,
            get_workflow_certification,
            run_workflow_certification,
            list_decision_sessions,
            get_active_decision_session,
            get_decision_session_diagnostics,
            get_decision_session_metrics,
            get_decision_session_statistics,
            get_decision_session_economics,
            get_decision_session_coherence,
            get_decision_session_analysis_diagnostics,
            get_decision_session_lifecycle_policy,
            get_decision_session_lifecycle_policy_diagnostics,
            get_decision_session_transfer_eligibility,
            get_decision_session_transfer_eligibility_diagnostics,
            get_decision_session_lifecycle_projection,
            get_decision_session_lifecycle_history,
            get_decision_session_lifecycle_influence,
            get_decision_session_lifecycle_health,
            list_decision_session_continuity_artifacts,
            get_decision_session_continuity_artifact,
            list_decision_session_transfers,
            list_decision_session_transfer_history,
            get_decision_session_transfer_diagnostics,
            execute_decision_session_transfer,
            get_decision_session_recovery,
            list_decision_session_recovery_history,
            get_decision_session_recovery_diagnostics,
            recover_decision_session,
            get_decision_session_workflow,
            get_decision_session_workflow_summary,
            get_decision_session_workflow_health,
            get_decision_session_workflow_influence,
            get_decision_session_certification,
            get_decision_session_certification_report,
            run_decision_session_certification,
            start_execution,
            cancel_execution,
            get_active_execution,
            get_git_status,
            get_execution_git_eligibility,
            prepare_commit,
            commit_execution,
            push_execution,
            get_execution_session,
            get_execution_prompt_manifest,
            get_execution_transparency,
            accept_execution_handoff,
            reject_execution_handoff
        ])
        .run(tauri::generate_context!())
        .expect("failed to run Command Center shell");
}

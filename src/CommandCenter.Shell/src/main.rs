use serde::{Deserialize, Serialize};
use serde_json::{Value, json};
use std::{
    env,
    path::PathBuf,
    process::{Child, Command},
    sync::Mutex,
    thread,
    time::Duration,
};
use tauri::Manager;

const BACKEND_URL: &str = "http://127.0.0.1:5000";

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
struct RepositoryWorkspaceProjection {
    repository: Repository,
    availability: String,
    readiness: String,
    execution_state: String,
    execution_summary: Option<ExecutionSessionSummary>,
    execution_history: Vec<ExecutionSessionSummary>,
    artifact_inventory: ArtifactInventory,
    milestone_count: i32,
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

#[tauri::command]
fn ping_backend() -> Result<String, String> {
    reqwest::blocking::get(format!("{BACKEND_URL}/api/ping"))
        .map_err(|error| error.to_string())?
        .text()
        .map_err(|error| error.to_string())
}

#[tauri::command]
fn get_backend_url() -> String {
    BACKEND_URL.to_string()
}

#[tauri::command]
fn select_repository_directory() -> Option<String> {
    rfd::FileDialog::new()
        .set_title("Select Repository")
        .pick_folder()
        .map(|path| path.display().to_string())
}

#[tauri::command]
fn list_repositories() -> Result<Vec<RepositoryDashboardProjection>, String> {
    reqwest::blocking::get(format!("{BACKEND_URL}/api/repositories"))
        .map_err(|error| error.to_string())?
        .error_for_status()
        .map_err(|error| error.to_string())?
        .json()
        .map_err(|error| error.to_string())
}

#[tauri::command]
fn register_repository(path: String) -> Result<(), String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn remove_repository(repository_id: String) -> Result<(), String> {
    let client = reqwest::blocking::Client::new();
    client
        .delete(format!("{BACKEND_URL}/api/repositories/{repository_id}"))
        .send()
        .map_err(|error| error.to_string())?
        .error_for_status()
        .map_err(|error| error.to_string())?;

    Ok(())
}

#[tauri::command]
fn get_repository_workspace(
    repository_id: String,
) -> Result<RepositoryWorkspaceProjection, String> {
    reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/workspace"
    ))
    .map_err(|error| error.to_string())?
    .error_for_status()
    .map_err(|error| error.to_string())?
    .json()
    .map_err(|error| error.to_string())
}

#[tauri::command]
fn refresh_repository_workspace(
    repository_id: String,
) -> Result<RepositoryWorkspaceProjection, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn load_artifact_content(repository_id: String, relative_path: String) -> Result<String, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn save_artifact_content(
    repository_id: String,
    relative_path: String,
    content: String,
) -> Result<(), String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn rotate_current_handoff(repository_id: String) -> Result<RepositoryWorkspaceProjection, String> {
    rotate_artifact(repository_id, "rotate-current-handoff")
}

#[tauri::command]
fn rotate_current_decisions(
    repository_id: String,
) -> Result<RepositoryWorkspaceProjection, String> {
    rotate_artifact(repository_id, "rotate-current-decisions")
}

#[tauri::command]
fn preview_execution_context(repository_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn get_plan_status(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/plan/status"),
        "plan status lookup failed",
    )
}

#[tauri::command]
fn write_plan(
    repository_id: String,
    roadmap: String,
    specs: Vec<String>,
    new_codebase: bool,
) -> Result<Value, String> {
    backend_post_json_value(
        &format!("/api/repositories/{repository_id}/plan/write"),
        &json!({
            "roadmap": roadmap,
            "specs": specs,
            "newCodebase": new_codebase,
        }),
        "plan write failed",
    )
}

#[tauri::command]
fn revise_plan(repository_id: String, feedback: String) -> Result<Value, String> {
    backend_post_json_value(
        &format!("/api/repositories/{repository_id}/plan/revise"),
        &json!({ "feedback": feedback }),
        "plan revision failed",
    )
}

#[tauri::command]
fn execute_plan(repository_id: String) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/plan/execute"),
        "plan execution failed",
    )
}

#[tauri::command]
fn decision_run(repository_id: String) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/decision/run"),
        "decision run failed",
    )
}

#[tauri::command]
fn decision_submit(repository_id: String, decisions: String) -> Result<Value, String> {
    backend_post_json_value(
        &format!("/api/repositories/{repository_id}/decision/submit"),
        &json!({ "decisions": decisions }),
        "decision submission failed",
    )
}

#[tauri::command]
fn generate_operational_context_proposal(repository_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_operational_context_proposals(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/operational-context/proposals"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "operational-context proposal listing failed")
}

#[tauri::command]
fn get_operational_context_proposal(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/operational-context/proposals/{proposal_id}"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "operational-context proposal lookup failed")
}

#[tauri::command]
fn edit_operational_context_proposal(
    repository_id: String,
    proposal_id: String,
    content: String,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn accept_operational_context_proposal(
    repository_id: String,
    proposal_id: String,
    review_note: Option<String>,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn reject_operational_context_proposal(
    repository_id: String,
    proposal_id: String,
    review_note: Option<String>,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn promote_operational_context_proposal(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn get_decision_context(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/context"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision context lookup failed")
}

#[tauri::command]
fn build_decision_context(repository_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_decision_candidates(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/candidates"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision candidate listing failed")
}

#[tauri::command]
fn get_decision_lifecycle_eligibility(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decisions/lifecycle/eligibility"),
        "decision lifecycle eligibility lookup failed",
    )
}

#[tauri::command]
fn discover_decisions(repository_id: String) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/decisions/discover"),
        "decision discovery failed",
    )
}

#[tauri::command]
fn promote_decision_candidate(
    repository_id: String,
    candidate_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    decision_candidate_transition(
        repository_id,
        candidate_id,
        "promote",
        json!({ "reason": reason }),
        "decision candidate promotion failed",
    )
}

#[tauri::command]
fn dismiss_decision_candidate(
    repository_id: String,
    candidate_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    decision_candidate_transition(
        repository_id,
        candidate_id,
        "dismiss",
        json!({ "reason": reason }),
        "decision candidate dismissal failed",
    )
}

#[tauri::command]
fn expire_decision_candidate(
    repository_id: String,
    candidate_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    decision_candidate_transition(
        repository_id,
        candidate_id,
        "expire",
        json!({ "reason": reason }),
        "decision candidate expiration failed",
    )
}

#[tauri::command]
fn mark_decision_candidate_duplicate(
    repository_id: String,
    candidate_id: String,
    duplicate_of_candidate_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
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
}

#[tauri::command]
fn list_decision_proposals(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision proposal listing failed")
}

#[tauri::command]
fn list_decision_proposal_browser(
    repository_id: String,
    states: Vec<String>,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn get_decision_proposal(repository_id: String, proposal_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision proposal lookup failed")
}

#[tauri::command]
fn generate_decision_proposal(
    repository_id: String,
    candidate_id: String,
) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/decisions/candidates/{candidate_id}/proposals"),
        "decision proposal generation failed",
    )
}

#[tauri::command]
fn get_decision_proposal_review(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/review"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision proposal review lookup failed")
}

#[tauri::command]
fn expire_decision_proposal(
    repository_id: String,
    proposal_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    decision_proposal_transition(
        repository_id,
        proposal_id,
        "expire",
        json!({ "reason": reason }),
        "decision proposal expiration failed",
    )
}

#[tauri::command]
fn discard_decision_proposal(
    repository_id: String,
    proposal_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    decision_proposal_transition(
        repository_id,
        proposal_id,
        "discard",
        json!({ "reason": reason }),
        "decision proposal discard failed",
    )
}

#[tauri::command]
fn mark_decision_proposal_viewed(
    repository_id: String,
    proposal_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    decision_proposal_transition(
        repository_id,
        proposal_id,
        "review/viewed",
        json!({ "reason": reason }),
        "decision proposal viewed transition failed",
    )
}

#[tauri::command]
fn mark_decision_proposal_needs_refinement(
    repository_id: String,
    proposal_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    decision_proposal_transition(
        repository_id,
        proposal_id,
        "review/needs-refinement",
        json!({ "reason": reason }),
        "decision proposal needs-refinement transition failed",
    )
}

#[tauri::command]
fn mark_decision_proposal_ready_for_resolution(
    repository_id: String,
    proposal_id: String,
    reason: Option<String>,
) -> Result<Value, String> {
    decision_proposal_transition(
        repository_id,
        proposal_id,
        "review/ready-for-resolution",
        json!({ "reason": reason }),
        "decision proposal ready-for-resolution transition failed",
    )
}

#[tauri::command]
fn get_decision_proposal_lineage(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/lineage"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision proposal lineage lookup failed")
}

#[tauri::command]
fn refine_decision_proposal(
    repository_id: String,
    proposal_id: String,
    request: Value,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn analyze_decision_refinement(
    repository_id: String,
    proposal_id: String,
    request: Value,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn regenerate_decision_refinement(
    repository_id: String,
    proposal_id: String,
    request: Value,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn resolve_decision_proposal(
    repository_id: String,
    proposal_id: String,
    request: Value,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn supersede_decision(
    repository_id: String,
    decision_id: String,
    request: Value,
) -> Result<Value, String> {
    backend_post_json_value(
        &format!("/api/repositories/{repository_id}/decisions/{decision_id}/supersede"),
        &request,
        "decision supersession failed",
    )
}

#[tauri::command]
fn archive_decision(
    repository_id: String,
    decision_id: String,
    request: Value,
) -> Result<Value, String> {
    backend_post_json_value(
        &format!("/api/repositories/{repository_id}/decisions/{decision_id}/archive"),
        &request,
        "decision archival failed",
    )
}

#[tauri::command]
fn get_decision_assimilation_recommendation(
    repository_id: String,
    decision_id: String,
) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/{decision_id}/assimilation"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(
        response,
        "decision assimilation recommendation lookup failed",
    )
}

#[tauri::command]
fn propose_decision_operational_context_assimilation(
    repository_id: String,
    decision_id: String,
    request: Value,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn get_decision_option_comparison(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/options"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision option comparison lookup failed")
}

#[tauri::command]
fn get_decision_evidence_inspection(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/evidence"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision evidence inspection lookup failed")
}

#[tauri::command]
fn list_decision_source_attributions(
    repository_id: String,
    proposal_id: String,
) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/proposals/{proposal_id}/sources"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision source attribution listing failed")
}

#[tauri::command]
fn get_decision_governance(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/governance"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision governance lookup failed")
}

#[tauri::command]
fn generate_decision_governance_report(repository_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_decision_governance_reports(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/governance/reports"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision governance report listing failed")
}

#[tauri::command]
fn get_decision_certification(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/certification"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision certification lookup failed")
}

#[tauri::command]
fn run_decision_certification(repository_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_decision_certification_reports(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/certification/reports"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision certification report listing failed")
}

#[tauri::command]
fn get_decision_generation_certification(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/generation-certification/current"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision generation certification lookup failed")
}

#[tauri::command]
fn run_decision_generation_certification(repository_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_decision_generation_certification_reports(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/generation-certification/reports"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(
        response,
        "decision generation certification report listing failed",
    )
}

#[tauri::command]
fn assess_decision_quality(repository_id: String, proposal_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_decision_quality_assessments(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/assessments"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision quality assessment listing failed")
}

#[tauri::command]
fn get_decision_quality_report(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/reports/current"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision quality report lookup failed")
}

#[tauri::command]
fn generate_decision_quality_report(repository_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_decision_quality_reports(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/reports"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision quality report listing failed")
}

#[tauri::command]
fn get_decision_quality_trend(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/trends/current"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision quality trend lookup failed")
}

#[tauri::command]
fn generate_decision_quality_trend(repository_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_decision_quality_trends(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/quality/trends"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision quality trend listing failed")
}

#[tauri::command]
fn get_execution_decision_projection(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/execution-projection"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "execution decision projection lookup failed")
}

#[tauri::command]
fn get_execution_decision_influence(
    repository_id: String,
    execution_id: String,
) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/influence/executions/{execution_id}"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "execution decision influence lookup failed")
}

#[tauri::command]
fn get_decision_influence(repository_id: String, decision_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/decisions/influence/decisions/{decision_id}"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "decision influence lookup failed")
}

#[tauri::command]
fn list_reasoning_events(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/events"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning event listing failed")
}

#[tauri::command]
fn get_reasoning_event(repository_id: String, event_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/events/{event_id}"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning event lookup failed")
}

#[tauri::command]
fn create_reasoning_event(repository_id: String, command: Value) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_reasoning_manual_capture_templates(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/manual-captures/templates"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning manual-capture template listing failed")
}

#[tauri::command]
fn capture_manual_reasoning(repository_id: String, command: Value) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_reasoning_threads(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/threads"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning thread listing failed")
}

#[tauri::command]
fn get_reasoning_thread(repository_id: String, thread_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/threads/{thread_id}"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning thread lookup failed")
}

#[tauri::command]
fn create_reasoning_thread(repository_id: String, command: Value) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn append_reasoning_thread_event(
    repository_id: String,
    thread_id: String,
    event_id: String,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_reasoning_relationships(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/relationships"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning relationship listing failed")
}

#[tauri::command]
fn create_reasoning_relationship(repository_id: String, command: Value) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn get_reasoning_graph(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/graph"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning graph lookup failed")
}

#[tauri::command]
fn trace_reasoning_backward(
    repository_id: String,
    kind: String,
    id: String,
) -> Result<Value, String> {
    trace_reasoning(
        repository_id,
        kind,
        id,
        "backward",
        "reasoning backward trace failed",
    )
}

#[tauri::command]
fn trace_reasoning_forward(
    repository_id: String,
    kind: String,
    id: String,
) -> Result<Value, String> {
    trace_reasoning(
        repository_id,
        kind,
        id,
        "forward",
        "reasoning forward trace failed",
    )
}

#[tauri::command]
fn query_reasoning(repository_id: String, query: Value) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn reconstruct_reasoning(repository_id: String, query: Value) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn run_reasoning_reconstruction(repository_id: String, query: Value) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_reasoning_reconstructions(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/reconstructions"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning reconstruction report listing failed")
}

#[tauri::command]
fn get_reasoning_materialization_review(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/materialization-review"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning materialization review lookup failed")
}

#[tauri::command]
fn run_reasoning_materialization_review(
    repository_id: String,
    request: Value,
) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn get_reasoning_certification(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/certification"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning certification lookup failed")
}

#[tauri::command]
fn run_reasoning_certification(repository_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_reasoning_certification_reports(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/reasoning/certification/reports"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "reasoning certification report listing failed")
}

#[tauri::command]
fn get_continuity_diagnostics(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/continuity/diagnostics"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "continuity diagnostics lookup failed")
}

#[tauri::command]
fn generate_continuity_report(repository_id: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn list_continuity_reports(repository_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/continuity/reports"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "continuity report listing failed")
}

#[tauri::command]
fn get_workflow_projection(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow"),
        "workflow projection lookup failed",
    )
}

#[tauri::command]
fn get_workflow_diagnostics(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/diagnostics"),
        "workflow diagnostics lookup failed",
    )
}

#[tauri::command]
fn get_workflow_timeline(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/timeline"),
        "workflow timeline lookup failed",
    )
}

#[tauri::command]
fn get_workflow_history(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/history"),
        "workflow history lookup failed",
    )
}

#[tauri::command]
fn get_workflow_transitions(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/transitions"),
        "workflow transitions lookup failed",
    )
}

#[tauri::command]
fn get_workflow_gates(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/gates"),
        "workflow gates lookup failed",
    )
}

#[tauri::command]
fn get_workflow_gate_history(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/gates/history"),
        "workflow gate history lookup failed",
    )
}

#[tauri::command]
fn get_workflow_recovery(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/recovery"),
        "workflow recovery lookup failed",
    )
}

#[tauri::command]
fn recover_workflow(repository_id: String) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/workflow/recover"),
        "workflow recovery failed",
    )
}

#[tauri::command]
fn get_workflow_execution(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/execution"),
        "workflow execution lookup failed",
    )
}

#[tauri::command]
fn get_workflow_handoff(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/handoff"),
        "workflow handoff lookup failed",
    )
}

#[tauri::command]
fn get_workflow_decisions(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/decisions"),
        "workflow decisions lookup failed",
    )
}

#[tauri::command]
fn get_workflow_operational_context(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/operational-context"),
        "workflow operational context lookup failed",
    )
}

#[tauri::command]
fn get_workflow_git(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/git"),
        "workflow git lookup failed",
    )
}

#[tauri::command]
fn get_workflow_continuation_evaluation(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/continuation/evaluation"),
        "workflow continuation evaluation lookup failed",
    )
}

#[tauri::command]
fn run_workflow_continuation(repository_id: String) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/workflow/continuation/run"),
        "workflow continuation failed",
    )
}

#[tauri::command]
fn get_workflow_continuation_history(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/continuation/history"),
        "workflow continuation history lookup failed",
    )
}

#[tauri::command]
fn get_workflow_preparation_evaluation(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/preparation/evaluation"),
        "workflow preparation evaluation lookup failed",
    )
}

#[tauri::command]
fn run_workflow_preparation(repository_id: String) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/workflow/preparation/run"),
        "workflow preparation failed",
    )
}

#[tauri::command]
fn get_workflow_preparation_history(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/preparation/history"),
        "workflow preparation history lookup failed",
    )
}

#[tauri::command]
fn get_workflow_health(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/health"),
        "workflow health lookup failed",
    )
}

#[tauri::command]
fn get_repository_workflow_report(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/reports/repository"),
        "repository workflow report lookup failed",
    )
}

#[tauri::command]
fn get_workflow_progression_report(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/reports/progression"),
        "workflow progression report lookup failed",
    )
}

#[tauri::command]
fn get_workflow_human_governance_report(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/reports/human-governance"),
        "workflow human governance report lookup failed",
    )
}

#[tauri::command]
fn get_workflow_readiness_report(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/reports/readiness"),
        "workflow readiness report lookup failed",
    )
}

#[tauri::command]
fn get_workflow_certification(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/workflow/certification"),
        "workflow certification lookup failed",
    )
}

#[tauri::command]
fn run_workflow_certification(repository_id: String) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/workflow/certification"),
        "workflow certification failed",
    )
}

#[tauri::command]
fn list_decision_sessions(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions"),
        "decision-session list lookup failed",
    )
}

#[tauri::command]
fn get_active_decision_session(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/active"),
        "active decision-session lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_diagnostics(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/diagnostics"),
        "decision-session diagnostics lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_metrics(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/analysis/metrics"),
        "decision-session metrics lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_statistics(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/analysis/statistics"),
        "decision-session statistics lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_economics(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/analysis/economics"),
        "decision-session economics lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_coherence(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/analysis/coherence"),
        "decision-session coherence lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_analysis_diagnostics(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/analysis/diagnostics"),
        "decision-session analysis diagnostics lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_lifecycle_policy(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/policy"),
        "decision-session lifecycle policy lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_lifecycle_policy_diagnostics(
    repository_id: String,
) -> Result<Value, String> {
    backend_get_value(
        &format!(
            "/api/repositories/{repository_id}/decision-sessions/lifecycle/policy/diagnostics"
        ),
        "decision-session lifecycle policy diagnostics lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_transfer_eligibility(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/eligibility"),
        "decision-session transfer eligibility lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_transfer_eligibility_diagnostics(
    repository_id: String,
) -> Result<Value, String> {
    backend_get_value(
        &format!(
            "/api/repositories/{repository_id}/decision-sessions/lifecycle/eligibility/diagnostics"
        ),
        "decision-session transfer eligibility diagnostics lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_lifecycle_projection(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/projection"),
        "decision-session lifecycle projection lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_lifecycle_history(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/history"),
        "decision-session lifecycle history lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_lifecycle_influence(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/influence"),
        "decision-session lifecycle influence lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_lifecycle_health(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/lifecycle/health"),
        "decision-session lifecycle health lookup failed",
    )
}

#[tauri::command]
fn list_decision_session_continuity_artifacts(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/continuity-artifacts"),
        "decision-session continuity artifact list lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_continuity_artifact(
    repository_id: String,
    artifact_id: String,
) -> Result<Value, String> {
    backend_get_value(
        &format!(
            "/api/repositories/{repository_id}/decision-sessions/continuity-artifacts/{artifact_id}"
        ),
        "decision-session continuity artifact lookup failed",
    )
}

#[tauri::command]
fn list_decision_session_transfers(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/transfers"),
        "decision-session transfer list lookup failed",
    )
}

#[tauri::command]
fn list_decision_session_transfer_history(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/transfers/history"),
        "decision-session transfer history lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_transfer_diagnostics(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/transfers/diagnostics"),
        "decision-session transfer diagnostics lookup failed",
    )
}

#[tauri::command]
fn execute_decision_session_transfer(repository_id: String) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/transfers"),
        "decision-session transfer execution failed",
    )
}

#[tauri::command]
fn get_decision_session_recovery(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/recovery"),
        "decision-session recovery lookup failed",
    )
}

#[tauri::command]
fn list_decision_session_recovery_history(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/recovery/history"),
        "decision-session recovery history lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_recovery_diagnostics(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/recovery/diagnostics"),
        "decision-session recovery diagnostics lookup failed",
    )
}

#[tauri::command]
fn recover_decision_session(repository_id: String) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/recovery"),
        "decision-session persisted recovery failed",
    )
}

#[tauri::command]
fn get_decision_session_workflow(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/workflow"),
        "decision-session workflow projection lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_workflow_summary(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/workflow/summary"),
        "decision-session workflow summary lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_workflow_health(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/workflow/health"),
        "decision-session workflow health lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_workflow_influence(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/workflow/influence"),
        "decision-session workflow influence lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_certification(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/certification"),
        "decision-session certification lookup failed",
    )
}

#[tauri::command]
fn get_decision_session_certification_report(repository_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/certification/report"),
        "decision-session certification report lookup failed",
    )
}

#[tauri::command]
fn run_decision_session_certification(repository_id: String) -> Result<Value, String> {
    backend_post_value(
        &format!("/api/repositories/{repository_id}/decision-sessions/certification"),
        "decision-session certification failed",
    )
}

#[tauri::command]
fn start_execution(repository_id: String) -> Result<ExecutionSessionSummary, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn get_active_execution(repository_id: String) -> Result<ExecutionSessionSummary, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/execution/active"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "active execution lookup failed")
}

#[tauri::command]
fn get_git_status(repository_id: String) -> Result<RepositoryGitStatus, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/repositories/{repository_id}/git/status"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "git status lookup failed")
}

#[tauri::command]
fn prepare_commit(session_id: String) -> Result<CommitPreparation, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn get_execution_git_eligibility(
    session_id: String,
    commit_message: Option<String>,
    selected_paths: Vec<String>,
) -> Result<Value, String> {
    backend_post_json_value(
        &format!("/api/execution-sessions/{session_id}/git/eligibility"),
        &ExecutionGitActionEligibilityRequest {
            commit_message,
            selected_paths,
        },
        "execution git eligibility lookup failed",
    )
}

#[tauri::command]
fn commit_execution(
    session_id: String,
    message: String,
    selected_paths: Vec<String>,
    status_snapshot_id: String,
) -> Result<ExecutionSessionSummary, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn push_execution(session_id: String) -> Result<PushAttemptResult, String> {
    let client = reqwest::blocking::Client::new();
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
}

#[tauri::command]
fn get_execution_session(session_id: String) -> Result<Value, String> {
    let response =
        reqwest::blocking::get(format!("{BACKEND_URL}/api/execution-sessions/{session_id}"))
            .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "execution session lookup failed")
}

#[tauri::command]
fn get_execution_prompt_manifest(session_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/execution-sessions/{session_id}/prompt"),
        "execution prompt manifest lookup failed",
    )
}

#[tauri::command]
fn get_execution_transparency(session_id: String) -> Result<Value, String> {
    backend_get_value(
        &format!("/api/execution-sessions/{session_id}/transparency"),
        "execution transparency lookup failed",
    )
}

#[tauri::command]
fn accept_execution_handoff(session_id: String) -> Result<ExecutionSessionSummary, String> {
    complete_handoff_decision(session_id, "accept")
}

#[tauri::command]
fn reject_execution_handoff(session_id: String) -> Result<ExecutionSessionSummary, String> {
    complete_handoff_decision(session_id, "reject")
}

fn rotate_artifact(
    repository_id: String,
    operation: &str,
) -> Result<RepositoryWorkspaceProjection, String> {
    let client = reqwest::blocking::Client::new();
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
    let client = reqwest::blocking::Client::new();
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
    let client = reqwest::blocking::Client::new();
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
    let response =
        reqwest::blocking::get(format!("{base_url}{path}")).map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, fallback)
}

fn backend_post_value(path: &str, fallback: &str) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
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
    let client = reqwest::blocking::Client::new();
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
    for _ in 0..40 {
        if let Some(status) = child.try_wait().map_err(|error| error.to_string())? {
            return Err(format!("backend exited before becoming ready: {status}"));
        }

        if ping_backend().is_ok() {
            return Ok(());
        }

        thread::sleep(Duration::from_millis(250));
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

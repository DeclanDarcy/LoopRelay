use serde::{Deserialize, Serialize};
use serde_json::Value;
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
struct ExecutionSessionSummary {
    session_id: String,
    state: String,
    repository_state: String,
    milestone_path: Option<String>,
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
struct ExecutionStartRequest {
    milestone_path: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ExecutionAcceptanceRequest {
    decision_note: Option<String>,
}

#[derive(Deserialize)]
struct ErrorResponse {
    error: String,
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
fn get_repository_workspace(repository_id: String) -> Result<RepositoryWorkspaceProjection, String> {
    reqwest::blocking::get(format!("{BACKEND_URL}/api/repositories/{repository_id}/workspace"))
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
        .post(format!("{BACKEND_URL}/api/repositories/{repository_id}/refresh"))
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
fn rotate_current_decisions(repository_id: String) -> Result<RepositoryWorkspaceProjection, String> {
    rotate_artifact(repository_id, "rotate-current-decisions")
}

#[tauri::command]
fn preview_execution_context(repository_id: String, milestone_path: String) -> Result<Value, String> {
    let client = reqwest::blocking::Client::new();
    client
        .get(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/execution/context"
        ))
        .query(&[("milestonePath", milestone_path)])
        .send()
        .map_err(|error| error.to_string())?
        .error_for_status()
        .map_err(|error| error.to_string())?
        .json()
        .map_err(|error| error.to_string())
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

    response_error(response, "decision assimilation recommendation lookup failed")
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

    response_error(response, "decision assimilation recommendation creation failed")
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
fn start_execution(
    repository_id: String,
    milestone_path: String,
) -> Result<ExecutionSessionSummary, String> {
    let client = reqwest::blocking::Client::new();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/repositories/{repository_id}/execution/start"
        ))
        .json(&ExecutionStartRequest { milestone_path })
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
fn push_execution(session_id: String) -> Result<ExecutionSessionSummary, String> {
    let client = reqwest::blocking::Client::new();
    let response = client
        .post(format!(
            "{BACKEND_URL}/api/execution-sessions/{session_id}/git/push"
        ))
        .json(&PushRequest {})
        .send()
        .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "push failed")
}

#[tauri::command]
fn get_execution_session(session_id: String) -> Result<Value, String> {
    let response = reqwest::blocking::get(format!(
        "{BACKEND_URL}/api/execution-sessions/{session_id}"
    ))
    .map_err(|error| error.to_string())?;

    if response.status().is_success() {
        return response.json().map_err(|error| error.to_string());
    }

    response_error(response, "execution session lookup failed")
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

fn response_error<T>(response: reqwest::blocking::Response, fallback: &str) -> Result<T, String> {
    let status = response.status();
    let message = response
        .json::<ErrorResponse>()
        .map(|response| response.error)
        .unwrap_or_else(|_| format!("{fallback} with status {status}"));

    Err(message)
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
            list_decision_proposals,
            list_decision_proposal_browser,
            get_decision_proposal,
            get_decision_proposal_review,
            get_decision_proposal_lineage,
            refine_decision_proposal,
            resolve_decision_proposal,
            get_decision_assimilation_recommendation,
            propose_decision_operational_context_assimilation,
            get_decision_option_comparison,
            get_decision_evidence_inspection,
            list_decision_source_attributions,
            get_decision_governance,
            generate_decision_governance_report,
            list_decision_governance_reports,
            get_execution_decision_projection,
            get_continuity_diagnostics,
            generate_continuity_report,
            list_continuity_reports,
            start_execution,
            get_active_execution,
            get_git_status,
            prepare_commit,
            commit_execution,
            push_execution,
            get_execution_session,
            accept_execution_handoff,
            reject_execution_handoff
        ])
        .run(tauri::generate_context!())
        .expect("failed to run Command Center shell");
}

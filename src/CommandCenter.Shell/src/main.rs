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
    milestone_count: i32,
    has_current_handoff: bool,
    has_current_decisions: bool,
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
    last_activity_at: Option<String>,
    provider_name: String,
    provider_executable_path: Option<String>,
    provider_process_id: Option<i32>,
    provider_started_at: Option<String>,
    handoff_path: Option<String>,
    failure_reason: Option<String>,
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
    artifact_inventory: ArtifactInventory,
    milestone_count: i32,
    has_plan: bool,
    has_operational_context: bool,
    has_current_handoff: bool,
    has_current_decisions: bool,
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
struct ExecutionStartRequest {
    milestone_path: String,
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
            start_execution,
            get_active_execution,
            get_execution_session
        ])
        .run(tauri::generate_context!())
        .expect("failed to run Command Center shell");
}

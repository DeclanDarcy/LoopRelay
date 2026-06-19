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
        .invoke_handler(tauri::generate_handler![ping_backend])
        .run(tauri::generate_context!())
        .expect("failed to run Command Center shell");
}

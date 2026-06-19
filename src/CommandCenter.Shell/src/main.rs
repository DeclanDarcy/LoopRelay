#[tauri::command]
fn ping_backend() -> Result<String, String> {
    reqwest::blocking::get("http://127.0.0.1:5000/api/ping")
        .map_err(|error| error.to_string())?
        .text()
        .map_err(|error| error.to_string())
}

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![ping_backend])
        .run(tauri::generate_context!())
        .expect("failed to run Command Center shell");
}

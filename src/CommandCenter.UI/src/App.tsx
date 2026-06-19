import { useState } from 'react'
import { invoke } from '@tauri-apps/api/core'
import './App.css'

function App() {
  const [pingResult, setPingResult] = useState<string>('Not checked')
  const [isPinging, setIsPinging] = useState(false)

  async function pingBackend() {
    setIsPinging(true)
    try {
      setPingResult(await invoke<string>('ping_backend'))
    } catch (error) {
      setPingResult(error instanceof Error ? error.message : String(error))
    } finally {
      setIsPinging(false)
    }
  }

  return (
    <main className="shell">
      <section className="workspace">
        <header>
          <p>Command Center</p>
          <h1>Repository Operations</h1>
        </header>
        <div className="status-row">
          <span>Backend</span>
          <strong>{pingResult}</strong>
        </div>
        <button
          type="button"
          className="primary-action"
          onClick={pingBackend}
          disabled={isPinging}
        >
          {isPinging ? 'Pinging...' : 'Ping Backend'}
        </button>
      </section>
    </main>
  )
}

export default App

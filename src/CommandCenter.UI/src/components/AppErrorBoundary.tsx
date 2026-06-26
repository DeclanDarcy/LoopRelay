import { Component, type ErrorInfo, type ReactNode } from 'react'

type AppErrorBoundaryProps = {
  children: ReactNode
}

type AppErrorBoundaryState = {
  error: Error | null
  componentStack: string | null
}

/**
 * Top-level boundary so an uncaught render/effect error surfaces the message
 * on screen instead of unmounting the whole React root (which renders a blank
 * window). Without this, any thrown error in the tree leaves no diagnostic in
 * the desktop shell except the webview console.
 */
export class AppErrorBoundary extends Component<AppErrorBoundaryProps, AppErrorBoundaryState> {
  constructor(props: AppErrorBoundaryProps) {
    super(props)
    this.state = { error: null, componentStack: null }
  }

  static getDerivedStateFromError(error: Error): Partial<AppErrorBoundaryState> {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    this.setState({ componentStack: info.componentStack ?? null })
    // Keep the raw error in the console for the webview devtools as well.
    console.error('Command Center crashed:', error, info.componentStack)
  }

  private handleReload = () => {
    this.setState({ error: null, componentStack: null })
    window.location.reload()
  }

  render() {
    const { error, componentStack } = this.state
    if (!error) {
      return this.props.children
    }

    return (
      <div
        role="alert"
        style={{
          padding: '2rem',
          maxWidth: '80ch',
          margin: '0 auto',
          fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
          color: '#e6e6e6',
          background: '#1a1a1a',
          minHeight: '100vh',
          boxSizing: 'border-box',
        }}
      >
        <h1 style={{ fontSize: '1.25rem', color: '#ff6b6b' }}>Command Center failed to render</h1>
        <p style={{ opacity: 0.8 }}>
          An unrecoverable error was thrown in the UI. The details below are also in the webview
          console (Ctrl+Shift+I).
        </p>
        <pre
          style={{
            whiteSpace: 'pre-wrap',
            background: '#0d0d0d',
            padding: '1rem',
            borderRadius: '6px',
            overflowX: 'auto',
          }}
        >
          {error.message}
          {error.stack ? `\n\n${error.stack}` : ''}
          {componentStack ? `\n\nComponent stack:${componentStack}` : ''}
        </pre>
        <button
          type="button"
          onClick={this.handleReload}
          style={{
            marginTop: '1rem',
            padding: '0.5rem 1rem',
            cursor: 'pointer',
            background: '#2d2d2d',
            color: '#e6e6e6',
            border: '1px solid #444',
            borderRadius: '6px',
          }}
        >
          Reload
        </button>
      </div>
    )
  }
}

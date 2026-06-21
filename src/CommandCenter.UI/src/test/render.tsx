import { render, type RenderOptions } from '@testing-library/react'
import { StrictMode, type ReactElement } from 'react'
import { installDevTauriMock } from '../devTauriMock'

export function installWorkspaceCertificationMock() {
  window.history.pushState({}, '', '/?mock=workspace-certification')
  installDevTauriMock()
}

export function renderWithWorkspaceCertification(
  ui: ReactElement,
  options?: Omit<RenderOptions, 'wrapper'>,
) {
  installWorkspaceCertificationMock()

  return render(ui, {
    wrapper: StrictMode,
    ...options,
  })
}


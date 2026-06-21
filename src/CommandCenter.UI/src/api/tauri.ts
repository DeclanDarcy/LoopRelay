import { invoke } from '@tauri-apps/api/core'

export function formatError(error: unknown) {
  return error instanceof Error ? error.message : String(error)
}

export async function invokeCommand<T>(command: string, args?: Record<string, unknown>) {
  try {
    return await invoke<T>(command, args)
  } catch (error) {
    throw new Error(formatError(error), { cause: error })
  }
}

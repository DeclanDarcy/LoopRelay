export function formatDateTime(value: string | null) {
  return value ? new Date(value).toLocaleString() : 'Not recorded'
}

export function formatDuration(value: string | null) {
  return value ?? 'Not recorded'
}

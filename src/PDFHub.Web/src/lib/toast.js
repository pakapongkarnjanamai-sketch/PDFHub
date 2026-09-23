// Tiny toast store; ToastViewport renders it. Use for transient confirmations only.

const listeners = new Set()
let toasts = []
let nextId = 1

function emit() {
  for (const listener of listeners) listener(toasts)
}

export function subscribeToasts(listener) {
  listeners.add(listener)
  listener(toasts)
  return () => listeners.delete(listener)
}

export function dismissToast(id) {
  toasts = toasts.filter((t) => t.id !== id)
  emit()
}

function show(kind, message, duration) {
  if (!message) return
  const id = nextId++
  toasts = [...toasts, { id, kind, message }]
  emit()
  // Errors and warnings are new information to read, often in a second language: give them longer.
  const timeout = duration ?? (kind === 'warning' || kind === 'error' ? 6000 : 3000)
  window.setTimeout(() => dismissToast(id), timeout)
}

export const toast = {
  info: (message, duration) => show('info', message, duration),
  success: (message, duration) => show('success', message, duration),
  warning: (message, duration) => show('warning', message, duration),
  error: (message, duration) => show('error', message, duration),
}

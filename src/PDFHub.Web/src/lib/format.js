// Display formatting only — dates stay Gregorian (dd/MM/yyyy), matching the original Excel sheet.

function pad(n) {
  return String(n).padStart(2, '0')
}

/** "2026-08-08" → "08/08/2026". Parsed by hand so the browser's time zone can never shift the day. */
export function formatDate(isoDate) {
  if (!isoDate) return ''
  const [y, m, d] = String(isoDate).slice(0, 10).split('-')
  return y && m && d ? `${d}/${m}/${y}` : ''
}

/** Server local timestamp → "08/08/2026 14:05". */
export function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

export function todayText() {
  const now = new Date()
  return `${pad(now.getDate())}/${pad(now.getMonth() + 1)}/${now.getFullYear()}`
}

/** Local date as "yyyy-MM-dd" for <input type="date">. */
export function todayIso() {
  const now = new Date()
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

const priceFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 })

export function formatPrice(value) {
  if (value === null || value === undefined || value === '') return ''
  try {
    return priceFormat.format(Number(value))
  } catch {
    return String(value)
  }
}

export function formatFileSize(bytes) {
  if (!bytes) return ''
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

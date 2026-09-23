// The only module that reads import.meta.env.

function normalizeBasePath(value) {
  const trimmed = (value ?? '').trim().replace(/^\/+|\/+$/g, '')
  return trimmed ? `/${trimmed}/` : '/'
}

const appBasePath = normalizeBasePath(import.meta.env.VITE_PDFHUB_APP_BASE_PATH)

export const appConfig = {
  /** "/" in development, "/PDFHub/" under IIS. Vite's base and BrowserRouter's basename use the same value. */
  appBasePath,
  /** The API serves this app, so both share one origin and the login cookie. */
  apiBaseUrl: (import.meta.env.VITE_PDFHUB_API_BASE_URL ?? `${appBasePath}api`).replace(/\/+$/, ''),
}

/** Shareable link to a drawing's PDF, e.g. /PDFHub/pdf/NB-06618.pdf */
export function pdfUrl(code, { download = false } = {}) {
  return `${appBasePath}pdf/${encodeURIComponent(code)}.pdf${download ? '?download=true' : ''}`
}

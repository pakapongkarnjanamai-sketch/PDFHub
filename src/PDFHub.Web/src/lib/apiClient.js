import { appConfig } from '../config/appConfig'

/** Normalized error from an RFC 7807 ProblemDetails response (or a network failure). */
export class ApiError extends Error {
  constructor({ status, title, detail, type, errors }) {
    super(detail || title || 'เกิดข้อผิดพลาด')
    this.status = status
    this.title = title
    this.detail = detail
    this.type = type
    /** Field errors keyed by the server's PascalCase property names, e.g. { PdfCode: ['...'] }. */
    this.errors = errors ?? {}
  }
}

/** A complete sentence for banners and toasts, preferring the server's specific message. */
export function errorMessage(error) {
  if (!(error instanceof ApiError)) return 'เกิดข้อผิดพลาดที่ไม่คาดคิด กรุณาลองใหม่อีกครั้ง'
  if (error.status === 0) return 'ติดต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบการเชื่อมต่อเครือข่ายแล้วลองใหม่'
  if (error.detail) return error.detail
  if (Object.keys(error.errors).length > 0) return 'กรุณาแก้ไขช่องที่มีข้อความสีแดง'
  if (error.status === 401) return 'กรุณาเข้าสู่ระบบก่อน'
  if (error.status === 403) return 'คุณไม่มีสิทธิ์ทำรายการนี้'
  if (error.status === 404) return 'ไม่พบข้อมูลที่ต้องการ'
  return `เกิดข้อผิดพลาดจากเซิร์ฟเวอร์ (${error.status})`
}

let onAuthProblem = () => {}

/** The session provider registers how to react to 401 / password-change-required / 403. */
export function setAuthProblemHandler(handler) {
  onAuthProblem = handler
}

function buildUrl(path, query) {
  const url = `${appConfig.apiBaseUrl}${path}`
  if (!query) return url
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  }
  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

/**
 * fetch wrapper used by every API module: same-origin cookie, the X-Requested-With header the API
 * requires on writes, JSON in/out, and ApiError on failure.
 */
export async function apiRequest(path, { method = 'GET', query, body, form, signal } = {}) {
  const headers = { 'X-Requested-With': 'PDFHub', Accept: 'application/json' }
  let payload
  if (form) payload = form
  else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }

  let response
  try {
    response = await fetch(buildUrl(path, query), { method, headers, body: payload, signal, credentials: 'same-origin' })
  } catch (err) {
    if (err?.name === 'AbortError') throw err
    throw new ApiError({ status: 0, title: 'Network error' })
  }

  if (response.ok) {
    if (response.status === 204) return null
    const type = response.headers.get('content-type') ?? ''
    return type.includes('json') ? response.json() : response.blob()
  }

  let problem = {}
  try {
    problem = await response.json()
  } catch {
    /* empty or non-JSON body */
  }
  const error = new ApiError({ status: response.status, ...problem })
  if (response.status === 401 || response.status === 403) onAuthProblem(error)
  throw error
}

export const api = {
  get: (path, options) => apiRequest(path, { ...options, method: 'GET' }),
  post: (path, body, options) => apiRequest(path, { ...options, method: 'POST', body }),
  put: (path, body, options) => apiRequest(path, { ...options, method: 'PUT', body }),
  delete: (path, options) => apiRequest(path, { ...options, method: 'DELETE' }),
  upload: (path, form, { method = 'POST', ...options } = {}) => apiRequest(path, { ...options, method, form }),
  url: buildUrl,
}

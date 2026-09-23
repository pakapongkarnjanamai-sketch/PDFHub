import { api } from '../lib/apiClient'

// Mirrors PDFHub.Api controllers and PDFHub.Domain.DTOs — change together with them.

export const sessionApi = {
  me: (signal) => api.get('/session/me', { signal }),
  login: (body) => api.post('/session/login', body),
  logout: () => api.post('/session/logout'),
  changePassword: (body) => api.post('/session/password', body),
}

export const drawingsApi = {
  /** query: { q, section, year, po, pdf, sort, dir, page, pageSize } */
  list: (query, signal) => api.get('/drawings', { query, signal }),
  exportUrl: (query) => api.url('/drawings/export', query),
  get: (code, signal) => api.get(`/drawings/${encodeURIComponent(code)}`, { signal }),
  lookups: (signal) => api.get('/drawings/lookups', { signal }),
  checkCode: (code, id, signal) => api.get('/drawings/check-code', { query: { code, id }, signal }),
  create: (body) => api.post('/drawings', body),
  update: (id, body) => api.put(`/drawings/${id}`, body),
  remove: (id) => api.delete(`/drawings/${id}`),
  uploadPdf: (id, file) => {
    const form = new FormData()
    form.append('file', file)
    return api.upload(`/drawings/${id}/pdf`, form, { method: 'PUT' })
  },
  /** One file per request; the file name picks the drawing. */
  uploadPdfBulk: (file, overwrite) => {
    const form = new FormData()
    form.append('file', file)
    form.append('overwrite', String(overwrite))
    return api.upload('/drawings/pdf-bulk', form)
  },
}

export const importApi = {
  excel: (file, mode, dryRun) => {
    const form = new FormData()
    form.append('file', file)
    form.append('mode', mode)
    form.append('dryRun', String(dryRun))
    return api.upload('/import/excel', form)
  },
}

export const sectionsApi = {
  list: (signal) => api.get('/sections', { signal }),
  create: (body) => api.post('/sections', body),
  update: (id, body) => api.put(`/sections/${id}`, body),
  remove: (id) => api.delete(`/sections/${id}`),
}

export const usersApi = {
  list: (signal) => api.get('/users', { signal }),
  create: (body) => api.post('/users', body),
  update: (id, body) => api.put(`/users/${id}`, body),
  resetPassword: (id, body) => api.post(`/users/${id}/reset-password`, body),
}

export const ROLE_LABELS = {
  Admin: 'ผู้ดูแลระบบ',
  Editor: 'ผู้บันทึกข้อมูล',
  Viewer: 'ผู้ดูข้อมูล',
}

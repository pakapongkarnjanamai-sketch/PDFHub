// Client mirror of PDFHub.Domain.PdfCodeRules and DrawingService validation. The server still decides;
// this only puts the message next to the field before a round trip. Keep the wording identical.

export function normalizeCode(value) {
  let code = (value ?? '').trim().toUpperCase()
  if (/^[A-Z]{2}\d{5}$/.test(code)) code = `${code.slice(0, 2)}-${code.slice(2)}`
  return code
}

/** @param sections map of active section code → name */
export function codeError(code, sections) {
  if (!code) return 'กรุณากรอก PdfCode'
  if (code.length !== 8 || code[2] !== '-' || !/^[A-Z]{2}/.test(code)) return 'รูปแบบ PdfCode ไม่ถูกต้อง ตัวอย่าง : HI-07666'
  if (!/^\d{5}$/.test(code.slice(3))) return 'ตัวเลขท้ายต้องมี 5 หลัก'
  if (sections && !sections[code.slice(0, 2)]) return `ไม่พบประเภท ${code.slice(0, 2)} ในรายการ Section`
  return null
}

export function parsePrice(text) {
  const cleaned = String(text ?? '').replaceAll(',', '').trim()
  if (cleaned === '') return { value: null }
  const value = Number(cleaned)
  return Number.isFinite(value) && value >= 0 ? { value } : { error: 'ราคาต้องเป็นตัวเลขตั้งแต่ 0 ขึ้นไป' }
}

export function validateDrawing(form, sections) {
  const errors = {}
  const pdfCodeError = codeError(normalizeCode(form.pdfCode), sections)
  if (pdfCodeError) errors.pdfCode = pdfCodeError
  if (!form.partName.trim()) errors.partName = 'กรุณากรอก Part Name'
  if (!form.inputDate) errors.inputDate = 'กรุณาระบุวันที่'
  const price = parsePrice(form.price)
  if (price.error) errors.price = price.error
  return errors
}

/** Server ValidationProblemDetails keys → form keys. */
export const SERVER_FIELD_KEYS = {
  PdfCode: 'pdfCode',
  PartName: 'partName',
  DrawingNo: 'drawingNo',
  Material: 'material',
  Price: 'price',
  InputDate: 'inputDate',
  QuoNo: 'quoNo',
  Remark: 'remark',
  File: 'file',
}

export const STICKER_HINT = 'กรอกเฉพาะรหัสที่มีใน Sticker Minebear เท่านั้น เช่น BL-xxxxx'

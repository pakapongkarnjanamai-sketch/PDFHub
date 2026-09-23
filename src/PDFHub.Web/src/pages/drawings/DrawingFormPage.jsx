import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useBeforeUnload, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { CircleCheck, CircleX, FileText, Trash2, Upload } from 'lucide-react'
import { drawingsApi } from '../../api'
import { ApiError, errorMessage } from '../../lib/apiClient'
import { focusFirstInvalid, mapServerFieldErrors } from '../../lib/formErrors'
import { formatDateTime, formatFileSize, todayIso } from '../../lib/format'
import { toast } from '../../lib/toast'
import { useAsyncData } from '../../lib/useAsyncData'
import { useSession } from '../../session/SessionContext'
import { pdfUrl } from '../../config/appConfig'
import { AppButton } from '../../components/ui/buttons'
import { ErrorSurface, LoadingSurface } from '../../components/ui/feedback'
import { Field, FormActions, FormSection, FormSectionDivider, Page, PageHeader, TitledSection } from '../../components/ui/layout'
import { ConfirmDialog } from '../../components/ui/Modal'
import { inputClassName, recordLinkClassName, textareaClassName } from '../../components/ui/styles'
import { codeError, normalizeCode, parsePrice, SERVER_FIELD_KEYS, STICKER_HINT, validateDrawing } from './drawingRules'

const MAX_PDF_MB = 100

function emptyForm(pdfCode = '', inputDate = todayIso()) {
  return { pdfCode, partName: '', drawingNo: '', material: '', price: '', inputDate, quoNo: '', remark: '', poNo: '' }
}

function formFrom(d) {
  return {
    pdfCode: d.pdfCode, partName: d.partName, drawingNo: d.drawingNo, material: d.material,
    price: d.price != null ? String(d.price) : '', inputDate: d.inputDate, quoNo: d.quoNo, remark: d.remark, poNo: d.poNo,
  }
}

export function DrawingFormPage() {
  const { code } = useParams()
  const isEdit = Boolean(code)
  const load = useCallback(async (signal) => {
    const [drawing, lookups] = await Promise.all([isEdit ? drawingsApi.get(code, signal) : null, drawingsApi.lookups(signal)])
    return { drawing, lookups }
  }, [code, isEdit])
  const { data, error, loading, retry } = useAsyncData(load)

  if (loading && !data) return <Page><LoadingSurface /></Page>
  if (!data) return <Page><ErrorSurface onRetry={retry}>{errorMessage(error)}</ErrorSurface></Page>
  // Keyed so switching between records (or back to "new") starts from a fresh form.
  return <DrawingForm key={data.drawing?.id ?? 'new'} drawing={data.drawing} lookups={data.lookups} />
}

function DrawingForm({ drawing, lookups }) {
  const isEdit = Boolean(drawing)
  const navigate = useNavigate()
  const { state } = useLocation()
  const [searchParams] = useSearchParams()
  const { session } = useSession()
  const sections = useMemo(() => Object.fromEntries(lookups.sections.map((s) => [s.code, s.name])), [lookups])

  const [form, setForm] = useState(() => drawing ? formFrom(drawing) : emptyForm(searchParams.get('next') ?? '', searchParams.get('date') ?? todayIso()))
  const [file, setFile] = useState(null)
  const [dirty, setDirty] = useState(false)
  const [errors, setErrors] = useState({})
  const [banner, setBanner] = useState(null)
  const [busyAction, setBusyAction] = useState(null)
  const [confirm, setConfirm] = useState(null) // 'discard' | 'delete'
  const [codeCheck, setCodeCheck] = useState(null)
  const codeRef = useRef(null)

  /** Every user edit goes through here, so `dirty` is exact without deep comparison. */
  const patch = (changes) => {
    setForm((current) => ({ ...current, ...changes }))
    setDirty(true)
    setErrors((current) => {
      const next = { ...current }
      for (const key of Object.keys(changes)) delete next[key]
      return next
    })
  }

  useBeforeUnload(useCallback((event) => { if (dirty) event.preventDefault() }, [dirty]))

  // Live PdfCode check: format and section locally, duplicates on the server.
  const code = normalizeCode(form.pdfCode)
  const localCodeError = code.length >= 8 ? codeError(code, sections) : null
  useEffect(() => {
    if (code.length < 8 || codeError(code, sections)) return
    const controller = new AbortController()
    const timeout = window.setTimeout(() => {
      drawingsApi.checkCode(code, drawing?.id, controller.signal).then(setCodeCheck).catch(() => {})
    }, 300)
    return () => {
      window.clearTimeout(timeout)
      controller.abort()
    }
  }, [code, sections, drawing?.id])
  const serverCheck = codeCheck?.code === code ? codeCheck : null
  const codeStatus = errors.pdfCode
    ? null
    : localCodeError
      ? { ok: false, text: localCodeError }
      : serverCheck && { ok: serverCheck.valid, text: serverCheck.valid ? `ใช้รหัสนี้ได้ · Section ${serverCheck.sectionName}` : serverCheck.error }

  const pickFile = (picked) => {
    setErrors((current) => ({ ...current, file: undefined }))
    if (!picked) return setFile(null)
    if (!/\.pdf$/i.test(picked.name)) return setErrors((current) => ({ ...current, file: 'กรุณาเลือกไฟล์ .pdf' }))
    if (picked.size > MAX_PDF_MB * 1024 * 1024) return setErrors((current) => ({ ...current, file: `ไฟล์ใหญ่เกิน ${MAX_PDF_MB} MB` }))
    setFile(picked)
    setDirty(true)
  }

  const save = async (action) => {
    const clientErrors = validateDrawing(form, sections)
    if (Object.keys(clientErrors).length > 0) {
      setErrors(clientErrors)
      setBanner(null)
      focusFirstInvalid()
      return
    }

    setBusyAction(action)
    setBanner(null)
    const body = { ...form, pdfCode: code, price: parsePrice(form.price).value }
    let saved
    try {
      saved = isEdit ? await drawingsApi.update(drawing.id, body) : await drawingsApi.create(body)
    } catch (err) {
      showServerErrors(err)
      setBusyAction(null)
      return
    }

    if (file) {
      try {
        saved = await drawingsApi.uploadPdf(saved.id, file)
      } catch (err) {
        // The data is saved; only the file failed. Continue editing that record so the user can retry.
        setBusyAction(null)
        setDirty(false)
        toast.error(`บันทึก ${saved.pdfCode} แล้ว แต่แนบไฟล์ PDF ไม่สำเร็จ: ${errorMessage(err)}`)
        navigate(`/drawings/${saved.pdfCode}/edit`, { replace: true })
        return
      }
    }

    setBusyAction(null)
    setDirty(false)
    toast.success(isEdit ? `บันทึก ${saved.pdfCode} แล้ว` : `เพิ่ม ${saved.pdfCode} แล้ว`)
    if (action === 'saveNext') {
      // Same sticker series, same day: the next code and date are filled in.
      setForm(emptyForm(saved.nextCode ?? '', saved.inputDate))
      setFile(null)
      setErrors({})
      setCodeCheck(null)
      codeRef.current?.focus()
    } else {
      navigate(`/drawings/${saved.pdfCode}`, { replace: true, state })
    }
  }

  const showServerErrors = (err) => {
    if (err instanceof ApiError && Object.keys(err.errors).length > 0) {
      const { fields, unmapped } = mapServerFieldErrors(err.errors, SERVER_FIELD_KEYS)
      setErrors(fields)
      setBanner(unmapped.length > 0 ? unmapped.join(' ') : 'กรุณาแก้ไขช่องที่มีข้อความสีแดง')
      focusFirstInvalid()
    } else {
      setBanner(errorMessage(err))
    }
  }

  const leave = () => navigate(isEdit ? `/drawings/${drawing.pdfCode}` : '/drawings', { state })
  const cancel = () => (dirty ? setConfirm('discard') : leave())

  const remove = async () => {
    setBusyAction('delete')
    try {
      await drawingsApi.remove(drawing.id)
      setDirty(false)
      toast.success(`ลบ ${drawing.pdfCode} แล้ว ไฟล์ PDF ถูกย้ายไปเก็บสำรองใน _archive`)
      navigate('/drawings', { replace: true })
    } catch (err) {
      setConfirm(null)
      setBanner(errorMessage(err))
      setBusyAction(null)
    }
  }

  const busy = busyAction !== null
  const input = (key, props = {}) => (
    <input value={form[key]} onChange={(e) => patch({ [key]: e.target.value })} aria-invalid={errors[key] ? true : undefined}
      className={inputClassName(`w-full ${props.className ?? ''}`)} {...props} />
  )

  return (
    <Page>
      <PageHeader
        title={isEdit ? <>แก้ไข <span className="font-mono">{drawing.pdfCode}</span></> : 'เพิ่ม Drawing'}
        meta={isEdit && drawing.updatedAt ? `แก้ไขล่าสุด ${formatDateTime(drawing.updatedAt)}${drawing.updatedBy ? ` โดย ${drawing.updatedBy}` : ''}` : undefined}
      />

      {banner && <ErrorSurface>{banner}</ErrorSurface>}

      <form noValidate onSubmit={(e) => { e.preventDefault(); save('save') }} className="space-y-5">
        <FormSection>
          <div className="grid gap-x-5 gap-y-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Field label="PdfCode" required error={errors.pdfCode} hint={codeStatus ? undefined : STICKER_HINT}>
                <input ref={codeRef} value={form.pdfCode} maxLength={8} autoComplete="off" autoFocus={!isEdit}
                  placeholder="เช่น NB-06618" aria-invalid={errors.pdfCode ? true : undefined}
                  onChange={(e) => patch({ pdfCode: e.target.value.toUpperCase() })}
                  onBlur={() => form.pdfCode !== code && setForm((f) => ({ ...f, pdfCode: code }))}
                  className={inputClassName('w-full font-mono text-heading font-semibold tracking-wide uppercase')} />
              </Field>
              {codeStatus && (
                <p className={`flex items-center gap-1 text-caption ${codeStatus.ok ? 'text-success' : 'text-danger'}`} aria-live="polite">
                  {codeStatus.ok ? <CircleCheck className="size-3.5" aria-hidden /> : <CircleX className="size-3.5" aria-hidden />}
                  {codeStatus.text}
                </p>
              )}
            </div>
            <div>
              <p className="mb-1 text-caption font-semibold text-ink-strong">Section</p>
              <p className="flex h-9 items-center rounded-md border border-dashed border-border-strong bg-surface-muted px-2.5 font-semibold">
                {sections[code.slice(0, 2)] ?? '—'}
              </p>
              <p className="mt-1 text-caption text-ink-muted">ระบบเลือกให้จาก 2 ตัวอักษรแรกของ PdfCode</p>
            </div>

            <Field label="Part Name" required error={errors.partName} className="sm:col-span-2">
              {input('partName', { maxLength: 200 })}
            </Field>
            <Field label="Drawing No." error={errors.drawingNo}>{input('drawingNo', { maxLength: 100, className: 'font-mono' })}</Field>
            <Field label="Material" error={errors.material}>
              {input('material', { maxLength: 100, list: 'materials', autoComplete: 'off' })}
              <datalist id="materials">{lookups.materials.map((m) => <option key={m} value={m} />)}</datalist>
            </Field>
            <Field label="Price (บาท)" error={errors.price}>{input('price', { inputMode: 'decimal', placeholder: '0.00', className: 'text-right tabular-nums' })}</Field>
            <Field label="Input Date" required error={errors.inputDate}>{input('inputDate', { type: 'date' })}</Field>
            <Field label="Quotation No." error={errors.quoNo}>{input('quoNo', { maxLength: 50 })}</Field>
            <Field label="PO No." error={errors.poNo} hint="เลขที่ใบสั่งซื้อจากลูกค้า เว้นว่างถ้ายังไม่ได้รับ PO">
              {input('poNo', { maxLength: 50, autoComplete: 'off' })}
            </Field>
            <Field label="Remark" error={errors.remark} className="sm:col-span-2">
              <textarea value={form.remark} rows={2} maxLength={1000} onChange={(e) => patch({ remark: e.target.value })}
                className={textareaClassName('w-full')} />
            </Field>
          </div>

          <FormSectionDivider />

          <Field label="ไฟล์ PDF" error={errors.file}
            hint={isEdit && drawing.hasPdf ? 'เลือกไฟล์ใหม่เพื่อแทนที่ ไฟล์เดิมจะถูกเก็บสำรองไว้ ไม่ได้ลบทิ้ง' : `ไม่เกิน ${MAX_PDF_MB} MB · แนบภายหลังได้`}>
            {isEdit && drawing.hasPdf && (
              <span className="mb-2 flex flex-wrap items-center gap-2 text-body">
                <FileText className="size-4 text-danger" aria-hidden />
                <a href={pdfUrl(drawing.pdfCode)} target="_blank" rel="noreferrer" className={recordLinkClassName}>{drawing.pdfCode}.pdf</a>
                <span className="text-ink-muted">{formatFileSize(drawing.pdfSize)} · อัปโหลด {formatDateTime(drawing.pdfUploadedAt)}</span>
              </span>
            )}
            <span className={`relative flex min-h-24 cursor-pointer flex-col items-center justify-center gap-1 rounded-lg border-2 border-dashed px-4 py-4 text-center focus-within:outline-2 focus-within:outline-offset-1 focus-within:outline-accent ${
              file ? 'border-success bg-success-soft text-success' : 'border-border-strong bg-surface-muted text-ink-muted hover:border-accent hover:text-accent'
            }`}>
              <Upload className="size-5" aria-hidden />
              <span className="font-medium">{file ? `${file.name} (${formatFileSize(file.size)})` : 'เลือกไฟล์ PDF หรือลากไฟล์มาวาง'}</span>
              <input type="file" accept=".pdf,application/pdf" onChange={(e) => pickFile(e.target.files?.[0])}
                className="absolute inset-0 cursor-pointer opacity-0" />
            </span>
          </Field>
        </FormSection>

        <FormActions>
          <AppButton type="submit" variant="primary" loading={busyAction === 'save'} disabled={busy}>บันทึก</AppButton>
          {!isEdit && (
            <AppButton loading={busyAction === 'saveNext'} disabled={busy} onClick={() => save('saveNext')}
              title="บันทึกแล้วเปิดฟอร์มใหม่ พร้อมรหัสถัดไปและวันที่เดิม">
              บันทึกและเพิ่มรายการถัดไป
            </AppButton>
          )}
          <AppButton variant="ghost" disabled={busy} onClick={cancel}>ยกเลิก</AppButton>
        </FormActions>
      </form>

      {isEdit && session.isAdmin && (
        <TitledSection title="ลบ Drawing นี้" description="ข้อมูลจะถูกลบออกจากฐานข้อมูล ส่วนไฟล์ PDF จะถูกย้ายไปเก็บในโฟลเดอร์ _archive ไม่ได้ลบทิ้ง">
          <AppButton variant="danger" disabled={busy} onClick={() => setConfirm('delete')}>
            <Trash2 className="size-4" aria-hidden /> ลบ {drawing.pdfCode}
          </AppButton>
        </TitledSection>
      )}

      <ConfirmDialog open={confirm === 'discard'} title="ทิ้งการแก้ไข?" confirmLabel="ทิ้งการแก้ไข" danger
        onCancel={() => setConfirm(null)} onConfirm={() => { setDirty(false); leave() }}>
        ข้อมูลที่กรอกไว้ยังไม่ได้บันทึก ถ้าออกจากหน้านี้ข้อมูลจะหายไป
      </ConfirmDialog>
      <ConfirmDialog open={confirm === 'delete'} title={`ลบ ${drawing?.pdfCode}?`} confirmLabel="ลบ" danger busy={busyAction === 'delete'}
        onCancel={() => setConfirm(null)} onConfirm={remove}>
        ลบ Drawing นี้ออกจากระบบ ไฟล์ PDF จะถูกย้ายไปเก็บสำรอง ไม่ได้ลบทิ้ง
      </ConfirmDialog>
    </Page>
  )
}

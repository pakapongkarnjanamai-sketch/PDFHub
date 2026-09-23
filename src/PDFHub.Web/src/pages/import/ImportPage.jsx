import { useEffect, useRef, useState } from 'react'
import { FileSpreadsheet, FileText, FolderOpen, Upload } from 'lucide-react'
import { drawingsApi, importApi } from '../../api'
import { errorMessage } from '../../lib/apiClient'
import { toast } from '../../lib/toast'
import { AppButton } from '../../components/ui/buttons'
import { ErrorSurface } from '../../components/ui/feedback'
import { FormActions, Page, PageHeader, TitledSection } from '../../components/ui/layout'
import { buttonClassName } from '../../components/ui/styles'

export function ImportPage() {
  return (
    <Page wide>
      <PageHeader title="นำเข้าข้อมูล" meta="ย้ายข้อมูลจากไฟล์ Excel เดิม แล้วอัปโหลดไฟล์ PDF ทีละหลายไฟล์" />
      <div className="grid items-start gap-5 xl:grid-cols-2">
        <ExcelImport />
        <BulkPdfUpload />
      </div>
    </Page>
  )
}

function Stat({ value, label, bad = false }) {
  return (
    <div className="rounded-md border border-border-subtle bg-surface-muted px-3 py-2">
      <p className={`text-title font-semibold tabular-nums ${bad && value > 0 ? 'text-danger' : 'text-ink-strong'}`}>{value.toLocaleString()}</p>
      <p className="text-caption text-ink-muted">{label}</p>
    </div>
  )
}

function ExcelImport() {
  const [file, setFile] = useState(null)
  const [mode, setMode] = useState('SkipExisting')
  const [busy, setBusy] = useState(null) // 'check' | 'import'
  const [result, setResult] = useState(null)
  const [error, setError] = useState(null)

  const run = async (dryRun) => {
    setBusy(dryRun ? 'check' : 'import')
    setError(null)
    try {
      const r = await importApi.excel(file, mode, dryRun)
      setResult(r)
      if (!dryRun) toast.success(`นำเข้าแล้ว: เพิ่ม ${r.added} · อัปเดต ${r.updated} · ข้าม ${r.skipped} รายการ`)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(null)
    }
  }

  const choose = (picked) => {
    setFile(picked ?? null)
    setResult(null)
    setError(null)
  }

  return (
    <TitledSection title="1. นำเข้าข้อมูลจาก Excel"
      description="ใช้ไฟล์ .xlsx รูปแบบเดียวกับชีต NMB-2026 เดิม (มีคอลัมน์ PdfCodeS, PartName, DrawingNo, MatS, Price, InputDate, QuoNo., Remark และ PO No.) หรือไฟล์ที่ Export จากระบบนี้ ไฟล์เดิมที่มีคอลัมน์ Have a PO แบบติ๊ก จะถูกบันทึก PO No. เป็น “มี PO (ไม่ระบุเลขที่)” เพราะไม่มีเลขที่จริง ระบบนำเข้าทุกชีตที่มีหัวคอลัมน์เหล่านี้ กดตรวจสอบก่อน ข้อมูลจะยังไม่ถูกบันทึกจนกว่าจะกดนำเข้า">
      <div className="space-y-4">
        <label className={buttonClassName('secondary', 'md', 'cursor-pointer focus-within:outline-2 focus-within:outline-accent')}>
          <FileSpreadsheet className="size-4" aria-hidden />
          {file ? file.name : 'เลือกไฟล์ Excel (.xlsx)'}
          <input type="file" accept=".xlsx" className="sr-only" onChange={(e) => choose(e.target.files?.[0])} />
        </label>

        <fieldset className="space-y-1.5">
          <legend className="mb-1 text-caption font-semibold text-ink-strong">ถ้า PdfCode มีอยู่ในระบบแล้ว</legend>
          {[['SkipExisting', 'ข้าม (เพิ่มเฉพาะรายการใหม่)'], ['UpdateExisting', 'อัปเดตด้วยข้อมูลในไฟล์']].map(([value, label]) => (
            <label key={value} className="flex min-h-9 cursor-pointer items-center gap-2">
              <input type="radio" name="mode" value={value} checked={mode === value}
                onChange={() => { setMode(value); setResult(null) }} className="size-4 accent-accent" />
              {label}
            </label>
          ))}
        </fieldset>

        <FormActions>
          <AppButton disabled={!file || busy !== null} loading={busy === 'check'} onClick={() => run(true)}>ตรวจสอบ</AppButton>
          <AppButton variant="primary" disabled={!file || busy !== null || !result?.dryRun || result.added + result.updated === 0}
            loading={busy === 'import'} onClick={() => run(false)}
            title={!result?.dryRun ? 'กดตรวจสอบก่อน' : undefined}>
            <Upload className="size-4" aria-hidden /> นำเข้า
          </AppButton>
        </FormActions>

        {error && <ErrorSurface>{error}</ErrorSurface>}

        {result && (
          <div className="space-y-3 border-t border-border-subtle pt-4">
            <p className="font-semibold">
              {result.dryRun ? 'ผลการตรวจสอบ (ยังไม่บันทึก)' : 'นำเข้าเรียบร้อย'}
              {result.sheets.length > 0 && <span className="font-normal text-ink-muted"> · ชีต {result.sheets.join(', ')} · อ่าน {result.rowsRead} แถว</span>}
            </p>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              <Stat value={result.added} label={result.dryRun ? 'จะเพิ่มใหม่' : 'เพิ่มใหม่'} />
              <Stat value={result.updated} label={result.dryRun ? 'จะอัปเดต' : 'อัปเดต'} />
              <Stat value={result.skipped} label="ข้าม (มีอยู่แล้ว)" />
              <Stat value={result.errors.length} label="ผิดพลาด" bad />
            </div>
            {(result.errors.length > 0 || result.warnings.length > 0) && (
              <>
                <div className="relative max-h-80 overflow-auto rounded-md border border-border-subtle">
                  <table className="w-full text-body">
                    <thead className="sticky top-0 bg-surface-muted text-left text-caption text-ink-muted">
                      <tr><th className="px-3 py-2">ชีต</th><th className="px-3 py-2 text-right">แถว</th><th className="px-3 py-2">PdfCode</th><th className="px-3 py-2">รายละเอียด</th></tr>
                    </thead>
                    <tbody>
                      {result.errors.map((e, i) => <IssueRow key={`e${i}`} issue={e} kind="ผิดพลาด" className="text-danger" />)}
                      {result.warnings.map((w, i) => <IssueRow key={`w${i}`} issue={w} kind="คำเตือน" className="text-warning" />)}
                    </tbody>
                  </table>
                </div>
                <p className="text-caption text-ink-muted">แถวที่ผิดพลาดจะไม่ถูกนำเข้า แถวที่มีคำเตือนจะนำเข้าตามปกติ</p>
              </>
            )}
            {result.dryRun && result.added + result.updated > 0 && (
              <p className="text-body">ถ้าผลถูกต้อง กด <strong>นำเข้า</strong> เพื่อบันทึก</p>
            )}
          </div>
        )}
      </div>
    </TitledSection>
  )
}

function IssueRow({ issue, kind, className }) {
  return (
    <tr className="border-t border-border-subtle">
      <td className="px-3 py-1.5">{issue.sheet}</td>
      <td className="px-3 py-1.5 text-right tabular-nums">{issue.row}</td>
      <td className="px-3 py-1.5 font-mono">{issue.code}</td>
      <td className={`px-3 py-1.5 ${className}`}>{kind}: {issue.message}</td>
    </tr>
  )
}

/** Uploads one file per request, three at a time, so hundreds of drawings never become one huge request. */
function BulkPdfUpload() {
  const [overwrite, setOverwrite] = useState(false)
  const [rows, setRows] = useState([])
  const [total, setTotal] = useState(0)
  const [onlyProblems, setOnlyProblems] = useState(false)
  const queueRef = useRef([])
  const runningRef = useRef(0)

  const done = rows.length
  const counts = rows.reduce((acc, r) => ({ ...acc, [r.status]: (acc[r.status] ?? 0) + 1 }), {})

  useEffect(() => {
    if (done >= total) return
    const onBeforeUnload = (e) => e.preventDefault()
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [done, total])

  const pump = () => {
    while (runningRef.current < 3 && queueRef.current.length > 0) {
      const { file, replace } = queueRef.current.shift()
      runningRef.current++
      drawingsApi.uploadPdfBulk(file, replace)
        .then((r) => ({ name: file.name, status: r.status, message: r.message }))
        .catch((err) => ({ name: file.name, status: 'error', message: errorMessage(err) }))
        .then((row) => {
          setRows((current) => [row, ...current])
          runningRef.current--
          pump()
        })
    }
  }

  const add = (fileList) => {
    const pdfs = [...(fileList ?? [])].filter((f) => /\.pdf$/i.test(f.name))
    if (pdfs.length === 0) {
      toast.warning('ไม่พบไฟล์ .pdf ในที่เลือก')
      return
    }
    // The overwrite choice at the moment the files were picked applies to them.
    queueRef.current.push(...pdfs.map((file) => ({ file, replace: overwrite })))
    setTotal((t) => t + pdfs.length)
    pump()
  }

  const visible = onlyProblems ? rows.filter((r) => r.status !== 'ok') : rows
  const labels = { ok: 'สำเร็จ', skip: 'ข้าม', error: 'ผิดพลาด' }
  const tone = { ok: 'text-success', skip: 'text-warning', error: 'text-danger' }

  return (
    <TitledSection title="2. อัปโหลดไฟล์ PDF หลายไฟล์"
      description="ชื่อไฟล์ต้องเป็น PdfCode เช่น NB-06618.pdf (แบบเดียวกับโฟลเดอร์ DrawingNMB2026 เดิม) ระบบจะจับคู่กับข้อมูลให้เอง ต้องมีข้อมูล Drawing ในระบบก่อน">
      <div className="space-y-4">
        <div className="flex flex-wrap gap-2">
          <label className={buttonClassName('secondary', 'md', 'cursor-pointer focus-within:outline-2 focus-within:outline-accent')}>
            <FileText className="size-4" aria-hidden /> เลือกไฟล์ PDF
            <input type="file" accept=".pdf,application/pdf" multiple className="sr-only"
              onChange={(e) => { add(e.target.files); e.target.value = '' }} />
          </label>
          <label className={buttonClassName('secondary', 'md', 'cursor-pointer focus-within:outline-2 focus-within:outline-accent')}>
            <FolderOpen className="size-4" aria-hidden /> เลือกทั้งโฟลเดอร์
            <input type="file" webkitdirectory="" multiple className="sr-only"
              onChange={(e) => { add(e.target.files); e.target.value = '' }} />
          </label>
        </div>
        <label className="flex min-h-9 cursor-pointer items-center gap-2">
          <input type="checkbox" checked={overwrite} onChange={(e) => setOverwrite(e.target.checked)} className="size-4 accent-accent" />
          <span>แทนที่ไฟล์ที่มีอยู่แล้ว <span className="text-ink-muted">(ไฟล์เดิมจะถูกเก็บสำรองใน _archive)</span></span>
        </label>

        {total > 0 && (
          <div className="space-y-3 border-t border-border-subtle pt-4">
            <div className="h-2 overflow-hidden rounded-full bg-surface-muted" role="progressbar" aria-valuemin={0} aria-valuemax={total} aria-valuenow={done}>
              <div className="h-full bg-accent transition-[width]" style={{ width: `${(done / total) * 100}%` }} />
            </div>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              <Stat value={counts.ok ?? 0} label="อัปโหลดแล้ว" />
              <Stat value={counts.skip ?? 0} label="ข้าม" />
              <Stat value={counts.error ?? 0} label="ผิดพลาด" bad />
              <Stat value={total - done} label="รอคิว" />
            </div>
            <label className="flex cursor-pointer items-center gap-2 text-body">
              <input type="checkbox" checked={onlyProblems} onChange={(e) => setOnlyProblems(e.target.checked)} className="size-4 accent-accent" />
              แสดงเฉพาะที่ข้ามหรือผิดพลาด
            </label>
            <div className="relative max-h-80 overflow-auto rounded-md border border-border-subtle">
              <table className="w-full text-body">
                <thead className="sticky top-0 bg-surface-muted text-left text-caption text-ink-muted">
                  <tr><th className="px-3 py-2">ไฟล์</th><th className="px-3 py-2">ผล</th></tr>
                </thead>
                <tbody>
                  {visible.map((r, i) => (
                    <tr key={`${r.name}-${i}`} className="border-t border-border-subtle">
                      <td className="px-3 py-1.5 font-mono">{r.name}</td>
                      <td className={`px-3 py-1.5 ${tone[r.status]}`}>{labels[r.status]} · {r.message}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>
    </TitledSection>
  )
}

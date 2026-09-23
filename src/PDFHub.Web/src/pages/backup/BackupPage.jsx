import { useCallback, useEffect, useState } from 'react'
import { CircleCheck, CircleX, DatabaseBackup, FolderSearch } from 'lucide-react'
import { backupsApi } from '../../api'
import { ApiError, errorMessage } from '../../lib/apiClient'
import { formatDateTime, formatFileSize } from '../../lib/format'
import { toast } from '../../lib/toast'
import { useAsyncData } from '../../lib/useAsyncData'
import { AppButton } from '../../components/ui/buttons'
import { ErrorSurface, LoadingSurface } from '../../components/ui/feedback'
import { Field, FormActions, Page, PageHeader, TitledSection } from '../../components/ui/layout'
import { inputClassName } from '../../components/ui/styles'

const STATUS = {
  Running: { label: 'กำลังสำรอง', className: 'bg-accent-soft text-accent' },
  Succeeded: { label: 'สำเร็จ', className: 'bg-success-soft text-success' },
  Failed: { label: 'ไม่สำเร็จ', className: 'bg-danger-soft text-danger' },
}

export function BackupPage() {
  const load = useCallback((signal) => backupsApi.overview(signal), [])
  const { data, error, loading, refreshing, retry } = useAsyncData(load)
  const running = data?.running

  // While a backup runs on the server, refresh its progress every 2 seconds. Stops as soon as it finishes
  // or a refresh fails (the error surface then offers Retry) — never a retry loop.
  useEffect(() => {
    if (!running || error) return
    const timeout = window.setTimeout(retry, 2000)
    return () => window.clearTimeout(timeout)
  }, [running, error, data, retry])

  // Tell the admin when the run they watched finishes.
  const [watchedId, setWatchedId] = useState(null)
  if (running && watchedId !== running.id) setWatchedId(running.id)
  if (!running && watchedId !== null && data) {
    const finished = data.history.find((r) => r.id === watchedId)
    setWatchedId(null)
    if (finished?.status === 'Succeeded') toast.success(`สำรองข้อมูลไปที่ ${finished.destination} เรียบร้อยแล้ว`)
    else if (finished?.status === 'Failed') toast.error(`สำรองข้อมูลไม่สำเร็จ: ${finished.error}`)
  }

  return (
    <Page wide>
      <PageHeader title="สำรองข้อมูล" meta="คัดลอกฐานข้อมูลและไฟล์ PDF ทั้งหมดไปเก็บที่ดิสก์อื่น, USB หรือ NAS" />

      {loading && !data ? <LoadingSurface /> : !data ? <ErrorSurface onRetry={retry}>{errorMessage(error)}</ErrorSurface> : (
        <>
          {error && <ErrorSurface onRetry={retry} compact>{errorMessage(error)} กำลังแสดงข้อมูลก่อนหน้า</ErrorSurface>}
          <StartBackup key={data.lastDestination} lastDestination={data.lastDestination} running={running} onStarted={retry} />
          {running && <Progress run={running} />}
          <History runs={data.history} refreshing={refreshing} />
          <p className="text-caption text-ink-muted">
            นอกจากนี้ระบบสำรองฐานข้อมูลอัตโนมัติวันละครั้งไว้ที่ <span className="font-mono">{data.dailyBackupFolder}</span> บน server
            (อยู่ดิสก์เดียวกับข้อมูล จึงควรสำรองไปที่อื่นด้วยหน้านี้เป็นประจำ)
          </p>
        </>
      )}
    </Page>
  )
}

function StartBackup({ lastDestination, running, onStarted }) {
  const [destination, setDestination] = useState(lastDestination)
  const [check, setCheck] = useState(null)
  const [fieldError, setFieldError] = useState(null)
  const [busy, setBusy] = useState(null) // 'check' | 'start'

  const change = (value) => {
    setDestination(value)
    setCheck(null)
    setFieldError(null)
  }

  const test = async () => {
    setBusy('check')
    try {
      setCheck(await backupsApi.check(destination))
    } catch (err) {
      toast.error(errorMessage(err))
    } finally {
      setBusy(null)
    }
  }

  const start = async () => {
    setBusy('start')
    setFieldError(null)
    try {
      await backupsApi.start(destination)
      toast.info('เริ่มสำรองข้อมูลแล้ว ทำงานต่อได้ตามปกติ ระบบจะแจ้งเมื่อเสร็จ')
      onStarted()
    } catch (err) {
      if (err instanceof ApiError && err.errors.Destination) setFieldError(err.errors.Destination.join(' '))
      else toast.error(errorMessage(err))
    } finally {
      setBusy(null)
    }
  }

  return (
    <TitledSection title="สำรองข้อมูลตอนนี้"
      description="ระบบจะสร้างโฟลเดอร์ PDFHub ในปลายทาง: ฐานข้อมูลได้สำเนาใหม่ทุกครั้งที่ PDFHub\db และไฟล์ PDF ถูกคัดลอกไปที่ PDFHub\pdf เฉพาะไฟล์ใหม่หรือไฟล์ที่เปลี่ยน ครั้งแรกจึงนานกว่าครั้งต่อไป ไฟล์ที่ปลายทางจะไม่ถูกลบ">
      <div className="space-y-4">
        <Field label="โฟลเดอร์ปลายทางบน server" required error={fieldError}
          hint={'path บนเครื่อง server เช่น E:\\PDFHubBackup (ดิสก์ลูกที่สอง / USB) หรือ \\\\NAS01\\Backup (แชร์ในเครือข่าย)'}>
          <input value={destination} onChange={(e) => change(e.target.value)} placeholder="E:\PDFHubBackup" spellCheck={false}
            aria-invalid={fieldError ? true : undefined} className={inputClassName('w-full font-mono')} />
        </Field>

        {check && (
          <p className={`flex items-start gap-1.5 text-body ${check.ok ? 'text-success' : 'text-danger'}`} role="status">
            {check.ok ? <CircleCheck className="mt-0.5 size-4 shrink-0" aria-hidden /> : <CircleX className="mt-0.5 size-4 shrink-0" aria-hidden />}
            {check.message}
          </p>
        )}

        <FormActions>
          <AppButton disabled={!destination.trim() || busy !== null} loading={busy === 'check'} onClick={test}>
            <FolderSearch className="size-4" aria-hidden /> ทดสอบปลายทาง
          </AppButton>
          <AppButton variant="primary" disabled={!destination.trim() || busy !== null || Boolean(running)} loading={busy === 'start'} onClick={start}
            title={running ? 'รอให้การสำรองครั้งที่กำลังทำงานเสร็จก่อน' : undefined}>
            <DatabaseBackup className="size-4" aria-hidden /> เริ่มสำรองข้อมูล
          </AppButton>
        </FormActions>
      </div>
    </TitledSection>
  )
}

function Progress({ run }) {
  const done = run.pdfCopied + run.pdfSkipped
  const percent = run.pdfTotal ? Math.round((done / run.pdfTotal) * 100) : 0
  return (
    <section className="space-y-2 rounded-lg border border-accent/30 bg-accent-soft p-4" aria-live="polite">
      <p className="font-semibold text-accent">กำลังสำรองข้อมูลไปที่ <span className="font-mono">{run.destination}</span></p>
      <div className="h-2 overflow-hidden rounded-full bg-surface-panel" role="progressbar" aria-valuemin={0} aria-valuemax={run.pdfTotal} aria-valuenow={done}>
        <div className="h-full bg-accent transition-[width]" style={{ width: `${percent}%` }} />
      </div>
      <p className="text-body text-ink-strong">
        {run.databaseFile ? 'สำเนาฐานข้อมูลเสร็จแล้ว · ' : 'กำลังสำเนาฐานข้อมูล… · '}
        PDF {done.toLocaleString()} / {run.pdfTotal.toLocaleString()} ไฟล์ (คัดลอกใหม่ {run.pdfCopied.toLocaleString()} · {formatFileSize(run.bytesCopied) || '0 B'})
      </p>
    </section>
  )
}

function History({ runs, refreshing }) {
  return (
    <TitledSection title="ประวัติการสำรองข้อมูล" description="30 ครั้งล่าสุด" bodyClassName="">
      {runs.length === 0 ? (
        <p className="px-4 py-6 text-center text-body text-ink-muted sm:px-5">ยังไม่เคยสำรองข้อมูลจากหน้านี้</p>
      ) : (
        <div aria-busy={refreshing} className="relative overflow-x-auto">
          <table className="w-full min-w-[860px] text-body">
            <thead className="border-b border-border-subtle bg-surface-muted text-left text-caption text-ink-muted">
              <tr>
                <th className="px-3 py-2.5">เริ่ม</th>
                <th className="px-3 py-2.5">ปลายทาง</th>
                <th className="px-3 py-2.5">สถานะ</th>
                <th className="px-3 py-2.5 text-right">PDF คัดลอกใหม่ / ทั้งหมด</th>
                <th className="px-3 py-2.5 text-right">ขนาดที่คัดลอก</th>
                <th className="px-3 py-2.5">โดย</th>
              </tr>
            </thead>
            <tbody>
              {runs.map((r) => (
                <tr key={r.id} className="border-b border-border-subtle align-top last:border-0">
                  <td className="px-3 py-2 whitespace-nowrap tabular-nums">
                    {formatDateTime(r.startedAt)}
                    {r.finishedAt && <span className="block text-caption text-ink-muted">ใช้เวลา {duration(r.startedAt, r.finishedAt)}</span>}
                  </td>
                  <td className="px-3 py-2 font-mono break-all">{r.destination}</td>
                  <td className="px-3 py-2">
                    <span className={`inline-block rounded-md px-2 py-0.5 text-caption font-semibold whitespace-nowrap ${STATUS[r.status]?.className ?? ''}`}>
                      {STATUS[r.status]?.label ?? r.status}
                    </span>
                    {r.error && <span className="mt-1 block max-w-md text-caption text-danger">{r.error}</span>}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">{r.pdfCopied.toLocaleString()} / {r.pdfTotal.toLocaleString()}</td>
                  <td className="px-3 py-2 text-right tabular-nums">{formatFileSize(r.bytesCopied) || '—'}</td>
                  <td className="px-3 py-2">{r.startedBy}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </TitledSection>
  )
}

function duration(from, to) {
  const seconds = Math.max(0, Math.round((new Date(to) - new Date(from)) / 1000))
  return seconds < 60 ? `${seconds} วินาที` : `${Math.floor(seconds / 60)} นาที ${seconds % 60} วินาที`
}

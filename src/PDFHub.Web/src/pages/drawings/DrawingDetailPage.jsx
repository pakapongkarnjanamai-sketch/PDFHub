import { useCallback } from 'react'
import { useLocation, useParams } from 'react-router-dom'
import { ArrowLeft, Check, Download, FileText, Pencil } from 'lucide-react'
import { drawingsApi } from '../../api'
import { errorMessage } from '../../lib/apiClient'
import { formatDate, formatDateTime, formatFileSize, formatPrice } from '../../lib/format'
import { useAsyncData } from '../../lib/useAsyncData'
import { useSession } from '../../session/SessionContext'
import { pdfUrl } from '../../config/appConfig'
import { ExternalAction, LinkButton } from '../../components/ui/buttons'
import { EmptySurface, ErrorSurface, LoadingSurface } from '../../components/ui/feedback'
import { Page, PageHeader } from '../../components/ui/layout'

export function DrawingDetailPage() {
  const { code } = useParams()
  const { state } = useLocation()
  const { session } = useSession()
  const load = useCallback((signal) => drawingsApi.get(code, signal), [code])
  const { data: d, error, loading, retry } = useAsyncData(load)

  // Back to the list the user came from, filters included; the plain list when opened from a link.
  const backTo = state?.listSearch ? `/drawings?${state.listSearch}` : '/drawings'
  const back = <LinkButton to={backTo} variant="ghost"><ArrowLeft className="size-4" aria-hidden /> กลับไปรายการ</LinkButton>

  if (loading && !d) return <Page wide><LoadingSurface /></Page>
  if (!d) {
    return (
      <Page wide>
        <ErrorSurface onRetry={error?.status === 404 ? undefined : retry} action={error?.status === 404 ? back : undefined}>
          {errorMessage(error)}
        </ErrorSurface>
      </Page>
    )
  }

  return (
    <Page wide>
      <PageHeader
        title={<span className="font-mono">{d.pdfCode}</span>}
        status={
          <>
            <span className="rounded-md bg-surface-muted px-2 py-0.5 text-caption font-semibold text-ink-strong">{d.sectionName}</span>
            {d.hasPo && (
              <span className="inline-flex items-center gap-1 rounded-md bg-success-soft px-2 py-0.5 text-caption font-semibold text-success">
                <Check className="size-3.5" aria-hidden /> มี PO แล้ว
              </span>
            )}
          </>
        }
        meta={d.partName}
        actions={
          <>
            {back}
            {d.hasPdf && (
              <>
                <ExternalAction href={pdfUrl(d.pdfCode)}>เปิดแท็บใหม่</ExternalAction>
                <ExternalAction href={pdfUrl(d.pdfCode, { download: true })} newTab={false} download>
                  <Download className="size-4" aria-hidden /> ดาวน์โหลด
                </ExternalAction>
              </>
            )}
            {session.canEdit && (
              <LinkButton to={`/drawings/${d.pdfCode}/edit`} state={state} variant="primary"><Pencil className="size-4" aria-hidden /> แก้ไข</LinkButton>
            )}
          </>
        }
      />

      <div className="grid items-start gap-4 xl:grid-cols-[22rem_1fr]">
        <section className="rounded-lg border border-border-subtle bg-surface-panel px-4 py-2" aria-label="ข้อมูล Drawing">
          <dl className="grid grid-cols-[7.5rem_1fr] text-body">
            <Row label="PdfCode"><span className="font-mono">{d.pdfCode}</span></Row>
            <Row label="Section">{d.sectionName} <span className="text-ink-muted">({d.sectionCode})</span></Row>
            <Row label="Part Name">{d.partName}</Row>
            <Row label="Drawing No."><span className="font-mono">{d.drawingNo || '—'}</span></Row>
            <Row label="Material">{d.material || '—'}</Row>
            <Row label="Price">{d.price != null ? `${formatPrice(d.price)} บาท` : '—'}</Row>
            <Row label="Input Date">{formatDate(d.inputDate)}</Row>
            <Row label="Quotation No.">{d.quoNo || '—'}</Row>
            <Row label="Have a PO">{d.hasPo ? 'มี PO แล้ว' : 'ยังไม่มี'}</Row>
            <Row label="Remark"><span className="whitespace-pre-wrap">{d.remark || '—'}</span></Row>
          </dl>
          <div className="space-y-0.5 py-3 text-caption text-ink-muted">
            {d.hasPdf && <p>ไฟล์ PDF {formatFileSize(d.pdfSize)} · อัปโหลด {formatDateTime(d.pdfUploadedAt)}</p>}
            <p>สร้าง {formatDateTime(d.createdAt)}{d.createdBy && ` โดย ${d.createdBy}`}</p>
            {d.updatedAt && <p>แก้ไขล่าสุด {formatDateTime(d.updatedAt)}{d.updatedBy && ` โดย ${d.updatedBy}`}</p>}
          </div>
        </section>

        <section className="overflow-hidden rounded-lg border border-border-subtle bg-surface-panel" aria-label="ไฟล์ PDF">
          {d.hasPdf ? (
            <iframe src={pdfUrl(d.pdfCode)} title={`${d.pdfCode}.pdf`} className="block h-[calc(100dvh-13rem)] min-h-[32rem] w-full border-0" />
          ) : (
            <EmptySurface title="ยังไม่มีไฟล์ PDF"
              action={session.canEdit && <LinkButton to={`/drawings/${d.pdfCode}/edit`}><FileText className="size-4" aria-hidden /> แนบไฟล์ PDF</LinkButton>}>
              แนบไฟล์ได้ที่หน้าแก้ไข หรืออัปโหลดหลายไฟล์พร้อมกันที่หน้านำเข้าข้อมูล
            </EmptySurface>
          )}
        </section>
      </div>
    </Page>
  )
}

function Row({ label, children }) {
  return (
    <>
      <dt className="border-b border-border-subtle py-2.5 text-ink-muted">{label}</dt>
      <dd className="border-b border-border-subtle py-2.5 font-medium break-words">{children}</dd>
    </>
  )
}

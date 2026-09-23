import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Download, FileText, Pencil, Plus, Search, X } from 'lucide-react'
import { drawingsApi } from '../../api'
import { errorMessage } from '../../lib/apiClient'
import { formatDate, formatPrice } from '../../lib/format'
import { useAsyncData } from '../../lib/useAsyncData'
import { useSession } from '../../session/SessionContext'
import { pdfUrl } from '../../config/appConfig'
import { AppButton, ExternalAction, IconButton, LinkButton } from '../../components/ui/buttons'
import { EmptySurface, ErrorSurface, LoadingSurface } from '../../components/ui/feedback'
import { Page, PageHeader } from '../../components/ui/layout'
import { Pagination } from '../../components/ui/Pagination'
import { inputClassName, recordLinkClassName } from '../../components/ui/styles'

const FILTER_KEYS = ['q', 'section', 'year', 'po', 'pdf']
const DEFAULT_SORT = 'date'

const COLUMNS = [
  { key: 'code', label: 'PdfCode' },
  { key: 'section', label: 'Section' },
  { key: 'part', label: 'Part Name' },
  { key: 'drawing', label: 'Drawing No.' },
  { key: 'material', label: 'Material' },
  { key: 'price', label: 'Price', numeric: true },
  { key: null, label: 'PDF', center: true },
  { key: 'date', label: 'Input Date' },
  { key: 'quo', label: 'Quo No.' },
  { key: null, label: 'Remark' },
  { key: 'po', label: 'PO No.' },
]

/** Descending first for dates and prices (newest, most expensive), ascending for text. */
const defaultDir = (key) => (key === 'date' || key === 'price' ? 'desc' : 'asc')

export function DrawingsPage() {
  const { session } = useSession()
  const [searchParams, setSearchParams] = useSearchParams()

  // The URL is the single source of truth: reload, Back and shared links reproduce the view.
  const query = useMemo(() => ({
    q: searchParams.get('q') ?? '',
    section: searchParams.get('section') ?? '',
    year: searchParams.get('year') ?? '',
    po: searchParams.get('po') ?? '',
    pdf: searchParams.get('pdf') ?? '',
    sort: searchParams.get('sort') ?? DEFAULT_SORT,
    dir: searchParams.get('dir') ?? defaultDir(searchParams.get('sort') ?? DEFAULT_SORT),
    page: Number(searchParams.get('page') ?? 1),
  }), [searchParams])

  const load = useCallback((signal) => drawingsApi.list(query, signal), [query])
  const { data, error, loading, refreshing, retry } = useAsyncData(load)

  const update = useCallback((changes, { resetPage = true } = {}) => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current)
      for (const [key, value] of Object.entries(changes)) {
        if (value === '' || value === null || value === undefined) next.delete(key)
        else next.set(key, String(value))
      }
      if (resetPage) next.delete('page')
      return next
    })
  }, [setSearchParams])

  const isFiltered = FILTER_KEYS.some((k) => searchParams.get(k))
  const clearFilters = () => update(Object.fromEntries(FILTER_KEYS.map((k) => [k, ''])))

  const sortBy = (key) => {
    const dir = query.sort === key ? (query.dir === 'asc' ? 'desc' : 'asc') : defaultDir(key)
    update(key === DEFAULT_SORT && dir === defaultDir(key) ? { sort: '', dir: '' } : { sort: key, dir })
  }

  const { page: _page, ...exportQuery } = query
  const listSearch = searchParams.toString()

  return (
    <Page wide>
      <PageHeader
        title="รายการ Drawing"
        meta={data && (isFiltered
          ? `พบ ${data.filteredCount.toLocaleString()} จากทั้งหมด ${data.totalCount.toLocaleString()} รายการ`
          : `ทั้งหมด ${data.totalCount.toLocaleString()} รายการ`)}
        actions={
          <>
            <ExternalAction href={drawingsApi.exportUrl(exportQuery)} newTab={false} download>
              <Download className="size-4" aria-hidden /> Export Excel
            </ExternalAction>
            {session.canEdit && (
              <LinkButton to="/drawings/new" variant="primary"><Plus className="size-4" aria-hidden /> เพิ่ม Drawing</LinkButton>
            )}
          </>
        }
      />

      <Toolbar query={query} options={data?.filterOptions} update={update} isFiltered={isFiltered} onClear={clearFilters} />

      {loading && !data ? (
        <LoadingSurface />
      ) : !data ? (
        <ErrorSurface onRetry={retry}>{errorMessage(error)}</ErrorSurface>
      ) : (
        <>
          {error && <ErrorSurface onRetry={retry} compact>{errorMessage(error)} กำลังแสดงผลลัพธ์ก่อนหน้า</ErrorSurface>}
          {data.items.length === 0 ? (
            isFiltered ? (
              <EmptySurface title="ไม่พบ Drawing ตามเงื่อนไข" action={<AppButton onClick={clearFilters}>ล้างตัวกรอง</AppButton>}>
                ลองใช้คำค้นที่สั้นลง หรือเลือกเงื่อนไขอื่น
              </EmptySurface>
            ) : (
              <EmptySurface title="ยังไม่มีข้อมูล Drawing"
                action={session.canEdit && <LinkButton to="/import">นำเข้าจากไฟล์ Excel เดิม</LinkButton>}>
                {session.canEdit ? 'เพิ่มทีละรายการด้วยปุ่ม "เพิ่ม Drawing" หรือนำเข้าจากไฟล์ Excel' : 'ยังไม่มีการบันทึกข้อมูล'}
              </EmptySurface>
            )
          ) : (
            <div className="overflow-hidden rounded-lg border border-border-subtle bg-surface-panel">
              <div aria-busy={refreshing} className={`relative overflow-x-auto transition-opacity ${refreshing ? 'pointer-events-none opacity-60' : ''}`}>
                <table className="w-full min-w-[1100px] border-collapse text-body">
                  <thead>
                    <tr className="border-b border-border-subtle bg-surface-muted text-left">
                      <th scope="col" className="px-3 py-2.5 text-right text-caption font-semibold text-ink-muted">No.</th>
                      {COLUMNS.map((col) => (
                        <th key={col.label} scope="col"
                          aria-sort={col.key && query.sort === col.key ? (query.dir === 'asc' ? 'ascending' : 'descending') : undefined}
                          className={`px-3 py-2.5 text-caption font-semibold whitespace-nowrap text-ink-muted ${col.numeric ? 'text-right' : col.center ? 'text-center' : ''}`}>
                          {col.key ? (
                            <button type="button" onClick={() => sortBy(col.key)}
                              className={`inline-flex items-center gap-1 rounded-sm hover:text-ink-strong focus-visible:outline-2 focus-visible:outline-accent ${query.sort === col.key ? 'text-accent' : ''}`}>
                              {col.label}
                              <span aria-hidden className="text-[10px]">{query.sort === col.key ? (query.dir === 'asc' ? '▲' : '▼') : '↕'}</span>
                            </button>
                          ) : col.label}
                        </th>
                      ))}
                      {session.canEdit && <th scope="col" className="w-12"><span className="sr-only">แก้ไข</span></th>}
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((d, i) => (
                      <tr key={d.id} className="border-b border-border-subtle last:border-0 hover:bg-accent-soft/40">
                        <td className="px-3 py-2 text-right text-ink-muted tabular-nums">{(data.page - 1) * data.pageSize + i + 1}</td>
                        <td className="px-3 py-2 whitespace-nowrap">
                          <Link to={`/drawings/${d.pdfCode}`} state={{ listSearch }} className={`font-mono font-semibold ${recordLinkClassName}`}>{d.pdfCode}</Link>
                        </td>
                        <td className="px-3 py-2 whitespace-nowrap" title={d.sectionCode}>{d.sectionName}</td>
                        <td className="px-3 py-2 font-medium">{d.partName}</td>
                        <td className="px-3 py-2 font-mono whitespace-nowrap">{d.drawingNo}</td>
                        <td className="px-3 py-2 whitespace-nowrap">{d.material}</td>
                        <td className="px-3 py-2 text-right tabular-nums">{formatPrice(d.price)}</td>
                        <td className="px-3 py-2 text-center">
                          {d.hasPdf ? (
                            <a href={pdfUrl(d.pdfCode)} target="_blank" rel="noreferrer" aria-label={`เปิด ${d.pdfCode}.pdf`} title={`เปิด ${d.pdfCode}.pdf`}
                              className="inline-grid size-8 place-items-center rounded-md text-danger hover:bg-danger-soft focus-visible:outline-2 focus-visible:outline-accent">
                              <FileText className="size-4" />
                            </a>
                          ) : (
                            <span className="text-ink-soft" title="ยังไม่มีไฟล์ PDF">—</span>
                          )}
                        </td>
                        <td className="px-3 py-2 whitespace-nowrap tabular-nums">{formatDate(d.inputDate)}</td>
                        <td className="px-3 py-2 whitespace-nowrap">{d.quoNo}</td>
                        <td className="max-w-56 truncate px-3 py-2 text-ink-muted" title={d.remark}>{d.remark}</td>
                        <td className="px-3 py-2 whitespace-nowrap">{d.poNo}</td>
                        {session.canEdit && (
                          <td className="px-2 py-1 text-right">
                            <IconButton label={`แก้ไข ${d.pdfCode}`} size="sm" tone="primary" to={`/drawings/${d.pdfCode}/edit`}>
                              <Pencil className="size-4" />
                            </IconButton>
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pagination page={data.page} pageSize={data.pageSize} total={data.filteredCount}
                onPageChange={(page) => update({ page: page > 1 ? page : '' }, { resetPage: false })} />
            </div>
          )}
        </>
      )}
    </Page>
  )
}

function Toolbar({ query, options, update, isFiltered, onClear }) {
  const [searchInput, setSearchInput] = useState(query.q)

  // Re-sync when the URL changes from outside (Back, a shared link).
  const [lastQ, setLastQ] = useState(query.q)
  if (lastQ !== query.q) {
    setLastQ(query.q)
    setSearchInput(query.q)
  }

  // Debounced write to the URL; Enter flushes immediately.
  useEffect(() => {
    if (searchInput === query.q) return
    const timeout = window.setTimeout(() => update({ q: searchInput.trim() }), 400)
    return () => window.clearTimeout(timeout)
  }, [searchInput, query.q, update])

  const select = (key, label, children) => (
    <label className="flex min-w-0 flex-col gap-1">
      <span className="text-caption font-semibold text-ink-muted">{label}</span>
      <select value={query[key]} onChange={(e) => update({ [key]: e.target.value })} className={inputClassName('w-full sm:w-auto sm:min-w-36')}>
        {children}
      </select>
    </label>
  )

  return (
    <div className="flex flex-wrap items-end gap-3 rounded-lg border border-border-subtle bg-surface-panel p-3">
      <label className="flex min-w-64 flex-[1_1_20rem] flex-col gap-1">
        <span className="text-caption font-semibold text-ink-muted">ค้นหา</span>
        <span className="relative">
          <Search className="pointer-events-none absolute top-2.5 left-2.5 size-4 text-ink-soft" aria-hidden />
          <input type="search" value={searchInput} onChange={(e) => setSearchInput(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') update({ q: searchInput.trim() }) }}
            placeholder="PdfCode, Part Name, Drawing No., Material, Quo No., PO No., หมายเหตุ"
            className={inputClassName('w-full pr-9 pl-8')} />
          {searchInput && (
            <button type="button" aria-label="ล้างคำค้น" onClick={() => { setSearchInput(''); update({ q: '' }) }}
              className="absolute top-1.5 right-1.5 grid size-6 place-items-center rounded-sm text-ink-muted hover:text-ink-strong focus-visible:outline-2 focus-visible:outline-accent">
              <X className="size-4" />
            </button>
          )}
        </span>
      </label>

      <div className="grid w-full grid-cols-2 gap-3 sm:flex sm:w-auto sm:flex-wrap">
        {select('section', 'Section', (
          <>
            <option value="">ทั้งหมด</option>
            {options?.sections.map((s) => <option key={s.code} value={s.code}>{s.code} · {s.name}</option>)}
          </>
        ))}
        {select('year', 'ปี', (
          <>
            <option value="">ทั้งหมด</option>
            {options?.years.map((y) => <option key={y} value={y}>{y}</option>)}
          </>
        ))}
        {select('po', 'PO', (
          <>
            <option value="">ทั้งหมด</option>
            <option value="yes">มี PO แล้ว</option>
            <option value="no">ยังไม่มี PO</option>
          </>
        ))}
        {select('pdf', 'ไฟล์ PDF', (
          <>
            <option value="">ทั้งหมด</option>
            <option value="has">มีไฟล์แล้ว</option>
            <option value="missing">ยังไม่มีไฟล์</option>
          </>
        ))}
      </div>

      {isFiltered && <AppButton variant="ghost" onClick={onClear}>ล้างตัวกรอง</AppButton>}
    </div>
  )
}

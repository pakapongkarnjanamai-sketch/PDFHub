import { ChevronLeft, ChevronRight } from 'lucide-react'
import { IconButton } from './buttons'

/** Footer of a paged table: range summary and page buttons. Pages are 1-based. */
export function Pagination({ page, pageSize, total, onPageChange }) {
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  if (total === 0) return null

  const first = (page - 1) * pageSize + 1
  const last = Math.min(total, page * pageSize)
  const pages = []
  for (let p = Math.max(1, page - 2); p <= Math.min(pageCount, page + 2); p++) pages.push(p)

  return (
    <nav className="flex flex-wrap items-center justify-between gap-3 border-t border-border-subtle px-4 py-3" aria-label="เลือกหน้า">
      <p className="text-body text-ink-muted">
        แสดง {first.toLocaleString()}–{last.toLocaleString()} จาก {total.toLocaleString()} รายการ
      </p>
      {pageCount > 1 && (
        <div className="flex items-center gap-1">
          <IconButton label="หน้าก่อนหน้า" size="sm" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
            <ChevronLeft className="size-4" />
          </IconButton>
          {pages[0] > 1 && <PageButton page={1} onClick={onPageChange} />}
          {pages[0] > 2 && <span className="px-1 text-ink-muted">…</span>}
          {pages.map((p) => <PageButton key={p} page={p} current={p === page} onClick={onPageChange} />)}
          {pages.at(-1) < pageCount - 1 && <span className="px-1 text-ink-muted">…</span>}
          {pages.at(-1) < pageCount && <PageButton page={pageCount} onClick={onPageChange} />}
          <IconButton label="หน้าถัดไป" size="sm" disabled={page >= pageCount} onClick={() => onPageChange(page + 1)}>
            <ChevronRight className="size-4" />
          </IconButton>
        </div>
      )}
    </nav>
  )
}

function PageButton({ page, current = false, onClick }) {
  return (
    <button type="button" onClick={() => onClick(page)} aria-current={current ? 'page' : undefined}
      className={`h-8 min-w-8 rounded-md px-2 text-body tabular-nums focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ${
        current ? 'bg-accent font-semibold text-white' : 'text-ink-strong hover:bg-surface-muted'
      }`}>
      {page}
    </button>
  )
}

import { CircleAlert, Inbox, LoaderCircle } from 'lucide-react'
import { AppButton } from './buttons'

export function LoadingSurface({ children = 'กำลังโหลดข้อมูล…' }) {
  return (
    <div className="flex min-h-48 items-center justify-center gap-2 rounded-lg border border-border-subtle bg-surface-panel text-ink-muted" role="status">
      <LoaderCircle className="size-5 animate-spin" aria-hidden />
      {children}
    </div>
  )
}

export function EmptySurface({ title, children, action }) {
  return (
    <div className="flex min-h-48 flex-col items-center justify-center gap-2 rounded-lg border border-border-subtle bg-surface-panel p-6 text-center">
      <Inbox className="size-8 text-ink-soft" aria-hidden />
      <p className="text-heading font-semibold text-ink-strong">{title}</p>
      {children && <p className="max-w-md text-body text-ink-muted">{children}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}

/** Every error offers a way forward. Pass onRetry, or an action. */
export function ErrorSurface({ children, onRetry, retryLabel = 'ลองใหม่', action, compact = false }) {
  return (
    <div role="alert"
      className={`flex flex-wrap items-center justify-between gap-3 rounded-lg border border-danger/30 bg-danger-soft text-body text-danger ${compact ? 'px-3 py-2' : 'p-4'}`}>
      <p className="flex min-w-0 items-start gap-2">
        <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
        <span>{children}</span>
      </p>
      {onRetry && <AppButton size="sm" onClick={onRetry}>{retryLabel}</AppButton>}
      {action}
    </div>
  )
}

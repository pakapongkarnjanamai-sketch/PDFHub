import { useEffect, useState } from 'react'
import { CircleAlert, CircleCheck, Info, TriangleAlert, X } from 'lucide-react'
import { dismissToast, subscribeToasts } from '../../lib/toast'

const kinds = {
  success: { icon: CircleCheck, className: 'text-success' },
  info: { icon: Info, className: 'text-accent' },
  warning: { icon: TriangleAlert, className: 'text-warning' },
  error: { icon: CircleAlert, className: 'text-danger' },
}

export function ToastViewport() {
  const [toasts, setToasts] = useState([])
  useEffect(() => subscribeToasts(setToasts), [])

  return (
    <div className="pointer-events-none fixed right-4 bottom-4 z-[60] flex w-[min(24rem,calc(100vw-2rem))] flex-col gap-2" aria-live="polite">
      {toasts.map((t) => {
        const { icon: Icon, className } = kinds[t.kind]
        return (
          <div key={t.id} role={t.kind === 'error' ? 'alert' : 'status'}
            className="pointer-events-auto flex items-start gap-2 rounded-lg border border-border-subtle bg-surface-panel px-3 py-2.5 text-body shadow-lg">
            <Icon className={`mt-0.5 size-4 shrink-0 ${className}`} aria-hidden />
            <p className="min-w-0 flex-1">{t.message}</p>
            <button type="button" onClick={() => dismissToast(t.id)} aria-label="ปิดข้อความ"
              className="rounded-sm text-ink-muted hover:text-ink-strong focus-visible:outline-2 focus-visible:outline-accent">
              <X className="size-4" />
            </button>
          </div>
        )
      })}
    </div>
  )
}

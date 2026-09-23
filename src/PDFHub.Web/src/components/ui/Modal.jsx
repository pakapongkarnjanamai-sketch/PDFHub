import { useEffect, useRef } from 'react'
import { X } from 'lucide-react'
import { AppButton, IconButton } from './buttons'

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

/** Dialog with Escape to close, focus kept inside while open, and focus returned to the trigger. */
export function Modal({ open, title, onClose, children, footer }) {
  const panelRef = useRef(null)

  useEffect(() => {
    if (!open) return
    const trigger = document.activeElement
    const panel = panelRef.current
    panel?.querySelector(FOCUSABLE)?.focus()

    const onKeyDown = (event) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
      } else if (event.key === 'Tab' && panel) {
        const items = [...panel.querySelectorAll(FOCUSABLE)]
        if (items.length === 0) return
        const first = items[0]
        const last = items[items.length - 1]
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault()
          last.focus()
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault()
          first.focus()
        }
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      trigger?.focus?.()
    }
  }, [open, onClose])

  if (!open) return null
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div aria-hidden className="absolute inset-0 bg-ink-strong/40" onClick={onClose} />
      <div ref={panelRef} role="dialog" aria-modal="true" aria-labelledby="modal-title"
        className="relative w-full max-w-md rounded-lg border border-border-subtle bg-surface-panel shadow-xl">
        <div className="flex items-center justify-between gap-3 border-b border-border-subtle px-5 py-3">
          <h2 id="modal-title" className="text-heading font-semibold">{title}</h2>
          <IconButton label="ปิด" size="sm" onClick={onClose}><X className="size-4" /></IconButton>
        </div>
        <div className="px-5 py-4 text-body text-ink-strong">{children}</div>
        {footer && <div className="flex flex-wrap justify-end gap-2 border-t border-border-subtle px-5 py-3">{footer}</div>}
      </div>
    </div>
  )
}

/** Replaces window.confirm: the app's own dialog, Cancel as ghost, confirm as primary or danger. */
export function ConfirmDialog({ open, title, children, confirmLabel, danger = false, busy = false, onConfirm, onCancel }) {
  return (
    <Modal open={open} title={title} onClose={busy ? () => {} : onCancel}
      footer={
        <>
          <AppButton variant="ghost" onClick={onCancel} disabled={busy}>ยกเลิก</AppButton>
          <AppButton variant={danger ? 'danger' : 'primary'} loading={busy} onClick={onConfirm}>{confirmLabel}</AppButton>
        </>
      }>
      {children}
    </Modal>
  )
}

/** One max width and vertical rhythm for list, form and detail pages. */
export function Page({ wide = false, children }) {
  return <div className={`mx-auto min-h-full w-full space-y-5 overflow-y-auto ${wide ? 'max-w-[1600px]' : 'max-w-5xl'}`}>{children}</div>
}

/** Title, optional status/meta line and wrapping actions — on the page surface, never in a card. */
export function PageHeader({ title, meta, status, actions }) {
  return (
    <header className="flex flex-wrap items-end justify-between gap-3">
      <div className="min-w-0 space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-title font-semibold text-ink-strong">{title}</h1>
          {status}
        </div>
        {meta && <p className="text-body text-ink-muted">{meta}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </header>
  )
}

/** Panel for a coherent block of fields or content. */
export function FormSection({ className = '', children }) {
  return (
    <section className={`rounded-lg border border-border-subtle bg-surface-panel p-4 sm:p-5 ${className}`}>{children}</section>
  )
}

/** Hairline divider inside a FormSection. */
export function FormSectionDivider() {
  return <hr className="-mx-4 my-4 border-t border-border-subtle sm:-mx-5" />
}

/** Section with a header bar; the children own the body layout. */
export function TitledSection({ title, description, action, children, bodyClassName = 'p-4 sm:p-5' }) {
  return (
    <section className="overflow-hidden rounded-lg border border-border-subtle bg-surface-panel">
      <div className="flex flex-wrap items-start justify-between gap-3 border-b border-border-subtle px-4 py-3 sm:px-5">
        <div className="min-w-0 space-y-0.5">
          <h2 className="text-heading font-semibold text-ink-strong">{title}</h2>
          {description && <p className="text-body text-ink-muted">{description}</p>}
        </div>
        {action}
      </div>
      <div className={bodyClassName}>{children}</div>
    </section>
  )
}

/** Ordering, wrapping and gap of a form's buttons. */
export function FormActions({ children, className = '' }) {
  return <div className={`flex flex-wrap items-center gap-2 ${className}`}>{children}</div>
}

/** A labelled field. data-invalid on the field (never on a group) is what focusFirstInvalid() looks for. */
export function Field({ label, required, error, hint, className = '', children }) {
  return (
    <label className={`block min-w-0 ${className}`} data-invalid={error ? 'true' : undefined}>
      <span className="mb-1 block text-caption font-semibold text-ink-strong">
        {label}
        {required && <span className="text-danger"> *</span>}
      </span>
      {children}
      {error ? (
        <span className="mt-1 block text-caption text-danger" role="alert">{error}</span>
      ) : (
        hint && <span className="mt-1 block text-caption text-ink-muted">{hint}</span>
      )}
    </label>
  )
}

/** Groups fields that are one idea; no frame of its own. */
export function FieldGroup({ legend, className = '', children }) {
  return (
    <fieldset className={`min-w-0 ${className}`}>
      <legend className="mb-2 text-caption font-semibold uppercase tracking-wider text-ink-muted">{legend}</legend>
      {children}
    </fieldset>
  )
}

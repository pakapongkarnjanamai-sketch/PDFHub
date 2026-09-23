import { ExternalLink, LoaderCircle } from 'lucide-react'
import { Link } from 'react-router-dom'
import { buttonClassName } from './styles'

/** Every text command goes through this: one look per role across the app. */
export function AppButton({ variant = 'secondary', size = 'md', loading = false, disabled, className = '', children, type = 'button', ...rest }) {
  return (
    <button type={type} disabled={disabled || loading} aria-busy={loading || undefined}
      className={buttonClassName(variant, size, className)} {...rest}>
      {loading && <LoaderCircle className="size-4 animate-spin" aria-hidden />}
      {children}
    </button>
  )
}

/** Internal navigation that looks like a button. */
export function LinkButton({ to, variant = 'secondary', size = 'md', className = '', children, ...rest }) {
  return (
    <Link to={to} className={buttonClassName(variant, size, className)} {...rest}>
      {children}
    </Link>
  )
}

/** A link leaving the SPA (PDF, file download) that looks like a button. */
export function ExternalAction({ href, variant = 'secondary', size = 'md', newTab = true, className = '', children, ...rest }) {
  return (
    <a href={href} className={buttonClassName(variant, size, className)}
      {...(newTab ? { target: '_blank', rel: 'noreferrer' } : {})} {...rest}>
      {children}
      {newTab && <ExternalLink className="size-3.5" aria-hidden />}
    </a>
  )
}

const tones = {
  neutral: 'text-ink-muted hover:bg-surface-muted hover:text-ink-strong',
  primary: 'text-accent hover:bg-accent-soft',
  danger: 'text-ink-muted hover:bg-danger-soft hover:text-danger',
}

const iconSizes = { sm: 'size-8', md: 'size-9', lg: 'size-11' }

/** Icon-only control: stable square hit area and a required accessible name. */
export function IconButton({ label, title, tone = 'neutral', size = 'md', to, className = '', children, type = 'button', ...rest }) {
  const classes = `inline-grid shrink-0 place-items-center rounded-md transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-50 ${iconSizes[size]} ${tones[tone]} ${className}`
  if (to) {
    return (
      <Link to={to} aria-label={label} title={title ?? label} className={classes} {...rest}>
        {children}
      </Link>
    )
  }
  return (
    <button type={type} aria-label={label} title={title ?? label} className={classes} {...rest}>
      {children}
    </button>
  )
}

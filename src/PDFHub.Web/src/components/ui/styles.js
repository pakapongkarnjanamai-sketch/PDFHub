// className helpers. They own appearance only — border, radius, colour, focus, typography.
// Width, height, margin and position are deliberately absent: Tailwind utilities that set the same
// property are exclusive, so a base `w-full` would silently beat a caller's `w-40`. State them at the call site.

const inputBase =
  'rounded-md border border-border-strong bg-surface-panel px-2.5 text-body text-ink-strong ' +
  'placeholder:text-ink-soft focus:border-accent focus:outline-2 focus:outline-offset-1 focus:outline-accent ' +
  'disabled:cursor-not-allowed disabled:bg-surface-muted disabled:text-ink-muted ' +
  'aria-invalid:border-danger'

export function inputClassName(className = '') {
  return `${inputBase} h-9 ${className}`
}

export function textareaClassName(className = '') {
  return `${inputBase} py-2 ${className}`
}

const buttonBase =
  'inline-flex items-center justify-center gap-1.5 rounded-md font-medium whitespace-nowrap transition-colors ' +
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ' +
  'disabled:cursor-not-allowed disabled:opacity-60'

const buttonVariants = {
  primary: 'bg-accent text-white hover:bg-accent-hover',
  secondary: 'border border-border-strong bg-surface-panel text-ink-strong hover:bg-surface-muted',
  danger: 'border border-danger bg-surface-panel text-danger hover:bg-danger-soft',
  ghost: 'text-ink-muted hover:bg-surface-muted hover:text-ink-strong',
}

const buttonSizes = {
  sm: 'h-8 px-2.5 text-caption',
  md: 'min-h-11 px-4 text-body sm:min-h-9',
}

export function buttonClassName(variant = 'secondary', size = 'md', className = '') {
  return `${buttonBase} ${buttonVariants[variant]} ${buttonSizes[size]} ${className}`
}

/** Record links: permanent underline, because the accent is too close to body ink to rely on colour. */
export const recordLinkClassName =
  'text-accent underline decoration-1 underline-offset-2 hover:text-accent-hover rounded-sm ' +
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent'

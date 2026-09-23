/**
 * Maps a ValidationProblemDetails `errors` object (PascalCase keys) onto form field keys.
 * Keys the map does not know go to `unmapped` so a new server rule never vanishes from the UI.
 */
export function mapServerFieldErrors(errors, fieldKeys) {
  const fields = {}
  const unmapped = []
  for (const [serverKey, messages] of Object.entries(errors ?? {})) {
    const fieldKey = fieldKeys[serverKey]
    const message = [].concat(messages).join(' ')
    if (!fieldKey) {
      unmapped.push(`${serverKey}: ${message}`)
      continue
    }
    fields[fieldKey] = fields[fieldKey] ? `${fields[fieldKey]} ${message}` : message
  }
  return { fields, unmapped }
}

/** Scrolls to and focuses the first field marked data-invalid, in document order. */
export function focusFirstInvalid() {
  requestAnimationFrame(() => {
    const invalid = document.querySelector('[data-invalid="true"]')
    invalid?.scrollIntoView({ block: 'center' })
    invalid?.querySelector('input, textarea, select')?.focus()
  })
}

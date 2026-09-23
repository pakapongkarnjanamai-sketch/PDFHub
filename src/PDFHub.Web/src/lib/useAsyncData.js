import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Loads data for a screen (frontend-react-async-ui-states skill):
 * - `loading` only until the first success; later reloads set `refreshing` and keep `data` on screen;
 * - a failed refresh keeps the previous data and exposes `error`;
 * - `retry()` re-runs the request (user-triggered only — never on a timer);
 * - every request is aborted when inputs change or the screen unmounts.
 *
 * `load` must be memoized with useCallback; its identity is what triggers a reload.
 */
export function useAsyncData(load) {
  const [data, setData] = useState(null)
  const [error, setError] = useState(null)
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [retryToken, setRetryToken] = useState(0)
  const hasDataRef = useRef(false)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(!hasDataRef.current)
    setRefreshing(hasDataRef.current)
    setError(null)

    load(controller.signal)
      .then((next) => {
        if (controller.signal.aborted) return
        hasDataRef.current = true
        setData(next)
      })
      .catch((err) => {
        if (!controller.signal.aborted) setError(err)
      })
      .finally(() => {
        if (controller.signal.aborted) return
        setLoading(false)
        setRefreshing(false)
      })

    return () => controller.abort()
  }, [load, retryToken])

  const retry = useCallback(() => setRetryToken((t) => t + 1), [])

  return { data, setData, error, loading, refreshing, retry }
}

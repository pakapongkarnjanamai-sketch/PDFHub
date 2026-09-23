import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { sessionApi } from '../api'
import { setAuthProblemHandler } from '../lib/apiClient'
import { toast } from '../lib/toast'
import { LoadingSurface } from '../components/ui/feedback'

const anonymous = { isAuthenticated: false, canEdit: false, isAdmin: false, mustChangePassword: false, requireLoginToView: false }

const SessionContext = createContext({ session: anonymous, loaded: false, refresh: async () => {} })

// eslint-disable-next-line react-refresh/only-export-components
export function useSession() {
  return useContext(SessionContext)
}

/** Loads /api/session/me once and reacts to 401/403 from any API call. */
export function SessionProvider({ children }) {
  const [session, setSession] = useState(anonymous)
  const [loaded, setLoaded] = useState(false)
  const navigate = useNavigate()
  const location = useLocation()

  const refresh = useCallback(
    () => sessionApi.me()
      .then((me) => setSession({ ...anonymous, ...me }))
      .catch(() => setSession(anonymous))
      .finally(() => setLoaded(true)),
    [],
  )

  useEffect(() => {
    refresh()
  }, [refresh])

  useEffect(() => {
    setAuthProblemHandler((error) => {
      if (error.status === 401) {
        // Signed out elsewhere (disabled account, password changed on another PC) or cookie expired.
        setSession(anonymous)
        navigate('/login', { state: { from: location.pathname + location.search } })
      } else if (error.type === 'password-change-required') {
        navigate('/account/password')
      } else if (error.status === 403) {
        toast.warning('คุณไม่มีสิทธิ์ทำรายการนี้')
      }
    })
  }, [navigate, location])

  const value = useMemo(() => ({ session, loaded, refresh }), [session, loaded, refresh])
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

/**
 * Route guard. `access`: 'view' (anyone unless the site requires login), 'auth', 'edit' or 'admin'.
 * These checks only decide what to render — every endpoint enforces its own policy.
 */
export function RequireAccess({ access = 'view', children }) {
  const { session, loaded } = useSession()
  const location = useLocation()

  if (!loaded) return <LoadingSurface />

  const needsLogin = access !== 'view' || session.requireLoginToView
  if (needsLogin && !session.isAuthenticated)
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  if (session.mustChangePassword && location.pathname !== '/account/password')
    return <Navigate to="/account/password" replace />
  if ((access === 'edit' && !session.canEdit) || (access === 'admin' && !session.isAdmin))
    return <Navigate to="/access-denied" replace />
  return children
}

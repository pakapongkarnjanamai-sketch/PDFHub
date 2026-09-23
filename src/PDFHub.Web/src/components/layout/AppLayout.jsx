import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { ChevronRight, KeyRound, LogIn, LogOut, Menu, X } from 'lucide-react'
import { NAV_GROUPS, canAccess, getBreadcrumbs, isActiveItem } from '../../config/navigation'
import { useSession } from '../../session/SessionContext'
import { ROLE_LABELS, sessionApi } from '../../api'
import { todayText } from '../../lib/format'
import { toast } from '../../lib/toast'
import { IconButton } from '../ui/buttons'

/** Shell: left sidebar + continuous top header + the page's own scroll area. */
export function AppLayout({ children } = {}) {
  const [drawerOpen, setDrawerOpen] = useState(false)
  const { pathname } = useLocation()

  // A navigation always closes the mobile drawer.
  const [lastPath, setLastPath] = useState(pathname)
  if (lastPath !== pathname) {
    setLastPath(pathname)
    setDrawerOpen(false)
  }

  useEffect(() => {
    if (!drawerOpen) return
    const onEscape = (e) => { if (e.key === 'Escape') setDrawerOpen(false) }
    document.addEventListener('keydown', onEscape)
    return () => document.removeEventListener('keydown', onEscape)
  }, [drawerOpen])

  return (
    <div className="flex h-dvh overflow-hidden bg-surface-app">
      {drawerOpen && <div aria-hidden className="fixed inset-0 z-30 bg-ink-strong/35 lg:hidden" onClick={() => setDrawerOpen(false)} />}
      <Sidebar open={drawerOpen} pathname={pathname} />

      <div className="flex min-w-0 flex-1 flex-col">
        <Header drawerOpen={drawerOpen} onToggleDrawer={() => setDrawerOpen((o) => !o)} pathname={pathname} />
        <main className="flex-1 overflow-hidden px-4 py-5 sm:px-6">{children ?? <Outlet />}</main>
      </div>
    </div>
  )
}

function Sidebar({ open, pathname }) {
  const { session } = useSession()

  return (
    <aside
      className={`fixed inset-y-0 left-0 z-40 flex w-60 shrink-0 flex-col border-r border-border-subtle bg-surface-panel transition-transform lg:static ${
        open ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'
      }`}>
      <Link to="/" className="flex h-16 shrink-0 items-center gap-2.5 border-b border-border-subtle px-5 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent">
        <img src={`${import.meta.env.BASE_URL}favicon.svg`} alt="" className="size-8" />
        <span className="leading-tight">
          <span className="block text-heading font-bold text-ink-strong">PDFHub</span>
          <span className="block text-caption text-ink-muted">Drawing Database</span>
        </span>
      </Link>

      <nav className="flex-1 space-y-5 overflow-y-auto p-3" aria-label="เมนูหลัก">
        {NAV_GROUPS.map((group) => {
          const items = group.items.filter((item) => canAccess(session, item.access))
          if (items.length === 0) return null
          return (
            <div key={group.label}>
              <p className="mb-1 px-3 text-caption font-semibold uppercase tracking-wider text-ink-muted">{group.label}</p>
              <ul className="space-y-0.5">
                {items.map((item) => {
                  const active = isActiveItem(item, pathname)
                  const Icon = item.icon
                  return (
                    <li key={item.path}>
                      <NavLink to={item.path} aria-current={active ? 'page' : undefined}
                        className={`flex min-h-11 items-center gap-2.5 rounded-md px-3 text-body focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent lg:min-h-9 ${
                          active ? 'bg-accent-soft font-semibold text-accent' : 'text-ink-strong hover:bg-surface-muted'
                        }`}>
                        <Icon className="size-4 shrink-0" aria-hidden />
                        {item.label}
                      </NavLink>
                    </li>
                  )
                })}
              </ul>
            </div>
          )
        })}
      </nav>

      <p className="border-t border-border-subtle px-5 py-3 text-caption text-ink-muted">© {new Date().getFullYear()} PDFHub</p>
    </aside>
  )
}

function Header({ drawerOpen, onToggleDrawer, pathname }) {
  const crumbs = getBreadcrumbs(pathname)

  return (
    <header className="flex h-16 shrink-0 items-center gap-3 border-b border-border-subtle bg-surface-panel px-4 sm:px-6">
      <IconButton label={drawerOpen ? 'ปิดเมนู' : 'เปิดเมนู'} size="lg" className="lg:hidden" onClick={onToggleDrawer}>
        {drawerOpen ? <X className="size-5" /> : <Menu className="size-5" />}
      </IconButton>

      <nav aria-label="ตำแหน่งปัจจุบัน" className="min-w-0 flex-1">
        <ol className="flex min-w-0 items-center gap-1 text-body">
          {crumbs.map((crumb, index) => (
            <li key={crumb.to} className="flex min-w-0 items-center gap-1">
              {index > 0 && <ChevronRight className="size-4 shrink-0 text-ink-soft" aria-hidden />}
              {crumb.to === pathname ? (
                <span className="truncate font-semibold text-ink-strong" aria-current="page">{crumb.label}</span>
              ) : (
                <Link to={crumb.to} className="truncate text-ink-muted hover:text-ink-strong focus-visible:outline-2 focus-visible:outline-accent">
                  {crumb.label}
                </Link>
              )}
            </li>
          ))}
        </ol>
      </nav>

      <UserMenu />
    </header>
  )
}

function UserMenu() {
  const { session, refresh } = useSession()
  const navigate = useNavigate()
  const { pathname } = useLocation()

  if (!session.isAuthenticated) {
    return (
      <Link to="/login" state={{ from: pathname }}
        className="inline-flex min-h-11 items-center gap-1.5 rounded-md px-3 text-body font-medium text-accent hover:bg-accent-soft focus-visible:outline-2 focus-visible:outline-accent sm:min-h-9">
        <LogIn className="size-4" aria-hidden /> เข้าสู่ระบบ
      </Link>
    )
  }

  const logout = async () => {
    try {
      await sessionApi.logout()
    } finally {
      await refresh()
      toast.info('ออกจากระบบแล้ว')
      navigate('/drawings')
    }
  }

  return (
    <div className="flex shrink-0 items-center gap-1">
      <div className="hidden items-center gap-4 px-3 md:flex">
        <div className="text-right">
          <p className="text-caption uppercase tracking-wider text-ink-muted">ผู้ใช้</p>
          <p className="text-body font-medium text-ink-strong">
            {session.displayName} <span className="text-caption font-normal text-ink-muted">({ROLE_LABELS[session.role] ?? session.role})</span>
          </p>
        </div>
        <div className="h-8 w-px bg-border-subtle" aria-hidden />
        <div className="text-right">
          <p className="text-caption uppercase tracking-wider text-ink-muted">วันนี้</p>
          <p className="text-body font-medium text-ink-strong tabular-nums">{todayText()}</p>
        </div>
      </div>
      <IconButton label="เปลี่ยนรหัสผ่าน" size="lg" to="/account/password" className="md:size-9"><KeyRound className="size-4" /></IconButton>
      <IconButton label="ออกจากระบบ" size="lg" onClick={logout} className="md:size-9"><LogOut className="size-4" /></IconButton>
    </div>
  )
}

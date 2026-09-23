import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/layout/AppLayout'
import { LoadingSurface } from './components/ui/feedback'
import { RequireAccess } from './session/SessionContext'
import { AccessDeniedPage, ChangePasswordPage, LoginPage } from './pages/account/AccountPages'
import { DrawingsPage } from './pages/drawings/DrawingsPage'

const lazyPage = (loader, name) => lazy(() => loader().then((m) => ({ default: m[name] })))
const DrawingDetailPage = lazyPage(() => import('./pages/drawings/DrawingDetailPage'), 'DrawingDetailPage')
const DrawingFormPage = lazyPage(() => import('./pages/drawings/DrawingFormPage'), 'DrawingFormPage')
const ImportPage = lazyPage(() => import('./pages/import/ImportPage'), 'ImportPage')
const SectionsPage = lazyPage(() => import('./pages/sections/SectionsPage'), 'SectionsPage')
const UsersPage = lazyPage(() => import('./pages/users/UsersPage'), 'UsersPage')
const BackupPage = lazyPage(() => import('./pages/backup/BackupPage'), 'BackupPage')

function guarded(access, element) {
  return (
    <RequireAccess access={access}>
      <Suspense fallback={<LoadingSurface />}>{element}</Suspense>
    </RequireAccess>
  )
}

export function App() {
  return (
    <Routes>
      <Route path="login" element={<LoginPage />} />
      <Route element={<AppLayout />}>
        <Route index element={<Navigate to="/drawings" replace />} />
        <Route path="drawings" element={guarded('view', <DrawingsPage />)} />
        <Route path="drawings/new" element={guarded('edit', <DrawingFormPage />)} />
        <Route path="drawings/:code" element={guarded('view', <DrawingDetailPage />)} />
        <Route path="drawings/:code/edit" element={guarded('edit', <DrawingFormPage />)} />
        <Route path="import" element={guarded('edit', <ImportPage />)} />
        <Route path="sections" element={guarded('admin', <SectionsPage />)} />
        <Route path="users" element={guarded('admin', <UsersPage />)} />
        <Route path="backup" element={guarded('admin', <BackupPage />)} />
        <Route path="account/password" element={guarded('auth', <ChangePasswordPage />)} />
        <Route path="access-denied" element={<AccessDeniedPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

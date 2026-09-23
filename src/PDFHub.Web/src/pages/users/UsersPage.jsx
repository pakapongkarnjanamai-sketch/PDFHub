import { useCallback, useState } from 'react'
import { Plus } from 'lucide-react'
import { ROLE_LABELS, usersApi } from '../../api'
import { ApiError, errorMessage } from '../../lib/apiClient'
import { formatDateTime } from '../../lib/format'
import { toast } from '../../lib/toast'
import { useAsyncData } from '../../lib/useAsyncData'
import { useSession } from '../../session/SessionContext'
import { AppButton } from '../../components/ui/buttons'
import { ErrorSurface, LoadingSurface } from '../../components/ui/feedback'
import { Field, FormSection, Page, PageHeader } from '../../components/ui/layout'
import { inputClassName } from '../../components/ui/styles'
import { DataGridShell } from '../../components/ui/DataGridShell'

const ROLES = ['Admin', 'Editor', 'Viewer']
const FIELD_KEYS = { UserName: 'userName', DisplayName: 'displayName', Role: 'role', Password: 'password' }

export function UsersPage() {
  const load = useCallback((signal) => usersApi.list(signal), [])
  const { data, setData, error, loading, refreshing, retry } = useAsyncData(load)
  const { session } = useSession()
  const replace = (user) => setData((rows) => rows.map((r) => (r.id === user.id ? user : r)))

  return (
    <Page wide>
      <PageHeader title="ผู้ใช้งาน"
        meta="ผู้ดูแลระบบจัดการได้ทุกอย่าง · ผู้บันทึกข้อมูลเพิ่ม/แก้ไข Drawing และอัปโหลด PDF · ผู้ดูข้อมูลค้นหาและเปิด PDF ได้อย่างเดียว" />
      <AddUser onAdded={(u) => setData((rows) => [...rows, u].sort((a, b) => a.userName.localeCompare(b.userName)))} />

      {loading && !data ? <LoadingSurface /> : !data ? <ErrorSurface onRetry={retry}>{errorMessage(error)}</ErrorSurface> : (
        <DataGridShell aria-busy={refreshing} className="rounded-lg border border-border-subtle bg-surface-panel">
          <table className="w-full min-w-[960px] text-body">
            <thead className="sticky top-0 z-10 border-b border-border-subtle bg-surface-muted text-left text-caption text-ink-muted">
              <tr>
                <th className="px-3 py-2.5">ชื่อผู้ใช้</th>
                <th className="px-3 py-2.5">ชื่อที่แสดง</th>
                <th className="px-3 py-2.5">สิทธิ์</th>
                <th className="px-3 py-2.5">สถานะ</th>
                <th className="px-3 py-2.5">เข้าระบบล่าสุด</th>
                <th className="px-3 py-2.5"><span className="sr-only">บันทึก</span></th>
                <th className="px-3 py-2.5">ตั้งรหัสผ่านชั่วคราว</th>
              </tr>
            </thead>
            <tbody>
              {data.map((u) => <UserRow key={u.id} user={u} isSelf={u.userName.toLowerCase() === session.userName.toLowerCase()} onSaved={replace} />)}
            </tbody>
          </table>
        </DataGridShell>
      )}
    </Page>
  )
}

function AddUser({ onAdded }) {
  const empty = { userName: '', displayName: '', role: 'Editor', password: '' }
  const [form, setForm] = useState(empty)
  const [errors, setErrors] = useState({})
  const [busy, setBusy] = useState(false)

  const submit = async (e) => {
    e.preventDefault()
    setBusy(true)
    setErrors({})
    try {
      const created = await usersApi.create(form)
      onAdded(created)
      setForm(empty)
      toast.success(`เพิ่มผู้ใช้ ${created.userName} แล้ว ผู้ใช้ต้องเปลี่ยนรหัสผ่านเมื่อเข้าสู่ระบบครั้งแรก`)
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.errors).length > 0) {
        setErrors(Object.fromEntries(Object.entries(err.errors).map(([k, v]) => [FIELD_KEYS[k] ?? k, v.join(' ')])))
      } else toast.error(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <FormSection>
      <form onSubmit={submit} noValidate className="grid gap-3 sm:grid-cols-2 lg:grid-cols-[1fr_1fr_12rem_1fr_auto] lg:items-start">
        <Field label="ชื่อผู้ใช้ (ใช้ login)" required error={errors.userName}>
          <input value={form.userName} autoComplete="off" onChange={(e) => setForm({ ...form, userName: e.target.value })} className={inputClassName('w-full')} />
        </Field>
        <Field label="ชื่อที่แสดง" error={errors.displayName}>
          <input value={form.displayName} autoComplete="off" onChange={(e) => setForm({ ...form, displayName: e.target.value })} className={inputClassName('w-full')} />
        </Field>
        <Field label="สิทธิ์" required error={errors.role}>
          <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })} className={inputClassName('w-full')}>
            {ROLES.map((r) => <option key={r} value={r}>{ROLE_LABELS[r]}</option>)}
          </select>
        </Field>
        <Field label="รหัสผ่านเริ่มต้น" required error={errors.password} hint="อย่างน้อย 6 ตัวอักษร">
          <input value={form.password} autoComplete="off" onChange={(e) => setForm({ ...form, password: e.target.value })} className={inputClassName('w-full')} />
        </Field>
        <AppButton type="submit" variant="primary" loading={busy} className="lg:mt-5"><Plus className="size-4" aria-hidden /> เพิ่มผู้ใช้</AppButton>
      </form>
    </FormSection>
  )
}

function UserRow({ user, isSelf, onSaved }) {
  const [displayName, setDisplayName] = useState(user.displayName)
  const [role, setRole] = useState(user.role)
  const [active, setActive] = useState(user.isActive)
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(null) // 'save' | 'reset'
  const changed = displayName !== user.displayName || role !== user.role || active !== user.isActive

  const save = async () => {
    setBusy('save')
    try {
      onSaved(await usersApi.update(user.id, { displayName, role, isActive: active }))
      toast.success(`บันทึกผู้ใช้ ${user.userName} แล้ว`)
    } catch (err) {
      toast.error(errorMessage(err))
    } finally {
      setBusy(null)
    }
  }

  const reset = async (e) => {
    e.preventDefault()
    setBusy('reset')
    try {
      await usersApi.resetPassword(user.id, { password })
      setPassword('')
      onSaved({ ...user, mustChangePassword: true })
      toast.success(`ตั้งรหัสผ่านชั่วคราวให้ ${user.userName} แล้ว ผู้ใช้ต้องเปลี่ยนรหัสเมื่อเข้าสู่ระบบครั้งถัดไป`)
    } catch (err) {
      toast.error(errorMessage(err))
    } finally {
      setBusy(null)
    }
  }

  return (
    <tr className={`border-b border-border-subtle last:border-0 ${user.isActive ? '' : 'text-ink-muted'}`}>
      <td className="px-3 py-2">
        <span className="font-mono font-semibold">{user.userName}</span>
        {isSelf && <span className="ml-2 rounded-md bg-accent-soft px-1.5 py-0.5 text-caption font-semibold text-accent">คุณ</span>}
        {user.mustChangePassword && <span className="ml-2 rounded-md bg-warning-soft px-1.5 py-0.5 text-caption font-semibold text-warning">รอเปลี่ยนรหัส</span>}
      </td>
      <td className="px-3 py-2">
        <input value={displayName} maxLength={100} aria-label={`ชื่อที่แสดงของ ${user.userName}`} onChange={(e) => setDisplayName(e.target.value)} className={inputClassName('w-full min-w-40')} />
      </td>
      <td className="px-3 py-2">
        {/* Admins cannot demote or disable themselves; another admin has to. */}
        <select value={role} disabled={isSelf} aria-label={`สิทธิ์ของ ${user.userName}`} onChange={(e) => setRole(e.target.value)} className={inputClassName('w-full min-w-36')}>
          {ROLES.map((r) => <option key={r} value={r}>{ROLE_LABELS[r]}</option>)}
        </select>
      </td>
      <td className="px-3 py-2">
        <label className="inline-flex cursor-pointer items-center gap-2">
          <input type="checkbox" checked={active} disabled={isSelf} onChange={(e) => setActive(e.target.checked)} className="size-4 accent-accent" />
          ใช้งาน
        </label>
      </td>
      <td className="px-3 py-2 whitespace-nowrap text-ink-muted tabular-nums">{formatDateTime(user.lastLoginAt)}</td>
      <td className="px-3 py-2"><AppButton size="sm" loading={busy === 'save'} disabled={!changed || busy !== null} onClick={save}>บันทึก</AppButton></td>
      <td className="px-3 py-2">
        <form onSubmit={reset} className="flex gap-1.5">
          <input value={password} minLength={6} required placeholder="รหัสใหม่" autoComplete="off" aria-label={`รหัสผ่านชั่วคราวของ ${user.userName}`}
            onChange={(e) => setPassword(e.target.value)} className={inputClassName('w-32')} />
          <AppButton type="submit" size="sm" loading={busy === 'reset'} disabled={password.length < 6 || busy !== null}>ตั้งรหัส</AppButton>
        </form>
      </td>
    </tr>
  )
}

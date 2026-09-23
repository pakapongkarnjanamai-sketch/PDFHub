import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { sessionApi } from '../../api'
import { ApiError, errorMessage } from '../../lib/apiClient'
import { focusFirstInvalid } from '../../lib/formErrors'
import { toast } from '../../lib/toast'
import { useSession } from '../../session/SessionContext'
import { AppButton, LinkButton } from '../../components/ui/buttons'
import { ErrorSurface } from '../../components/ui/feedback'
import { Field, FormSection, Page, PageHeader } from '../../components/ui/layout'
import { inputClassName } from '../../components/ui/styles'

export function LoginPage() {
  const { refresh } = useSession()
  const navigate = useNavigate()
  const { state } = useLocation()
  const [form, setForm] = useState({ userName: '', password: '', remember: false })
  const [error, setError] = useState(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await sessionApi.login(form)
      await refresh()
      navigate(state?.from && state.from !== '/login' ? state.from : '/drawings', { replace: true })
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <div className="flex min-h-full items-center justify-center bg-surface-app p-4">
      <form onSubmit={submit} className="w-full max-w-sm space-y-4 rounded-lg border border-border-subtle bg-surface-panel p-6">
        <div className="space-y-2">
          <img src={`${import.meta.env.BASE_URL}favicon.svg`} alt="" className="size-11" />
          <h1 className="text-title font-semibold">เข้าสู่ระบบ PDFHub</h1>
          <p className="text-body text-ink-muted">ต้องเข้าสู่ระบบเพื่อเพิ่มหรือแก้ไขข้อมูล</p>
        </div>
        {error && <ErrorSurface compact>{error}</ErrorSurface>}
        <Field label="ชื่อผู้ใช้">
          <input value={form.userName} autoComplete="username" autoFocus required
            onChange={(e) => setForm({ ...form, userName: e.target.value })} className={inputClassName('w-full')} />
        </Field>
        <Field label="รหัสผ่าน">
          <input type="password" value={form.password} autoComplete="current-password" required
            onChange={(e) => setForm({ ...form, password: e.target.value })} className={inputClassName('w-full')} />
        </Field>
        <label className="flex cursor-pointer items-start gap-2 text-body">
          <input type="checkbox" checked={form.remember} onChange={(e) => setForm({ ...form, remember: e.target.checked })} className="mt-1 size-4 accent-accent" />
          <span>จำการเข้าสู่ระบบไว้ 30 วัน <span className="text-ink-muted">(ไม่ควรเลือกบนเครื่องที่ใช้ร่วมกัน)</span></span>
        </label>
        <AppButton type="submit" variant="primary" loading={busy} className="w-full">เข้าสู่ระบบ</AppButton>
        <p className="text-center text-body">
          <Link to="/drawings" className="text-ink-muted underline underline-offset-2 hover:text-ink-strong">ดูรายการ Drawing โดยไม่เข้าสู่ระบบ</Link>
        </p>
      </form>
    </div>
  )
}

const PASSWORD_KEYS = { CurrentPassword: 'currentPassword', NewPassword: 'newPassword' }

export function ChangePasswordPage() {
  const { session, refresh } = useSession()
  const navigate = useNavigate()
  const [form, setForm] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' })
  const [errors, setErrors] = useState({})
  const [banner, setBanner] = useState(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e) => {
    e.preventDefault()
    const clientErrors = {}
    if (!form.currentPassword) clientErrors.currentPassword = 'กรุณากรอกรหัสผ่านปัจจุบัน'
    if (form.newPassword.length < 6) clientErrors.newPassword = 'รหัสผ่านใหม่ต้องมีอย่างน้อย 6 ตัวอักษร'
    if (form.newPassword !== form.confirmPassword) clientErrors.confirmPassword = 'ยืนยันรหัสผ่านไม่ตรงกัน'
    setErrors(clientErrors)
    setBanner(null)
    if (Object.keys(clientErrors).length > 0) return focusFirstInvalid()

    setBusy(true)
    try {
      await sessionApi.changePassword({ currentPassword: form.currentPassword, newPassword: form.newPassword })
      await refresh()
      toast.success('เปลี่ยนรหัสผ่านเรียบร้อยแล้ว')
      navigate('/drawings', { replace: true })
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.errors).length > 0) {
        setErrors(Object.fromEntries(Object.entries(err.errors).map(([k, v]) => [PASSWORD_KEYS[k] ?? k, v.join(' ')])))
        focusFirstInvalid()
      } else setBanner(errorMessage(err))
      setBusy(false)
    }
  }

  const input = (key, autoComplete) => (
    <input type="password" value={form[key]} autoComplete={autoComplete} aria-invalid={errors[key] ? true : undefined}
      onChange={(e) => setForm({ ...form, [key]: e.target.value })} className={inputClassName('w-full')} />
  )

  return (
    <Page>
      <PageHeader title="เปลี่ยนรหัสผ่าน" meta={session.mustChangePassword ? 'กรุณาตั้งรหัสผ่านของคุณเองก่อนเริ่มใช้งาน' : undefined} />
      {banner && <ErrorSurface>{banner}</ErrorSurface>}
      <FormSection className="max-w-md">
        <form onSubmit={submit} noValidate className="space-y-4">
          <Field label="รหัสผ่านปัจจุบัน" required error={errors.currentPassword}>{input('currentPassword', 'current-password')}</Field>
          <Field label="รหัสผ่านใหม่" required error={errors.newPassword} hint="อย่างน้อย 6 ตัวอักษร">{input('newPassword', 'new-password')}</Field>
          <Field label="ยืนยันรหัสผ่านใหม่" required error={errors.confirmPassword}>{input('confirmPassword', 'new-password')}</Field>
          <AppButton type="submit" variant="primary" loading={busy}>บันทึกรหัสผ่านใหม่</AppButton>
        </form>
      </FormSection>
    </Page>
  )
}

export function AccessDeniedPage() {
  return (
    <Page>
      <PageHeader title="ไม่มีสิทธิ์ใช้งานหน้านี้" meta="ติดต่อผู้ดูแลระบบหากต้องการสิทธิ์เพิ่ม" />
      <LinkButton to="/drawings">กลับไปรายการ Drawing</LinkButton>
    </Page>
  )
}

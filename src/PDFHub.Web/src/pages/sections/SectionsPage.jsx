import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Trash2 } from 'lucide-react'
import { sectionsApi } from '../../api'
import { ApiError, errorMessage } from '../../lib/apiClient'
import { toast } from '../../lib/toast'
import { useAsyncData } from '../../lib/useAsyncData'
import { AppButton, IconButton } from '../../components/ui/buttons'
import { ErrorSurface, LoadingSurface } from '../../components/ui/feedback'
import { Field, FormSection, Page, PageHeader } from '../../components/ui/layout'
import { ConfirmDialog } from '../../components/ui/Modal'
import { inputClassName, recordLinkClassName } from '../../components/ui/styles'

export function SectionsPage() {
  const load = useCallback((signal) => sectionsApi.list(signal), [])
  const { data, setData, error, loading, refreshing, retry } = useAsyncData(load)
  const [deleting, setDeleting] = useState(null)
  const [busy, setBusy] = useState(false)

  const replace = (section) => setData((rows) => rows.map((r) => (r.id === section.id ? section : r)))

  const remove = async () => {
    setBusy(true)
    try {
      await sectionsApi.remove(deleting.id)
      setData((rows) => rows.filter((r) => r.id !== deleting.id))
      toast.success(`ลบ Section ${deleting.code} แล้ว`)
    } catch (err) {
      toast.error(errorMessage(err))
    } finally {
      setBusy(false)
      setDeleting(null)
    }
  }

  return (
    <Page>
      <PageHeader title="Section" meta="รหัส 2 ตัวอักษรหน้า PdfCode (เดิมคือชีต List Type) ใช้ตรวจว่า PdfCode ที่กรอกถูกต้อง" />
      <AddSection onAdded={(s) => setData((rows) => [...rows, s].sort((a, b) => a.code.localeCompare(b.code)))} />

      {loading && !data ? <LoadingSurface /> : !data ? <ErrorSurface onRetry={retry}>{errorMessage(error)}</ErrorSurface> : (
        <div aria-busy={refreshing} className="relative overflow-x-auto rounded-lg border border-border-subtle bg-surface-panel">
          <table className="w-full min-w-[640px] text-body">
            <thead className="border-b border-border-subtle bg-surface-muted text-left text-caption text-ink-muted">
              <tr>
                <th className="px-3 py-2.5">รหัส</th>
                <th className="px-3 py-2.5">ชื่อ Section</th>
                <th className="px-3 py-2.5 text-right">จำนวน Drawing</th>
                <th className="px-3 py-2.5">ใช้งาน</th>
                <th className="px-3 py-2.5"><span className="sr-only">จัดการ</span></th>
              </tr>
            </thead>
            <tbody>
              {data.map((s) => <SectionRow key={s.id} section={s} onSaved={replace} onDelete={() => setDeleting(s)} />)}
            </tbody>
          </table>
        </div>
      )}
      <p className="text-caption text-ink-muted">Section ที่ปิดใช้งานจะรับ PdfCode ใหม่ไม่ได้ แต่ Drawing เดิมยังค้นหาและเปิดดูได้ตามปกติ</p>

      <ConfirmDialog open={deleting !== null} title={`ลบ Section ${deleting?.code}?`} confirmLabel="ลบ" danger busy={busy}
        onCancel={() => setDeleting(null)} onConfirm={remove}>
        Section นี้ยังไม่มี Drawing ใช้งาน ลบได้อย่างปลอดภัย
      </ConfirmDialog>
    </Page>
  )
}

function AddSection({ onAdded }) {
  const [form, setForm] = useState({ code: '', name: '' })
  const [errors, setErrors] = useState({})
  const [busy, setBusy] = useState(false)

  const submit = async (e) => {
    e.preventDefault()
    setBusy(true)
    setErrors({})
    try {
      const created = await sectionsApi.create(form)
      onAdded(created)
      setForm({ code: '', name: '' })
      toast.success(`เพิ่ม Section ${created.code} · ${created.name} แล้ว`)
    } catch (err) {
      if (err instanceof ApiError && err.errors.Code) setErrors((x) => ({ ...x, code: err.errors.Code.join(' ') }))
      if (err instanceof ApiError && err.errors.Name) setErrors((x) => ({ ...x, name: err.errors.Name.join(' ') }))
      if (!(err instanceof ApiError) || Object.keys(err.errors).length === 0) toast.error(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <FormSection>
      <form onSubmit={submit} noValidate className="flex flex-wrap items-start gap-3">
        <Field label="รหัส" required error={errors.code} className="w-28">
          <input value={form.code} maxLength={2} placeholder="NB" onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
            className={inputClassName('w-full font-mono uppercase')} />
        </Field>
        <Field label="ชื่อ Section" required error={errors.name} className="min-w-56 flex-1">
          <input value={form.name} maxLength={100} placeholder="NMB" onChange={(e) => setForm({ ...form, name: e.target.value })}
            className={inputClassName('w-full')} />
        </Field>
        <AppButton type="submit" variant="primary" loading={busy} className="sm:mt-5"><Plus className="size-4" aria-hidden /> เพิ่ม Section</AppButton>
      </form>
    </FormSection>
  )
}

function SectionRow({ section, onSaved, onDelete }) {
  const [name, setName] = useState(section.name)
  const [active, setActive] = useState(section.isActive)
  const [busy, setBusy] = useState(false)
  const changed = name !== section.name || active !== section.isActive

  const save = async () => {
    setBusy(true)
    try {
      onSaved(await sectionsApi.update(section.id, { name, isActive: active }))
      toast.success(`บันทึก Section ${section.code} แล้ว`)
    } catch (err) {
      toast.error(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <tr className={`border-b border-border-subtle last:border-0 ${section.isActive ? '' : 'text-ink-muted'}`}>
      <td className="px-3 py-2 font-mono font-semibold">{section.code}</td>
      <td className="px-3 py-2">
        <input value={name} maxLength={100} aria-label={`ชื่อ Section ${section.code}`} onChange={(e) => setName(e.target.value)}
          className={inputClassName('w-full min-w-40')} />
      </td>
      <td className="px-3 py-2 text-right tabular-nums">
        <Link to={`/drawings?section=${section.code}`} className={recordLinkClassName}>{section.drawingCount.toLocaleString()}</Link>
      </td>
      <td className="px-3 py-2">
        <label className="inline-flex cursor-pointer items-center gap-2">
          <input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} className="size-4 accent-accent" />
          เปิดใช้
        </label>
      </td>
      <td className="px-3 py-2">
        <div className="flex items-center justify-end gap-1">
          <AppButton size="sm" loading={busy} disabled={!changed} onClick={save}>บันทึก</AppButton>
          {section.drawingCount === 0 && (
            <IconButton label={`ลบ Section ${section.code}`} tone="danger" size="sm" onClick={onDelete}><Trash2 className="size-4" /></IconButton>
          )}
        </div>
      </td>
    </tr>
  )
}

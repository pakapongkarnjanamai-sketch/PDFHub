import { DatabaseBackup, FileText, FolderUp, Tags, Users } from 'lucide-react'

/**
 * Sidebar groups. `access` is checked against the session: 'all', 'edit' (Admin/Editor) or 'admin'.
 * `match` lists the path prefixes that keep an item active, so one route never lights up two items.
 */
export const NAV_GROUPS = [
  {
    label: 'Drawing',
    items: [
      { label: 'รายการ Drawing', path: '/drawings', icon: FileText, access: 'all', match: ['/drawings'] },
      { label: 'นำเข้าข้อมูล', path: '/import', icon: FolderUp, access: 'edit', match: ['/import'] },
    ],
  },
  {
    label: 'ตั้งค่า',
    items: [
      { label: 'Section', path: '/sections', icon: Tags, access: 'admin', match: ['/sections'] },
      { label: 'ผู้ใช้งาน', path: '/users', icon: Users, access: 'admin', match: ['/users'] },
      { label: 'สำรองข้อมูล', path: '/backup', icon: DatabaseBackup, access: 'admin', match: ['/backup'] },
    ],
  },
]

export function canAccess(session, access) {
  if (access === 'edit') return session.canEdit
  if (access === 'admin') return session.isAdmin
  return true
}

export function isActiveItem(item, pathname) {
  return item.match.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`))
}

/**
 * Breadcrumb trail for a path. The record crumb (a PdfCode) is appended as plain text; the list crumb
 * stays a link so the user can always get back.
 */
export function getBreadcrumbs(pathname) {
  const segments = pathname.split('/').filter(Boolean)
  const [first, second, third] = segments
  const crumbs = []

  if (first === 'drawings') {
    crumbs.push({ label: 'รายการ Drawing', to: '/drawings' })
    if (second === 'new') crumbs.push({ label: 'เพิ่ม Drawing', to: pathname })
    else if (second) {
      crumbs.push({ label: decodeURIComponent(second), to: `/drawings/${second}` })
      if (third === 'edit') crumbs.push({ label: 'แก้ไข', to: pathname })
    }
  } else if (first === 'import') crumbs.push({ label: 'นำเข้าข้อมูล', to: '/import' })
  else if (first === 'sections') crumbs.push({ label: 'Section', to: '/sections' })
  else if (first === 'users') crumbs.push({ label: 'ผู้ใช้งาน', to: '/users' })
  else if (first === 'backup') crumbs.push({ label: 'สำรองข้อมูล', to: '/backup' })
  else if (first === 'account') crumbs.push({ label: 'เปลี่ยนรหัสผ่าน', to: pathname })
  else if (first === 'access-denied') crumbs.push({ label: 'ไม่มีสิทธิ์', to: pathname })

  return crumbs
}

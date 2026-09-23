import { Component } from 'react'
import { TriangleAlert } from 'lucide-react'
import { AppButton } from './buttons'

/** A render error shows this instead of a blank page. The one place a class component is needed. */
export class ErrorBoundary extends Component {
  state = { hasError: false }

  static getDerivedStateFromError() {
    return { hasError: true }
  }

  componentDidCatch(error, info) {
    console.error('render error:', error, info)
  }

  render() {
    if (!this.state.hasError) return this.props.children
    return (
      <div className="flex min-h-full items-center justify-center p-6">
        <div className="max-w-md space-y-3 rounded-lg border border-border-subtle bg-surface-panel p-6 text-center">
          <TriangleAlert className="mx-auto size-8 text-warning" aria-hidden />
          <h1 className="text-heading font-semibold">หน้านี้แสดงผลไม่สำเร็จ</h1>
          <p className="text-body text-ink-muted">ข้อมูลในระบบไม่ได้รับผลกระทบ โหลดหน้าใหม่เพื่อใช้งานต่อ</p>
          <AppButton variant="primary" onClick={() => window.location.reload()}>โหลดหน้าใหม่</AppButton>
        </div>
      </div>
    )
  }
}

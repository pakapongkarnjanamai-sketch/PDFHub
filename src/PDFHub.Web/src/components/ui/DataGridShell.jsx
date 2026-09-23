import { useEffect, useRef, useState } from 'react'

/** Shared viewport-sized scrolling surface for operational tables. */
export function DataGridShell({ scrollRef, className = '', children, ...props }) {
  const containerRef = useRef(null)
  const [height, setHeight] = useState(320)

  useEffect(() => {
    if (scrollRef) scrollRef.current = containerRef.current
    const updateHeight = () => {
      const top = containerRef.current?.getBoundingClientRect().top
      if (top === undefined) return
      setHeight(Math.max(220, window.innerHeight - top - 20))
    }
    updateHeight()
    window.addEventListener('resize', updateHeight)
    return () => window.removeEventListener('resize', updateHeight)
  }, [scrollRef])

  return (
    <div ref={containerRef} style={{ height }} className={`relative overflow-auto ${className}`} {...props}>
      {children}
    </div>
  )
}

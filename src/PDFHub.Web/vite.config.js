import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Leading and trailing slash, e.g. "/" in development or "/PDFHub/" under IIS.
function normalizeBasePath(value) {
  const trimmed = (value ?? '').trim().replace(/^\/+|\/+$/g, '')
  return trimmed ? `/${trimmed}/` : '/'
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.PDFHUB_DEV_API_URL || 'http://localhost:5079'

  return {
    base: normalizeBasePath(env.VITE_PDFHUB_APP_BASE_PATH),
    plugins: [react(), tailwindcss()],
    server: {
      port: 5217,
      strictPort: true,
      // Same origin in development as in production, so the login cookie just works.
      proxy: {
        '/api': apiTarget,
        '/pdf': apiTarget,
      },
    },
    build: {
      rollupOptions: {
        output: {
          manualChunks: (id) => {
            if (id.includes('react-dom') || id.includes('react-router')) return 'react-vendor'
          },
        },
      },
    },
  }
})

import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import '@fontsource-variable/noto-sans-thai'
import './index.css'
import { App } from './App'
import { appConfig } from './config/appConfig'
import { SessionProvider } from './session/SessionContext'
import { ErrorBoundary } from './components/ui/ErrorBoundary'
import { ToastViewport } from './components/ui/ToastViewport'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter basename={appConfig.appBasePath}>
      <ErrorBoundary>
        <SessionProvider>
          <App />
        </SessionProvider>
      </ErrorBoundary>
    </BrowserRouter>
    <ToastViewport />
  </StrictMode>,
)

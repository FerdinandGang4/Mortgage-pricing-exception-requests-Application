import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource-variable/dm-sans'
import '@fontsource-variable/manrope'
import './index.css'
import './theme.css'
import { readTheme } from './hooks/useTheme'
import App from './App.tsx'

// Restore the palette before rendering the workspace to avoid a light-content flash.
document.documentElement.dataset.theme = readTheme()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

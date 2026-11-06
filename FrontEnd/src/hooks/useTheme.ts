import { useEffect, useState } from 'react'

export type Theme = 'light' | 'dark'
const themeStorageKey = 'blueharvest.theme'

export function readTheme(): Theme {
  try { return localStorage.getItem(themeStorageKey) === 'dark' ? 'dark' : 'light' }
  catch { return 'light' }
}

export function useTheme() {
  const [theme, setTheme] = useState<Theme>(readTheme)

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    try { localStorage.setItem(themeStorageKey, theme) }
    catch { /* Theme switching still works when browser storage is unavailable. */ }
  }, [theme])

  return { theme, toggleTheme: () => setTheme(current => current === 'light' ? 'dark' : 'light') }
}

import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const backendProxyTarget = process.env.API_PROXY_TARGET?.trim() || 'http://localhost:5267'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': { target: backendProxyTarget, changeOrigin: true },
      '/openapi': { target: backendProxyTarget, changeOrigin: true },
    },
  },
})

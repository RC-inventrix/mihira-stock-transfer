import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Forward /api/* to the ASP.NET Core API so the browser never makes a cross-origin call.
    proxy: {
      '/api': {
        target: 'http://localhost:5131',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})

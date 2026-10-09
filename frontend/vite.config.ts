import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:7xxx',   // el puerto de tu API
        changeOrigin: true,
        secure: false,                       // acepta el certificado de desarrollo
      },
     },
   },
})

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vuetify from 'vite-plugin-vuetify'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vuetify({ autoImport: true }),
  ],
  server: {
    port: 5583,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5250',
        changeOrigin: true,
        secure: false,
        ws: false,
      },
    },
  },
})

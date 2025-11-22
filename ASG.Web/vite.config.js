import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vuetify from 'vite-plugin-vuetify'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue({
      template: {
        compilerOptions: {
          isCustomElement: (tag) => ['lottie-player'].includes(tag)
        }
      }
    }),
    vuetify({ autoImport: true }),
  ],
  server: {
    port: 5175,
    strictPort: false,
  },
})

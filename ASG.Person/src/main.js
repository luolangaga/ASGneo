import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import router from './router'
import vuetify from './plugins/vuetify'
import { startRealtime } from './services/realtime'

createApp(App)
  .use(router)
  .use(vuetify)
  .mount('#app')

startRealtime()

import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import router from './router'
import vuetify from './plugins/vuetify'
import { setupSEO } from './plugins/seo'
import '@lottiefiles/lottie-player'
import { initMobilePush } from './mobile/push'

createApp(App)
  .use(router)
  .use(vuetify)
  .mount('#app')

setupSEO(router)
initMobilePush(router)

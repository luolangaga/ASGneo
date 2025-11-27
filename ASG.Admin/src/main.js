import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import router from './router'

import 'vuetify/styles'
import { createVuetify } from 'vuetify'
import { aliases, mdi } from 'vuetify/iconsets/mdi'
import '@mdi/font/css/materialdesignicons.css'

const myCustomTheme = {
    dark: false,
    colors: {
        background: '#F3F4F6', // 浅灰背景，比纯白更有层次
        surface: '#FFFFFF',
        primary: '#4F46E5', // Indigo 600 - 现代感强的蓝色
        'primary-darken-1': '#3730A3',
        secondary: '#10B981', // Emerald 500
        'secondary-darken-1': '#059669',
        error: '#EF4444',
        info: '#3B82F6',
        success: '#22C55E',
        warning: '#F59E0B',
    },
}

const vuetify = createVuetify({
    icons: {
        defaultSet: 'mdi',
        aliases,
        sets: { mdi },
    },
    theme: {
        defaultTheme: 'myCustomTheme',
        themes: {
            myCustomTheme,
        },
    },
    defaults: {
        VCard: {
            elevation: 2,
            rounded: 'lg',
        },
        VBtn: {
            rounded: 'lg',
            fontWeight: '600',
            letterSpacing: '0.025em',
        },
        VTextField: {
            variant: 'outlined',
            density: 'comfortable',
            color: 'primary',
        },
        VSelect: {
            variant: 'outlined',
            density: 'comfortable',
            color: 'primary',
        },
    }
})

createApp(App)
    .use(router)
    .use(vuetify)
    .mount('#app')

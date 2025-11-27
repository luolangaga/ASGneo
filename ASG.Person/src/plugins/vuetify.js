import 'vuetify/styles'
import { createVuetify } from 'vuetify'
import { h } from 'vue'
import { mdi } from 'vuetify/iconsets/mdi'
import { aliases as mdAliases } from 'vuetify/iconsets/md'

const md3Light = { dark: false, colors: { primary: '#3B82F6', secondary: '#6B7280', tertiary: '#2AB3A7', surface: '#FCFCFE', background: '#F8F9FB', surfaceVariant: '#E5E7EB', outline: '#8F95A1', error: '#B00020', info: '#1E88E5', success: '#2E7D32', warning: '#FBBC05' } }
const md3Dark = { dark: true, colors: { primary: '#3B82F6', secondary: '#C9D1D9', tertiary: '#A5EDE3', surface: '#1B2028', background: '#151922', surfaceVariant: '#2B313B', outline: '#9DA4AE', error: '#CF6679', info: '#90CAF9', success: '#81C784', warning: '#FFD54F' } }
const defaults = { VBtn: { variant: 'tonal', rounded: 'lg', class: 'text-none' }, VCard: { rounded: 'lg', elevation: 1 }, VTextField: { variant: 'outlined', density: 'comfortable' }, VSelect: { variant: 'outlined', density: 'comfortable' }, VAppBar: { flat: true, elevation: 0, color: 'surface', density: 'comfortable' }, VChip: { variant: 'elevated' }, VAvatar: { variant: 'tonal' } }

export const vuetify = createVuetify({
  icons: {
    defaultSet: 'ms',
    aliases: { ...mdAliases },
    sets: {
      ms: { component: (props) => h('span', { class: 'material-symbols-outlined', style: 'font-variation-settings: "FILL" 0, "wght" 400, "opsz" 24' }, props.icon) },
      mdi,
    },
  },
  theme: { defaultTheme: 'md3Light', themes: { md3Light, md3Dark } },
  defaults,
})

export default vuetify

import 'vuetify/styles'
import { createVuetify } from 'vuetify'
import { h } from 'vue'
import { mdi } from 'vuetify/iconsets/mdi'
import { aliases as mdAliases } from 'vuetify/iconsets/md'

// Material 3 inspired color schemes (can be tuned to brand later)
const md3Light = {
  dark: false,
  colors: {
    // 主色改为蓝色（更稳重且通用）：#3B82F6
    primary: '#3B82F6',
    // 中性灰：用于次要操作与文字强调
    secondary: '#6B7280',
    // 低饱和青色：用于第三色，适度点缀
    tertiary: '#2AB3A7',
    // 更柔和的浅表面与背景
    surface: '#FCFCFE',
    background: '#F8F9FB',
    surfaceVariant: '#E5E7EB',
    outline: '#8F95A1',
    error: '#B00020',
    info: '#1E88E5',
    success: '#2E7D32',
    warning: '#FBBC05',
  },
}

const md3Dark = {
  dark: true,
  colors: {
    // 深色主题主色同样采用蓝色以保持一致
    primary: '#3B82F6',
    secondary: '#C9D1D9',
    tertiary: '#A5EDE3',
    surface: '#121317',
    background: '#0F1115',
    surfaceVariant: '#2A2F3A',
    outline: '#9DA4AE',
    error: '#CF6679',
    info: '#90CAF9',
    success: '#81C784',
    warning: '#FFD54F',
  },
}

// Global component defaults to align with M3
const defaults = {
  VBtn: {
    variant: 'tonal',
    rounded: 'lg',
    class: 'text-none',
  },
  VCard: {
    rounded: 'lg',
    elevation: 1,
  },
  VTextField: {
    variant: 'outlined',
    density: 'comfortable',
  },
  VSelect: {
    variant: 'outlined',
    density: 'comfortable',
  },
  VAppBar: {
    flat: true,
    elevation: 0,
    color: 'surface',
    density: 'comfortable',
  },
  VChip: {
    variant: 'elevated',
  },
  VAvatar: {
    variant: 'tonal',
  },
}

export const vuetify = createVuetify({
  icons: {
    // Use Material Symbols Outlined as the default icon set
    defaultSet: 'ms',
    aliases: mdAliases,
    sets: {
      ms: {
        // Render Material Symbols via font glyphs
        component: (props) => h(
          'span',
          {
            class: 'material-symbols-outlined',
            style: 'font-variation-settings: "FILL" 0, "wght" 400, "opsz" 24',
          },
          props.icon,
        ),
      },
      // Keep MDI available for any legacy/fallback usage
      mdi,
    },
  },
  theme: {
    defaultTheme: 'md3Light',
    themes: {
      md3Light,
      md3Dark,
    },
  },
  defaults,
})

export default vuetify
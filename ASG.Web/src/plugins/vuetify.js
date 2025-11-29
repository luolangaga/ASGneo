import 'vuetify/styles'
import { createVuetify } from 'vuetify'
import { h } from 'vue'
import { mdi } from 'vuetify/iconsets/mdi'
import { aliases as mdAliases } from 'vuetify/iconsets/md'

// Material 3 inspired color schemes (can be tuned to brand later)
const md3Light = {
  dark: false,
  colors: {
    // Standard Blue: Professional and clean
    primary: '#3B82F6',
    // Cool Gray: For secondary text and icons
    secondary: '#64748B',
    // Teal: For refreshing accents
    tertiary: '#14B8A6',
    // Pure White: Cleanest surface
    surface: '#FFFFFF',
    // Very Light Cool Gray: subtle contrast against white surfaces
    background: '#F3F4F6',
    surfaceVariant: '#F1F5F9',
    outline: '#CBD5E1',
    error: '#EF4444',
    info: '#3B82F6',
    success: '#10B981',
    warning: '#F59E0B',
  },
}

const md3Dark = {
  dark: true,
  colors: {
    // Lighter Blue for dark mode visibility
    primary: '#60A5FA',
    secondary: '#94A3B8',
    tertiary: '#2DD4BF',
    // Deep Blue-Gray: Modern dark theme background (not pure black)
    surface: '#1E293B',
    background: '#0F172A',
    surfaceVariant: '#334155',
    outline: '#475569',
    error: '#F87171',
    info: '#60A5FA',
    success: '#34D399',
    warning: '#FBBF24',
  },
}

const tapeFuturism = {
  dark: true,
  colors: {
    primary: '#00FFFF',
    secondary: '#FF00FF',
    tertiary: '#FFFF00',
    surface: '#1A1A2E',
    background: '#0F1A2B',
    surfaceVariant: '#23273A',
    outline: '#00FFFF',
    error: '#FF2E63',
    info: '#00B4D8',
    success: '#3CFCC5',
    warning: '#FFC300',
  },
}

// Global component defaults to align with M3
const defaults = {
  VBtn: {
    variant: 'flat', // Flatter, more modern look
    rounded: 'lg', // Slightly rounded, professional
    class: 'text-none font-weight-medium letter-spacing-normal', // Remove uppercase, better weight
    elevation: 0,
  },
  VCard: {
    rounded: 'lg', // Consistent rounded corners
    elevation: 0, // Flat design
    border: true, // Subtle border for definition
    flat: true,
  },
  VTextField: {
    variant: 'outlined',
    density: 'comfortable',
    color: 'primary',
    bgColor: 'surface',
  },
  VSelect: {
    variant: 'outlined',
    density: 'comfortable',
    color: 'primary',
    bgColor: 'surface',
  },
  VAutocomplete: {
    variant: 'outlined',
    density: 'comfortable',
    color: 'primary',
    bgColor: 'surface',
  },
  VTextarea: {
    variant: 'outlined',
    density: 'comfortable',
    color: 'primary',
    bgColor: 'surface',
  },
  VAppBar: {
    flat: true,
    elevation: 0,
    color: 'background', // Blend with background
    density: 'comfortable',
    border: true, // Subtle separator
  },
  VNavigationDrawer: {
    elevation: 0,
    color: 'surface',
    border: 'e', // Right border
  },
  VChip: {
    variant: 'tonal', // Tonal is softer than elevated
    rounded: 'lg',
  },
  VAvatar: {
    variant: 'tonal',
  },
  VDialog: {
    cardProps: {
      elevation: 5, // Popups should have elevation
      rounded: 'xl',
    }
  },
  VMenu: {
    cardProps: {
      elevation: 3,
      rounded: 'lg',
    }
  }
}

export const vuetify = createVuetify({
  icons: {
    // 默认使用 Google Material Symbols Outlined（通过 index.html 引入字体）
    defaultSet: 'ms',
    // 使用 Material 的标准别名集合，覆盖常见的 close/delete 等名称
    aliases: {
      ...mdAliases,
    },
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
      tapeFuturism,
    },
  },
  defaults,
})

export default vuetify

import type { LiturgicalColor } from '../api/types'

interface ColorTheme {
  label: string
  /** Cor sólida usada em barras/badges/acentos. */
  hex: string
  /** Classes Tailwind para texto sobre a cor sólida (contraste). */
  onColorText: string
}

export const LITURGICAL_COLOR_THEME: Record<LiturgicalColor, ColorTheme> = {
  Verde: { label: 'Verde', hex: '#2f6f4f', onColorText: 'text-white' },
  Branco: { label: 'Branco', hex: '#c9a84c', onColorText: 'text-white' },
  Vermelho: { label: 'Vermelho', hex: '#a4243b', onColorText: 'text-white' },
  Roxo: { label: 'Roxo', hex: '#5b2a6e', onColorText: 'text-white' },
  Rosa: { label: 'Rosa', hex: '#c96b8a', onColorText: 'text-white' },
  Preto: { label: 'Preto', hex: '#26262b', onColorText: 'text-white' },
}

export function getLiturgicalColorTheme(color: LiturgicalColor): ColorTheme {
  return LITURGICAL_COLOR_THEME[color] ?? LITURGICAL_COLOR_THEME.Verde
}

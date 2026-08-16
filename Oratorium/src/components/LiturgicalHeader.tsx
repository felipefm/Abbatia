import { formatLongDate } from '../lib/date'
import { getLiturgicalColorTheme } from '../lib/liturgicalColor'
import type { LiturgicalColor } from '../api/types'

interface LiturgicalHeaderProps {
  date: string
  liturgicalTitle: string
  liturgicalColor: LiturgicalColor
}

export function LiturgicalHeader({ date, liturgicalTitle, liturgicalColor }: LiturgicalHeaderProps) {
  const theme = getLiturgicalColorTheme(liturgicalColor)

  return (
    <header className="overflow-hidden rounded-2xl border border-black/5 bg-white shadow-sm dark:border-white/10 dark:bg-neutral-900">
      <div className="h-2" style={{ backgroundColor: theme.hex }} />
      <div className="px-6 py-5">
        <p className="text-sm font-medium text-neutral-500 dark:text-neutral-400">{formatLongDate(date)}</p>
        <h1 className="mt-1 font-serif-reading text-2xl leading-snug text-neutral-900 dark:text-neutral-50">
          {liturgicalTitle}
        </h1>
        <span
          className={`mt-3 inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ${theme.onColorText}`}
          style={{ backgroundColor: theme.hex }}
        >
          <span className="h-2 w-2 rounded-full bg-white/70" />
          Cor litúrgica: {theme.label}
        </span>
      </div>
    </header>
  )
}

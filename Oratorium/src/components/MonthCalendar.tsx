import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMonthCalendar } from '../hooks/useMonthCalendar'
import { getLiturgicalColorTheme } from '../lib/liturgicalColor'
import { todayISO } from '../lib/date'

interface MonthCalendarProps {
  /** Data atualmente exibida na página (yyyy-MM-dd) — define o mês inicial
   * mostrado e qual dia aparece destacado. */
  currentDate: string
}

const WEEKDAY_LABELS = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S']
const MONTH_FORMATTER = new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric', timeZone: 'UTC' })

function parseYearMonth(iso: string): { year: number; month: number } {
  const [year, month] = iso.split('-').map(Number)
  return { year, month }
}

/** Calendário mensal colorido pela cor litúrgica de cada dia — fica na
 * barra lateral esquerda, abaixo do sumário. Serve tanto para navegar entre
 * dias (clicar num dia leva pra /dia/{date}) quanto para ver o ritmo do
 * mês. Escondido no mobile (ver `hidden lg:block` abaixo) — nessa largura
 * o DateNav já cobre a navegação de data sozinho. */
export function MonthCalendar({ currentDate }: MonthCalendarProps) {
  const navigate = useNavigate()
  const [{ year, month }, setViewDate] = useState(() => parseYearMonth(currentDate))

  // Se o usuário navegar para outro mês por fora do calendário (ex: input
  // de data do DateNav, ou "Voltar para hoje"), o calendário acompanha.
  useEffect(() => {
    setViewDate(parseYearMonth(currentDate))
  }, [currentDate])

  const { data, loading, error } = useMonthCalendar(year, month)
  const today = todayISO()

  const goToMonth = (deltaMonths: number) => {
    setViewDate(({ year: y, month: m }) => {
      const total = (y * 12 + (m - 1)) + deltaMonths
      return { year: Math.floor(total / 12), month: (total % 12) + 1 }
    })
  }

  const firstOfMonth = new Date(Date.UTC(year, month - 1, 1))
  const leadingBlanks = firstOfMonth.getUTCDay()
  const monthLabel = MONTH_FORMATTER.format(firstOfMonth)

  return (
    <div className="hidden rounded-2xl border border-black/5 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-neutral-900 lg:block">
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={() => goToMonth(-1)}
          aria-label="Mês anterior"
          className="rounded-full px-2 py-1 text-neutral-500 hover:bg-black/5 dark:text-neutral-400 dark:hover:bg-white/5"
        >
          ←
        </button>
        <p className="text-sm font-medium text-neutral-700 capitalize dark:text-neutral-200">{monthLabel}</p>
        <button
          type="button"
          onClick={() => goToMonth(1)}
          aria-label="Próximo mês"
          className="rounded-full px-2 py-1 text-neutral-500 hover:bg-black/5 dark:text-neutral-400 dark:hover:bg-white/5"
        >
          →
        </button>
      </div>

      <div className="mt-3 grid grid-cols-7 gap-y-1 text-center text-xs text-neutral-400 dark:text-neutral-500">
        {WEEKDAY_LABELS.map((label, index) => (
          <span key={index}>{label}</span>
        ))}
      </div>

      {error && <p className="mt-2 text-xs text-red-600 dark:text-red-400">Não foi possível carregar o calendário.</p>}

      <div className="mt-1 grid grid-cols-7 gap-y-1 text-center">
        {Array.from({ length: leadingBlanks }).map((_, index) => (
          <span key={`blank-${index}`} />
        ))}

        {data?.days.map((day) => {
          const dayNumber = Number(day.date.slice(-2))
          const isToday = day.date === today
          const isSelected = day.date === currentDate
          const theme = getLiturgicalColorTheme(day.liturgicalColor)

          return (
            <button
              key={day.date}
              type="button"
              title={day.liturgicalTitle}
              onClick={() => navigate(`/dia/${day.date}`)}
              className={`flex flex-col items-center gap-0.5 rounded-lg py-1 text-sm transition hover:bg-black/5 dark:hover:bg-white/5 ${
                isSelected
                  ? 'bg-amber-100 font-semibold text-amber-900 dark:bg-amber-950/50 dark:text-amber-300'
                  : 'text-neutral-700 dark:text-neutral-300'
              } ${isToday && !isSelected ? 'underline decoration-dotted underline-offset-2' : ''}`}
            >
              {dayNumber}
              <span className="h-1.5 w-1.5 rounded-full" style={{ backgroundColor: theme.hex }} />
            </button>
          )
        })}

        {loading &&
          !data &&
          Array.from({ length: 7 }).map((_, index) => (
            <span key={`skeleton-${index}`} className="py-1 text-sm text-neutral-300 dark:text-neutral-700">
              ⋯
            </span>
          ))}
      </div>
    </div>
  )
}

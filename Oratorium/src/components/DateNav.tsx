import { useNavigate } from 'react-router-dom'
import { addDays, todayISO } from '../lib/date'

interface DateNavProps {
  /** Data atualmente exibida (yyyy-MM-dd). */
  currentDate: string
}

export function DateNav({ currentDate }: DateNavProps) {
  const navigate = useNavigate()
  const isToday = currentDate === todayISO()

  return (
    <nav className="flex items-center justify-between gap-2">
      <button
        type="button"
        onClick={() => navigate(`/dia/${addDays(currentDate, -1)}`)}
        className="rounded-full border border-black/10 px-4 py-2 text-sm font-medium text-neutral-700 transition hover:bg-black/5 dark:border-white/10 dark:text-neutral-200 dark:hover:bg-white/5"
        aria-label="Dia anterior"
      >
        ← Anterior
      </button>

      {!isToday && (
        <button
          type="button"
          onClick={() => navigate('/hoje')}
          className="rounded-full px-4 py-2 text-sm font-semibold text-amber-700 underline decoration-dotted underline-offset-4 dark:text-amber-400"
        >
          Voltar para hoje
        </button>
      )}

      <button
        type="button"
        onClick={() => navigate(`/dia/${addDays(currentDate, 1)}`)}
        className="rounded-full border border-black/10 px-4 py-2 text-sm font-medium text-neutral-700 transition hover:bg-black/5 dark:border-white/10 dark:text-neutral-200 dark:hover:bg-white/5"
        aria-label="Próximo dia"
      >
        Próximo →
      </button>
    </nav>
  )
}
